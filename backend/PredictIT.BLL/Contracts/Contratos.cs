using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;

namespace PredictIT.BLL.Contracts;

/// <summary>
/// Claves de las patentes, en un solo lugar.
///
/// Se usan como constantes y no como texto suelto para que un error de tipeo lo
/// atrape el compilador. Los valores tienen que coincidir exactamente con la
/// columna <c>data_key</c> de la tabla Patente.
/// </summary>
public static class Patentes
{
    public const string EquipoVer = "EQUIPO_VER";
    public const string EquipoGestionar = "EQUIPO_GESTIONAR";
    public const string EquipoCambiarEstado = "EQUIPO_CAMBIAR_ESTADO";
    public const string IncidenciaVerTodas = "INCIDENCIA_VER_TODAS";
    public const string IncidenciaVerPropias = "INCIDENCIA_VER_PROPIAS";
    public const string IncidenciaRegistrar = "INCIDENCIA_REGISTRAR";
    public const string IncidenciaAtender = "INCIDENCIA_ATENDER";
    public const string IncidenciaReasignarPropias = "INCIDENCIA_REASIGNAR_PROPIAS";
    public const string IncidenciaReasignarTodas = "INCIDENCIA_REASIGNAR_TODAS";
    public const string MantenimientoVer = "MANTENIMIENTO_VER";
    public const string MantenimientoRegistrar = "MANTENIMIENTO_REGISTRAR";

    /// <summary>
    /// Definir cada cuánto le toca el preventivo a un equipo o a un tipo.
    ///
    /// Es aparte de registrar mantenimientos porque son decisiones de nivel
    /// distinto: registrar es contar lo que se hizo, y planificar es
    /// comprometer a la organización a hacerlo. El técnico hace lo primero;
    /// lo segundo lo decide quien responde por el parque.
    /// </summary>
    public const string MantenimientoPlanificar = "MANTENIMIENTO_PLANIFICAR";
    public const string HistorialVerOrganizacion = "HISTORIAL_VER_ORGANIZACION";
    public const string HistorialVerPropio = "HISTORIAL_VER_PROPIO";
    public const string AlertaVer = "ALERTA_VER";
    public const string ReglaGestionar = "REGLA_GESTIONAR";
    public const string DashboardVer = "DASHBOARD_VER";
    public const string ReporteGenerar = "REPORTE_GENERAR";
    public const string UsuarioGestionar = "USUARIO_GESTIONAR";
    public const string RolGestionar = "ROL_GESTIONAR";
    public const string OrganizacionGestionar = "ORGANIZACION_GESTIONAR";

    /// <summary>Ver los comprobantes del servicio y lo que se debe.</summary>
    public const string FacturacionVer = "FACTURACION_VER";

    /// <summary>
    /// Generar, emitir, cobrar y anular comprobantes.
    ///
    /// Es aparte de verlos porque son cosas distintas: quien administra la
    /// organización tiene que poder consultar qué le facturaron y desde cuándo
    /// debe, y eso no lo habilita a emitir ni a dar algo por cobrado.
    /// </summary>
    public const string FacturacionGestionar = "FACTURACION_GESTIONAR";
    public const string IaConfigurar = "IA_CONFIGURAR";
    public const string BitacoraVer = "BITACORA_VER";
    public const string BackupGestionar = "BACKUP_GESTIONAR";
    public const string IdiomaGestionar = "IDIOMA_GESTIONAR";

    /// <summary>
    /// Ver los errores del sistema con su traza (CU.Arq.007).
    ///
    /// Es aparte de BITACORA_VER porque una traza dice cosas que la descripción
    /// no: nombres de tablas, rutas de archivos, versiones de bibliotecas. Es
    /// información útil para diagnosticar y también para atacar.
    /// </summary>
    public const string ErrorVer = "ERROR_VER";
    public const string OrganizacionCambiar = "ORGANIZACION_CAMBIAR";
}

public interface IAutenticacionBusiness
{
    /// <summary>
    /// Asienta el cierre de sesión. No invalida el token: con JWT no hay
    /// sesión del lado del servidor que cerrar. Existe para que la bitácora
    /// tenga las salidas y no sólo las entradas.
    /// </summary>
    void CerrarSesion(string? ip);

    /// <summary>Valida credenciales y devuelve la sesión con su token (CU-001).</summary>
    SesionDto IniciarSesion(string username, string contrasena, string? ip);

    SesionDto CambiarOrganizacion(Guid idOrganizacion);
    SesionDto SesionActual();
}

public interface IEquipoBusiness
{
    PaginaDto<EquipoListaDto> Buscar(FiltroEquipos filtro);

    /// <summary>Cuántos equipos caen en cada segmento, con los filtros puestos.</summary>
    SegmentosDto Segmentos(FiltroEquipos filtro);
    EquipoDetalleDto ObtenerDetalle(Guid id);
    Guid Registrar(EquipoEntradaDto entrada);
    void Actualizar(Guid id, EquipoEntradaDto entrada);
    void DarDeBaja(Guid id);

    /// <summary>
    /// Cambia el estado operativo del equipo (patente EQUIPO_CAMBIAR_ESTADO).
    ///
    /// No es lo mismo que dar de baja: un equipo «en reparación» sigue siendo
    /// del parque. Importa al motor predictivo, que no evalúa los equipos no
    /// operativos.
    /// </summary>
    void CambiarEstado(Guid id, CambioEstadoEquipoDto entrada);
    CatalogosEquipoDto Catalogos();

    /// <summary>
    /// Los equipos de los que la persona es responsable (RF-05, módulo del
    /// cliente). Exige HISTORIAL_VER_PROPIO y no EQUIPO_VER: el solicitante ve
    /// los suyos, no el inventario.
    /// </summary>
    PaginaDto<EquipoListaDto> MisEquipos();
}

public interface IOrganizacionBusiness
{
    OrganizacionDto Actual();

    /// <summary>
    /// Situación del plan contra el parque real: equipos incluidos, adicionales y
    /// costo del mes. Es lo que muestra la pantalla de Organización.
    /// </summary>
    SituacionPlanDto SituacionDelPlan();

    /// <summary>Consumo del servicio de IA en el mes corriente.</summary>
    ConsumoIaDto ConsumoDeIa();

    /// <summary>
    /// Los contadores de la navegación. Devuelve sólo los que el usuario tiene
    /// permiso de ver; el resto viaja en nulo.
    /// </summary>
    ResumenNavegacionDto ResumenDeNavegacion();
}

public interface IIncidenciaBusiness
{
    PaginaDto<IncidenciaListaDto> Buscar(FiltroIncidencias filtro);
    IncidenciaDetalleDto ObtenerDetalle(Guid id);

    /// <summary>Registra la incidencia y dispara triage y asignación (CU-006, CU-007).</summary>
    IncidenciaRegistradaDto Registrar(IncidenciaEntradaDto entrada);

    /// <summary>Reasignación manual (CU-013).</summary>
    void Reasignar(Guid idIncidencia, Guid idTecnico);

    /// <summary>
    /// Avanza la atención de una incidencia: mueve el estado y guarda
    /// diagnóstico y solución (patente INCIDENCIA_ATENDER).
    ///
    /// La máquina de estados está acá y no en la pantalla. Pasar a «resuelta»
    /// exige diagnóstico y solución, y escribe la fecha de resolución, que es
    /// el dato del que sale el tiempo de reparación y el cumplimiento del SLA.
    /// </summary>
    void Atender(Guid idIncidencia, AtencionDto entrada);

    /// <summary>
    /// A qué estados se puede pasar desde el actual. La pantalla la usa para
    /// ofrecer sólo lo posible, en lugar de dejar intentar y fallar.
    /// </summary>
    IReadOnlyList<TransicionDto> TransicionesDe(Guid idIncidencia);

    /// <summary>
    /// Guía de reparación sugerida para la incidencia (RF-15, CU-018).
    ///
    /// Se genera cuando el técnico la pide y no al registrar la incidencia: la
    /// mayoría se resuelve sin mirarla, y consultarla siempre sería pagarle al
    /// proveedor por cada ticket. No se persiste; lo que queda es el asiento en
    /// la bitácora.
    /// </summary>
    GuiaReparacionDto SugerirReparacion(Guid idIncidencia);

    CatalogosIncidenciaDto Catalogos();
}

public interface IPrediccionBusiness
{
    RiesgoEquipoDto EvaluarEquipo(Guid idEquipo);

    /// <summary>Evalúa el parque completo y genera las alertas nuevas (CU-010).</summary>
    ResultadoEvaluacionDto EvaluarParque();

    /// <summary>
    /// Reevalúa un equipo a raíz de un hecho —una incidencia nueva, un
    /// mantenimiento registrado— y no de que alguien haya abierto la pantalla.
    ///
    /// Es lo que hace que el análisis sea predictivo y no una consulta: la
    /// alerta aparece cuando la condición se cumple. Devuelve cuántas alertas
    /// nuevas generó.
    ///
    /// No exige patente y no propaga excepciones: es una consecuencia del alta,
    /// y que el análisis falle no puede hacer fallar el registro de la falla
    /// que lo disparó.
    /// </summary>
    int ReevaluarPorEvento(Guid idEquipo);

    IReadOnlyList<AlertaDto> AlertasActivas();

    /// <summary>
    /// Rendimiento de cada regla: cuántas alertas generó, cuántas se
    /// atendieron y cuántas se descartaron (H-48). Exige ALERTA_VER.
    /// </summary>
    IReadOnlyList<CalibracionReglaDto> Calibracion();

    /// <summary>Distribución, ranking y alertas, sin reevaluar (CU-011).</summary>
    PanelPredictivoDto Panel();
    void AtenderAlerta(Guid idAlerta, bool descartar);

    IReadOnlyList<ReglaDto> Reglas();
    Guid GuardarRegla(ReglaDto entrada);

    /// <summary>Estado del proveedor de IA y del disyuntor (ADR 0009).</summary>
    EstadoIaDto EstadoDeLaIa();

    /// <summary>Indicadores, equipos en riesgo y alertas recientes (RF-13, CU-011).</summary>
    DashboardDto Dashboard();
}

/// <summary>
/// Administración del sistema: mantenimientos, usuarios, bitácora y la
/// configuración de la asignación automática.
/// </summary>
/// <summary>
/// Multiidioma (Req. Arq. 001 · CU.Arq.004).
///
/// Las lecturas no exigen patente: la pantalla de inicio de sesión necesita su
/// diccionario antes de que exista un usuario. La escritura opera siempre sobre
/// el usuario de la sesión.
/// </summary>
public interface IIdiomaBusiness
{
    IReadOnlyList<IdiomaDto> Idiomas();

    /// <summary>
    /// El diccionario del idioma pedido, o del que eligió el usuario, o del que
    /// está por defecto —en ese orden—. Las claves que falten en el idioma
    /// elegido se completan con el idioma por defecto.
    /// </summary>
    DiccionarioDto Diccionario(string? codigo);

    void CambiarIdioma(string codigo);

    /// <summary>Cobertura de traducción. Exige IDIOMA_GESTIONAR.</summary>
    CoberturaIdiomaDto Cobertura();
}

/// <summary>
/// Reportes exportables (RF-14, patente REPORTE_GENERAR).
///
/// Está aparte a propósito. Antes existía una única IAdministracionBusiness con
/// veinte métodos y cinco temas, y todo consumidor dependía de los cinco: era
/// la violación de segregación de interfaces que el proyecto tenía anotada. Se
/// partió en cinco (H-43) y los reportes quedan como una sexta, no dentro de
/// ninguna.
/// </summary>
public interface IReporteBusiness
{
    IReadOnlyList<CatalogoItemDto> Disponibles();
    ReporteGeneradoDto Generar(string reporte, FiltroReporteDto filtro);
}

/// <summary>
/// Mantenimientos y el historial del parque (RF-03 · CU-008, CU-009).
/// </summary>
/// <summary>
/// El plan de mantenimiento preventivo y su agenda (RF-16, CU-019, CU-020).
///
/// Va aparte de <see cref="IMantenimientoBusiness"/> por la misma razón que en
/// el DAO: registrar lo que se hizo y planificar lo que hay que hacer son dos
/// responsabilidades, y la segunda necesita su propia patente.
/// </summary>
public interface IMantenimientoProgramadoBusiness
{
    IReadOnlyList<PlanMantenimientoDto> Planes();
    Guid GuardarPlan(Guid? id, PlanMantenimientoEntradaDto entrada);

    IReadOnlyList<MantenimientoProgramadoDto> Programados(string? estado);
    PanelProgramadoDto Panel();

    Guid Programar(ProgramarEntradaDto entrada);
    void Reprogramar(Guid id, ReprogramarEntradaDto entrada);
    void Anular(Guid id, string motivo);

    /// <summary>Crea las fechas que faltan a partir de los planes activos.</summary>
    GeneracionDto GenerarDesdePlanes();
}

public interface IMantenimientoBusiness
{
    IReadOnlyList<MantenimientoListaDto> Mantenimientos(Guid? idEquipo);
    Guid RegistrarMantenimiento(MantenimientoEntradaDto entrada);
    CatalogosMantenimientoDto CatalogosMantenimiento();

    /// <summary>
    /// Historial técnico de la organización: incidencias y mantenimientos de
    /// todos los equipos en una sola línea de tiempo (patente
    /// HISTORIAL_VER_ORGANIZACION).
    ///
    /// Es lo que se mira cuando la pregunta no es «qué le pasó a este equipo»
    /// sino «qué viene pasando en el parque».
    /// </summary>
    IReadOnlyList<HechoHistorialDto> Historial(DateTime? desde, DateTime? hasta);
}

/// <summary>Usuarios, roles y bloqueos (RF-02 · CU-002).</summary>
/// <summary>
/// La facturación del servicio (RF-17, CU-021).
///
/// Convierte el cálculo mensual del plan —que la pantalla de Organización ya
/// muestra— en un comprobante con número, fecha, vencimiento y estado de cobro.
/// </summary>
public interface IFacturacionBusiness
{
    IReadOnlyList<ComprobanteDto> Comprobantes(string? estado, string? periodo);
    ComprobanteDto Comprobante(Guid id);
    PanelFacturacionDto Panel();

    /// <summary>
    /// Arma el borrador del período. Si ya está facturado devuelve el que hay:
    /// el importe de un mes cerrado no cambia porque se vuelva a pedir.
    /// </summary>
    GeneracionComprobanteDto Generar(string? periodo);

    ComprobanteDto AgregarAjuste(Guid id, AjusteEntradaDto entrada);

    /// <summary>Le pone número y fecha. A partir de acá no se toca.</summary>
    ComprobanteDto Emitir(Guid id);

    ComprobanteDto RegistrarPago(Guid id, DateOnly? fecha);

    /// <summary>Un borrador se descarta; un emitido se anula con su motivo.</summary>
    void Anular(Guid id, string motivo);
}

public interface IUsuarioBusiness
{
    IReadOnlyList<UsuarioListaDto> Usuarios();
    UsuarioDetalleDto Usuario(Guid id);
    IReadOnlyList<RolDto> Roles();
    void CambiarEstadoUsuario(Guid id, bool activo);
    void DesbloquearUsuario(Guid id);
    void AsignarRoles(Guid id, IReadOnlyList<Guid> idsRol);
}

/// <summary>
/// Bitácora y errores del sistema (Req. Arq. 002 · CU.Arq.007).
///
/// Sólo lee. La bitácora no se puede modificar ni conectándose directo a la
/// base, así que ofrecer una escritura acá sería mentir sobre lo que se puede
/// hacer.
/// </summary>
public interface IAuditoriaBusiness
{
    IReadOnlyList<EntradaBitacoraDto> Bitacora(FiltroBitacoraDto filtro);
    IReadOnlyList<CatalogoItemDto> TiposDeEvento();

    /// <summary>Errores del sistema con su traza (CU.Arq.007). Exige ERROR_VER.</summary>
    IReadOnlyList<ErrorDto> Errores(FiltroErroresDto filtro);
}

/// <summary>Configuración del proveedor de IA (CU.Arq.006).</summary>
public interface IIntegracionIaBusiness
{
    PanelIaDto PanelIa();
    void GuardarConfiguracionIa(ConfiguracionIaDto entrada);

    /// <summary>
    /// Guarda la clave de API del proveedor, cifrada (CU.Arq.006).
    ///
    /// Es la única forma de que entre una clave al sistema, y no tiene lectura:
    /// una vez guardada no vuelve a salir del servidor. Lo que se puede
    /// consultar es si hay una y de dónde sale, nunca su valor.
    /// </summary>
    void GuardarClaveIa(string clave);

    /// <summary>Borra la clave guardada. Deja al sistema sin clave, no la reemplaza.</summary>
    void BorrarClaveIa();
}

/// <summary>Respaldos y restauración de las dos bases (Req. Arq. 003).</summary>
public interface IRespaldoBusiness
{
    PanelRespaldosDto PanelRespaldos();

    /// <summary>Respalda una base y registra el resultado, haya salido bien o mal.</summary>
    RespaldoDto Respaldar(string nombreBase);

    /// <summary>
    /// Restaura un respaldo por su identificador (Req. Arq. 003).
    /// </summary>
    /// <param name="confirmoLaBase">
    /// El nombre de la base, escrito por quien opera. Tiene que coincidir con
    /// la del respaldo: es la confirmación de que sabe qué va a reemplazar.
    /// </param>
    RespaldoDto Restaurar(Guid idRespaldo, string confirmoLaBase);
}
