using System.Data;
using Microsoft.Data.SqlClient;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Helpers;
using PredictIT.Domain.Negocio;

namespace PredictIT.DAO.Implementations;

public class PlanMantenimientoMapper : IObjectMapper<PlanMantenimiento>
{
    public PlanMantenimiento Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_plan"),
        IdOrganizacion = f.Guid("id_organizacion"),
        IdEquipo = f.GuidNulo("id_equipo"),
        CodigoEquipo = f.TextoNulo("equipo_codigo"),
        IdTipoEquipo = f.GuidNulo("id_tipo_equipo"),
        NombreTipoEquipo = f.TextoNulo("tipo_equipo_nombre"),
        IdTipoMantenimiento = f.Guid("id_tipo_mantenimiento"),
        NombreTipoMantenimiento = f.TextoNulo("tipo_nombre"),
        CadaDias = f.Entero("cada_dias"),
        Activo = f.Booleano("activo"),
        Descripcion = f.TextoNulo("descripcion"),
        FechaAlta = f.Fecha("fecha_alta"),
    };
}

public class MantenimientoProgramadoMapper : IObjectMapper<MantenimientoProgramado>
{
    public MantenimientoProgramado Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_programado"),
        IdOrganizacion = f.Guid("id_organizacion"),
        IdPlan = f.GuidNulo("id_plan"),
        IdEquipo = f.Guid("id_equipo"),
        CodigoEquipo = f.TextoNulo("equipo_codigo"),
        UbicacionEquipo = f.TextoNulo("ubicacion_nombre"),
        IdTipoMantenimiento = f.Guid("id_tipo_mantenimiento"),
        NombreTipoMantenimiento = f.TextoNulo("tipo_nombre"),
        FechaProgramada = f.SoloFechaNula("fecha_programada") ?? default,
        Estado = f.Texto("estado"),
        IdMantenimiento = f.GuidNulo("id_mantenimiento"),
        Motivo = f.TextoNulo("motivo"),
        FechaAlta = f.Fecha("fecha_alta"),
    };
}

/// <summary>
/// Planes de mantenimiento y las fechas concretas que salen de ellos.
///
/// El filtro por organización se aplica acá y no en el controlador, igual que
/// en el resto de los DAO: la organización viene del contexto de sesión y nunca
/// de la petición.
/// </summary>
public class MantenimientoProgramadoDao(SqlHelper sql, IContextoSesion contexto)
    : IMantenimientoProgramadoDao
{
    private const string SeleccionPlan = @"
        SELECT p.id_plan, p.id_organizacion, p.id_equipo, p.id_tipo_equipo,
               p.id_tipo_mantenimiento, p.cada_dias, p.activo, p.descripcion, p.fecha_alta,
               e.codigo AS equipo_codigo, te.nombre AS tipo_equipo_nombre,
               tm.nombre AS tipo_nombre
        FROM dbo.PlanMantenimiento p
        LEFT JOIN dbo.Equipo e      ON e.id_equipo = p.id_equipo
        LEFT JOIN dbo.TipoEquipo te ON te.id_tipo_equipo = p.id_tipo_equipo
        JOIN dbo.TipoMantenimiento tm ON tm.id_tipo_mantenimiento = p.id_tipo_mantenimiento";

    private const string SeleccionProgramado = @"
        SELECT g.id_programado, g.id_organizacion, g.id_plan, g.id_equipo,
               g.id_tipo_mantenimiento, g.fecha_programada, g.estado, g.id_mantenimiento,
               g.motivo, g.fecha_alta,
               e.codigo AS equipo_codigo, u.nombre AS ubicacion_nombre,
               tm.nombre AS tipo_nombre
        FROM dbo.MantenimientoProgramado g
        JOIN dbo.Equipo e             ON e.id_equipo = g.id_equipo
        LEFT JOIN dbo.Ubicacion u     ON u.id_ubicacion = e.id_ubicacion
        JOIN dbo.TipoMantenimiento tm ON tm.id_tipo_mantenimiento = g.id_tipo_mantenimiento";

    private readonly PlanMantenimientoMapper _planes = new();
    private readonly MantenimientoProgramadoMapper _programados = new();

    private SqlParameter Organizacion() => new("@org", contexto.IdOrganizacion);

    // ------------------------------------------------------------- planes

    public IList<PlanMantenimiento> Planes(bool soloActivos = false) => sql.Consultar(
        SeleccionPlan + (soloActivos ? " WHERE p.id_organizacion = @org AND p.activo = 1"
                                     : " WHERE p.id_organizacion = @org")
                      + " ORDER BY p.fecha_alta DESC;",
        _planes.Map, Organizacion());

    public PlanMantenimiento? PlanPorId(Guid id) => sql.ConsultarUno(
        SeleccionPlan + " WHERE p.id_plan = @id AND p.id_organizacion = @org;",
        _planes.Map, new SqlParameter("@id", id), Organizacion());

    public Guid GuardarPlan(PlanMantenimiento plan)
    {
        var nuevo = plan.Id == Guid.Empty;
        if (nuevo) plan.Id = Guid.NewGuid();
        plan.IdOrganizacion = contexto.IdOrganizacion;

        sql.EjecutarNoConsulta(nuevo
            ? @"INSERT INTO dbo.PlanMantenimiento
                    (id_plan, id_organizacion, id_equipo, id_tipo_equipo,
                     id_tipo_mantenimiento, cada_dias, activo, descripcion)
                VALUES (@id, @org, @equipo, @tipoEquipo, @tipo, @cada, @activo, @desc);"
            : @"UPDATE dbo.PlanMantenimiento SET
                    id_equipo = @equipo, id_tipo_equipo = @tipoEquipo,
                    id_tipo_mantenimiento = @tipo, cada_dias = @cada,
                    activo = @activo, descripcion = @desc
                WHERE id_plan = @id AND id_organizacion = @org;",
            new SqlParameter("@id", plan.Id),
            Organizacion(),
            new SqlParameter("@equipo", plan.IdEquipo ?? (object)DBNull.Value),
            new SqlParameter("@tipoEquipo", plan.IdTipoEquipo ?? (object)DBNull.Value),
            new SqlParameter("@tipo", plan.IdTipoMantenimiento),
            new SqlParameter("@cada", plan.CadaDias),
            new SqlParameter("@activo", plan.Activo),
            new SqlParameter("@desc", plan.Descripcion ?? (object)DBNull.Value));

        return plan.Id;
    }

    /// <summary>Los equipos que alcanza un plan, resueltos contra el parque.</summary>
    public IList<Guid> EquiposAlcanzados(Guid idPlan) => sql.Consultar(@"
        SELECT e.id_equipo
        FROM dbo.PlanMantenimiento p
        JOIN dbo.Equipo e ON e.id_organizacion = p.id_organizacion
                         AND (e.id_equipo = p.id_equipo OR e.id_tipo_equipo = p.id_tipo_equipo)
        JOIN dbo.EstadoEquipo ee ON ee.id_estado_equipo = e.id_estado_equipo
        WHERE p.id_plan = @plan AND p.id_organizacion = @org
          -- Un equipo dado de baja no se programa: no hay nada que mantener.
          AND ee.operativo = 1;",
        f => f.Guid("id_equipo"),
        new SqlParameter("@plan", idPlan), Organizacion());

    // -------------------------------------------------------- programados

    public MantenimientoProgramado? PorId(Guid id) => sql.ConsultarUno(
        SeleccionProgramado + " WHERE g.id_programado = @id AND g.id_organizacion = @org;",
        _programados.Map, new SqlParameter("@id", id), Organizacion());

    public IList<MantenimientoProgramado> Buscar(string? estado, DateOnly? hasta) => sql.Consultar(
        SeleccionProgramado + @"
        WHERE g.id_organizacion = @org
          AND (@estado IS NULL OR g.estado = @estado)
          AND (@hasta IS NULL OR g.fecha_programada <= @hasta)
        ORDER BY g.fecha_programada, e.codigo;",
        _programados.Map,
        Organizacion(),
        new SqlParameter("@estado", (object?)estado ?? DBNull.Value),
        new SqlParameter("@hasta", hasta.HasValue
            ? hasta.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value));

    public IList<MantenimientoProgramado> PendientesDeEquipo(Guid idEquipo) => sql.Consultar(
        SeleccionProgramado + @"
        WHERE g.id_organizacion = @org AND g.id_equipo = @equipo AND g.estado = 'PROGRAMADO'
        ORDER BY g.fecha_programada;",
        _programados.Map, Organizacion(), new SqlParameter("@equipo", idEquipo));

    /// <summary>Todos los pendientes de la organización, para el motor predictivo.</summary>
    public IList<MantenimientoProgramado> PendientesDeLaOrganizacion() => sql.Consultar(
        SeleccionProgramado + @"
        WHERE g.id_organizacion = @org AND g.estado = 'PROGRAMADO'
        ORDER BY g.fecha_programada;",
        _programados.Map, Organizacion());

    public bool YaProgramado(Guid idEquipo, Guid idTipo, DateOnly fecha) => sql.Consultar(@"
        SELECT TOP 1 1 AS existe FROM dbo.MantenimientoProgramado
        WHERE id_organizacion = @org AND id_equipo = @equipo
          AND id_tipo_mantenimiento = @tipo AND fecha_programada = @fecha
          AND estado = 'PROGRAMADO';",
        f => f.Entero("existe"),
        Organizacion(),
        new SqlParameter("@equipo", idEquipo),
        new SqlParameter("@tipo", idTipo),
        new SqlParameter("@fecha", fecha.ToDateTime(TimeOnly.MinValue))).Count > 0;

    public Guid Programar(MantenimientoProgramado p)
    {
        if (p.Id == Guid.Empty) p.Id = Guid.NewGuid();
        p.IdOrganizacion = contexto.IdOrganizacion;

        sql.EjecutarNoConsulta(@"
            INSERT INTO dbo.MantenimientoProgramado
                (id_programado, id_organizacion, id_plan, id_equipo,
                 id_tipo_mantenimiento, fecha_programada, estado, motivo)
            VALUES (@id, @org, @plan, @equipo, @tipo, @fecha, @estado, @motivo);",
            new SqlParameter("@id", p.Id),
            Organizacion(),
            new SqlParameter("@plan", p.IdPlan ?? (object)DBNull.Value),
            new SqlParameter("@equipo", p.IdEquipo),
            new SqlParameter("@tipo", p.IdTipoMantenimiento),
            new SqlParameter("@fecha", p.FechaProgramada.ToDateTime(TimeOnly.MinValue)),
            new SqlParameter("@estado", p.Estado),
            new SqlParameter("@motivo", p.Motivo ?? (object)DBNull.Value));

        return p.Id;
    }

    public void Reprogramar(Guid id, DateOnly fecha, string motivo) => sql.EjecutarNoConsulta(@"
        UPDATE dbo.MantenimientoProgramado
        SET fecha_programada = @fecha, motivo = @motivo
        WHERE id_programado = @id AND id_organizacion = @org AND estado = 'PROGRAMADO';",
        new SqlParameter("@id", id), Organizacion(),
        new SqlParameter("@fecha", fecha.ToDateTime(TimeOnly.MinValue)),
        new SqlParameter("@motivo", motivo));

    public void Anular(Guid id, string motivo) => sql.EjecutarNoConsulta(@"
        UPDATE dbo.MantenimientoProgramado
        SET estado = 'ANULADO', motivo = @motivo
        WHERE id_programado = @id AND id_organizacion = @org AND estado = 'PROGRAMADO';",
        new SqlParameter("@id", id), Organizacion(), new SqlParameter("@motivo", motivo));

    public void MarcarEjecutado(Guid id, Guid idMantenimiento) => sql.EjecutarNoConsulta(@"
        UPDATE dbo.MantenimientoProgramado
        SET estado = 'EJECUTADO', id_mantenimiento = @mant
        WHERE id_programado = @id AND id_organizacion = @org AND estado = 'PROGRAMADO';",
        new SqlParameter("@id", id), Organizacion(),
        new SqlParameter("@mant", idMantenimiento));
}
