using Microsoft.Data.SqlClient;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Helpers;
using PredictIT.DAO.Mappers;
using PredictIT.Domain.Negocio;

namespace PredictIT.DAO.Implementations;

/// <summary>
/// Catálogos de negocio.
///
/// Los tipos y estados de equipo son globales: los comparten todas las
/// organizaciones. Las ubicaciones no, porque son sectores físicos de una
/// empresa concreta, así que ésas sí filtran por el contexto.
/// </summary>
public class CatalogoDao : ICatalogoDao
{
    private readonly SqlHelper _sql;
    private readonly IContextoSesion _contexto;

    public CatalogoDao(SqlHelper sql, IContextoSesion contexto)
    {
        _sql = sql;
        _contexto = contexto;
    }

    public IList<TipoEquipo> TiposDeEquipo() => _sql.Consultar(
        "SELECT id_tipo_equipo, nombre FROM dbo.TipoEquipo ORDER BY nombre;",
        new TipoEquipoMapper().Map);

    public IList<EstadoEquipo> EstadosDeEquipo() => _sql.Consultar(
        "SELECT id_estado_equipo, nombre, operativo, orden FROM dbo.EstadoEquipo ORDER BY orden;",
        new EstadoEquipoMapper().Map);

    public IList<EstadoIncidencia> EstadosDeIncidencia() => _sql.Consultar(
        "SELECT id_estado_incidencia, nombre, es_final, orden FROM dbo.EstadoIncidencia ORDER BY orden;",
        new EstadoIncidenciaMapper().Map);

    public IList<PrioridadIncidencia> Prioridades() => _sql.Consultar(
        "SELECT id_prioridad, nombre, nivel, horas_objetivo FROM dbo.PrioridadIncidencia ORDER BY nivel;",
        new PrioridadIncidenciaMapper().Map);

    public IList<CategoriaIncidencia> Categorias() => _sql.Consultar(
        "SELECT id_categoria, nombre, descripcion, id_especialidad FROM dbo.CategoriaIncidencia ORDER BY nombre;",
        new CategoriaIncidenciaMapper().Map);

    public IList<Especialidad> Especialidades() => _sql.Consultar(
        "SELECT id_especialidad, nombre FROM dbo.Especialidad ORDER BY nombre;",
        new EspecialidadMapper().Map);

    public IList<TipoMantenimiento> TiposDeMantenimiento() => _sql.Consultar(
        "SELECT id_tipo_mantenimiento, nombre, es_preventivo FROM dbo.TipoMantenimiento ORDER BY nombre;",
        new TipoMantenimientoMapper().Map);

    public IList<EstadoAlerta> EstadosDeAlerta() => _sql.Consultar(
        "SELECT id_estado_alerta, nombre, es_final FROM dbo.EstadoAlerta ORDER BY nombre;",
        f => new EstadoAlerta
        {
            Id = f.Guid("id_estado_alerta"),
            Nombre = f.Texto("nombre"),
            EsFinal = f.Booleano("es_final")
        });

    /// <summary>
    /// El estado inicial es el de menor orden entre los no terminales, y no uno
    /// buscado por nombre: la organización puede renombrar sus estados y el alta
    /// de incidencias no se puede romper por eso.
    /// </summary>
    public EstadoIncidencia? EstadoIncidenciaInicial() => _sql.ConsultarUno(@"
        SELECT TOP 1 id_estado_incidencia, nombre, es_final, orden
        FROM dbo.EstadoIncidencia WHERE es_final = 0 ORDER BY orden;",
        new EstadoIncidenciaMapper().Map);

    /// <summary>
    /// Una alerta nace «Activa»: es el significado del estado, no una posición
    /// en una lista. EstadoAlerta no tiene columna de orden, así que se busca por
    /// nombre con respaldo a cualquier estado no terminal —si alguien renombró
    /// «Activa», es mejor que la alerta nazca en otro estado abierto que que el
    /// motor no pueda generarla—.
    /// </summary>
    public EstadoIncidencia? EstadoIncidenciaAsignada() => _sql.ConsultarUno(@"
        SELECT TOP 1 id_estado_incidencia, nombre, es_final, orden
        FROM dbo.EstadoIncidencia
        WHERE es_final = 0 AND orden > (SELECT MIN(orden) FROM dbo.EstadoIncidencia WHERE es_final = 0)
        ORDER BY orden;",
        new EstadoIncidenciaMapper().Map);

    public EstadoAlerta? EstadoAlertaInicial() => _sql.ConsultarUno(@"
        SELECT TOP 1 id_estado_alerta, nombre, es_final
        FROM dbo.EstadoAlerta
        WHERE es_final = 0
        ORDER BY CASE WHEN nombre = 'Activa' THEN 0 ELSE 1 END, nombre;",
        f => new EstadoAlerta
        {
            Id = f.Guid("id_estado_alerta"),
            Nombre = f.Texto("nombre"),
            EsFinal = f.Booleano("es_final")
        });

    public IList<Ubicacion> Ubicaciones() => _sql.Consultar(@"
        SELECT id_ubicacion, id_organizacion, nombre, descripcion, activa
        FROM dbo.Ubicacion
        WHERE id_organizacion = @org AND activa = 1
        ORDER BY nombre;",
        new UbicacionMapper().Map,
        new SqlParameter("@org", _contexto.IdOrganizacion));
}

public class OrganizacionDao : IOrganizacionDao
{
    private readonly SqlHelper _sql;

    public OrganizacionDao(SqlHelper sql) => _sql = sql;

    public Organizacion? GetById(Guid id)
    {
        var org = _sql.ConsultarUno(@"
            SELECT id_organizacion, razon_social, nombre_corto, cuit, activa, fecha_alta, id_plan
            FROM dbo.Organizacion WHERE id_organizacion = @id;",
            new OrganizacionMapper().Map, new SqlParameter("@id", id));

        if (org is not null && org.IdPlan is { } idPlan)
        {
            org.Plan = PlanPorId(idPlan);
        }
        return org;
    }

    public PlanComercial? PlanDe(Guid idOrganizacion) => _sql.ConsultarUno(@"
        SELECT p.id_plan, p.codigo, p.nombre, p.abono_mensual, p.equipos_incluidos,
               p.precio_equipo_adicional, p.soporte, p.vigente_desde
        FROM dbo.PlanComercial p
        JOIN dbo.Organizacion o ON o.id_plan = p.id_plan
        WHERE o.id_organizacion = @org;",
        new PlanComercialMapper().Map, new SqlParameter("@org", idOrganizacion));

    public IList<Organizacion> Activas() => _sql.Consultar(@"
        SELECT id_organizacion, razon_social, nombre_corto, cuit, activa, fecha_alta, id_plan
        FROM dbo.Organizacion WHERE activa = 1 ORDER BY razon_social;",
        new OrganizacionMapper().Map);

    public IList<PlanComercial> TodosLosPlanes() => _sql.Consultar(@"
        SELECT id_plan, codigo, nombre, abono_mensual, equipos_incluidos,
               precio_equipo_adicional, soporte, vigente_desde
        FROM dbo.PlanComercial ORDER BY abono_mensual;",
        new PlanComercialMapper().Map);

    private PlanComercial? PlanPorId(Guid id) => _sql.ConsultarUno(@"
        SELECT id_plan, codigo, nombre, abono_mensual, equipos_incluidos,
               precio_equipo_adicional, soporte, vigente_desde
        FROM dbo.PlanComercial WHERE id_plan = @id;",
        new PlanComercialMapper().Map, new SqlParameter("@id", id));
}
