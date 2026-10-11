using Microsoft.Data.SqlClient;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Helpers;
using PredictIT.DAO.Mappers;
using PredictIT.Domain.Negocio;

namespace PredictIT.DAO.Implementations;

/// <summary>Reglas, evaluaciones de riesgo y alertas predictivas (RF-11, RF-12).</summary>
public class PrediccionDao(SqlHelper sql, IContextoSesion contexto) : IPrediccionDao
{
    private readonly ReglaAlertaMapper _mapperRegla = new();
    private readonly AlertaPredictivaMapper _mapperAlerta = new();
    private readonly EvaluacionRiesgoMapper _mapperEvaluacion = new();

    private SqlParameter Organizacion() => new("@org", contexto.IdOrganizacion);

    public IList<ReglaAlerta> ReglasDeLaOrganizacion(bool soloActivas = true) => sql.Consultar(@"
        SELECT id_regla, id_organizacion, nombre, descripcion, tipo, condicion, peso, activa
        FROM dbo.ReglaAlerta
        WHERE id_organizacion = @org AND (@soloActivas = 0 OR activa = 1)
        ORDER BY peso DESC, nombre;",
        _mapperRegla.Map, Organizacion(), new SqlParameter("@soloActivas", soloActivas ? 1 : 0));

    public ReglaAlerta? ReglaPorId(Guid id) => sql.ConsultarUno(@"
        SELECT id_regla, id_organizacion, nombre, descripcion, tipo, condicion, peso, activa
        FROM dbo.ReglaAlerta
        WHERE id_regla = @id AND id_organizacion = @org;",
        _mapperRegla.Map, new SqlParameter("@id", id), Organizacion());

    public void GuardarRegla(ReglaAlerta r)
    {
        if (r.Id == Guid.Empty) r.Id = Guid.NewGuid();
        r.IdOrganizacion = contexto.IdOrganizacion;

        // MERGE en lugar de decidir en C# si es alta o modificación: la decisión
        // y la escritura quedan en la misma sentencia, y no hay ventana entre
        // preguntar si existe y escribir.
        sql.EjecutarNoConsulta(@"
            MERGE dbo.ReglaAlerta AS d
            USING (SELECT @id AS id) AS s ON d.id_regla = s.id AND d.id_organizacion = @org
            WHEN MATCHED THEN UPDATE SET
                nombre = @nombre, descripcion = @desc, tipo = @tipo,
                condicion = @cond, peso = @peso, activa = @activa
            WHEN NOT MATCHED THEN INSERT
                (id_regla, id_organizacion, nombre, descripcion, tipo, condicion, peso, activa)
                VALUES (@id, @org, @nombre, @desc, @tipo, @cond, @peso, @activa);",
            new SqlParameter("@id", r.Id),
            Organizacion(),
            new SqlParameter("@nombre", r.Nombre),
            new SqlParameter("@desc", r.Descripcion ?? (object)DBNull.Value),
            new SqlParameter("@tipo", TipoReglaTexto.A(r.Tipo)),
            new SqlParameter("@cond", r.Condicion),
            new SqlParameter("@peso", r.Peso),
            new SqlParameter("@activa", r.Activa));
    }

    public void GuardarEvaluacion(EvaluacionRiesgo e)
    {
        if (e.Id == Guid.Empty) e.Id = Guid.NewGuid();
        e.IdOrganizacion = contexto.IdOrganizacion;

        sql.EjecutarNoConsulta(@"
            INSERT INTO dbo.EvaluacionRiesgo
                (id_evaluacion, id_organizacion, id_equipo, fecha, score, nivel, detalle)
            VALUES (@id, @org, @equipo, @fecha, @score, @nivel, @detalle);",
            new SqlParameter("@id", e.Id),
            Organizacion(),
            new SqlParameter("@equipo", e.IdEquipo),
            new SqlParameter("@fecha", e.Fecha == default ? DateTime.Now : e.Fecha),
            new SqlParameter("@score", e.Score),
            new SqlParameter("@nivel", NivelRiesgoTexto.A(e.Nivel)),
            new SqlParameter("@detalle", e.Detalle ?? (object)DBNull.Value));
    }

    public EvaluacionRiesgo? UltimaEvaluacion(Guid idEquipo) => sql.ConsultarUno(@"
        SELECT TOP 1 id_evaluacion, id_organizacion, id_equipo, fecha, score, nivel, detalle
        FROM dbo.EvaluacionRiesgo
        WHERE id_organizacion = @org AND id_equipo = @equipo
        ORDER BY fecha DESC;",
        _mapperEvaluacion.Map, Organizacion(), new SqlParameter("@equipo", idEquipo));

    public IList<(EvaluacionRiesgo Evaluacion, string CodigoEquipo)> UltimasEvaluaciones() =>
        sql.Consultar(@"
            WITH ultima AS (
                SELECT e.*, ROW_NUMBER() OVER (PARTITION BY e.id_equipo ORDER BY e.fecha DESC) AS n
                FROM dbo.EvaluacionRiesgo e
                WHERE e.id_organizacion = @org
            )
            SELECT u.id_evaluacion, u.id_organizacion, u.id_equipo, u.fecha, u.score, u.nivel,
                   u.detalle, eq.codigo AS equipo_codigo
            FROM ultima u
            JOIN dbo.Equipo eq ON eq.id_equipo = u.id_equipo
            WHERE u.n = 1
            ORDER BY u.score DESC, eq.codigo;",
            f => (_mapperEvaluacion.Map(f), f.Texto("equipo_codigo")),
            Organizacion());

    public AlertaPredictiva? AlertaActiva(Guid idEquipo, Guid idRegla) => sql.ConsultarUno(
        SeleccionAlerta + @"
        WHERE a.id_organizacion = @org AND a.id_equipo = @equipo AND a.id_regla = @regla
          AND ea.es_final = 0
        ORDER BY a.fecha_generacion DESC;",
        _mapperAlerta.Map,
        Organizacion(),
        new SqlParameter("@equipo", idEquipo),
        new SqlParameter("@regla", idRegla));

    public Guid GuardarAlerta(AlertaPredictiva a)
    {
        if (a.Id == Guid.Empty) a.Id = Guid.NewGuid();
        a.IdOrganizacion = contexto.IdOrganizacion;

        sql.EjecutarNoConsulta(@"
            INSERT INTO dbo.AlertaPredictiva
                (id_alerta, id_organizacion, id_equipo, id_regla, id_estado_alerta,
                 fecha_generacion, motivo, recomendacion, nivel_riesgo)
            VALUES (@id, @org, @equipo, @regla, @estado, @fecha, @motivo, @recom, @nivel);",
            new SqlParameter("@id", a.Id),
            Organizacion(),
            new SqlParameter("@equipo", a.IdEquipo),
            new SqlParameter("@regla", a.IdRegla),
            new SqlParameter("@estado", a.IdEstado),
            new SqlParameter("@fecha", a.FechaGeneracion == default ? DateTime.Now : a.FechaGeneracion),
            new SqlParameter("@motivo", a.Motivo),
            new SqlParameter("@recom", a.Recomendacion ?? (object)DBNull.Value),
            new SqlParameter("@nivel", a.NivelRiesgo));

        return a.Id;
    }

    public IList<AlertaPredictiva> AlertasActivas() => sql.Consultar(
        SeleccionAlerta + @"
        WHERE a.id_organizacion = @org AND ea.es_final = 0
        ORDER BY a.nivel_riesgo DESC, a.fecha_generacion DESC;",
        _mapperAlerta.Map, Organizacion());

    public void AtenderAlerta(Guid idAlerta, Guid idUsuario, bool descartar) =>
        sql.EjecutarNoConsulta(@"
            UPDATE dbo.AlertaPredictiva
            SET id_estado_alerta = (SELECT id_estado_alerta FROM dbo.EstadoAlerta WHERE nombre = @nombreEstado),
                fecha_atencion = @ahora,
                id_usuario_atencion = @usuario
            WHERE id_alerta = @id AND id_organizacion = @org;",
            new SqlParameter("@id", idAlerta),
            Organizacion(),
            Reloj.Parametro(),
            new SqlParameter("@usuario", idUsuario),
            // Descartada y atendida son cosas distintas: una dice «lo resolví»,
            // la otra «esta alerta no aplica». Distinguirlas es lo que permite
            // después saber si las reglas están bien calibradas.
            new SqlParameter("@nombreEstado", descartar ? "Descartada" : "Atendida"));

    /// <summary>
    /// Recuento de alertas por regla y por resultado, para calibrar umbrales.
    ///
    /// Se agrupa en SQL y no en memoria: la alternativa es traer todas las
    /// alertas historicas de la organizacion para contarlas, y son las que mas
    /// crecen de todas las tablas.
    /// </summary>
    public IList<(Guid IdRegla, string Estado, int Cantidad, DateTime? Ultima)> RecuentoPorRegla() =>
        sql.Consultar(@"
            SELECT a.id_regla, ea.nombre AS estado, COUNT(*) AS cantidad,
                   MAX(a.fecha_generacion) AS ultima
            FROM dbo.AlertaPredictiva a
            JOIN dbo.EstadoAlerta ea ON ea.id_estado_alerta = a.id_estado_alerta
            WHERE a.id_organizacion = @org
            GROUP BY a.id_regla, ea.nombre;",
            lector => (
                lector.GetGuid(lector.GetOrdinal("id_regla")),
                lector.GetString(lector.GetOrdinal("estado")),
                lector.GetInt32(lector.GetOrdinal("cantidad")),
                lector.IsDBNull(lector.GetOrdinal("ultima"))
                    ? (DateTime?)null
                    : lector.GetDateTime(lector.GetOrdinal("ultima"))),
            Organizacion());

    private const string SeleccionAlerta = @"
        SELECT a.id_alerta, a.id_organizacion, a.id_equipo, a.id_regla, a.id_estado_alerta,
               a.fecha_generacion, a.motivo, a.recomendacion, a.nivel_riesgo,
               a.fecha_atencion, a.id_usuario_atencion,
               eq.codigo AS equipo_codigo,
               r.nombre  AS regla_nombre,
               ea.nombre AS estado_alerta_nombre, ea.es_final AS estado_alerta_es_final
        FROM dbo.AlertaPredictiva a
        JOIN dbo.Equipo      eq ON eq.id_equipo        = a.id_equipo
        JOIN dbo.ReglaAlerta r  ON r.id_regla          = a.id_regla
        JOIN dbo.EstadoAlerta ea ON ea.id_estado_alerta = a.id_estado_alerta";
}

/// <summary>
/// Técnicos de la organización con su carga de trabajo.
///
/// Cruza las dos bases: las especialidades y la carga están en negocio, los
/// nombres en servicio. Se resuelve con dos consultas y un cruce en memoria, no
/// con una consulta entre bases: una consulta con tres partes las ataría a estar
/// en el mismo servidor, que es justo lo que el ADR 0003 evita.
/// </summary>
public class TecnicoDao(SqlHelper negocio, SqlHelperSeguridad servicio, IContextoSesion contexto)
    : ITecnicoDao
{
    public IList<TecnicoConCarga> CandidatosPara(Guid? idEquipo)
    {
        var org = contexto.IdOrganizacion;

        // 1) Quiénes son técnicos de esta organización, con su nombre.
        // La pertenencia a una organizacion se expresa de dos formas y hay que
        // mirar las dos: los usuarios internos la tienen en Usuario.id_organizacion,
        // y el Partner —que no pertenece a ninguna— la tiene en
        // Usuario_Organizacion, una fila por cada cliente que atiende. Con solo
        // el join a Usuario_Organizacion no aparecia ningun tecnico interno.
        var personas = servicio.Consultar(@"
            SELECT u.id_usuario, u.nombre, u.apellido
            FROM dbo.Usuario u
            JOIN dbo.Usuario_Rol ur ON ur.id_usuario = u.id_usuario
            JOIN dbo.Rol r          ON r.id_rol      = ur.id_rol
            WHERE u.activo = 1
              AND u.bloqueado = 0
              AND (u.id_organizacion = @org
                   OR EXISTS (SELECT 1 FROM dbo.Usuario_Organizacion uo
                              WHERE uo.id_usuario = u.id_usuario
                                AND uo.id_organizacion = @org))
              -- Los codigos de rol son los del seed y estan marcados es_sistema:
              -- forman parte del modelo, no son datos que el usuario renombre.
              -- El Partner entra porque en el Segmento C es justamente quien
              -- atiende las incidencias del cliente.
              AND r.nombre IN ('RESPONSABLE_TECNICO', 'ADMINISTRADOR', 'PARTNER')
            GROUP BY u.id_usuario, u.nombre, u.apellido
            ORDER BY u.apellido, u.nombre;",
            f => new TecnicoConCarga
            {
                IdUsuario = f.Guid("id_usuario"),
                NombreCompleto = $"{f.Texto("nombre")} {f.Texto("apellido")}".Trim()
            },
            new SqlParameter("@org", org));

        if (personas.Count == 0) return personas;

        // 2) Especialidades y carga, de la base de negocio.
        var especialidades = negocio.Consultar(@"
            SELECT te.id_tecnico, te.id_especialidad, te.nivel, e.nombre
            FROM dbo.TecnicoEspecialidad te
            JOIN dbo.Especialidad e ON e.id_especialidad = te.id_especialidad
            WHERE te.id_organizacion = @org;",
            f => new
            {
                Tecnico = f.Guid("id_tecnico"),
                Especialidad = new TecnicoEspecialidad
                {
                    IdOrganizacion = org,
                    IdTecnico = f.Guid("id_tecnico"),
                    IdEspecialidad = f.Guid("id_especialidad"),
                    Nivel = f.Entero("nivel"),
                    Especialidad = new Especialidad
                    {
                        Id = f.Guid("id_especialidad"),
                        Nombre = f.Texto("nombre")
                    }
                }
            },
            new SqlParameter("@org", org));

        var carga = negocio.Consultar(@"
            SELECT i.id_tecnico, COUNT(*) AS abiertas
            FROM dbo.Incidencia i
            JOIN dbo.EstadoIncidencia es ON es.id_estado_incidencia = i.id_estado_incidencia
            WHERE i.id_organizacion = @org AND i.id_tecnico IS NOT NULL AND es.es_final = 0
            GROUP BY i.id_tecnico;",
            f => new { Tecnico = f.Guid("id_tecnico"), Abiertas = f.Entero("abiertas") },
            new SqlParameter("@org", org));

        var enEsteEquipo = idEquipo is not { } equipo
            ? []
            : negocio.Consultar(@"
                SELECT i.id_tecnico, COUNT(*) AS resueltas
                FROM dbo.Incidencia i
                JOIN dbo.EstadoIncidencia es ON es.id_estado_incidencia = i.id_estado_incidencia
                WHERE i.id_organizacion = @org AND i.id_equipo = @equipo
                  AND i.id_tecnico IS NOT NULL AND es.es_final = 1
                GROUP BY i.id_tecnico;",
                f => new { Tecnico = f.Guid("id_tecnico"), Resueltas = f.Entero("resueltas") },
                new SqlParameter("@org", org), new SqlParameter("@equipo", equipo));

        foreach (var persona in personas)
        {
            persona.Especialidades = especialidades
                .Where(e => e.Tecnico == persona.IdUsuario)
                .Select(e => e.Especialidad)
                .ToList();

            persona.IncidenciasAbiertas =
                carga.FirstOrDefault(c => c.Tecnico == persona.IdUsuario)?.Abiertas ?? 0;

            persona.ResueltasEnEsteEquipo =
                enEsteEquipo.FirstOrDefault(c => c.Tecnico == persona.IdUsuario)?.Resueltas ?? 0;
        }

        return personas;
    }
}

/// <summary>Configuración de asignación por organización (pantalla 10.5.1.7.7).</summary>
public class ConfiguracionAsignacionDao(SqlHelper sql, IContextoSesion contexto)
    : IConfiguracionAsignacionDao
{
    private readonly ConfiguracionAsignacionMapper _mapper = new();

    private SqlParameter Organizacion() => new("@org", contexto.IdOrganizacion);

    public ConfiguracionAsignacion? DeLaOrganizacion() => sql.ConsultarUno(
        "SELECT * FROM dbo.ConfiguracionAsignacion WHERE id_organizacion = @org;",
        _mapper.Map, Organizacion());

    public void Guardar(ConfiguracionAsignacion c)
    {
        c.IdOrganizacion = contexto.IdOrganizacion;

        sql.EjecutarNoConsulta(@"
            MERGE dbo.ConfiguracionAsignacion AS d
            USING (SELECT @org AS id) AS s ON d.id_organizacion = s.id
            WHEN MATCHED THEN UPDATE SET
                proveedor_ia = @prov, api_key_cifrada = @key, modelo = @modelo,
                enviar_carga = @carga, enviar_especialidad = @esp, enviar_historial = @hist,
                enviar_disponibilidad = @disp, estrategia_respaldo = @respaldo,
                timeout_segundos = @timeout
            WHEN NOT MATCHED THEN INSERT
                (id_organizacion, proveedor_ia, api_key_cifrada, modelo, enviar_carga,
                 enviar_especialidad, enviar_historial, enviar_disponibilidad,
                 estrategia_respaldo, timeout_segundos)
                VALUES (@org, @prov, @key, @modelo, @carga, @esp, @hist, @disp, @respaldo, @timeout);",
            Organizacion(),
            new SqlParameter("@prov", c.ProveedorIa),
            new SqlParameter("@key", c.ApiKeyCifrada ?? (object)DBNull.Value),
            new SqlParameter("@modelo", c.Modelo ?? (object)DBNull.Value),
            new SqlParameter("@carga", c.EnviarCarga),
            new SqlParameter("@esp", c.EnviarEspecialidad),
            new SqlParameter("@hist", c.EnviarHistorial),
            new SqlParameter("@disp", c.EnviarDisponibilidad),
            new SqlParameter("@respaldo", c.EstrategiaRespaldo),
            new SqlParameter("@timeout", c.TimeoutSegundos));
    }

    /// <summary>
    /// Deja registrado el resultado de la última consulta al proveedor. Es lo que
    /// la pantalla de Integración de IA muestra para que el administrador sepa si
    /// el servicio está respondiendo.
    /// </summary>
    public void RegistrarResultado(bool ok, string? error) => sql.EjecutarNoConsulta(@"
        UPDATE dbo.ConfiguracionAsignacion
        SET ultima_consulta_ok = CASE WHEN @ok = 1 THEN @ahora ELSE ultima_consulta_ok END,
            ultimo_error       = CASE WHEN @ok = 1 THEN NULL ELSE @error END
        WHERE id_organizacion = @org;",
        Organizacion(),
        Reloj.Parametro(),
        new SqlParameter("@ok", ok),
        new SqlParameter("@error", error ?? (object)DBNull.Value));
}
