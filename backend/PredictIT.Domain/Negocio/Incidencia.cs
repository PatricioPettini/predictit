namespace PredictIT.Domain.Negocio;

public class EstadoIncidencia
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Un estado terminal cierra el ciclo de la incidencia. Se guarda como dato y
    /// no se deduce del nombre: la organización puede renombrar sus estados.
    /// </summary>
    public bool EsFinal { get; set; }

    public int Orden { get; set; }
    public override string ToString() => Nombre;
}

public class PrioridadIncidencia
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>De 1 (baja) a 4 (crítica). Pondera el scoring de riesgo.</summary>
    public int Nivel { get; set; }

    /// <summary>
    /// Horas comprometidas de resolución. Null si la prioridad no tiene objetivo
    /// pactado, y en ese caso no se puede afirmar que se incumplió nada.
    /// </summary>
    public int? HorasObjetivo { get; set; }

    public override string ToString() => Nombre;
}

/// <summary>
/// Catálogo global, no por organización: el nombre es único en todo el sistema.
/// Las categorías son vocabulario técnico compartido —«Hardware», «Red»— y no
/// algo que cada cliente redefina.
/// </summary>
public class CategoriaIncidencia
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Especialidad que la resuelve. Alimenta el contexto de asignación.</summary>
    public Guid? IdEspecialidad { get; set; }

    public override string ToString() => Nombre;
}

public class Especialidad
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public override string ToString() => Nombre;
}

/// <summary>Cómo quedó asignada una incidencia. Es trazabilidad, no configuración.</summary>
public enum TipoAsignacion
{
    /// <summary>Todavía sin técnico.</summary>
    Pendiente,

    /// <summary>La decidió el proveedor de IA.</summary>
    Ia,

    /// <summary>
    /// La decidió el proveedor determinístico por heurísticas. Va aparte de
    /// <see cref="Ia"/> a propósito: son decisiones de distinta naturaleza y la
    /// trazabilidad tiene que poder distinguirlas.
    /// </summary>
    Heuristica,

    /// <summary>La decidió la estrategia de respaldo porque la IA no respondió.</summary>
    Respaldo,

    /// <summary>La decidió una persona.</summary>
    Manual,
}

public static class TipoAsignacionTexto
{
    public static string A(TipoAsignacion t) => t switch
    {
        TipoAsignacion.Ia => "IA",
        TipoAsignacion.Heuristica => "HEURISTICA",
        TipoAsignacion.Respaldo => "RESPALDO",
        TipoAsignacion.Manual => "MANUAL",
        _ => "PENDIENTE",
    };

    public static TipoAsignacion De(string? valor) => valor?.ToUpperInvariant() switch
    {
        "IA" => TipoAsignacion.Ia,
        "HEURISTICA" => TipoAsignacion.Heuristica,
        "RESPALDO" => TipoAsignacion.Respaldo,
        "MANUAL" => TipoAsignacion.Manual,
        _ => TipoAsignacion.Pendiente,
    };
}

/// <summary>
/// Falla reportada sobre un equipo (RF-05 a RF-09).
///
/// <see cref="IdTecnico"/> e <see cref="IdUsuarioReportante"/> apuntan a usuarios
/// de la base de servicio: son referencias lógicas, sin clave foránea, porque
/// cruzan el límite entre las dos bases.
/// </summary>
public class Incidencia
{
    public Guid Id { get; set; }
    public Guid IdOrganizacion { get; set; }

    /// <summary>Correlativo visible, por organización. Es el número que el cliente cita.</summary>
    public int Numero { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }
    public DateTime? FechaResolucion { get; set; }

    public Guid IdEquipo { get; set; }
    public Equipo? Equipo { get; set; }

    public Guid IdEstado { get; set; }
    public EstadoIncidencia? Estado { get; set; }

    public Guid IdPrioridad { get; set; }
    public PrioridadIncidencia? Prioridad { get; set; }

    public Guid? IdCategoria { get; set; }
    public CategoriaIncidencia? Categoria { get; set; }

    public Guid? IdTecnico { get; set; }
    public Guid IdUsuarioReportante { get; set; }

    public TipoAsignacion TipoAsignacion { get; set; } = TipoAsignacion.Pendiente;

    /// <summary>
    /// La marca la asignación de respaldo (CP-14): la IA no respondió, se asignó
    /// por carga, y una persona tiene que revisar que el técnico sea el adecuado.
    /// </summary>
    public bool PendienteRevision { get; set; }

    public string? Diagnostico { get; set; }
    public string? Solucion { get; set; }

    public bool Cerrada => FechaResolucion is not null;

    /// <summary>Horas que llevó resolverla, o que lleva abierta si sigue abierta.</summary>
    public double HorasTranscurridas(DateTime hasta) =>
        ((FechaResolucion ?? hasta) - Fecha).TotalHours;

    /// <summary>
    /// Si se pasó del SLA de su prioridad. Sin prioridad cargada no se puede
    /// afirmar que se incumplió, así que devuelve false.
    /// </summary>
    public bool FueraDeObjetivo(DateTime hasta) =>
        Prioridad?.HorasObjetivo is { } horas && HorasTranscurridas(hasta) > horas;

    public override string ToString() => $"#{Numero} {Titulo}";
}

public class TipoMantenimiento
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Un mantenimiento preventivo reinicia el contador de la regla de
    /// mantenimiento vencido; uno correctivo no, porque es una reparación.
    /// </summary>
    public bool EsPreventivo { get; set; }

    public override string ToString() => Nombre;
}

/// <summary>Intervención sobre un equipo, preventiva o correctiva (RF-10).</summary>
public class Mantenimiento
{
    public Guid Id { get; set; }
    public Guid IdOrganizacion { get; set; }
    public Guid IdEquipo { get; set; }
    public Equipo? Equipo { get; set; }

    public Guid IdTipo { get; set; }
    public TipoMantenimiento? Tipo { get; set; }

    public DateTime Fecha { get; set; }

    public string Descripcion { get; set; } = string.Empty;
    public string? Resultado { get; set; }
    public string? Repuestos { get; set; }
    public string? Observaciones { get; set; }
    public decimal? Costo { get; set; }

    /// <summary>Técnico que lo ejecutó. Usuario de la base de servicio.</summary>
    public Guid IdTecnico { get; set; }

    /// <summary>Incidencia que lo originó, si fue correctivo por un reporte.</summary>
    public Guid? IdIncidencia { get; set; }

    public override string ToString() => $"{Tipo?.Nombre ?? "Mantenimiento"} {Fecha:dd/MM/yyyy}";
}

/// <summary>Especialidades de un técnico, con su nivel. Contexto para la asignación.</summary>
public class TecnicoEspecialidad
{
    public Guid IdOrganizacion { get; set; }
    public Guid IdTecnico { get; set; }
    public Guid IdEspecialidad { get; set; }
    public Especialidad? Especialidad { get; set; }

    /// <summary>De 1 (básico) a 3 (referente).</summary>
    public int Nivel { get; set; } = 1;
}
