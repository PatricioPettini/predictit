using System.Data;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Helpers;
using PredictIT.Domain.Negocio;

namespace PredictIT.DAO.Mappers;

/// <summary>
/// Mapeadores de incidencias, mantenimientos y análisis predictivo.
///
/// Van en un archivo aparte de <c>Mappers.cs</c> nada más que por tamaño: son
/// las mismas clases del mismo patrón.
/// </summary>
public class EstadoIncidenciaMapper : IObjectMapper<EstadoIncidencia>
{
    public EstadoIncidencia Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_estado_incidencia"),
        Nombre = f.Texto("nombre"),
        EsFinal = f.Booleano("es_final"),
        Orden = f.Entero("orden")
    };
}

public class PrioridadIncidenciaMapper : IObjectMapper<PrioridadIncidencia>
{
    public PrioridadIncidencia Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_prioridad"),
        Nombre = f.Texto("nombre"),
        Nivel = f.Entero("nivel"),
        HorasObjetivo = f.EnteroNulo("horas_objetivo")
    };
}

public class CategoriaIncidenciaMapper : IObjectMapper<CategoriaIncidencia>
{
    public CategoriaIncidencia Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_categoria"),
        Nombre = f.Texto("nombre"),
        Descripcion = f.TextoNulo("descripcion"),
        IdEspecialidad = f.GuidNulo("id_especialidad")
    };
}

public class EspecialidadMapper : IObjectMapper<Especialidad>
{
    public Especialidad Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_especialidad"),
        Nombre = f.Texto("nombre")
    };
}

public class TipoMantenimientoMapper : IObjectMapper<TipoMantenimiento>
{
    public TipoMantenimiento Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_tipo_mantenimiento"),
        Nombre = f.Texto("nombre"),
        EsPreventivo = f.Booleano("es_preventivo")
    };
}

public class IncidenciaMapper : IObjectMapper<Incidencia>
{
    public Incidencia Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_incidencia"),
        IdOrganizacion = f.Guid("id_organizacion"),
        Numero = f.Entero("numero"),
        Titulo = f.Texto("titulo"),
        Descripcion = f.Texto("descripcion"),
        Fecha = f.Fecha("fecha"),
        FechaResolucion = f.FechaNula("fecha_resolucion"),
        IdEquipo = f.Guid("id_equipo"),
        IdEstado = f.Guid("id_estado_incidencia"),
        IdPrioridad = f.Guid("id_prioridad"),
        IdCategoria = f.GuidNulo("id_categoria"),
        IdTecnico = f.GuidNulo("id_tecnico"),
        IdUsuarioReportante = f.Guid("id_usuario_reportante"),
        TipoAsignacion = TipoAsignacionTexto.De(f.TextoNulo("tipo_asignacion")),
        PendienteRevision = f.Booleano("pendiente_revision"),
        Diagnostico = f.TextoNulo("diagnostico"),
        Solucion = f.TextoNulo("solucion"),

        // Los nombres vienen del join y se cargan como entidades parciales: es
        // lo que la lista necesita mostrar, sin traer la fila completa de cada
        // catálogo.
        Equipo = f.Tiene("equipo_codigo")
            ? new Equipo { Id = f.Guid("id_equipo"), Codigo = f.Texto("equipo_codigo") }
            : null,

        Estado = f.Tiene("estado_nombre")
            ? new EstadoIncidencia
            {
                Id = f.Guid("id_estado_incidencia"),
                Nombre = f.Texto("estado_nombre"),
                EsFinal = f.Booleano("estado_es_final")
            }
            : null,

        Prioridad = f.Tiene("prioridad_nombre")
            ? new PrioridadIncidencia
            {
                Id = f.Guid("id_prioridad"),
                Nombre = f.Texto("prioridad_nombre"),
                Nivel = f.Entero("prioridad_nivel"),
                HorasObjetivo = f.EnteroNulo("prioridad_horas")
            }
            : null,

        Categoria = f.Tiene("categoria_nombre") && f.GuidNulo("id_categoria") is { } idCategoria
            ? new CategoriaIncidencia
            {
                Id = idCategoria,
                Nombre = f.Texto("categoria_nombre"),
                IdEspecialidad = f.GuidNulo("categoria_especialidad")
            }
            : null
    };
}

public class MantenimientoMapper : IObjectMapper<Mantenimiento>
{
    public Mantenimiento Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_mantenimiento"),
        IdOrganizacion = f.Guid("id_organizacion"),
        IdEquipo = f.Guid("id_equipo"),
        IdTipo = f.Guid("id_tipo_mantenimiento"),
        IdTecnico = f.Guid("id_tecnico"),
        IdIncidencia = f.GuidNulo("id_incidencia"),
        Fecha = f.Fecha("fecha"),
        Descripcion = f.Texto("descripcion"),
        Resultado = f.TextoNulo("resultado"),
        Repuestos = f.TextoNulo("repuestos"),
        Observaciones = f.TextoNulo("observaciones"),
        Costo = f.DecimalNulo("costo"),

        Tipo = f.Tiene("tipo_nombre")
            ? new TipoMantenimiento
            {
                Id = f.Guid("id_tipo_mantenimiento"),
                Nombre = f.Texto("tipo_nombre"),
                EsPreventivo = f.Booleano("tipo_es_preventivo")
            }
            : null
    };
}

public class ReglaAlertaMapper : IObjectMapper<ReglaAlerta>
{
    public ReglaAlerta Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_regla"),
        IdOrganizacion = f.Guid("id_organizacion"),
        Nombre = f.Texto("nombre"),
        Descripcion = f.TextoNulo("descripcion"),

        // El CHECK de la base restringe los tipos a los cinco que el motor
        // conoce, así que el null del parseo no puede pasar salvo con un
        // rollback de versión del código. En ese caso la regla queda con un tipo
        // cualquiera y el motor la evalúa como lo que dice el enum; el CHECK es
        // el que garantiza que esto no ocurra en la práctica.
        Tipo = TipoReglaTexto.De(f.Texto("tipo")) ?? TipoRegla.RecurrenciaFallas,

        Condicion = f.Texto("condicion"),
        Peso = f.Entero("peso"),
        Activa = f.Booleano("activa")
    };
}

public class AlertaPredictivaMapper : IObjectMapper<AlertaPredictiva>
{
    public AlertaPredictiva Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_alerta"),
        IdOrganizacion = f.Guid("id_organizacion"),
        IdEquipo = f.Guid("id_equipo"),
        IdRegla = f.Guid("id_regla"),
        IdEstado = f.Guid("id_estado_alerta"),
        FechaGeneracion = f.Fecha("fecha_generacion"),
        Motivo = f.Texto("motivo"),
        Recomendacion = f.TextoNulo("recomendacion"),
        NivelRiesgo = f.Entero("nivel_riesgo"),
        FechaAtencion = f.FechaNula("fecha_atencion"),
        IdUsuarioAtencion = f.GuidNulo("id_usuario_atencion"),

        Equipo = f.Tiene("equipo_codigo")
            ? new Equipo { Id = f.Guid("id_equipo"), Codigo = f.Texto("equipo_codigo") }
            : null,

        Regla = f.Tiene("regla_nombre")
            ? new ReglaAlerta { Id = f.Guid("id_regla"), Nombre = f.Texto("regla_nombre") }
            : null,

        Estado = f.Tiene("estado_alerta_nombre")
            ? new EstadoAlerta
            {
                Id = f.Guid("id_estado_alerta"),
                Nombre = f.Texto("estado_alerta_nombre"),
                EsFinal = f.Booleano("estado_alerta_es_final")
            }
            : null
    };
}

public class EvaluacionRiesgoMapper : IObjectMapper<EvaluacionRiesgo>
{
    public EvaluacionRiesgo Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_evaluacion"),
        IdOrganizacion = f.Guid("id_organizacion"),
        IdEquipo = f.Guid("id_equipo"),
        Fecha = f.Fecha("fecha"),
        Score = f.Entero("score"),
        Nivel = NivelRiesgoTexto.Parsear(f.Texto("nivel")),
        Detalle = f.TextoNulo("detalle")
    };
}

public class ClasificacionIncidenciaMapper : IObjectMapper<ClasificacionIncidencia>
{
    public ClasificacionIncidencia Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_clasificacion"),
        IdIncidencia = f.Guid("id_incidencia"),
        IdCategoriaSugerida = f.GuidNulo("id_categoria_sugerida"),
        IdPrioridadSugerida = f.GuidNulo("id_prioridad_sugerida"),
        IdIncidenciaDuplicada = f.GuidNulo("id_incidencia_duplicada"),
        Confianza = f.DecimalNulo("confianza"),
        Origen = f.Texto("origen"),
        Justificacion = f.TextoNulo("justificacion"),
        Fecha = f.Fecha("fecha")
    };
}

public class RecomendacionAsignacionMapper : IObjectMapper<RecomendacionAsignacion>
{
    public RecomendacionAsignacion Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_recomendacion"),
        IdIncidencia = f.Guid("id_incidencia"),
        IdTecnicoSugerido = f.GuidNulo("id_tecnico_sugerido"),
        Justificacion = f.TextoNulo("justificacion"),
        ContextoEnviado = f.TextoNulo("contexto_enviado"),
        Origen = f.Texto("origen"),
        MotivoRespaldo = f.TextoNulo("motivo_respaldo"),
        FechaHora = f.Fecha("fecha_hora")
    };
}

public class ConfiguracionAsignacionMapper : IObjectMapper<ConfiguracionAsignacion>
{
    public ConfiguracionAsignacion Map(IDataRecord f) => new()
    {
        IdOrganizacion = f.Guid("id_organizacion"),
        ProveedorIa = f.Texto("proveedor_ia"),
        ApiKeyCifrada = f.TextoNulo("api_key_cifrada"),
        Modelo = f.TextoNulo("modelo"),
        EnviarCarga = f.Booleano("enviar_carga"),
        EnviarEspecialidad = f.Booleano("enviar_especialidad"),
        EnviarHistorial = f.Booleano("enviar_historial"),
        EnviarDisponibilidad = f.Booleano("enviar_disponibilidad"),
        EstrategiaRespaldo = f.Texto("estrategia_respaldo"),
        TimeoutSegundos = f.Entero("timeout_segundos"),
        UltimaConsultaOk = f.FechaNula("ultima_consulta_ok"),
        UltimoError = f.TextoNulo("ultimo_error")
    };
}
