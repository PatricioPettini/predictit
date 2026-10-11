using System.Data;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;

namespace PredictIT.DAO.Contracts;

/// <summary>Traduce una fila del lector a una entidad del dominio.</summary>
public interface IObjectMapper<out T>
{
    T Map(IDataRecord fila);
}

/// <summary>Operaciones que toda entidad persistida comparte.</summary>
public interface IGenericDao<T>
{
    Guid Insert(T entidad);
    void Update(T entidad);
    void Delete(Guid id);
    T? GetById(Guid id);
    IList<T> GetAll();
}

/// <summary>
/// Quién está operando y sobre qué organización.
///
/// Los DAO de negocio lo consumen para filtrar. Deliberadamente el identificador
/// de organización no viaja en la petición HTTP: si viajara, un controlador
/// podría pedir datos de otra organización. Se resuelve del token (ADR 0005).
/// </summary>
public interface IContextoSesion
{
    Guid IdUsuario { get; }
    string Username { get; }
    Guid IdOrganizacion { get; }

    /// <summary>
    /// Idioma que el usuario eligió, o null si no eligió ninguno. Es un
    /// atributo de la sesión igual que la organización: se resuelve una vez al
    /// autenticar y no viaja en la petición (Req. Arq. 001).
    /// </summary>
    Guid? IdIdioma { get; }

    bool Tiene(string dataKey);
}

/// <summary>Criterios de búsqueda del listado de activos (RF-05).</summary>
public class FiltroEquipos
{
    public string? Texto { get; set; }
    public Guid? IdTipoEquipo { get; set; }
    public Guid? IdEstadoEquipo { get; set; }
    public Guid? IdUbicacion { get; set; }
    public Guid? IdResponsable { get; set; }

    /// <summary>
    /// Uno de los tres segmentos del inventario, o null para todo el parque.
    ///
    /// No son filtros por columna sino las tres preguntas que alguien se hace
    /// parado frente al inventario: qué está por romperse, a qué le debo el
    /// preventivo, y qué ya no tiene garantía. Son <see cref="Segmentos"/>.
    /// </summary>
    public string? Segmento { get; set; }

    public int Pagina { get; set; } = 1;
    public int PorPagina { get; set; } = 20;
}

/// <summary>Los tres segmentos del inventario, con su nombre estable.</summary>
public static class Segmentos
{
    public const string RiesgoAlto = "riesgoAlto";
    public const string PreventivoVencido = "preventivoVencido";
    public const string GarantiaVencida = "garantiaVencida";

    public static bool Existe(string? s) =>
        s is RiesgoAlto or PreventivoVencido or GarantiaVencida;
}

/// <summary>Cuántos equipos cae en cada segmento, con los demás filtros puestos.</summary>
public record RecuentoSegmentos(int Todos, int RiesgoAlto, int PreventivoVencido, int GarantiaVencida);

public class PaginaDe<T>
{
    public IList<T> Items { get; set; } = new List<T>();
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int PorPagina { get; set; }
}

public interface IEquipoDao : IGenericDao<Equipo>
{
    PaginaDe<Equipo> Buscar(FiltroEquipos filtro);
    bool ExisteCodigo(string codigo, Guid? excepto = null);
    bool ExisteNumeroSerie(string numeroSerie, Guid? excepto = null);

    /// <summary>Cambia el estado operativo del equipo (patente EQUIPO_CAMBIAR_ESTADO).</summary>
    void CambiarEstado(Guid idEquipo, Guid idEstado);
    int ContarPorOrganizacion();

    /// <summary>
    /// Cuántos equipos caen en cada segmento. Aplica todos los filtros salvo el
    /// segmento mismo: así cada número dice cuántos vería quien lo pulse, y no
    /// un total del parque que no coincide con lo que después aparece.
    /// </summary>
    RecuentoSegmentos ContarSegmentos(FiltroEquipos filtro);
}

public interface ICatalogoDao
{
    IList<TipoEquipo> TiposDeEquipo();
    IList<EstadoEquipo> EstadosDeEquipo();
    IList<Ubicacion> Ubicaciones();

    IList<EstadoIncidencia> EstadosDeIncidencia();
    IList<PrioridadIncidencia> Prioridades();
    IList<CategoriaIncidencia> Categorias();
    IList<Especialidad> Especialidades();
    IList<TipoMantenimiento> TiposDeMantenimiento();
    IList<EstadoAlerta> EstadosDeAlerta();

    /// <summary>Estado inicial de una incidencia recién reportada.</summary>
    EstadoIncidencia? EstadoIncidenciaInicial();

    /// <summary>
    /// Estado al que pasa una incidencia cuando queda con técnico.
    ///
    /// Es el segundo estado no terminal por orden: la organización puede
    /// renombrarlos, así que se toma por posición y no por nombre.
    /// </summary>
    EstadoIncidencia? EstadoIncidenciaAsignada();

    /// <summary>Estado con el que nace una alerta predictiva.</summary>
    EstadoAlerta? EstadoAlertaInicial();
}

public interface IOrganizacionDao
{
    Organizacion? GetById(Guid id);
    PlanComercial? PlanDe(Guid idOrganizacion);

    /// <summary>Catálogo completo de planes, para comparar el contratado contra los otros.</summary>
    IList<PlanComercial> TodosLosPlanes();

    /// <summary>
    /// Todas las organizaciones activas.
    ///
    /// Es la única lectura del sistema que cruza organizaciones, y existe para
    /// el barrido predictivo programado: un proceso que corre sin sesión tiene
    /// que saber sobre qué organizaciones iterar. No se expone en ningún
    /// business, justamente para que no sea alcanzable desde una petición.
    /// </summary>
    IList<Organizacion> Activas();
}

public interface IUsuarioDao
{
    Usuario? PorUsername(string username);
    Usuario? GetById(Guid id);
    IList<Usuario> PorOrganizacion(Guid idOrganizacion);
    void RegistrarAccesoExitoso(Guid idUsuario);
    void RegistrarAccesoFallido(Guid idUsuario);

    /// <summary>Activa o desactiva. Un usuario inactivo no puede iniciar sesión.</summary>
    void CambiarEstado(Guid idUsuario, bool activo);

    /// <summary>
    /// Levanta el bloqueo por intentos fallidos y pone el contador en cero. Las
    /// dos cosas juntas: dejar el contador en cinco haría que el próximo error
    /// bloqueara de nuevo.
    /// </summary>
    void Desbloquear(Guid idUsuario);

    /// <summary>Reemplaza los roles del usuario por el conjunto indicado.</summary>
    void AsignarRoles(Guid idUsuario, IEnumerable<Guid> idsRol);

    /// <summary>
    /// Reemplaza el hash de la contraseña. Se usa para volver a derivarlo con
    /// más iteraciones cuando el usuario entra: la contraseña en claro sólo
    /// existe en ese momento, así que es la única oportunidad de hacerlo sin
    /// pedirle que la cambie.
    /// </summary>
    void ActualizarHash(Guid idUsuario, string hash);
}

/// <summary>Carga el árbol de permisos: roles, familias anidadas y patentes.</summary>
public interface IPermisoDao
{
    IList<Rol> RolesDe(Guid idUsuario);
    IList<Guid> OrganizacionesDe(Guid idUsuario);

    /// <summary>Catálogo de roles, con su árbol de permisos ya resuelto.</summary>
    IList<Rol> TodosLosRoles();
}

/// <summary>Un respaldo hecho, tal como quedó registrado.</summary>
public class Respaldo
{
    public Guid Id { get; set; }
    public string Base { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string Ruta { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public long? TamanoBytes { get; set; }

    /// <summary>MANUAL o AUTOMATICO.</summary>
    public string Tipo { get; set; } = "MANUAL";

    public Guid? IdUsuario { get; set; }

    /// <summary>OK o ERROR.</summary>
    public string Resultado { get; set; } = "OK";

    public string? Detalle { get; set; }
}

public interface IRespaldoDao
{
    void Registrar(Respaldo respaldo);

    /// <summary>
    /// Los respaldos hechos, del más nuevo al más viejo. Incluye los que
    /// fallaron: un respaldo que falló es justamente lo que hay que ver.
    /// </summary>
    IList<Respaldo> Historial(int cantidad = 50);

    /// <summary>Un respaldo por su identificador, para poder restaurarlo.</summary>
    Respaldo? PorId(Guid id);
}

/// <summary>
/// Idiomas y traducciones (Req. Arq. 001 · CU.Arq.004).
///
/// No recibe contexto de sesión: el diccionario tiene que poder servirse antes
/// de que haya sesión, porque la pantalla de inicio de sesión también se
/// traduce.
/// </summary>
public interface IIdiomaDao
{
    IList<Idioma> Idiomas(bool soloActivos = true);
    Idioma? PorCodigo(string codigo);
    Idioma? PorDefecto();

    /// <summary>El diccionario completo de un idioma, en una sola consulta.</summary>
    IDictionary<string, string> Diccionario(Guid idIdioma);

    void CambiarIdiomaDeUsuario(Guid idUsuario, Guid idIdioma);

    /// <summary>Claves que faltan en algún idioma activo. Hace medible la cobertura.</summary>
    IList<(string Clave, string CodigoIdioma)> ClavesFaltantes();
}

public interface IBitacoraDao
{
    void Registrar(Bitacora entrada);
    /// <summary>
    /// Consulta de la bitácora (CU.Arq.002).
    ///
    /// El filtro por usuario está en la consulta y no en memoria: filtrar
    /// después de traer treinta días de auditoría es traer treinta días de
    /// auditoría para descartar casi todo.
    /// </summary>
    IList<Bitacora> Consultar(DateTime desde, DateTime hasta, Guid? idOrganizacion,
                              string? codigoTipoEvento, Guid? idUsuario = null);

    /// <summary>
    /// Sólo lo que salió mal: las entradas cuyo tipo de evento está clasificado
    /// como ERROR o CRITICO (CU.Arq.007).
    ///
    /// Es una consulta aparte y no un filtro más de la anterior porque devuelve
    /// la traza, que la consulta general deliberadamente no trae.
    /// </summary>
    IList<Bitacora> ConsultarErrores(DateTime desde, DateTime hasta, Guid? idOrganizacion,
                                     bool incluirAdvertencias);
    Guid? IdTipoEventoPorCodigo(string codigo);

    /// <summary>
    /// Catálogo de tipos de evento. La consulta de bitácora no lo trae por fila:
    /// se cruza en memoria para no repetir el nombre y la criticidad en cada una
    /// de las miles de entradas que puede devolver.
    /// </summary>
    IList<TipoEvento> TiposDeEvento();

    /// <summary>
    /// Cuantas veces se consulto cada clase de evento en un periodo.
    ///
    /// Cuenta en el motor y no en memoria. La alternativa es traer las entradas
    /// del mes para descartarlas despues de contarlas, y la bitacora de una
    /// organizacion activa son miles de filas por mes.
    /// </summary>
    IDictionary<string, int> ContarPorTipo(DateTime desde, DateTime hasta,
                                           Guid? idOrganizacion,
                                           IEnumerable<string> codigos);
}

/// <summary>
/// Coordina varias operaciones en una sola transacción.
///
/// Sólo aplica dentro de una misma base: entre negocio y servicio no hay
/// transacción posible y no se usa transacción distribuida a propósito
/// (ADR 0003).
/// </summary>
public interface IUnitOfWork : IDisposable
{
    void Comprometer();
    void Revertir();
}

/* ----------------------------------------------------------------------
   Incidencias, mantenimientos y análisis predictivo
   ---------------------------------------------------------------------- */

/// <summary>Criterios de búsqueda del listado de incidencias (RF-07).</summary>
public class FiltroIncidencias
{
    public string? Texto { get; set; }
    public Guid? IdEquipo { get; set; }
    public Guid? IdEstado { get; set; }
    public Guid? IdPrioridad { get; set; }
    public Guid? IdCategoria { get; set; }
    public Guid? IdTecnico { get; set; }

    /// <summary>
    /// Incidencias en las que la persona está involucrada: las que tiene
    /// asignadas **o** las que reportó.
    ///
    /// Es distinto de <see cref="IdTecnico"/> y hace falta que lo sea: el
    /// solicitante reporta pero no atiende, así que filtrar por técnico le
    /// dejaba el listado vacío aunque hubiera reportado media docena.
    /// </summary>
    public Guid? IdInvolucrado { get; set; }

    /// <summary>Sólo las que están en un estado no terminal.</summary>
    public bool SoloAbiertas { get; set; }

    /// <summary>Sólo las que la asignación de respaldo dejó marcadas (CP-14).</summary>
    public bool SoloPendientesDeRevision { get; set; }

    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public int Pagina { get; set; } = 1;
    public int PorPagina { get; set; } = 20;
}

public interface IIncidenciaDao : IGenericDao<Incidencia>
{
    PaginaDe<Incidencia> Buscar(FiltroIncidencias filtro);

    /// <summary>
    /// Siguiente correlativo de la organización. Se resuelve en la base y dentro
    /// de la transacción del alta: calcularlo antes, en memoria, daría números
    /// repetidos con dos altas simultáneas.
    /// </summary>
    int ProximoNumero();

    /// <summary>Incidencias no cerradas de un equipo. Alimenta la detección de duplicados.</summary>
    IList<Incidencia> AbiertasDeEquipo(Guid idEquipo);

    /// <summary>Incidencias de un equipo desde una fecha. Alimenta el motor predictivo.</summary>
    IList<Incidencia> DeEquipoDesde(Guid idEquipo, DateTime desde);

    /// <param name="idEstado">
    /// Estado al que pasa la incidencia, o null para dejarlo como está. Quién
    /// decide el estado es la BLL: el DAO no conoce el ciclo de vida.
    /// </param>
    void Asignar(
        Guid idIncidencia,
        Guid? idTecnico,
        TipoAsignacion tipo,
        bool pendienteRevision,
        Guid? idEstado = null);

    /// <summary>
    /// Avance de la atencion: mueve el estado y guarda diagnostico y solucion.
    /// </summary>
    /// <param name="fechaResolucion">
    /// La fecha en que se resolvio, o <c>null</c> para borrarla. No hay opcion
    /// de "dejar la que estaba" a proposito: toda transicion la escribe o la
    /// limpia, y una tercera opcion silenciosa dejaria fechas de resolucion en
    /// incidencias reabiertas.
    /// </param>
    void Atender(Guid idIncidencia, Guid idEstado, string? diagnostico,
                 string? solucion, DateTime? fechaResolucion);

    void GuardarClasificacion(ClasificacionIncidencia clasificacion);
    void GuardarRecomendacion(RecomendacionAsignacion recomendacion);

    ClasificacionIncidencia? ClasificacionDe(Guid idIncidencia);
    RecomendacionAsignacion? RecomendacionDe(Guid idIncidencia);
}

/// <summary>Sugerencia de clasificación guardada para una incidencia.</summary>
public class ClasificacionIncidencia
{
    public Guid Id { get; set; }
    public Guid IdIncidencia { get; set; }
    public Guid? IdCategoriaSugerida { get; set; }
    public Guid? IdPrioridadSugerida { get; set; }
    public Guid? IdIncidenciaDuplicada { get; set; }
    public decimal? Confianza { get; set; }

    /// <summary>HEURISTICA o IA. Es lo que hace auditable de dónde salió la sugerencia.</summary>
    public string Origen { get; set; } = "HEURISTICA";

    public string? Justificacion { get; set; }
    public DateTime Fecha { get; set; }
}

/// <summary>
/// Recomendación de asignación guardada para una incidencia.
///
/// <see cref="ContextoEnviado"/> guarda el JSON exacto que se le mandó al
/// proveedor. Sin eso no se puede reconstruir por qué recomendó lo que
/// recomendó, y la trazabilidad de la decisión se pierde.
/// </summary>
public class RecomendacionAsignacion
{
    public Guid Id { get; set; }
    public Guid IdIncidencia { get; set; }
    public Guid? IdTecnicoSugerido { get; set; }
    public string? Justificacion { get; set; }
    public string? ContextoEnviado { get; set; }

    /// <summary>IA o RESPALDO.</summary>
    public string Origen { get; set; } = "IA";

    public string? MotivoRespaldo { get; set; }

    /// <summary>
    /// Cuando se decidio la asignacion.
    ///
    /// Arranca en la hora actual y no en `default`. Antes la ponia el DEFAULT
    /// del motor, con dos consecuencias: la entidad recien guardada volvia con
    /// 0001-01-01 y esa fecha imposible salia en la respuesta del alta, y la
    /// marca quedaba en el reloj de la base mientras el resto del sistema usa
    /// el de la aplicacion -tres horas de diferencia entre «se registro» y «se
    /// asigno» para algo que paso en el mismo segundo-.
    /// </summary>
    public DateTime FechaHora { get; set; } = DateTime.Now;
}

public interface IMantenimientoDao : IGenericDao<Mantenimiento>
{
    IList<Mantenimiento> DeEquipo(Guid idEquipo);
    IList<Mantenimiento> DeEquipoDesde(Guid idEquipo, DateTime desde);
}

/// <summary>
/// El plan de mantenimiento preventivo y las fechas concretas que salen de él.
///
/// Va aparte de <see cref="IMantenimientoDao"/> y no adentro porque son dos
/// cosas distintas: uno registra lo que pasó y éste, lo que está previsto que
/// pase. Mezclarlos haría que el historial que alimenta al motor predictivo se
/// confunda con la agenda.
/// </summary>
public interface IMantenimientoProgramadoDao
{
    IList<PlanMantenimiento> Planes(bool soloActivos = false);
    PlanMantenimiento? PlanPorId(Guid id);
    Guid GuardarPlan(PlanMantenimiento plan);

    /// <summary>Los equipos operativos que alcanza un plan, resueltos contra el parque.</summary>
    IList<Guid> EquiposAlcanzados(Guid idPlan);

    MantenimientoProgramado? PorId(Guid id);
    IList<MantenimientoProgramado> Buscar(string? estado, DateOnly? hasta);
    IList<MantenimientoProgramado> PendientesDeEquipo(Guid idEquipo);
    IList<MantenimientoProgramado> PendientesDeLaOrganizacion();

    /// <summary>Evita programar dos veces lo mismo para el mismo día.</summary>
    bool YaProgramado(Guid idEquipo, Guid idTipo, DateOnly fecha);

    Guid Programar(MantenimientoProgramado programado);
    void Reprogramar(Guid id, DateOnly fecha, string motivo);
    void Anular(Guid id, string motivo);
    void MarcarEjecutado(Guid id, Guid idMantenimiento);
}

/// <summary>
/// Comprobantes del servicio y sus líneas (RF-17).
///
/// Las líneas no tienen su propio contrato: no existen fuera de su comprobante
/// —se borran con él y un borrador las regenera enteras—, así que darles un DAO
/// propio sería ofrecer una puerta para dejarlas huérfanas.
/// </summary>
public interface IFacturacionDao
{
    IList<Comprobante> Buscar(string? estado, string? periodo);

    /// <summary>El comprobante con sus líneas.</summary>
    Comprobante? PorId(Guid id);

    /// <summary>Sin líneas: se usa para saber si el período ya está facturado.</summary>
    Comprobante? PorPeriodo(string periodo);

    IList<LineaComprobante> Lineas(Guid idComprobante);

    /// <summary>
    /// Equipos administrados a una fecha. Se mide al cierre del período y no
    /// hoy: un comprobante de agosto dice el parque de agosto, aunque se
    /// genere en noviembre.
    /// </summary>
    int EquiposAdministradosAl(DateOnly fecha);

    Guid Guardar(Comprobante comprobante);
    void ReemplazarLineas(Guid idComprobante, IEnumerable<LineaComprobante> lineas);

    /// <summary>Sólo borra borradores: lo emitido se anula, no se borra.</summary>
    void Eliminar(Guid id);

    int ProximoNumero();
}

public interface IPrediccionDao
{
    IList<ReglaAlerta> ReglasDeLaOrganizacion(bool soloActivas = true);
    ReglaAlerta? ReglaPorId(Guid id);
    void GuardarRegla(ReglaAlerta regla);

    void GuardarEvaluacion(EvaluacionRiesgo evaluacion);
    EvaluacionRiesgo? UltimaEvaluacion(Guid idEquipo);

    /// <summary>
    /// La última evaluación de cada equipo, de mayor a menor score. Es el
    /// ranking del parque: se lee de lo ya calculado y no se reevalúa, porque
    /// abrir la pantalla no debería recalcular 47 equipos.
    /// </summary>
    IList<(EvaluacionRiesgo Evaluacion, string CodigoEquipo)> UltimasEvaluaciones();

    /// <summary>
    /// Alerta activa de un equipo por una regla. Se consulta antes de generar
    /// una nueva: sin esto, cada evaluación duplicaría la misma alerta y la
    /// pantalla quedaría inservible en una semana.
    /// </summary>
    AlertaPredictiva? AlertaActiva(Guid idEquipo, Guid idRegla);

    Guid GuardarAlerta(AlertaPredictiva alerta);
    IList<AlertaPredictiva> AlertasActivas();
    void AtenderAlerta(Guid idAlerta, Guid idUsuario, bool descartar);

    /// <summary>
    /// Alertas agrupadas por regla y por resultado (nueva, atendida,
    /// descartada), con la fecha de la ultima. Es lo que permite calibrar los
    /// umbrales con datos en lugar de a ojo.
    /// </summary>
    IList<(Guid IdRegla, string Estado, int Cantidad, DateTime? Ultima)> RecuentoPorRegla();
}

/// <summary>Un técnico de la organización con lo que hace falta para asignarle trabajo.</summary>
public class TecnicoConCarga
{
    public Guid IdUsuario { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public IList<TecnicoEspecialidad> Especialidades { get; set; } = new List<TecnicoEspecialidad>();
    public int IncidenciasAbiertas { get; set; }
    public int ResueltasEnEsteEquipo { get; set; }
}

public interface ITecnicoDao
{
    /// <summary>
    /// Técnicos de la organización con su carga y su historial sobre un equipo.
    ///
    /// Cruza las dos bases: las especialidades y la carga están en negocio, los
    /// nombres en servicio. Se resuelve con dos consultas y un join en memoria,
    /// no con una consulta entre bases (ADR 0003).
    /// </summary>
    /// <param name="idEquipo">
    /// Equipo de la incidencia, para contar cuántas de ese equipo ya resolvió
    /// cada técnico. Null cuando sólo se quiere la lista de técnicos, sin
    /// referencia a un equipo.
    /// </param>
    IList<TecnicoConCarga> CandidatosPara(Guid? idEquipo);
}

public interface IConfiguracionAsignacionDao
{
    ConfiguracionAsignacion? DeLaOrganizacion();
    void Guardar(ConfiguracionAsignacion configuracion);
    void RegistrarResultado(bool ok, string? error);
}

/// <summary>Configuración de asignación por organización (pantalla 10.5.1.7.7).</summary>
public class ConfiguracionAsignacion
{
    public Guid IdOrganizacion { get; set; }

    /// <summary>SIMULADO o CLAUDE.</summary>
    public string ProveedorIa { get; set; } = "SIMULADO";

    public string? ApiKeyCifrada { get; set; }
    public string? Modelo { get; set; }

    // Qué se le manda al servicio externo. Lo decide el cliente: son datos de su gente.
    public bool EnviarCarga { get; set; } = true;
    public bool EnviarEspecialidad { get; set; } = true;
    public bool EnviarHistorial { get; set; } = true;
    public bool EnviarDisponibilidad { get; set; }

    /// <summary>MENOR_CARGA o SIN_ASIGNAR.</summary>
    public string EstrategiaRespaldo { get; set; } = "MENOR_CARGA";

    public int TimeoutSegundos { get; set; } = 15;
    public DateTime? UltimaConsultaOk { get; set; }
    public string? UltimoError { get; set; }
}
