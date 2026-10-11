namespace PredictIT.BLL.DTO;

/// <summary>
/// Lo que la API devuelve al frontend.
///
/// Son deliberadamente distintos de las entidades del dominio: el frontend no
/// necesita —ni debe recibir— el hash de la contraseña, ni el identificador de
/// organización, ni las claves foráneas que sólo sirven adentro. Además, un
/// cambio en el dominio no rompe el contrato con el frontend por accidente.
/// </summary>

public record SesionDto(
    Guid IdUsuario,
    string Username,
    string NombreCompleto,
    string Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Patentes,
    OrganizacionDto Organizacion,
    IReadOnlyCollection<OrganizacionDto> OrganizacionesDisponibles,
    string Token,
    DateTime Expira);

public record OrganizacionDto(
    Guid Id,
    string RazonSocial,
    string NombreCorto,
    string? Cuit,
    PlanDto? Plan);

public record PlanDto(
    string Codigo,
    string Nombre,
    decimal AbonoMensual,
    int EquiposIncluidos,
    decimal PrecioEquipoAdicional,
    string? Soporte);

/// <summary>
/// Situación del plan frente al parque real.
///
/// Es la pantalla de Organización: cuántos equipos hay contra cuántos incluye el
/// plan, y cuánto sale el mes. Se calcula en la BLL y no en el frontend para que
/// el número sea el mismo en cualquier consumidor.
/// </summary>
public record SituacionPlanDto(
    PlanDto Plan,
    int EquiposAdministrados,
    int EquiposIncluidos,
    int EquiposAdicionales,
    decimal AbonoBase,
    decimal CostoAdicionales,
    decimal TotalMensual,
    PlanAlternativoDto? Alternativa);

/// <summary>
/// Cuántos equipos hay en cada segmento del inventario.
///
/// Acompaña al listado y se recalcula con los mismos filtros: cada número dice
/// cuántos vería quien pulse ese segmento. Un total del parque que después no
/// coincide con lo que aparece es peor que no tener el número.
/// </summary>
public record SegmentosDto(int Todos, int RiesgoAlto, int PreventivoVencido, int GarantiaVencida);

/// <summary>
/// Los contadores que la navegación muestra al lado de cada sección.
///
/// Cada uno es nulo cuando el usuario no tiene la patente que lo habilita, y
/// no cero: cero es un dato —«no hay incidencias abiertas»— y la ausencia de
/// permiso no lo es. Con cero, el solicitante vería una insignia en «Activos»
/// afirmando que el parque está vacío.
///
/// <see cref="IncidenciasAbiertas"/> cuenta lo que cada perfil puede ver: las
/// de toda la organización para quien las atiende, las propias para quien las
/// reporta. Es la misma distinción que ya hace el menú.
/// </summary>
public record ResumenNavegacionDto(
    int? Equipos,
    int? IncidenciasAbiertas,
    int? MantenimientosVencidos);

public record PlanAlternativoDto(
    string Nombre,
    decimal TotalMensual,
    decimal Diferencia,
    bool Conviene,
    int EquiposDesdeLosQueConviene);

/// <summary>
/// Cuanto se uso el servicio de IA en un periodo, por clase de consulta.
///
/// Sale de la bitacora y no de un contador propio: cada consulta ya queda
/// asentada ahi con su codigo de evento, y un segundo contador seria un numero
/// mas que puede contradecir al registro de auditoria.
///
/// Se separan las que resolvio el proveedor de las que resolvio la heuristica
/// porque son dos cosas distintas de cara al costo: las primeras se pagan.
/// </summary>
/// <param name="Desde">Inicio del periodo, inclusive.</param>
/// <param name="Hasta">Fin del periodo, exclusive.</param>
public record ConsumoIaDto(
    DateTime Desde,
    DateTime Hasta,
    int ClasificacionesIa,
    int ClasificacionesHeuristica,
    int AsignacionesIa,
    int AsignacionesHeuristica,
    int AsignacionesRespaldo,
    int GuiasIa,
    int GuiasHeuristica)
{
    /// <summary>Las que le costaron algo a la organizacion.</summary>
    public int TotalIa => ClasificacionesIa + AsignacionesIa + GuiasIa;

    /// <summary>Las que resolvio el propio sistema, sin salir a la red.</summary>
    public int TotalPropio =>
        ClasificacionesHeuristica + AsignacionesHeuristica + AsignacionesRespaldo + GuiasHeuristica;
}

public record TipoEquipoDto(Guid Id, string Nombre);

public record EstadoEquipoDto(Guid Id, string Nombre, bool Operativo);

public record UbicacionDto(Guid Id, string Nombre, string? Descripcion);

public record CatalogosEquipoDto(
    IReadOnlyCollection<TipoEquipoDto> Tipos,
    IReadOnlyCollection<EstadoEquipoDto> Estados,
    IReadOnlyCollection<UbicacionDto> Ubicaciones,
    IReadOnlyCollection<ResponsableDto> Responsables);

public record ResponsableDto(Guid Id, string NombreCompleto);

/// <summary>Fila del listado de activos. Sólo lo que muestra la tabla.</summary>
public record EquipoListaDto(
    Guid Id,
    string Codigo,
    string Tipo,
    string MarcaModelo,
    string? NumeroSerie,
    string? Ubicacion,
    string? Responsable,
    string Estado,
    bool EstadoOperativo);

/// <summary>Ficha completa del equipo.</summary>
public record EquipoDetalleDto(
    Guid Id,
    string Codigo,
    Guid IdTipoEquipo,
    string Tipo,
    string? Marca,
    string? Modelo,
    string? NumeroSerie,
    string? DescripcionTecnica,
    DateTime FechaAlta,
    DateOnly? FechaAdquisicion,
    DateOnly? FechaFinGarantia,
    bool GarantiaVencida,
    int? DiasParaVencimientoGarantia,
    int? AntiguedadEnMeses,
    string? Proveedor,
    int Criticidad,
    Guid IdEstadoEquipo,
    string Estado,
    Guid? IdUbicacion,
    string? Ubicacion,
    Guid? IdResponsable,
    string? Responsable);

/// <summary>Lo que llega del formulario de alta y edición de un equipo.</summary>
/// <summary>Cambio de estado operativo de un equipo.</summary>
public record CambioEstadoEquipoDto
{
    public Guid IdEstado { get; init; }

    /// <summary>
    /// Por qué se cambia. Va a la bitácora: el estado dice que un equipo está
    /// fuera de servicio, y el motivo es lo que permite entender por qué.
    /// </summary>
    public string? Motivo { get; init; }
}

public class EquipoEntradaDto
{
    public string Codigo { get; set; } = string.Empty;
    public Guid IdTipoEquipo { get; set; }
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? NumeroSerie { get; set; }
    public string? DescripcionTecnica { get; set; }
    public DateOnly? FechaAdquisicion { get; set; }
    public DateOnly? FechaFinGarantia { get; set; }
    public string? Proveedor { get; set; }
    public int Criticidad { get; set; } = 2;
    public Guid IdEstadoEquipo { get; set; }
    public Guid? IdUbicacion { get; set; }
    public Guid? IdResponsable { get; set; }
}

public class PaginaDto<T>
{
    public IReadOnlyCollection<T> Items { get; init; } = Array.Empty<T>();
    public int Total { get; init; }
    public int Pagina { get; init; }
    public int PorPagina { get; init; }
    public int TotalPaginas => PorPagina == 0 ? 0 : (int)Math.Ceiling(Total / (double)PorPagina);
}
