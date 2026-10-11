using Microsoft.Data.SqlClient;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Helpers;
using PredictIT.DAO.Mappers;
using PredictIT.Domain.Negocio;

namespace PredictIT.DAO.Implementations;

/// <summary>
/// Acceso a las incidencias (RF-05 a RF-09).
///
/// Como en el resto de los DAO de negocio, toda consulta filtra por la
/// organización del contexto y ninguna deja elegir otra (ADR 0005).
/// </summary>
public class IncidenciaDao(SqlHelper sql, IContextoSesion contexto) : IIncidenciaDao
{
    private const string Seleccion = @"
        SELECT i.id_incidencia, i.id_organizacion, i.numero, i.titulo, i.descripcion,
               i.fecha, i.fecha_resolucion, i.id_equipo, i.id_estado_incidencia,
               i.id_prioridad, i.id_categoria, i.id_tecnico, i.id_usuario_reportante,
               i.tipo_asignacion, i.pendiente_revision, i.diagnostico, i.solucion,
               eq.codigo    AS equipo_codigo,
               es.nombre    AS estado_nombre,    es.es_final AS estado_es_final,
               pr.nombre    AS prioridad_nombre, pr.nivel    AS prioridad_nivel,
               pr.horas_objetivo AS prioridad_horas,
               ca.nombre    AS categoria_nombre, ca.id_especialidad AS categoria_especialidad
        FROM dbo.Incidencia i
        JOIN dbo.Equipo               eq ON eq.id_equipo            = i.id_equipo
        JOIN dbo.EstadoIncidencia     es ON es.id_estado_incidencia = i.id_estado_incidencia
        JOIN dbo.PrioridadIncidencia  pr ON pr.id_prioridad         = i.id_prioridad
        LEFT JOIN dbo.CategoriaIncidencia ca ON ca.id_categoria     = i.id_categoria";

    private readonly IncidenciaMapper _mapper = new();
    private readonly ClasificacionIncidenciaMapper _mapperClasificacion = new();
    private readonly RecomendacionAsignacionMapper _mapperRecomendacion = new();

    private SqlParameter Organizacion() => new("@org", contexto.IdOrganizacion);

    public Incidencia? GetById(Guid id) => sql.ConsultarUno(
        Seleccion + " WHERE i.id_incidencia = @id AND i.id_organizacion = @org;",
        _mapper.Map, new SqlParameter("@id", id), Organizacion());

    public IList<Incidencia> GetAll() => sql.Consultar(
        Seleccion + " WHERE i.id_organizacion = @org ORDER BY i.numero DESC;",
        _mapper.Map, Organizacion());

    public PaginaDe<Incidencia> Buscar(FiltroIncidencias filtro)
    {
        const string condiciones = @"
            WHERE i.id_organizacion = @org
              AND (@equipo    IS NULL OR i.id_equipo            = @equipo)
              AND (@estado    IS NULL OR i.id_estado_incidencia = @estado)
              AND (@prioridad IS NULL OR i.id_prioridad         = @prioridad)
              AND (@categoria IS NULL OR i.id_categoria         = @categoria)
              AND (@tecnico   IS NULL OR i.id_tecnico           = @tecnico)
              AND (@involucrado IS NULL
                   OR i.id_tecnico            = @involucrado
                   OR i.id_usuario_reportante = @involucrado)
              AND (@desde     IS NULL OR i.fecha               >= @desde)
              AND (@hasta     IS NULL OR i.fecha                < @hasta)
              AND (@abiertas  = 0     OR es.es_final            = 0)
              AND (@revision  = 0     OR i.pendiente_revision   = 1)
              AND (@texto     IS NULL
                   OR i.titulo      LIKE '%' + @texto + '%'
                   OR i.descripcion LIKE '%' + @texto + '%'
                   OR eq.codigo     LIKE '%' + @texto + '%'
                   OR CAST(i.numero AS VARCHAR(20)) = @texto)";

        SqlParameter[] Parametros() =>
        [
            Organizacion(),
            new SqlParameter("@equipo",    filtro.IdEquipo    ?? (object)DBNull.Value),
            new SqlParameter("@estado",    filtro.IdEstado    ?? (object)DBNull.Value),
            new SqlParameter("@prioridad", filtro.IdPrioridad ?? (object)DBNull.Value),
            new SqlParameter("@categoria", filtro.IdCategoria ?? (object)DBNull.Value),
            new SqlParameter("@tecnico",   filtro.IdTecnico   ?? (object)DBNull.Value),
            new SqlParameter("@involucrado", filtro.IdInvolucrado ?? (object)DBNull.Value),
            new SqlParameter("@desde",     filtro.Desde       ?? (object)DBNull.Value),
            // El hasta se compara con < : si se usara <=, el filtro «hasta el 31»
            // dejaría afuera todo lo cargado ese día después de medianoche.
            new SqlParameter("@hasta",     filtro.Hasta is { } h ? h.Date.AddDays(1) : (object)DBNull.Value),
            new SqlParameter("@abiertas",  filtro.SoloAbiertas ? 1 : 0),
            new SqlParameter("@revision",  filtro.SoloPendientesDeRevision ? 1 : 0),
            new SqlParameter("@texto",     string.IsNullOrWhiteSpace(filtro.Texto)
                                               ? DBNull.Value : filtro.Texto.Trim())
        ];

        var total = Convert.ToInt32(sql.EjecutarEscalar(@"
            SELECT COUNT(*)
            FROM dbo.Incidencia i
            JOIN dbo.Equipo           eq ON eq.id_equipo            = i.id_equipo
            JOIN dbo.EstadoIncidencia es ON es.id_estado_incidencia = i.id_estado_incidencia"
            + condiciones + ";", Parametros()) ?? 0);

        var pagina = Math.Max(1, filtro.Pagina);
        var porPagina = Math.Clamp(filtro.PorPagina, 1, 200);

        var parametros = Parametros()
            .Append(new SqlParameter("@saltar", (pagina - 1) * porPagina))
            .Append(new SqlParameter("@tomar", porPagina))
            .ToArray();

        var items = sql.Consultar(
            Seleccion + condiciones + @"
            ORDER BY i.numero DESC
            OFFSET @saltar ROWS FETCH NEXT @tomar ROWS ONLY;",
            _mapper.Map, parametros);

        return new PaginaDe<Incidencia>
        {
            Items = items,
            Total = total,
            Pagina = pagina,
            PorPagina = porPagina
        };
    }

    /// <summary>
    /// El correlativo lo calcula la base. Hacerlo en memoria —leer el máximo y
    /// sumar uno— daría el mismo número a dos altas simultáneas, y el número de
    /// incidencia es lo que el cliente cita por teléfono.
    /// </summary>
    public int ProximoNumero() => Convert.ToInt32(sql.EjecutarEscalar(
        "SELECT ISNULL(MAX(numero), 0) + 1 FROM dbo.Incidencia WHERE id_organizacion = @org;",
        Organizacion()) ?? 1);

    public IList<Incidencia> AbiertasDeEquipo(Guid idEquipo) => sql.Consultar(
        Seleccion + @"
        WHERE i.id_organizacion = @org AND i.id_equipo = @equipo AND es.es_final = 0
        ORDER BY i.numero DESC;",
        _mapper.Map, Organizacion(), new SqlParameter("@equipo", idEquipo));

    public IList<Incidencia> DeEquipoDesde(Guid idEquipo, DateTime desde) => sql.Consultar(
        Seleccion + @"
        WHERE i.id_organizacion = @org AND i.id_equipo = @equipo AND i.fecha >= @desde
        ORDER BY i.fecha DESC;",
        _mapper.Map,
        Organizacion(),
        new SqlParameter("@equipo", idEquipo),
        new SqlParameter("@desde", desde));

    public Guid Insert(Incidencia i)
    {
        if (i.Id == Guid.Empty) i.Id = Guid.NewGuid();
        i.IdOrganizacion = contexto.IdOrganizacion;
        if (i.Numero <= 0) i.Numero = ProximoNumero();

        const string comando = @"
            INSERT INTO dbo.Incidencia
                (id_incidencia, id_organizacion, numero, titulo, descripcion, fecha,
                 fecha_resolucion, id_equipo, id_estado_incidencia, id_prioridad, id_categoria,
                 id_tecnico, id_usuario_reportante, tipo_asignacion, pendiente_revision,
                 diagnostico, solucion)
            VALUES
                (@id, @org, @numero, @titulo, @desc, @fecha,
                 @resolucion, @equipo, @estado, @prioridad, @categoria,
                 @tecnico, @reportante, @tipoAsig, @revision,
                 @diagnostico, @solucion);";

        sql.EjecutarNoConsulta(comando, Parametros(i));
        return i.Id;
    }

    public void Update(Incidencia i)
    {
        const string comando = @"
            UPDATE dbo.Incidencia SET
                titulo = @titulo, descripcion = @desc, fecha_resolucion = @resolucion,
                id_equipo = @equipo, id_estado_incidencia = @estado, id_prioridad = @prioridad,
                id_categoria = @categoria, id_tecnico = @tecnico,
                tipo_asignacion = @tipoAsig, pendiente_revision = @revision,
                diagnostico = @diagnostico, solucion = @solucion
            WHERE id_incidencia = @id AND id_organizacion = @org;";

        sql.EjecutarNoConsulta(comando, Parametros(i));
    }

    /// <summary>
    /// No se borran incidencias: son el historial técnico del equipo y la base
    /// del análisis predictivo. Cerrarlas es cambiarles el estado.
    /// </summary>
    public void Delete(Guid id) => throw new NotSupportedException(
        "Las incidencias no se borran: son el historial que alimenta el análisis predictivo.");

    public void Asignar(
        Guid idIncidencia,
        Guid? idTecnico,
        TipoAsignacion tipo,
        bool pendienteRevision,
        Guid? idEstado = null) =>
        sql.EjecutarNoConsulta(@"
            UPDATE dbo.Incidencia
            SET id_tecnico = @tecnico,
                tipo_asignacion = @tipo,
                pendiente_revision = @revision,
                -- Con @estado nulo se deja el que tenía: el estado se mueve sólo
                -- cuando la BLL lo indica.
                id_estado_incidencia = ISNULL(@estado, id_estado_incidencia)
            WHERE id_incidencia = @id AND id_organizacion = @org;",
            new SqlParameter("@id", idIncidencia),
            Organizacion(),
            new SqlParameter("@tecnico", idTecnico ?? (object)DBNull.Value),
            new SqlParameter("@tipo", TipoAsignacionTexto.A(tipo)),
            new SqlParameter("@revision", pendienteRevision),
            new SqlParameter("@estado", idEstado ?? (object)DBNull.Value));

    public void Atender(Guid idIncidencia, Guid idEstado, string? diagnostico,
                        string? solucion, DateTime? fechaResolucion) =>
        sql.EjecutarNoConsulta(@"
            UPDATE dbo.Incidencia
            SET id_estado_incidencia = @estado,
                -- Con nulo se deja el texto que ya estaba: quien pasa a «en
                -- espera de repuesto» no tiene por qué volver a escribir el
                -- diagnóstico que cargó.
                diagnostico = ISNULL(@diagnostico, diagnostico),
                solucion = ISNULL(@solucion, solucion),
                fecha_resolucion = @resolucion
            WHERE id_incidencia = @id AND id_organizacion = @org;",
            new SqlParameter("@id", idIncidencia),
            Organizacion(),
            new SqlParameter("@estado", idEstado),
            new SqlParameter("@diagnostico", (object?)diagnostico ?? DBNull.Value),
            new SqlParameter("@solucion", (object?)solucion ?? DBNull.Value),
            new SqlParameter("@resolucion", fechaResolucion ?? (object)DBNull.Value));

    public void GuardarClasificacion(ClasificacionIncidencia c)
    {
        if (c.Id == Guid.Empty) c.Id = Guid.NewGuid();

        sql.EjecutarNoConsulta(@"
            INSERT INTO dbo.ClasificacionIncidencia
                (id_clasificacion, id_incidencia, id_categoria_sugerida, id_prioridad_sugerida,
                 id_incidencia_duplicada, confianza, origen, justificacion)
            VALUES (@id, @inc, @cat, @pri, @dup, @conf, @origen, @just);",
            new SqlParameter("@id", c.Id),
            new SqlParameter("@inc", c.IdIncidencia),
            new SqlParameter("@cat", c.IdCategoriaSugerida ?? (object)DBNull.Value),
            new SqlParameter("@pri", c.IdPrioridadSugerida ?? (object)DBNull.Value),
            new SqlParameter("@dup", c.IdIncidenciaDuplicada ?? (object)DBNull.Value),
            new SqlParameter("@conf", c.Confianza ?? (object)DBNull.Value),
            new SqlParameter("@origen", c.Origen),
            new SqlParameter("@just", c.Justificacion ?? (object)DBNull.Value));
    }

    public void GuardarRecomendacion(RecomendacionAsignacion r)
    {
        if (r.Id == Guid.Empty) r.Id = Guid.NewGuid();

        sql.EjecutarNoConsulta(@"
            INSERT INTO dbo.RecomendacionAsignacion
                (id_recomendacion, id_incidencia, id_tecnico_sugerido, justificacion,
                 contexto_enviado, origen, motivo_respaldo, fecha_hora)
            VALUES (@id, @inc, @tec, @just, @ctx, @origen, @motivo, @fecha);",
            new SqlParameter("@id", r.Id),
            // La fecha viaja explicita en vez de dejar que dispare el DEFAULT
            // del motor: asi la entidad que la BLL acaba de guardar tiene la
            // misma marca que la fila, y no un 0001-01-01 que despues sale en
            // la respuesta del alta.
            new SqlParameter("@fecha", r.FechaHora),
            new SqlParameter("@inc", r.IdIncidencia),
            new SqlParameter("@tec", r.IdTecnicoSugerido ?? (object)DBNull.Value),
            new SqlParameter("@just", r.Justificacion ?? (object)DBNull.Value),
            new SqlParameter("@ctx", r.ContextoEnviado ?? (object)DBNull.Value),
            new SqlParameter("@origen", r.Origen),
            new SqlParameter("@motivo", r.MotivoRespaldo ?? (object)DBNull.Value));
    }

    /// <summary>
    /// La última clasificación de la incidencia. El join contra Incidencia no es
    /// decorativo: ClasificacionIncidencia no tiene id_organizacion, así que sin
    /// el join se podría leer la clasificación de otra organización pasando el
    /// identificador.
    /// </summary>
    public ClasificacionIncidencia? ClasificacionDe(Guid idIncidencia) => sql.ConsultarUno(@"
        SELECT TOP 1 c.*
        FROM dbo.ClasificacionIncidencia c
        JOIN dbo.Incidencia i ON i.id_incidencia = c.id_incidencia
        WHERE c.id_incidencia = @inc AND i.id_organizacion = @org
        ORDER BY c.fecha DESC;",
        _mapperClasificacion.Map, new SqlParameter("@inc", idIncidencia), Organizacion());

    public RecomendacionAsignacion? RecomendacionDe(Guid idIncidencia) => sql.ConsultarUno(@"
        SELECT TOP 1 r.*
        FROM dbo.RecomendacionAsignacion r
        JOIN dbo.Incidencia i ON i.id_incidencia = r.id_incidencia
        WHERE r.id_incidencia = @inc AND i.id_organizacion = @org
        ORDER BY r.fecha_hora DESC;",
        _mapperRecomendacion.Map, new SqlParameter("@inc", idIncidencia), Organizacion());

    private SqlParameter[] Parametros(Incidencia i) =>
    [
        new SqlParameter("@id", i.Id),
        Organizacion(),
        new SqlParameter("@numero", i.Numero),
        new SqlParameter("@titulo", i.Titulo),
        new SqlParameter("@desc", i.Descripcion),
        new SqlParameter("@fecha", i.Fecha == default ? DateTime.Now : i.Fecha),
        new SqlParameter("@resolucion", i.FechaResolucion ?? (object)DBNull.Value),
        new SqlParameter("@equipo", i.IdEquipo),
        new SqlParameter("@estado", i.IdEstado),
        new SqlParameter("@prioridad", i.IdPrioridad),
        new SqlParameter("@categoria", i.IdCategoria ?? (object)DBNull.Value),
        new SqlParameter("@tecnico", i.IdTecnico ?? (object)DBNull.Value),
        new SqlParameter("@reportante", i.IdUsuarioReportante),
        new SqlParameter("@tipoAsig", TipoAsignacionTexto.A(i.TipoAsignacion)),
        new SqlParameter("@revision", i.PendienteRevision),
        new SqlParameter("@diagnostico", i.Diagnostico ?? (object)DBNull.Value),
        new SqlParameter("@solucion", i.Solucion ?? (object)DBNull.Value)
    ];
}

/// <summary>Acceso a los mantenimientos (RF-10).</summary>
public class MantenimientoDao(SqlHelper sql, IContextoSesion contexto) : IMantenimientoDao
{
    private const string Seleccion = @"
        SELECT m.id_mantenimiento, m.id_organizacion, m.id_equipo, m.id_tipo_mantenimiento,
               m.id_tecnico, m.id_incidencia, m.fecha, m.descripcion, m.resultado,
               m.repuestos, m.costo, m.observaciones,
               tm.nombre AS tipo_nombre, tm.es_preventivo AS tipo_es_preventivo
        FROM dbo.Mantenimiento m
        JOIN dbo.TipoMantenimiento tm ON tm.id_tipo_mantenimiento = m.id_tipo_mantenimiento";

    private readonly MantenimientoMapper _mapper = new();

    private SqlParameter Organizacion() => new("@org", contexto.IdOrganizacion);

    public Mantenimiento? GetById(Guid id) => sql.ConsultarUno(
        Seleccion + " WHERE m.id_mantenimiento = @id AND m.id_organizacion = @org;",
        _mapper.Map, new SqlParameter("@id", id), Organizacion());

    public IList<Mantenimiento> GetAll() => sql.Consultar(
        Seleccion + " WHERE m.id_organizacion = @org ORDER BY m.fecha DESC;",
        _mapper.Map, Organizacion());

    public IList<Mantenimiento> DeEquipo(Guid idEquipo) => sql.Consultar(
        Seleccion + " WHERE m.id_organizacion = @org AND m.id_equipo = @equipo ORDER BY m.fecha DESC;",
        _mapper.Map, Organizacion(), new SqlParameter("@equipo", idEquipo));

    public IList<Mantenimiento> DeEquipoDesde(Guid idEquipo, DateTime desde) => sql.Consultar(
        Seleccion + @"
        WHERE m.id_organizacion = @org AND m.id_equipo = @equipo AND m.fecha >= @desde
        ORDER BY m.fecha DESC;",
        _mapper.Map,
        Organizacion(),
        new SqlParameter("@equipo", idEquipo),
        new SqlParameter("@desde", desde));

    public Guid Insert(Mantenimiento m)
    {
        if (m.Id == Guid.Empty) m.Id = Guid.NewGuid();
        m.IdOrganizacion = contexto.IdOrganizacion;

        sql.EjecutarNoConsulta(@"
            INSERT INTO dbo.Mantenimiento
                (id_mantenimiento, id_organizacion, id_equipo, id_tipo_mantenimiento, id_tecnico,
                 id_incidencia, fecha, descripcion, resultado, repuestos, costo, observaciones)
            VALUES (@id, @org, @equipo, @tipo, @tecnico, @inc, @fecha, @desc, @res, @rep, @costo, @obs);",
            Parametros(m));

        return m.Id;
    }

    public void Update(Mantenimiento m) => sql.EjecutarNoConsulta(@"
        UPDATE dbo.Mantenimiento SET
            id_equipo = @equipo, id_tipo_mantenimiento = @tipo, id_tecnico = @tecnico,
            id_incidencia = @inc, fecha = @fecha, descripcion = @desc, resultado = @res,
            repuestos = @rep, costo = @costo, observaciones = @obs
        WHERE id_mantenimiento = @id AND id_organizacion = @org;",
        Parametros(m));

    public void Delete(Guid id) => throw new NotSupportedException(
        "Los mantenimientos no se borran: son el historial que alimenta el análisis predictivo.");

    private SqlParameter[] Parametros(Mantenimiento m) =>
    [
        new SqlParameter("@id", m.Id),
        Organizacion(),
        new SqlParameter("@equipo", m.IdEquipo),
        new SqlParameter("@tipo", m.IdTipo),
        new SqlParameter("@tecnico", m.IdTecnico),
        new SqlParameter("@inc", m.IdIncidencia ?? (object)DBNull.Value),
        new SqlParameter("@fecha", m.Fecha == default ? DateTime.Now : m.Fecha),
        new SqlParameter("@desc", m.Descripcion),
        new SqlParameter("@res", m.Resultado ?? (object)DBNull.Value),
        new SqlParameter("@rep", m.Repuestos ?? (object)DBNull.Value),
        new SqlParameter("@costo", m.Costo ?? (object)DBNull.Value),
        new SqlParameter("@obs", m.Observaciones ?? (object)DBNull.Value)
    ];
}
