namespace PredictIT.Domain.Negocio;

/// <summary>
/// Cada cuánto le toca el preventivo a un equipo, o a todos los de un tipo.
///
/// El alcance es uno u otro y nunca los dos: un plan sin alcance no se puede
/// aplicar y uno con los dos no dice cuál manda. La base lo garantiza con un
/// CHECK, y el negocio lo valida antes para poder explicarlo.
/// </summary>
public class PlanMantenimiento
{
    public Guid Id { get; set; }
    public Guid IdOrganizacion { get; set; }

    /// <summary>Alcance por equipo puntual. Excluyente con <see cref="IdTipoEquipo"/>.</summary>
    public Guid? IdEquipo { get; set; }
    public string? CodigoEquipo { get; set; }

    /// <summary>Alcance por tipo: alcanza a todos los equipos de ese tipo.</summary>
    public Guid? IdTipoEquipo { get; set; }
    public string? NombreTipoEquipo { get; set; }

    public Guid IdTipoMantenimiento { get; set; }
    public string? NombreTipoMantenimiento { get; set; }

    /// <summary>Cada cuántos días le toca. Entre una semana y diez años.</summary>
    public int CadaDias { get; set; }

    public bool Activo { get; set; } = true;
    public string? Descripcion { get; set; }
    public DateTime FechaAlta { get; set; }

    /// <summary>
    /// Cómo se lo nombra en una lista, sin repetir la lógica en cada pantalla.
    ///
    /// El tipo va entrecomillado y no concordado: los nombres de los tipos
    /// salen de un catálogo que el usuario carga, y «Todos los Notebook» o
    /// «Todos los Impresora» es lo que sale de concordar con un nombre que no
    /// se conoce de antemano.
    /// </summary>
    public string Alcance =>
        CodigoEquipo ?? (NombreTipoEquipo is null ? "—" : $"Todos los equipos «{NombreTipoEquipo}»");
}

/// <summary>Estados de un mantenimiento programado. «Vencido» no está: se deriva.</summary>
public static class EstadoProgramado
{
    public const string Programado = "PROGRAMADO";
    public const string Ejecutado = "EJECUTADO";
    public const string Anulado = "ANULADO";

    public static readonly string[] Todos = [Programado, Ejecutado, Anulado];
}

/// <summary>
/// Una fecha concreta en la que hay que hacerle el preventivo a un equipo.
///
/// <b>Vencido no es un estado.</b> Es <c>PROGRAMADO</c> con la fecha pasada, y
/// se calcula cada vez que se mira. Guardarlo como estado obligaría a un
/// proceso que recorra la tabla todos los días para moverlo, y el día que ese
/// proceso no corra el sistema mostraría datos viejos como si fueran ciertos.
/// </summary>
public class MantenimientoProgramado
{
    public Guid Id { get; set; }
    public Guid IdOrganizacion { get; set; }

    /// <summary>Nulo si se programó a mano y no desde un plan.</summary>
    public Guid? IdPlan { get; set; }

    public Guid IdEquipo { get; set; }
    public string? CodigoEquipo { get; set; }
    public string? UbicacionEquipo { get; set; }

    public Guid IdTipoMantenimiento { get; set; }
    public string? NombreTipoMantenimiento { get; set; }

    public DateOnly FechaProgramada { get; set; }
    public string Estado { get; set; } = EstadoProgramado.Programado;

    /// <summary>El mantenimiento que lo ejecutó, cuando se ejecutó.</summary>
    public Guid? IdMantenimiento { get; set; }

    /// <summary>Por qué se reprogramó o se anuló. Queda escrito, no se pierde.</summary>
    public string? Motivo { get; set; }

    public DateTime FechaAlta { get; set; }

    /// <summary>Días de atraso a una fecha dada. Negativo si todavía no venció.</summary>
    public int DiasDeAtraso(DateOnly hoy) => hoy.DayNumber - FechaProgramada.DayNumber;

    /// <summary>Está pendiente y la fecha ya pasó.</summary>
    public bool EstaVencido(DateOnly hoy) =>
        Estado == EstadoProgramado.Programado && FechaProgramada < hoy;
}
