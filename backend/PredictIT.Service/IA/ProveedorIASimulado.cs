using System.Globalization;
using System.Text;

namespace PredictIT.Service.IA;

/// <summary>
/// Proveedor determinístico, sin red (ADR 0006).
///
/// No pretende imitar a un modelo de lenguaje: resuelve el mismo problema con
/// heurísticas explicables. Existe por tres razones, en orden de importancia:
///
/// 1. El sistema tiene que funcionar sin crédito de API y sin internet. La
///    asignación es parte del flujo de registrar una incidencia, no un extra.
/// 2. Es lo que hace demostrable el CP-14: forzándolo a fallar se puede mostrar
///    la asignación de respaldo y las transiciones del disyuntor.
/// 3. Es la línea base contra la cual se mide si la IA aporta algo. Sin esto,
///    «la IA asigna mejor» no sería una afirmación verificable.
///
/// Determinístico significa que el mismo contexto da siempre el mismo resultado:
/// sin eso no habría caso de prueba posible.
/// </summary>
public class ProveedorIASimulado(Func<int>? fallarCadaTantas = null) : IProveedorIA
{
    public string Nombre => "Simulado";

    /// <summary>
    /// Cuenta de llamadas. Se usa para forzar fallos en la demostración del
    /// CP-14 sin tocar el código del proveedor.
    /// </summary>
    private int _llamadas;

    /// <summary>
    /// Palabras que asocian el texto libre a una categoría. Es la parte
    /// deliberadamente simple: el aporte de la IA real es justamente entender lo
    /// que estas palabras no alcanzan a cubrir.
    /// </summary>
    private static readonly Dictionary<string, string[]> PalabrasPorCategoria = new()
    {
        ["Red"] = ["red", "internet", "wifi", "conexion", "conecta", "cable", "lan", "ip", "vpn", "navegar"],
        ["Hardware"] = ["no prende", "no arranca", "ruido", "pantalla", "disco", "memoria", "fuente", "ventilador", "quemado", "humo", "golpe"],
        ["Software"] = ["windows", "office", "excel", "word", "programa", "aplicacion", "sistema", "actualizacion", "licencia", "error", "instalar"],
        ["Periféricos"] = ["impresora", "imprime", "toner", "escaner", "teclado", "mouse", "monitor", "auricular"],
        ["Energía"] = ["corte", "luz", "ups", "bateria", "electrica", "apaga solo", "reinicia solo", "tension"],
        ["Rendimiento"] = ["lento", "lenta", "demora", "tarda", "se cuelga", "traba", "freeze", "rendimiento"],
    };

    /// <summary>Palabras que elevan la prioridad. Se busca de la más grave a la menos.</summary>
    private static readonly (string Prioridad, string[] Palabras)[] PalabrasPorPrioridad =
    [
        ("Crítica", ["no puedo trabajar", "toda la oficina", "todos", "servidor", "facturacion", "urgente", "parado", "no prende", "perdi", "perdida"]),
        ("Alta", ["no funciona", "no anda", "no imprime", "no conecta", "bloqueado", "caido", "varios"]),
        ("Media", ["intermitente", "a veces", "lento", "demora"]),
    ];

    public Task<ResultadoIA<ClasificacionIA>> ClasificarAsync(
        ContextoTriage contexto, CancellationToken ct = default)
    {
        if (DebeFallar() is { } falla)
            return Task.FromResult(ResultadoIA<ClasificacionIA>.Mal(falla, "Fallo forzado (simulación)."));

        var texto = Normalizar($"{contexto.Titulo} {contexto.Descripcion}");

        var categoria = ElegirPorPalabras(texto, contexto.CategoriasPosibles);
        var prioridad = ElegirPrioridad(texto, contexto.PrioridadesPosibles);
        var duplicada = BuscarDuplicada(contexto);

        // La confianza es baja a propósito y no un número inventado alto: esto es
        // coincidencia de palabras, y decir lo contrario sería mentirle al
        // técnico que después decide si acepta la sugerencia.
        var confianza = categoria is null ? 0.20m : 0.55m;

        var justificacion = new StringBuilder("Clasificación por coincidencia de términos");
        if (categoria is not null) justificacion.Append($"; categoría «{categoria}»");
        if (duplicada is not null) justificacion.Append($"; parece duplicar la #{duplicada}");
        justificacion.Append('.');

        return Task.FromResult(ResultadoIA<ClasificacionIA>.Bien(new ClasificacionIA
        {
            Categoria = categoria,
            Prioridad = prioridad,
            DuplicadaDe = duplicada,
            Confianza = confianza,
            Justificacion = justificacion.ToString(),
        }));
    }

    public Task<ResultadoIA<RecomendacionIA>> RecomendarTecnicoAsync(
        ContextoAsignacion contexto, CancellationToken ct = default)
    {
        if (DebeFallar() is { } falla)
            return Task.FromResult(ResultadoIA<RecomendacionIA>.Mal(falla, "Fallo forzado (simulación)."));

        if (contexto.Candidatos.Count == 0)
        {
            return Task.FromResult(ResultadoIA<RecomendacionIA>.Mal(
                MotivoFalla.TecnicoInexistente, "La organización no tiene técnicos activos."));
        }

        var elegido = contexto.Candidatos
            .Select(t => new { Tecnico = t, Puntaje = Puntuar(t, contexto) })
            // El desempate por Id no es arbitrario: sin él, dos técnicos con el
            // mismo puntaje se alternarían según el orden que devuelva la base y
            // el proveedor dejaría de ser determinístico.
            .OrderByDescending(x => x.Puntaje)
            .ThenBy(x => x.Tecnico.Id)
            .First();

        return Task.FromResult(ResultadoIA<RecomendacionIA>.Bien(new RecomendacionIA
        {
            IdTecnico = elegido.Tecnico.Id,
            Confianza = 0.60m,
            Justificacion = Explicar(elegido.Tecnico, contexto),
        }));
    }

    /// <summary>
    /// Guías por categoría, ordenadas de menos a más invasivo.
    ///
    /// El orden no es estético: el primer paso que borra algo o apaga el equipo
    /// tiene que venir después de todos los que no lo hacen. Un procedimiento
    /// que arranca formateando es peor que no tener procedimiento.
    /// </summary>
    private static readonly Dictionary<string, (string Titulo, string Detalle, string? Riesgo)[]> GuiaPorCategoria = new()
    {
        ["Red"] =
        [
            ("Confirmar el alcance", "Probar si otros equipos de la misma ubicación tienen el problema. Si es general, la falla no está en este equipo.", null),
            ("Revisar el enlace físico", "Cable de red enchufado en los dos extremos y luz del puerto encendida. En equipos con wifi, confirmar que la placa esté habilitada.", null),
            ("Verificar la configuración IP", "Comprobar que el equipo tenga dirección, máscara y puerta de enlace. Renovar la concesión de DHCP.", null),
            ("Probar resolución de nombres", "Resolver un nombre externo y uno interno. Si responde por IP y no por nombre, el problema es de DNS.", null),
            ("Reiniciar la interfaz de red", "Deshabilitar y volver a habilitar el adaptador.", "Corta las sesiones abiertas del usuario"),
        ],
        ["Hardware"] =
        [
            ("Registrar lo que se ve y se oye", "Anotar luces, pitidos y mensajes en pantalla antes de tocar nada: es lo que después permite reconstruir la falla.", null),
            ("Revisar alimentación y conexiones", "Cable de alimentación, ficha y regleta. Reasentar los cables de datos y de video.", null),
            ("Probar con periféricos mínimos", "Dejar sólo teclado y monitor. Si arranca así, el problema está en lo que se sacó.", null),
            ("Revisar temperatura y ventilación", "Filtros y ventiladores. Un equipo que se apaga solo bajo carga casi siempre es térmico.", null),
            ("Abrir el gabinete y reasentar módulos", "Memoria y placas. Con el equipo desconectado de la corriente.", "Requiere apagar el equipo y puede invalidar la garantía"),
        ],
        ["Software"] =
        [
            ("Reproducir la falla", "Confirmar con el usuario los pasos exactos. Una falla que no se puede reproducir no se puede dar por resuelta.", null),
            ("Leer el registro de eventos", "Buscar el error en el visor de eventos en el horario que indicó el usuario.", null),
            ("Cerrar y volver a abrir la aplicación", "Con la sesión del usuario, no con una cuenta de administrador: hay fallas que sólo aparecen con su perfil.", null),
            ("Revisar actualizaciones pendientes", "Del sistema y de la aplicación. Anotar la versión antes de actualizar.", null),
            ("Reparar la instalación", "Usar la opción de reparación antes que la de desinstalar.", null),
            ("Reinstalar la aplicación", "Sólo si lo anterior no alcanzó.", "Puede perderse la configuración local del usuario"),
        ],
        ["Periféricos"] =
        [
            ("Probar el periférico en otro equipo", "Separa el problema del dispositivo del problema de la máquina.", null),
            ("Revisar cable, alimentación e insumos", "En impresoras, además: papel, tóner y bandeja.", null),
            ("Confirmar que sea el dispositivo predeterminado", "Muchos reportes de «no imprime» son trabajos que salen por otra impresora.", null),
            ("Vaciar la cola de impresión", "Cancelar los trabajos encolados y reiniciar el servicio de cola.", "Se pierden los trabajos pendientes del usuario"),
            ("Reinstalar el controlador", "Descargar la versión del fabricante para el sistema operativo del equipo.", "Se pierden las preferencias guardadas del dispositivo"),
        ],
        ["Energía"] =
        [
            ("Confirmar si hubo corte", "Preguntar por la ubicación completa y revisar si otros equipos se apagaron.", null),
            ("Revisar la UPS", "Estado de la batería, alarma y tiempo de autonomía. Una batería agotada apaga el equipo sin aviso.", null),
            ("Probar en otro tomacorriente", "Preferentemente en otro circuito.", null),
            ("Revisar la fuente del equipo", "Ruido, olor y ventilador. Un equipo que reinicia solo bajo carga suele ser fuente.", "Requiere apagar el equipo"),
        ],
        ["Rendimiento"] =
        [
            ("Medir antes de tocar", "Uso de procesador, memoria y disco durante la falla. Sin medición no hay forma de saber si mejoró.", null),
            ("Revisar el espacio libre en disco", "Por debajo del 10 % libre, el sistema se degrada solo.", null),
            ("Revisar los programas que arrancan con el equipo", "Deshabilitar los que el usuario no reconozca.", null),
            ("Revisar antivirus y análisis programados", "Un análisis completo en horario laboral explica la mitad de los reportes de lentitud.", null),
            ("Evaluar la antigüedad del equipo", "Si el equipo pasó su vida útil, el próximo paso es la reposición y no otro ajuste.", null),
        ],
    };

    private static readonly (string Titulo, string Detalle, string? Riesgo)[] GuiaGenerica =
    [
        ("Confirmar el síntoma con el usuario", "Qué esperaba que pasara y qué pasó. La mitad de los reportes cambia con esa pregunta.", null),
        ("Revisar si es general o de este equipo", "Probar el mismo caso en otro equipo de la ubicación.", null),
        ("Revisar el registro de eventos", "Buscar errores en el horario en que el usuario notó la falla.", null),
        ("Reiniciar el equipo", "Anotar antes qué había abierto.", "El usuario pierde el trabajo sin guardar"),
    ];

    public Task<ResultadoIA<GuiaReparacionIA>> SugerirReparacionAsync(
        ContextoReparacion contexto, CancellationToken ct = default)
    {
        if (DebeFallar() is { } falla)
            return Task.FromResult(ResultadoIA<GuiaReparacionIA>.Mal(falla, "Fallo forzado (simulación)."));

        var categoria = contexto.Categoria;
        if (string.IsNullOrWhiteSpace(categoria) || !GuiaPorCategoria.ContainsKey(categoria))
        {
            // Sin categoría todavía se puede intentar deducirla del texto: es la
            // misma heurística del triage.
            var texto = Normalizar($"{contexto.TituloIncidencia} {contexto.DescripcionIncidencia}");
            categoria = ElegirPorPalabras(texto, [.. GuiaPorCategoria.Keys]);
        }

        var receta = categoria is not null && GuiaPorCategoria.TryGetValue(categoria, out var g)
            ? g
            : GuiaGenerica;

        var pasos = new List<PasoReparacion>();

        // El antecedente va primero cuando existe: si esta máquina ya tuvo esta
        // falla y se resolvió, empezar por otro lado es perder el tiempo.
        var antecedente = contexto.Antecedentes.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Solucion));
        if (antecedente is not null)
        {
            pasos.Add(new PasoReparacion
            {
                Orden = 1,
                Titulo = $"Revisar cómo se resolvió la incidencia #{antecedente.Numero} de este mismo equipo",
                Detalle = antecedente.Solucion,
            });
        }

        foreach (var (titulo, detalle, riesgo) in receta)
        {
            pasos.Add(new PasoReparacion
            {
                Orden = pasos.Count + 1,
                Titulo = titulo,
                Detalle = detalle,
                Riesgo = riesgo,
            });
        }

        var resumen = categoria is not null
            ? $"Guía para una falla de {categoria.ToLowerInvariant()} en el equipo {contexto.CodigoEquipo}."
            : $"Guía general de diagnóstico para el equipo {contexto.CodigoEquipo}: el texto del reporte no alcanzó para encuadrar la falla.";

        if (contexto.AntiguedadMeses is { } meses && meses >= 48)
        {
            resumen += $" El equipo tiene {meses} meses: si la falla es de hardware, evaluar reposición antes de invertir horas.";
        }

        return Task.FromResult(ResultadoIA<GuiaReparacionIA>.Bien(new GuiaReparacionIA
        {
            Resumen = resumen,
            Pasos = pasos,
            CuandoEscalar = "Si después del último paso el síntoma sigue igual, o si algún paso marcado con "
                          + "riesgo no se puede hacer sin cortarle el trabajo al usuario, escalar en lugar de insistir.",
            // Baja a propósito: son recetas por categoría, no diagnóstico del caso.
            Confianza = antecedente is not null ? 0.55m : 0.40m,
        }));
    }

    /// <summary>
    /// Puntaje de un candidato. La especialidad manda sobre la carga: es mejor
    /// que el problema de red lo atienda el especialista con tres tickets que
    /// alguien libre que no sabe de redes.
    /// </summary>
    private static double Puntuar(TecnicoCandidato t, ContextoAsignacion ctx)
    {
        double puntaje = 0;

        if (ctx.EspecialidadRequerida is { } requerida)
        {
            var coincide = t.Especialidades.FirstOrDefault(e =>
                string.Equals(e.Nombre, requerida, StringComparison.OrdinalIgnoreCase));

            if (coincide is not null) puntaje += 50 + 10 * coincide.Nivel;
        }

        // Haber resuelto fallas de este mismo equipo vale: ya conoce el caso.
        puntaje += 5 * Math.Min(4, t.ResueltasEnEsteEquipo ?? 0);

        // La carga descuenta, con techo para que no domine a la especialidad.
        puntaje -= 3 * Math.Min(10, t.IncidenciasAbiertas ?? 0);

        return puntaje;
    }

    private static string Explicar(TecnicoCandidato t, ContextoAsignacion ctx)
    {
        var razones = new List<string>();

        if (ctx.EspecialidadRequerida is { } requerida &&
            t.Especialidades.FirstOrDefault(e =>
                string.Equals(e.Nombre, requerida, StringComparison.OrdinalIgnoreCase)) is { } esp)
        {
            razones.Add($"especialidad «{esp.Nombre}» nivel {esp.Nivel}");
        }

        if (t.IncidenciasAbiertas is { } abiertas)
            razones.Add($"{abiertas} incidencia{(abiertas == 1 ? "" : "s")} abierta{(abiertas == 1 ? "" : "s")}");

        if (t.ResueltasEnEsteEquipo is > 0 and { } resueltas)
        {
            razones.Add(resueltas == 1
                ? "ya resolvió un caso de este equipo"
                : $"ya resolvió {resueltas} casos de este equipo");
        }

        return razones.Count == 0
            ? "Único candidato disponible."
            : $"Seleccionado por {string.Join("; ", razones)}.";
    }

    private static string? ElegirPorPalabras(string texto, IReadOnlyList<string> posibles)
    {
        var mejor = PalabrasPorCategoria
            .Select(par => new
            {
                Categoria = par.Key,
                Coincidencias = par.Value.Count(p => texto.Contains(p, StringComparison.Ordinal)),
            })
            .Where(x => x.Coincidencias > 0)
            .OrderByDescending(x => x.Coincidencias)
            .ThenBy(x => x.Categoria, StringComparer.Ordinal)
            .FirstOrDefault();

        if (mejor is null) return null;

        // Sólo se sugiere una categoría que la organización realmente tenga.
        return posibles.FirstOrDefault(p =>
            string.Equals(Normalizar(p), Normalizar(mejor.Categoria), StringComparison.Ordinal));
    }

    private static string? ElegirPrioridad(string texto, IReadOnlyList<string> posibles)
    {
        foreach (var (prioridad, palabras) in PalabrasPorPrioridad)
        {
            if (!palabras.Any(p => texto.Contains(p, StringComparison.Ordinal))) continue;

            var encontrada = posibles.FirstOrDefault(p =>
                string.Equals(Normalizar(p), Normalizar(prioridad), StringComparison.Ordinal));

            if (encontrada is not null) return encontrada;
        }

        // Sin señales, no se inventa: se deja que decida la persona.
        return null;
    }

    /// <summary>
    /// Duplicado por parecido de título. Se compara por palabras significativas
    /// compartidas y no por texto exacto: nadie escribe dos veces igual.
    /// </summary>
    private static int? BuscarDuplicada(ContextoTriage ctx)
    {
        if (ctx.AbiertasDelEquipo.Count == 0) return null;

        var propias = PalabrasSignificativas(ctx.Titulo);
        if (propias.Count == 0) return null;

        var candidata = ctx.AbiertasDelEquipo
            .Select(i => new
            {
                i.Numero,
                Parecido = Jaccard(propias, PalabrasSignificativas(i.Titulo)),
            })
            .OrderByDescending(x => x.Parecido)
            .ThenBy(x => x.Numero)
            .First();

        // 0,6 es exigente a propósito: marcar como duplicada una incidencia que
        // no lo es hace que se pierda un reporte real, que es peor que no marcarla.
        return candidata.Parecido >= 0.6 ? candidata.Numero : null;
    }

    private static HashSet<string> PalabrasSignificativas(string texto) =>
        Normalizar(texto)
            .Split([' ', ',', '.', ';', ':', '-', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 3)
            .ToHashSet(StringComparer.Ordinal);

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var union = a.Count + b.Count - a.Count(b.Contains);
        return union == 0 ? 0 : (double)a.Count(b.Contains) / union;
    }

    /// <summary>
    /// Minúsculas y sin acentos. Los usuarios escriben «conexion» y «conexión»
    /// indistintamente, y una heurística que distinga las dos no sirve de nada.
    /// </summary>
    private static string Normalizar(string texto)
    {
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);

        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Fallo forzado para la demostración del CP-14 y del disyuntor. En uso
    /// normal <c>fallarCadaTantas</c> es null y nunca falla.
    /// </summary>
    private MotivoFalla? DebeFallar()
    {
        var cada = fallarCadaTantas?.Invoke() ?? 0;
        var n = Interlocked.Increment(ref _llamadas);

        if (cada <= 0) return null;
        return n % cada == 0 ? MotivoFalla.Timeout : null;
    }
}

/// <summary>
/// Proveedor que siempre falla. Es el doble de prueba del CP-14: no tiene otra
/// razón de existir, y por eso está acá y no en el proyecto de pruebas —también
/// lo usa la demostración en vivo desde la pantalla de Integración de IA—.
/// </summary>
public class ProveedorIACaido(MotivoFalla motivo = MotivoFalla.Timeout, string? detalle = null)
    : IProveedorIA
{
    public string Nombre => "Simulado (caído)";
    public int Intentos { get; private set; }

    public Task<ResultadoIA<ClasificacionIA>> ClasificarAsync(
        ContextoTriage contexto, CancellationToken ct = default)
    {
        Intentos++;
        return Task.FromResult(ResultadoIA<ClasificacionIA>.Mal(motivo, Detalle()));
    }

    public Task<ResultadoIA<RecomendacionIA>> RecomendarTecnicoAsync(
        ContextoAsignacion contexto, CancellationToken ct = default)
    {
        Intentos++;
        return Task.FromResult(ResultadoIA<RecomendacionIA>.Mal(motivo, Detalle()));
    }

    public Task<ResultadoIA<GuiaReparacionIA>> SugerirReparacionAsync(
        ContextoReparacion contexto, CancellationToken ct = default)
    {
        Intentos++;
        return Task.FromResult(ResultadoIA<GuiaReparacionIA>.Mal(motivo, Detalle()));
    }

    private string Detalle() => detalle ?? "El proveedor no respondió (simulación de caída).";
}
