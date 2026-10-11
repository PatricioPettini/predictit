namespace PredictIT.Service.IA;

public enum EstadoDisyuntor
{
    /// <summary>Las llamadas pasan.</summary>
    Cerrado,

    /// <summary>No se intenta: se va directo al respaldo.</summary>
    Abierto,

    /// <summary>Se deja pasar una llamada de prueba.</summary>
    Semiabierto,
}

/// <summary>
/// Disyuntor de tres estados sobre el proveedor de IA (ADR 0009).
///
/// El problema que resuelve no es que el servicio se cuelgue —de eso ya se ocupa
/// el timeout— sino que esté consistentemente caído: sin disyuntor, cada
/// incidencia paga el timeout completo para terminar en el mismo respaldo
/// previsible. Veinte reportes en una mañana de corte son cinco minutos de espera
/// acumulada.
///
/// Escrito a mano y no delegado a una biblioteca: son unas pocas decenas de
/// líneas y conviene que el patrón esté a la vista.
///
/// El reloj entra por constructor para que las pruebas puedan mover el tiempo sin
/// esperar sesenta segundos.
/// </summary>
public class Disyuntor(
    int fallosParaAbrir = 5,
    TimeSpan? esperaParaProbar = null,
    Func<DateTime>? reloj = null)
{
    private readonly TimeSpan _espera = esperaParaProbar ?? TimeSpan.FromSeconds(60);
    private readonly Func<DateTime> _reloj = reloj ?? (() => DateTime.UtcNow);
    private readonly object _candado = new();

    private int _fallosConsecutivos;
    private DateTime? _abiertoDesde;
    private bool _pruebaEnCurso;

    public int FallosParaAbrir { get; } = fallosParaAbrir;

    public EstadoDisyuntor Estado
    {
        get
        {
            lock (_candado) return EstadoInterno();
        }
    }

    /// <summary>Desde cuándo no responde. Es lo que se muestra en pantalla.</summary>
    public DateTime? AbiertoDesde
    {
        get { lock (_candado) return _abiertoDesde; }
    }

    public int FallosConsecutivos
    {
        get { lock (_candado) return _fallosConsecutivos; }
    }

    private EstadoDisyuntor EstadoInterno()
    {
        if (_abiertoDesde is not { } desde) return EstadoDisyuntor.Cerrado;
        return _reloj() - desde >= _espera ? EstadoDisyuntor.Semiabierto : EstadoDisyuntor.Abierto;
    }

    /// <summary>
    /// Si conviene intentar la llamada. Con el circuito abierto devuelve false y
    /// el llamador va directo al respaldo, sin esperar el timeout.
    ///
    /// En semiabierto deja pasar **una sola** llamada de prueba: si pasaran todas,
    /// al vencer la espera se dispararía una avalancha contra un servicio que
    /// quizás sigue caído.
    /// </summary>
    public bool PermiteIntentar()
    {
        lock (_candado)
        {
            switch (EstadoInterno())
            {
                case EstadoDisyuntor.Cerrado:
                    return true;

                case EstadoDisyuntor.Semiabierto when !_pruebaEnCurso:
                    _pruebaEnCurso = true;
                    return true;

                default:
                    return false;
            }
        }
    }

    public void RegistrarExito()
    {
        lock (_candado)
        {
            _fallosConsecutivos = 0;
            _abiertoDesde = null;
            _pruebaEnCurso = false;
        }
    }

    /// <summary>
    /// Registra un fallo. Devuelve true si este fallo abrió el circuito, para que
    /// el llamador lo asiente en bitácora una sola vez y no en cada intento.
    /// </summary>
    public bool RegistrarFallo()
    {
        lock (_candado)
        {
            // El fallo de la llamada de prueba reabre el circuito y reinicia la
            // espera: el servicio sigue sin responder.
            if (_pruebaEnCurso)
            {
                _pruebaEnCurso = false;
                _abiertoDesde = _reloj();
                return false;
            }

            _fallosConsecutivos++;

            if (_fallosConsecutivos >= FallosParaAbrir && _abiertoDesde is null)
            {
                _abiertoDesde = _reloj();
                return true;
            }

            return false;
        }
    }

    /// <summary>Vuelve al estado inicial. La usa el administrador desde la pantalla de IA.</summary>
    public void Reiniciar() => RegistrarExito();
}

/// <summary>
/// Envuelve un proveedor con el disyuntor (patrón Decorator).
///
/// Va aparte del proveedor a propósito: la resiliencia no es asunto de la
/// implementación concreta, y así el mismo disyuntor protege tanto al proveedor
/// real como al simulado —que es lo que hace demostrable el CP-14—.
/// </summary>
public class ProveedorIAConDisyuntor(
    IProveedorIA interno,
    Disyuntor disyuntor,
    Action<string>? asentar = null) : IProveedorIA
{
    public string Nombre => interno.Nombre;
    public Disyuntor Disyuntor { get; } = disyuntor;

    public Task<ResultadoIA<ClasificacionIA>> ClasificarAsync(
        ContextoTriage contexto, CancellationToken ct = default) =>
        ConDisyuntor(() => interno.ClasificarAsync(contexto, ct), "clasificación");

    public Task<ResultadoIA<RecomendacionIA>> RecomendarTecnicoAsync(
        ContextoAsignacion contexto, CancellationToken ct = default) =>
        ConDisyuntor(() => interno.RecomendarTecnicoAsync(contexto, ct), "asignación");

    public Task<ResultadoIA<GuiaReparacionIA>> SugerirReparacionAsync(
        ContextoReparacion contexto, CancellationToken ct = default) =>
        ConDisyuntor(() => interno.SugerirReparacionAsync(contexto, ct), "guía de reparación");

    private async Task<ResultadoIA<T>> ConDisyuntor<T>(
        Func<Task<ResultadoIA<T>>> llamar, string operacion) where T : class
    {
        if (!Disyuntor.PermiteIntentar())
        {
            var desde = Disyuntor.AbiertoDesde;
            return ResultadoIA<T>.Mal(
                MotivoFalla.CircuitoAbierto,
                desde is null
                    ? "El circuito está abierto: se aplica la regla de respaldo."
                    : $"Sin respuesta del proveedor desde {desde:HH:mm} UTC: " +
                      "se aplica la regla de respaldo.");
        }

        ResultadoIA<T> resultado;
        try
        {
            resultado = await llamar();
        }
        catch (Exception ex)
        {
            // Un proveedor no debería lanzar, pero si lo hace, el disyuntor tiene
            // que contarlo igual: si no, un proveedor que lanza nunca abre el
            // circuito y se pierde justo la protección que se busca.
            AnotarFallo(operacion, ex.Message);
            return ResultadoIA<T>.Mal(MotivoFalla.Error, ex.Message);
        }

        if (resultado.Ok)
        {
            Disyuntor.RegistrarExito();
            return resultado;
        }

        AnotarFallo(operacion, resultado.Detalle ?? resultado.Falla?.ToString() ?? "sin detalle");
        return resultado;
    }

    private void AnotarFallo(string operacion, string detalle)
    {
        if (Disyuntor.RegistrarFallo())
        {
            asentar?.Invoke(
                $"Circuito abierto tras {Disyuntor.FallosParaAbrir} fallos consecutivos del " +
                $"proveedor «{Nombre}» ({operacion}): {detalle}. " +
                "Las asignaciones pasan a la regla de respaldo.");
        }
    }
}
