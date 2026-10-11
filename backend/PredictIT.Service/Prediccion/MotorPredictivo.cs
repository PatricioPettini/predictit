using System.Text.Json;
using PredictIT.Domain.Negocio;

namespace PredictIT.Service.Prediccion;

/// <summary>
/// Todo lo que una regla necesita saber de un equipo para evaluarse.
///
/// La fecha de referencia entra como dato y no se lee del reloj: es lo que hace
/// que el motor sea determinístico y que un caso de prueba pueda fijar el
/// escenario. Un motor que consulta <c>DateTime.Now</c> por dentro no se puede
/// probar sin esperar.
/// </summary>
public sealed record ContextoEquipo(
    Equipo Equipo,
    IReadOnlyList<Incidencia> Incidencias,
    IReadOnlyList<Mantenimiento> Mantenimientos,
    DateTime Hoy)
{
    /// <summary>
    /// Los preventivos agendados y todavía pendientes de este equipo.
    ///
    /// Es opcional: un equipo sin plan de mantenimiento no tiene ninguno, y la
    /// regla de mantenimiento vencido sigue funcionando con su umbral. Cuando
    /// hay plan, la regla mide contra él.
    /// </summary>
    public IReadOnlyList<MantenimientoProgramado> Programados { get; init; } = [];

    /// <summary>El preventivo agendado más atrasado, si hay alguno vencido.</summary>
    public MantenimientoProgramado? ProgramadoVencido =>
        Programados.Where(p => p.EstaVencido(HoyFecha))
                   .OrderBy(p => p.FechaProgramada)
                   .FirstOrDefault();

    public DateOnly HoyFecha => DateOnly.FromDateTime(Hoy);

    /// <summary>Incidencias con fecha dentro de los últimos <paramref name="dias"/> días.</summary>
    public IEnumerable<Incidencia> IncidenciasDesde(int dias)
    {
        var desde = Hoy.AddDays(-dias);
        return Incidencias.Where(i => i.Fecha >= desde);
    }

    /// <summary>
    /// Último mantenimiento preventivo. El correctivo no cuenta: es una
    /// reparación después de la falla, no el mantenimiento que se dejó de hacer.
    /// </summary>
    public Mantenimiento? UltimoPreventivo =>
        Mantenimientos
            .Where(m => m.Tipo?.EsPreventivo == true)
            .OrderByDescending(m => m.Fecha)
            .FirstOrDefault();
}

/// <summary>Resultado de evaluar una regla sobre un equipo.</summary>
public sealed record ResultadoRegla
{
    public required ReglaAlerta Regla { get; init; }

    /// <summary>
    /// False cuando la regla no se puede evaluar por falta de datos —por ejemplo
    /// antigüedad sin fecha de adquisición—. Una regla no aplicable se excluye
    /// del promedio ponderado en lugar de aportar cero: si aportara cero, un
    /// equipo con datos incompletos mostraría menos riesgo que uno cargado
    /// completo, que es exactamente al revés de lo que corresponde.
    /// </summary>
    public bool Aplicable { get; init; } = true;

    /// <summary>Si se alcanzó el umbral configurado. Es lo que dispara la alerta.</summary>
    public bool Cumple { get; init; }

    /// <summary>Qué tan lejos está del umbral, de 0 a 1. Es lo que aporta al score.</summary>
    public double Intensidad { get; init; }

    /// <summary>Texto para el usuario. Dice el número concreto, no la regla en abstracto.</summary>
    public string Motivo { get; init; } = string.Empty;

    public string? Recomendacion { get; init; }

    public static ResultadoRegla NoAplica(ReglaAlerta regla, string motivo) =>
        new() { Regla = regla, Aplicable = false, Cumple = false, Intensidad = 0, Motivo = motivo };
}

/// <summary>Estrategia de evaluación de un tipo de regla.</summary>
public interface IEstrategiaRegla
{
    TipoRegla Tipo { get; }
    ResultadoRegla Evaluar(ReglaAlerta regla, ContextoEquipo ctx);
}

/// <summary>Resultado completo para un equipo.</summary>
public sealed record RiesgoEquipo(
    Guid IdEquipo,
    int Score,
    NivelRiesgo Nivel,
    IReadOnlyList<ResultadoRegla> Resultados)
{
    /// <summary>Reglas que alcanzaron su umbral. Cada una amerita una alerta.</summary>
    public IEnumerable<ResultadoRegla> Disparadas => Resultados.Where(r => r.Cumple);
}

/// <summary>
/// Motor de análisis predictivo (RF-11, RF-12, CU-010).
///
/// El score es el promedio de las intensidades ponderado por el peso de cada
/// regla, normalizado sobre las reglas que efectivamente se pudieron evaluar.
/// Normalizar importa: los pesos los configura la organización y no hay nada que
/// garantice que sumen 100.
/// </summary>
public class MotorPredictivo
{
    private readonly Dictionary<TipoRegla, IEstrategiaRegla> _estrategias;

    public MotorPredictivo(IEnumerable<IEstrategiaRegla>? estrategias = null)
    {
        var lista = estrategias?.ToList() ?? EstrategiasPorDefecto().ToList();
        _estrategias = lista.ToDictionary(e => e.Tipo);
    }

    public static IEnumerable<IEstrategiaRegla> EstrategiasPorDefecto()
    {
        yield return new EstrategiaRecurrenciaFallas();
        yield return new EstrategiaAcumulacionIncidencias();
        yield return new EstrategiaMantenimientoVencido();
        yield return new EstrategiaAntiguedadEquipo();
        yield return new EstrategiaGarantiaPorVencer();
    }

    public RiesgoEquipo Evaluar(ContextoEquipo ctx, IEnumerable<ReglaAlerta> reglas)
    {
        var resultados = new List<ResultadoRegla>();

        foreach (var regla in reglas.Where(r => r.Activa))
        {
            if (!_estrategias.TryGetValue(regla.Tipo, out var estrategia))
            {
                // Una regla cuyo tipo este código no conoce se saltea. No se
                // interrumpe la evaluación del resto del parque por una regla.
                resultados.Add(ResultadoRegla.NoAplica(regla, "Tipo de regla no soportado."));
                continue;
            }

            try
            {
                resultados.Add(estrategia.Evaluar(regla, ctx));
            }
            catch (JsonException)
            {
                // La condición la edita el usuario: un JSON roto es un error de
                // configuración de esa regla, no una falla del motor.
                resultados.Add(ResultadoRegla.NoAplica(
                    regla, "La condición de la regla no es un JSON válido."));
            }
        }

        var aplicables = resultados.Where(r => r.Aplicable && r.Regla.Peso > 0).ToList();
        var pesoTotal = aplicables.Sum(r => r.Regla.Peso);

        var crudo = pesoTotal == 0
            ? 0d
            : aplicables.Sum(r => r.Regla.Peso * Math.Clamp(r.Intensidad, 0, 1)) / pesoTotal;

        var score = (int)Math.Round(Math.Clamp(crudo * 100 * FactorCriticidad(ctx.Equipo), 0, 100));

        return new RiesgoEquipo(ctx.Equipo.Id, score, NivelRiesgoTexto.De(score), resultados);
    }

    /// <summary>
    /// La criticidad del equipo amplifica el score: las mismas tres fallas no
    /// significan lo mismo en el servidor de facturación que en un monitor de
    /// recepción. Es multiplicativo y no aditivo a propósito, así un equipo
    /// crítico sin ningún problema sigue dando cero.
    /// </summary>
    private static double FactorCriticidad(Equipo equipo) =>
        0.70 + 0.15 * (Math.Clamp(equipo.Criticidad, 1, 4) - 1);
}

/// <summary>Lectura de los parámetros JSON de una regla.</summary>
internal static class Condicion
{
    public static JsonElement Leer(ReglaAlerta regla)
    {
        if (string.IsNullOrWhiteSpace(regla.Condicion)) return default;
        using var doc = JsonDocument.Parse(regla.Condicion);
        return doc.RootElement.Clone();
    }

    public static int Entero(JsonElement raiz, string clave, int porDefecto)
    {
        if (raiz.ValueKind != JsonValueKind.Object) return porDefecto;
        if (!raiz.TryGetProperty(clave, out var v)) return porDefecto;
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : porDefecto;
    }

    public static bool Booleano(JsonElement raiz, string clave, bool porDefecto)
    {
        if (raiz.ValueKind != JsonValueKind.Object) return porDefecto;
        if (!raiz.TryGetProperty(clave, out var v)) return porDefecto;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => porDefecto,
        };
    }
}

/// <summary>
/// Cantidad de incidencias en una ventana de tiempo.
/// Parámetros: <c>minIncidencias</c>, <c>ventanaDias</c>.
/// </summary>
public sealed class EstrategiaRecurrenciaFallas : IEstrategiaRegla
{
    public TipoRegla Tipo => TipoRegla.RecurrenciaFallas;

    public ResultadoRegla Evaluar(ReglaAlerta regla, ContextoEquipo ctx)
    {
        var cond = Condicion.Leer(regla);
        var minimo = Math.Max(1, Condicion.Entero(cond, "minIncidencias", 3));
        var ventana = Math.Max(1, Condicion.Entero(cond, "ventanaDias", 30));

        var cantidad = ctx.IncidenciasDesde(ventana).Count();
        var cumple = cantidad >= minimo;

        return new ResultadoRegla
        {
            Regla = regla,
            Cumple = cumple,
            Intensidad = Math.Min(1, (double)cantidad / minimo),
            Motivo = $"{cantidad} incidencia{(cantidad == 1 ? "" : "s")} en los últimos {ventana} días " +
                     $"(umbral: {minimo}).",
            Recomendacion = cumple
                ? "Revisar el equipo: la frecuencia de fallas sugiere una causa de fondo sin resolver."
                : null,
        };
    }
}

/// <summary>
/// Incidencias repetidas, opcionalmente de la misma categoría.
/// Parámetros: <c>minIncidencias</c>, <c>ventanaDias</c>, <c>mismaCategoria</c>.
///
/// Se diferencia de la recurrencia en que acá interesa que sea *la misma* falla:
/// tres problemas distintos hablan de un equipo viejo, tres veces el mismo
/// problema hablan de una reparación que no resolvió nada.
/// </summary>
public sealed class EstrategiaAcumulacionIncidencias : IEstrategiaRegla
{
    public TipoRegla Tipo => TipoRegla.AcumulacionIncidencias;

    public ResultadoRegla Evaluar(ReglaAlerta regla, ContextoEquipo ctx)
    {
        var cond = Condicion.Leer(regla);
        var minimo = Math.Max(1, Condicion.Entero(cond, "minIncidencias", 2));
        var ventana = Math.Max(1, Condicion.Entero(cond, "ventanaDias", 90));
        var porCategoria = Condicion.Booleano(cond, "mismaCategoria", true);

        var enVentana = ctx.IncidenciasDesde(ventana).ToList();

        int cantidad;
        string detalle;

        if (porCategoria)
        {
            // Las incidencias sin categoría no se agrupan entre sí: «sin
            // clasificar» no es una falla en común, es la ausencia del dato.
            var grupo = enVentana
                .Where(i => i.IdCategoria is not null)
                .GroupBy(i => i.IdCategoria!.Value)
                .Select(g => new { Cantidad = g.Count(), Nombre = g.First().Categoria?.Nombre })
                .OrderByDescending(g => g.Cantidad)
                .FirstOrDefault();

            cantidad = grupo?.Cantidad ?? 0;
            detalle = grupo?.Nombre is { } nombre ? $" de la categoría «{nombre}»" : "";
        }
        else
        {
            cantidad = enVentana.Count;
            detalle = "";
        }

        var cumple = cantidad >= minimo;

        return new ResultadoRegla
        {
            Regla = regla,
            Cumple = cumple,
            Intensidad = Math.Min(1, (double)cantidad / minimo),
            Motivo = $"{cantidad} incidencia{(cantidad == 1 ? "" : "s")}{detalle} " +
                     $"en los últimos {ventana} días (umbral: {minimo}).",
            Recomendacion = cumple
                ? "La falla se repite: revisar si la reparación anterior atacó la causa."
                : null,
        };
    }
}

/// <summary>
/// Días desde el último mantenimiento preventivo.
/// Parámetros: <c>diasSinMantenimiento</c>.
/// </summary>
public sealed class EstrategiaMantenimientoVencido : IEstrategiaRegla
{
    public TipoRegla Tipo => TipoRegla.MantenimientoVencido;

    public ResultadoRegla Evaluar(ReglaAlerta regla, ContextoEquipo ctx)
    {
        var cond = Condicion.Leer(regla);
        var umbral = Math.Max(1, Condicion.Entero(cond, "diasSinMantenimiento", 180));

        // Con plan de mantenimiento, «vencido» tiene una definición exacta: el
        // trabajo que la organización se comprometió a hacer y cuya fecha ya
        // pasó. Se prefiere sobre el umbral porque el umbral es una estimación
        // genérica —«cada 180 días»— y el plan es la decisión de esta
        // organización para este equipo.
        if (ctx.Programados.Count > 0)
        {
            // Con plan, el plan manda y el umbral no vuelve a mirarse. Si se
            // mirara, un equipo con el próximo preventivo agendado para la
            // semana que viene daría «vencido» sólo porque hace 250 días del
            // anterior, contradiciendo la decisión que la organización acaba de
            // tomar sobre ese mismo equipo.
            if (ctx.ProgramadoVencido is null)
            {
                var proximo = ctx.Programados.OrderBy(p => p.FechaProgramada).First();
                return new ResultadoRegla
                {
                    Regla = regla,
                    Cumple = false,
                    Intensidad = 0,
                    Motivo = $"{proximo.NombreTipoMantenimiento} programado para el "
                             + $"{proximo.FechaProgramada:dd/MM/yyyy}.",
                };
            }

            var agendado = ctx.ProgramadoVencido!;
            var atraso = agendado.DiasDeAtraso(ctx.HoyFecha);

            return new ResultadoRegla
            {
                Regla = regla,
                Cumple = true,
                // El atraso se satura contra el propio intervalo del plan: a un
                // mes de atraso sobre un trimestral no le corresponde la misma
                // gravedad que sobre un plan semanal.
                Intensidad = Math.Min(1, (double)atraso / umbral),
                Motivo = $"{agendado.NombreTipoMantenimiento} estaba programado para el "
                         + $"{agendado.FechaProgramada:dd/MM/yyyy} y lleva {atraso} día(s) de atraso.",
                Recomendacion = "Ejecutar el mantenimiento agendado, o reprogramarlo con su motivo.",
            };
        }

        var ultimo = ctx.UltimoPreventivo;

        // Si nunca tuvo preventivo se cuenta desde el alta. Tratarlo como «no
        // aplica» sería justo al revés: un equipo que nunca se mantuvo es el
        // caso que la regla existe para detectar.
        var referencia = ultimo?.Fecha ?? ctx.Equipo.FechaAlta;
        var dias = (int)(ctx.Hoy - referencia).TotalDays;
        if (dias < 0) dias = 0;

        var cumple = dias >= umbral;
        var desde = ultimo is null ? "desde el alta del equipo, sin preventivo registrado" : "desde el último preventivo";

        return new ResultadoRegla
        {
            Regla = regla,
            Cumple = cumple,
            Intensidad = Math.Min(1, (double)dias / umbral),
            Motivo = $"{dias} días {desde} (umbral: {umbral}).",
            Recomendacion = cumple ? "Programar el mantenimiento preventivo." : null,
        };
    }
}

/// <summary>
/// Antigüedad del equipo desde la compra.
/// Parámetros: <c>aniosUmbral</c>.
/// </summary>
public sealed class EstrategiaAntiguedadEquipo : IEstrategiaRegla
{
    public TipoRegla Tipo => TipoRegla.AntiguedadEquipo;

    public ResultadoRegla Evaluar(ReglaAlerta regla, ContextoEquipo ctx)
    {
        var cond = Condicion.Leer(regla);
        var umbral = Math.Max(1, Condicion.Entero(cond, "aniosUmbral", 4));

        if (ctx.Equipo.FechaAdquisicion is not { } compra)
        {
            return ResultadoRegla.NoAplica(
                regla, "El equipo no tiene fecha de adquisición cargada.");
        }

        var anios = (ctx.HoyFecha.DayNumber - compra.DayNumber) / 365.25;
        if (anios < 0) anios = 0;

        var cumple = anios >= umbral;

        return new ResultadoRegla
        {
            Regla = regla,
            Cumple = cumple,
            Intensidad = Math.Min(1, anios / umbral),
            Motivo = $"{anios:0.#} años de antigüedad (umbral: {umbral}).",
            Recomendacion = cumple ? "Evaluar el recambio del equipo." : null,
        };
    }
}

/// <summary>
/// Proximidad del vencimiento de la garantía.
/// Parámetros: <c>diasAviso</c>.
/// </summary>
public sealed class EstrategiaGarantiaPorVencer : IEstrategiaRegla
{
    public TipoRegla Tipo => TipoRegla.GarantiaPorVencer;

    public ResultadoRegla Evaluar(ReglaAlerta regla, ContextoEquipo ctx)
    {
        var cond = Condicion.Leer(regla);
        var aviso = Math.Max(1, Condicion.Entero(cond, "diasAviso", 60));

        if (ctx.Equipo.FechaFinGarantia is not { } fin)
        {
            return ResultadoRegla.NoAplica(regla, "El equipo no tiene garantía cargada.");
        }

        var dias = fin.DayNumber - ctx.HoyFecha.DayNumber;

        if (dias < 0)
        {
            return new ResultadoRegla
            {
                Regla = regla,
                Cumple = true,
                Intensidad = 1,
                Motivo = $"Garantía vencida hace {-dias} días.",
                Recomendacion = "Toda reparación pasa a ser con cargo: preverlo en el presupuesto.",
            };
        }

        var cumple = dias <= aviso;

        return new ResultadoRegla
        {
            Regla = regla,
            Cumple = cumple,
            // Cuanto más cerca del vencimiento, más aporta.
            Intensidad = cumple ? (double)(aviso - dias) / aviso : 0,
            Motivo = $"La garantía vence en {dias} días (aviso: {aviso}).",
            Recomendacion = cumple
                ? "Resolver los reclamos pendientes mientras la garantía siga vigente."
                : null,
        };
    }
}
