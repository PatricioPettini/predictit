using Microsoft.Data.SqlClient;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Helpers;
using PredictIT.DAO.Mappers;
using PredictIT.Domain.Negocio;

namespace PredictIT.DAO.Implementations;

/// <summary>
/// Acceso a los activos informáticos.
///
/// Toda consulta filtra por la organización del contexto de sesión, sin
/// excepción y sin que el llamador pueda elegir otra. No existe un método que
/// devuelva equipos "de todas las organizaciones": si existiera, alcanzaría un
/// descuido para filtrar datos entre clientes (ADR 0005).
/// </summary>
public class EquipoDao : IEquipoDao
{
    private const string Seleccion = @"
        SELECT e.id_equipo, e.id_organizacion, e.codigo, e.id_tipo_equipo, e.marca, e.modelo,
               e.numero_serie, e.descripcion_tecnica, e.fecha_alta, e.fecha_adquisicion,
               e.fecha_fin_garantia, e.proveedor, e.criticidad, e.id_estado_equipo,
               e.id_ubicacion, e.id_responsable,
               te.nombre  AS tipo_nombre,
               ee.nombre  AS estado_nombre, ee.operativo AS estado_operativo,
               u.nombre   AS ubicacion_nombre
        FROM dbo.Equipo e
        JOIN dbo.TipoEquipo   te ON te.id_tipo_equipo   = e.id_tipo_equipo
        JOIN dbo.EstadoEquipo ee ON ee.id_estado_equipo = e.id_estado_equipo
        LEFT JOIN dbo.Ubicacion u ON u.id_ubicacion     = e.id_ubicacion";

    private readonly SqlHelper _sql;
    private readonly IContextoSesion _contexto;
    private readonly EquipoMapper _mapper = new();

    public EquipoDao(SqlHelper sql, IContextoSesion contexto)
    {
        _sql = sql;
        _contexto = contexto;
    }

    private SqlParameter Organizacion() => new("@org", _contexto.IdOrganizacion);

    public Equipo? GetById(Guid id) => _sql.ConsultarUno(
        Seleccion + " WHERE e.id_equipo = @id AND e.id_organizacion = @org;",
        _mapper.Map, new SqlParameter("@id", id), Organizacion());

    public IList<Equipo> GetAll() => _sql.Consultar(
        Seleccion + " WHERE e.id_organizacion = @org ORDER BY e.codigo;",
        _mapper.Map, Organizacion());

    public PaginaDe<Equipo> Buscar(FiltroEquipos filtro)
    {
        // Los filtros se arman como condiciones opcionales sobre parámetros que
        // pueden venir nulos. Es la forma de tener una sola sentencia en vez de
        // concatenar SQL según qué filtros llegaron.
        var condiciones = @"
            WHERE e.id_organizacion = @org
              AND (@tipo     IS NULL OR e.id_tipo_equipo    = @tipo)
              AND (@estado   IS NULL OR e.id_estado_equipo  = @estado)
              AND (@ubic     IS NULL OR e.id_ubicacion      = @ubic)
              AND (@resp     IS NULL OR e.id_responsable    = @resp)
              AND (@texto    IS NULL
                   OR e.codigo       LIKE '%' + @texto + '%'
                   OR e.numero_serie LIKE '%' + @texto + '%'
                   OR e.marca        LIKE '%' + @texto + '%'
                   OR e.modelo       LIKE '%' + @texto + '%')"
            + CondicionDeSegmento(filtro.Segmento);

        SqlParameter[] Parametros() =>
        [
            Organizacion(),
            new SqlParameter("@tipo",   filtro.IdTipoEquipo    ?? (object)DBNull.Value),
            new SqlParameter("@estado", filtro.IdEstadoEquipo  ?? (object)DBNull.Value),
            new SqlParameter("@ubic",   filtro.IdUbicacion     ?? (object)DBNull.Value),
            new SqlParameter("@resp",   filtro.IdResponsable   ?? (object)DBNull.Value),
            new SqlParameter("@texto",  string.IsNullOrWhiteSpace(filtro.Texto)
                                            ? DBNull.Value : filtro.Texto.Trim())
        ];

        var total = Convert.ToInt32(_sql.EjecutarEscalar(
            "SELECT COUNT(*) FROM dbo.Equipo e " + condiciones + ";", Parametros()) ?? 0);

        var pagina = Math.Max(1, filtro.Pagina);
        var porPagina = Math.Clamp(filtro.PorPagina, 1, 200);

        var parametros = Parametros()
            .Append(new SqlParameter("@saltar", (pagina - 1) * porPagina))
            .Append(new SqlParameter("@tomar", porPagina))
            .ToArray();

        var items = _sql.Consultar(
            Seleccion + condiciones + @"
            ORDER BY e.codigo
            OFFSET @saltar ROWS FETCH NEXT @tomar ROWS ONLY;",
            _mapper.Map, parametros);

        return new PaginaDe<Equipo>
        {
            Items = items,
            Total = total,
            Pagina = pagina,
            PorPagina = porPagina
        };
    }

    public Guid Insert(Equipo e)
    {
        if (e.Id == Guid.Empty) e.Id = Guid.NewGuid();
        // La organización se toma del contexto y no de la entidad: así no se
        // puede insertar en otra organización pasando un identificador distinto.
        e.IdOrganizacion = _contexto.IdOrganizacion;

        const string sql = @"
            INSERT INTO dbo.Equipo
                (id_equipo, id_organizacion, codigo, id_tipo_equipo, marca, modelo, numero_serie,
                 descripcion_tecnica, fecha_adquisicion, fecha_fin_garantia, proveedor, criticidad,
                 id_estado_equipo, id_ubicacion, id_responsable)
            VALUES
                (@id, @org, @codigo, @tipo, @marca, @modelo, @serie,
                 @desc, @adq, @gar, @prov, @crit, @estado, @ubic, @resp);";

        _sql.EjecutarNoConsulta(sql, Parametros(e));
        return e.Id;
    }

    public void Update(Equipo e)
    {
        const string sql = @"
            UPDATE dbo.Equipo SET
                codigo = @codigo, id_tipo_equipo = @tipo, marca = @marca, modelo = @modelo,
                numero_serie = @serie, descripcion_tecnica = @desc, fecha_adquisicion = @adq,
                fecha_fin_garantia = @gar, proveedor = @prov, criticidad = @crit,
                id_estado_equipo = @estado, id_ubicacion = @ubic, id_responsable = @resp
            WHERE id_equipo = @id AND id_organizacion = @org;";

        _sql.EjecutarNoConsulta(sql, Parametros(e));
    }

    /// <summary>
    /// Baja lógica: se pasa el equipo al estado "Dado de baja".
    ///
    /// No se borra la fila porque el historial técnico —incidencias y
    /// mantenimientos— la referencia, y perderlo dejaría al motor predictivo sin
    /// antecedentes. Es la razón por la que existe ese estado en el catálogo.
    /// </summary>
    public void Delete(Guid id)
    {
        const string sql = @"
            UPDATE dbo.Equipo
            SET id_estado_equipo = (SELECT id_estado_equipo FROM dbo.EstadoEquipo WHERE nombre = 'Dado de baja')
            WHERE id_equipo = @id AND id_organizacion = @org;";

        _sql.EjecutarNoConsulta(sql, new SqlParameter("@id", id), Organizacion());
    }

    /// <summary>
    /// La condición SQL de un segmento, o vacío si no hay ninguno.
    ///
    /// Sale de una constante y nunca del texto que llegó: el nombre del
    /// segmento se valida contra <see cref="Segmentos.Existe"/> antes de
    /// llegar acá, y lo que se concatena es una de tres cadenas fijas. Un
    /// segmento desconocido no filtra nada en vez de filtrar cualquier cosa.
    /// </summary>
    private static string CondicionDeSegmento(string? segmento) => segmento switch
    {
        // El nivel de la evaluación *más reciente*: un ALTO de hace tres meses,
        // ya resuelto, no deja al equipo marcado para siempre.
        Segmentos.RiesgoAlto => @"
              AND EXISTS (SELECT 1 FROM dbo.EvaluacionRiesgo ev
                          WHERE ev.id_equipo = e.id_equipo
                            AND ev.nivel = 'ALTO'
                            AND ev.fecha = (SELECT MAX(f.fecha)
                                            FROM dbo.EvaluacionRiesgo f
                                            WHERE f.id_equipo = e.id_equipo))",

        // «Vencido» no es un estado guardado sino una fecha que pasó, igual que
        // en la agenda: el estado sigue siendo PROGRAMADO.
        Segmentos.PreventivoVencido => @"
              AND EXISTS (SELECT 1 FROM dbo.MantenimientoProgramado mp
                          WHERE mp.id_equipo = e.id_equipo
                            AND mp.estado = 'PROGRAMADO'
                            AND mp.fecha_programada < CAST(SYSDATETIME() AS DATE))",

        Segmentos.GarantiaVencida => @"
              AND e.fecha_fin_garantia IS NOT NULL
              AND e.fecha_fin_garantia < CAST(SYSDATETIME() AS DATE)",

        _ => string.Empty
    };

    public RecuentoSegmentos ContarSegmentos(FiltroEquipos filtro)
    {
        // Una sola pasada sobre el mismo conjunto: tres consultas separadas
        // recorrerían el inventario tres veces para contestar lo mismo.
        // Las marcas se calculan por equipo en un CROSS APPLY y recien despues
        // se suman: SQL Server no admite un agregado sobre una expresion que
        // contiene una subconsulta, asi que `SUM(CASE WHEN EXISTS (...))` no
        // compila aunque se lea bien.
        var sql = @"
            SELECT COUNT(*)      AS todos,
                   SUM(m.riesgo) AS riesgo_alto,
                   SUM(m.preventivo) AS preventivo,
                   SUM(m.garantia)   AS garantia
            FROM dbo.Equipo e
            CROSS APPLY (SELECT
                CASE WHEN EXISTS (SELECT 1 FROM dbo.EvaluacionRiesgo ev
                                  WHERE ev.id_equipo = e.id_equipo
                                    AND ev.nivel = 'ALTO'
                                    AND ev.fecha = (SELECT MAX(f.fecha)
                                                    FROM dbo.EvaluacionRiesgo f
                                                    WHERE f.id_equipo = e.id_equipo))
                     THEN 1 ELSE 0 END AS riesgo,
                CASE WHEN EXISTS (SELECT 1 FROM dbo.MantenimientoProgramado mp
                                  WHERE mp.id_equipo = e.id_equipo
                                    AND mp.estado = 'PROGRAMADO'
                                    AND mp.fecha_programada < CAST(SYSDATETIME() AS DATE))
                     THEN 1 ELSE 0 END AS preventivo,
                CASE WHEN e.fecha_fin_garantia IS NOT NULL
                      AND e.fecha_fin_garantia < CAST(SYSDATETIME() AS DATE)
                     THEN 1 ELSE 0 END AS garantia) m
            WHERE e.id_organizacion = @org
              AND (@tipo   IS NULL OR e.id_tipo_equipo   = @tipo)
              AND (@estado IS NULL OR e.id_estado_equipo = @estado)
              AND (@ubic   IS NULL OR e.id_ubicacion     = @ubic)
              AND (@resp   IS NULL OR e.id_responsable   = @resp)
              AND (@texto  IS NULL
                   OR e.codigo       LIKE '%' + @texto + '%'
                   OR e.numero_serie LIKE '%' + @texto + '%'
                   OR e.marca        LIKE '%' + @texto + '%'
                   OR e.modelo       LIKE '%' + @texto + '%');";

        SqlParameter[] parametros =
        [
            Organizacion(),
            new SqlParameter("@tipo",   filtro.IdTipoEquipo   ?? (object)DBNull.Value),
            new SqlParameter("@estado", filtro.IdEstadoEquipo ?? (object)DBNull.Value),
            new SqlParameter("@ubic",   filtro.IdUbicacion    ?? (object)DBNull.Value),
            new SqlParameter("@resp",   filtro.IdResponsable  ?? (object)DBNull.Value),
            new SqlParameter("@texto",  string.IsNullOrWhiteSpace(filtro.Texto)
                                            ? DBNull.Value : filtro.Texto.Trim())
        ];

        return _sql.Consultar(sql, f => new RecuentoSegmentos(
                                  f.GetInt32(0),
                                  f.IsDBNull(1) ? 0 : f.GetInt32(1),
                                  f.IsDBNull(2) ? 0 : f.GetInt32(2),
                                  f.IsDBNull(3) ? 0 : f.GetInt32(3)),
                              parametros)
                   .FirstOrDefault() ?? new RecuentoSegmentos(0, 0, 0, 0);
    }

    public bool ExisteCodigo(string codigo, Guid? excepto = null) => Convert.ToInt32(
        _sql.EjecutarEscalar(@"
            SELECT COUNT(*) FROM dbo.Equipo
            WHERE id_organizacion = @org AND codigo = @codigo
              AND (@excepto IS NULL OR id_equipo <> @excepto);",
            Organizacion(),
            new SqlParameter("@codigo", codigo),
            new SqlParameter("@excepto", excepto ?? (object)DBNull.Value)) ?? 0) > 0;

    public void CambiarEstado(Guid idEquipo, Guid idEstado) => _sql.EjecutarNoConsulta(@"
        UPDATE dbo.Equipo SET id_estado_equipo = @estado
        WHERE id_equipo = @id AND id_organizacion = @org;",
        new SqlParameter("@id", idEquipo),
        Organizacion(),
        new SqlParameter("@estado", idEstado));

    public bool ExisteNumeroSerie(string numeroSerie, Guid? excepto = null) => Convert.ToInt32(
        _sql.EjecutarEscalar(@"
            SELECT COUNT(*) FROM dbo.Equipo
            WHERE id_organizacion = @org AND numero_serie = @serie
              AND (@excepto IS NULL OR id_equipo <> @excepto);",
            Organizacion(),
            new SqlParameter("@serie", numeroSerie),
            new SqlParameter("@excepto", excepto ?? (object)DBNull.Value)) ?? 0) > 0;

    public int ContarPorOrganizacion() => Convert.ToInt32(
        _sql.EjecutarEscalar("SELECT COUNT(*) FROM dbo.Equipo WHERE id_organizacion = @org;",
            Organizacion()) ?? 0);

    private SqlParameter[] Parametros(Equipo e) =>
    [
        new SqlParameter("@id", e.Id),
        Organizacion(),
        new SqlParameter("@codigo", e.Codigo),
        new SqlParameter("@tipo", e.IdTipoEquipo),
        new SqlParameter("@marca", e.Marca ?? (object)DBNull.Value),
        new SqlParameter("@modelo", e.Modelo ?? (object)DBNull.Value),
        new SqlParameter("@serie", e.NumeroSerie ?? (object)DBNull.Value),
        new SqlParameter("@desc", e.DescripcionTecnica ?? (object)DBNull.Value),
        new SqlParameter("@adq", e.FechaAdquisicion?.ToDateTime(TimeOnly.MinValue) ?? (object)DBNull.Value),
        new SqlParameter("@gar", e.FechaFinGarantia?.ToDateTime(TimeOnly.MinValue) ?? (object)DBNull.Value),
        new SqlParameter("@prov", e.Proveedor ?? (object)DBNull.Value),
        new SqlParameter("@crit", e.Criticidad),
        new SqlParameter("@estado", e.IdEstadoEquipo),
        new SqlParameter("@ubic", e.IdUbicacion ?? (object)DBNull.Value),
        new SqlParameter("@resp", e.IdResponsable ?? (object)DBNull.Value)
    ];
}
