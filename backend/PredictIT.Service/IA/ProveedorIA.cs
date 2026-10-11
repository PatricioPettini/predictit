using System.Text.Json.Serialization;

namespace PredictIT.Service.IA;

/// <summary>Un técnico candidato, tal como se le describe al proveedor.</summary>
public sealed record TecnicoCandidato
{
    public required Guid Id { get; init; }
    public required string NombreCompleto { get; init; }

    /// <summary>Especialidades con su nivel, 1 a 3. Vacío si la organización no las manda.</summary>
    public IReadOnlyList<EspecialidadNivel> Especialidades { get; init; } = [];

    /// <summary>Incidencias abiertas asignadas. Null si la organización no manda la carga.</summary>
    public int? IncidenciasAbiertas { get; init; }

    /// <summary>Incidencias que ya resolvió sobre este mismo equipo. Null si no se manda el historial.</summary>
    public int? ResueltasEnEsteEquipo { get; init; }
}

public sealed record EspecialidadNivel(string Nombre, int Nivel);

/// <summary>
/// Lo que se le manda al proveedor para decidir la asignación.
///
/// Se construye a partir de <c>ConfiguracionAsignacion</c>: lo que la
/// organización eligió no enviar, no viaja. Son datos de su gente.
/// </summary>
public sealed record ContextoAsignacion
{
    public required string TituloIncidencia { get; init; }
    public required string DescripcionIncidencia { get; init; }
    public string? Categoria { get; init; }
    public string? EspecialidadRequerida { get; init; }
    public string? Prioridad { get; init; }
    public required string CodigoEquipo { get; init; }
    public string? TipoEquipo { get; init; }
    public string? UbicacionEquipo { get; init; }
    public required IReadOnlyList<TecnicoCandidato> Candidatos { get; init; }
}

/// <summary>Lo que se le manda al proveedor para clasificar el texto libre del solicitante.</summary>
public sealed record ContextoTriage
{
    public required string Titulo { get; init; }
    public required string Descripcion { get; init; }
    public required IReadOnlyList<string> CategoriasPosibles { get; init; }
    public required IReadOnlyList<string> PrioridadesPosibles { get; init; }

    /// <summary>Incidencias abiertas del mismo equipo, para detectar duplicados.</summary>
    public IReadOnlyList<IncidenciaAbierta> AbiertasDelEquipo { get; init; } = [];
}

public sealed record IncidenciaAbierta(Guid Id, int Numero, string Titulo);

/// <summary>Resultado de la clasificación. Todo es sugerencia: el técnico puede cambiarlo.</summary>
public sealed record ClasificacionIA
{
    [JsonPropertyName("categoria")]
    public string? Categoria { get; init; }

    [JsonPropertyName("prioridad")]
    public string? Prioridad { get; init; }

    /// <summary>Número de la incidencia que ésta parece duplicar, o null.</summary>
    [JsonPropertyName("duplicadaDe")]
    public int? DuplicadaDe { get; init; }

    [JsonPropertyName("confianza")]
    public decimal? Confianza { get; init; }

    [JsonPropertyName("justificacion")]
    public string? Justificacion { get; init; }
}

/// <summary>Una falla anterior del mismo equipo, con lo que se hizo para resolverla.</summary>
public sealed record AntecedenteEquipo(int Numero, string Titulo, string? Diagnostico, string? Solucion);

/// <summary>
/// Lo que se le manda al proveedor para que arme la guía de reparación.
///
/// Incluye el historial del equipo a propósito: la falla que ya se resolvió una
/// vez en esa máquina es la primera candidata, y es información que un técnico
/// que recién entra no tiene en la cabeza.
/// </summary>
public sealed record ContextoReparacion
{
    public required string TituloIncidencia { get; init; }
    public required string DescripcionIncidencia { get; init; }
    public string? Categoria { get; init; }
    public string? Prioridad { get; init; }
    public required string CodigoEquipo { get; init; }
    public string? TipoEquipo { get; init; }
    public string? SistemaOperativo { get; init; }

    /// <summary>Antigüedad del equipo en meses. Null si no se conoce la fecha de compra.</summary>
    public int? AntiguedadMeses { get; init; }

    /// <summary>Incidencias ya resueltas de este mismo equipo, de la más reciente a la más vieja.</summary>
    public IReadOnlyList<AntecedenteEquipo> Antecedentes { get; init; } = [];
}

/// <summary>Un paso de la guía. El riesgo va aparte porque decide el orden y la advertencia.</summary>
public sealed record PasoReparacion
{
    [JsonPropertyName("orden")]
    public int Orden { get; init; }

    [JsonPropertyName("titulo")]
    public string Titulo { get; init; } = string.Empty;

    [JsonPropertyName("detalle")]
    public string? Detalle { get; init; }

    /// <summary>Qué puede salir mal. Null o vacío significa que el paso no tiene riesgo.</summary>
    [JsonPropertyName("riesgo")]
    public string? Riesgo { get; init; }
}

/// <summary>
/// Guía de reparación sugerida. Es asistencia al técnico, nunca una instrucción
/// que el sistema ejecute: PredictIT no toca el equipo del cliente.
/// </summary>
public sealed record GuiaReparacionIA
{
    [JsonPropertyName("resumen")]
    public string? Resumen { get; init; }

    [JsonPropertyName("pasos")]
    public IReadOnlyList<PasoReparacion> Pasos { get; init; } = [];

    /// <summary>Cuándo dejar de intentar y escalar. Es la parte que evita el daño.</summary>
    [JsonPropertyName("cuandoEscalar")]
    public string? CuandoEscalar { get; init; }

    [JsonPropertyName("confianza")]
    public decimal? Confianza { get; init; }
}

/// <summary>Resultado de la recomendación de asignación.</summary>
public sealed record RecomendacionIA
{
    [JsonPropertyName("idTecnico")]
    public Guid? IdTecnico { get; init; }

    [JsonPropertyName("justificacion")]
    public string? Justificacion { get; init; }

    [JsonPropertyName("confianza")]
    public decimal? Confianza { get; init; }
}

/// <summary>
/// Por qué una consulta al proveedor no sirvió. Distingue los cuatro casos que el
/// ADR 0006 define como fallo, porque el motivo se le muestra al administrador y
/// «no respondió» y «recomendó a alguien que no existe» piden acciones distintas.
/// </summary>
public enum MotivoFalla
{
    Error,
    Timeout,
    RespuestaMalformada,
    TecnicoInexistente,
    CircuitoAbierto,
}

/// <summary>Respuesta del proveedor: o el valor, o el motivo por el que no hay valor.</summary>
public sealed record ResultadoIA<T> where T : class
{
    public T? Valor { get; init; }
    public MotivoFalla? Falla { get; init; }
    public string? Detalle { get; init; }

    public bool Ok => Valor is not null;

    public static ResultadoIA<T> Bien(T valor) => new() { Valor = valor };

    public static ResultadoIA<T> Mal(MotivoFalla motivo, string detalle) =>
        new() { Falla = motivo, Detalle = detalle };
}

/// <summary>
/// Proveedor de inteligencia artificial (patrón Adapter, ADR 0006).
///
/// La interfaz existe para que el sistema no dependa de un servicio externo
/// concreto: hay una implementación contra la API de Anthropic y otra
/// determinística sin red. Se conmutan por configuración, sin recompilar.
///
/// Ninguna implementación lanza por un fallo del servicio: devuelven un
/// <see cref="ResultadoIA{T}"/> con el motivo. Que el servicio externo no
/// responda es un caso previsto del negocio, no una excepción.
/// </summary>
public interface IProveedorIA
{
    /// <summary>Nombre para mostrar y para la bitácora.</summary>
    string Nombre { get; }

    Task<ResultadoIA<ClasificacionIA>> ClasificarAsync(
        ContextoTriage contexto, CancellationToken ct = default);

    Task<ResultadoIA<RecomendacionIA>> RecomendarTecnicoAsync(
        ContextoAsignacion contexto, CancellationToken ct = default);

    /// <summary>
    /// Guía paso a paso para resolver la incidencia, ordenada de menos a más
    /// invasiva. Es asistencia al técnico: el sistema no ejecuta nada sobre el
    /// equipo, y cada paso que implique riesgo viene marcado.
    /// </summary>
    Task<ResultadoIA<GuiaReparacionIA>> SugerirReparacionAsync(
        ContextoReparacion contexto, CancellationToken ct = default);
}
