namespace PredictIT.BLL.DTO;

/// <summary>Un plan de mantenimiento preventivo, como se lista.</summary>
/// <param name="EquiposAlcanzados">
/// Cuántos equipos operativos alcanza hoy. Un plan que no alcanza a ninguno
/// está bien formado y no sirve para nada, y eso hay que verlo.
/// </param>
public record PlanMantenimientoDto(
    Guid Id,
    Guid? IdEquipo,
    Guid? IdTipoEquipo,
    string Alcance,
    Guid IdTipoMantenimiento,
    string TipoMantenimiento,
    int CadaDias,
    bool Activo,
    string? Descripcion,
    int EquiposAlcanzados);

public record PlanMantenimientoEntradaDto
{
    public Guid? IdEquipo { get; init; }
    public Guid? IdTipoEquipo { get; init; }
    public Guid IdTipoMantenimiento { get; init; }
    public int CadaDias { get; init; }
    public bool Activo { get; init; } = true;
    public string? Descripcion { get; init; }
}

/// <summary>
/// Una fecha concreta en la que hay que hacerle el preventivo a un equipo.
/// </summary>
/// <param name="DiasDeAtraso">
/// Positivo si ya venció, negativo si falta. Se calcula en el servidor y no en
/// la pantalla: el navegador puede tener otra fecha, y entonces dos personas
/// verían distinto qué está vencido.
/// </param>
public record MantenimientoProgramadoDto(
    Guid Id,
    Guid IdEquipo,
    string CodigoEquipo,
    string? Ubicacion,
    Guid IdTipoMantenimiento,
    string TipoMantenimiento,
    DateOnly FechaProgramada,
    string Estado,
    bool Vencido,
    int DiasDeAtraso,
    Guid? IdPlan,
    Guid? IdMantenimiento,
    string? Motivo);

public record ProgramarEntradaDto
{
    public Guid IdEquipo { get; init; }
    public Guid IdTipoMantenimiento { get; init; }
    public DateOnly FechaProgramada { get; init; }
    public string? Motivo { get; init; }
}

public record ReprogramarEntradaDto
{
    public DateOnly FechaProgramada { get; init; }
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>Lo que dejó una corrida de generación desde los planes.</summary>
public record GeneracionDto(int PlanesActivos, int Generados, int YaEstaban);

/// <summary>El panel de mantenimiento programado.</summary>
public record PanelProgramadoDto(
    int Vencidos,
    int ProximosSieteDias,
    int ProgramadosTotal,
    int PlanesActivos,
    IReadOnlyList<MantenimientoProgramadoDto> Proximos);
