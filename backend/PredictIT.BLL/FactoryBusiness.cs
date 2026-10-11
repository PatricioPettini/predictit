using PredictIT.BLL.Contracts;
using PredictIT.BLL.Implementations;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Service.IA;
using PredictIT.Service.Prediccion;
using PredictIT.Service.Reportes;
using PredictIT.Service.Respaldos;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL;

/// <summary>
/// Único punto donde se instancian los business (Abstract Factory).
///
/// La API pide contratos a la fábrica y no nombra clases concretas de negocio,
/// igual que el negocio no nombra clases concretas de acceso a datos.
/// </summary>
public interface IFactoryBusiness
{
    IAutenticacionBusiness Autenticacion { get; }
    IEquipoBusiness Equipos { get; }
    IOrganizacionBusiness Organizaciones { get; }
    IIncidenciaBusiness Incidencias { get; }
    IPrediccionBusiness Prediccion { get; }
    IMantenimientoBusiness Mantenimientos { get; }
    IMantenimientoProgramadoBusiness MantenimientosProgramados { get; }
    IFacturacionBusiness Facturacion { get; }
    IUsuarioBusiness Usuarios { get; }
    IAuditoriaBusiness Auditoria { get; }
    IIntegracionIaBusiness IntegracionIa { get; }
    IRespaldoBusiness Respaldos { get; }
    IIdiomaBusiness Idiomas { get; }
    IReporteBusiness Reportes { get; }
}

public class FactoryBusiness : IFactoryBusiness
{
    private readonly Lazy<IAutenticacionBusiness> _autenticacion;
    private readonly Lazy<IEquipoBusiness> _equipos;
    private readonly Lazy<IOrganizacionBusiness> _organizaciones;
    private readonly Lazy<IIncidenciaBusiness> _incidencias;
    private readonly Lazy<IPrediccionBusiness> _prediccion;
    private readonly Lazy<IMantenimientoBusiness> _mantenimientos;
    private readonly Lazy<IMantenimientoProgramadoBusiness> _programados;
    private readonly Lazy<IFacturacionBusiness> _facturacion;
    private readonly Lazy<IUsuarioBusiness> _usuarios;
    private readonly Lazy<IAuditoriaBusiness> _auditoria;
    private readonly Lazy<IIntegracionIaBusiness> _integracionIa;
    private readonly Lazy<IRespaldoBusiness> _respaldosBll;
    private readonly Lazy<IIdiomaBusiness> _idiomas;
    private readonly Lazy<IReporteBusiness> _reportes;

    public FactoryBusiness(IFactoryDao dao, IFactoryDaoSeguridad daoSeguridad, IContextoSesion contexto,
                           IBitacoraService bitacora, ISeguridadService seguridad, ITokenService tokens,
                           IProveedorIA proveedorIa, MotorPredictivo motor,
                           IRespaldoService respaldos, ICifradoService cifrado,
                           IReporteService reportesPdf)
    {
        _autenticacion = new Lazy<IAutenticacionBusiness>(
            () => new AutenticacionBusiness(seguridad, tokens, dao, daoSeguridad, contexto, bitacora));
        _equipos = new Lazy<IEquipoBusiness>(
            () => new EquipoBusiness(dao, daoSeguridad, contexto, bitacora,
                                     () => _prediccion!.Value));
        _organizaciones = new Lazy<IOrganizacionBusiness>(
            () => new OrganizacionBusiness(dao, daoSeguridad, contexto));
        // La predicción se pasa como función y no como instancia: si se pasara
        // resuelta, construir el business de incidencias construiría también el
        // de predicción aunque la operación no lo necesite.
        _incidencias = new Lazy<IIncidenciaBusiness>(
            () => new IncidenciaBusiness(dao, daoSeguridad, contexto, bitacora, proveedorIa,
                                         () => _prediccion!.Value));
        _prediccion = new Lazy<IPrediccionBusiness>(
            () => new PrediccionBusiness(dao, daoSeguridad, contexto, bitacora, proveedorIa, motor));
        _reportes = new Lazy<IReporteBusiness>(
            () => new ReporteBusiness(dao, daoSeguridad, contexto, bitacora, reportesPdf));
        _idiomas = new Lazy<IIdiomaBusiness>(
            () => new IdiomaBusiness(daoSeguridad, contexto, bitacora));
        // Las cinco areas de administracion. Cada una recibe solo lo que usa:
        // la clase unica que habia antes pedia las ocho dependencias para
        // cualquiera de sus veinte metodos.
        _mantenimientos = new Lazy<IMantenimientoBusiness>(
            () => new MantenimientoBusiness(daoSeguridad, contexto, dao, bitacora,
                                            () => _prediccion!.Value));
        _programados = new Lazy<IMantenimientoProgramadoBusiness>(
            () => new MantenimientoProgramadoBusiness(daoSeguridad, contexto, dao, bitacora));
        _facturacion = new Lazy<IFacturacionBusiness>(
            () => new FacturacionBusiness(daoSeguridad, contexto, dao, bitacora));
        _usuarios = new Lazy<IUsuarioBusiness>(
            () => new UsuarioBusiness(daoSeguridad, contexto, dao, bitacora));
        _auditoria = new Lazy<IAuditoriaBusiness>(
            () => new AuditoriaBusiness(daoSeguridad, contexto));
        _integracionIa = new Lazy<IIntegracionIaBusiness>(
            () => new IntegracionIaBusiness(daoSeguridad, contexto, dao, bitacora,
                                            proveedorIa, cifrado));
        _respaldosBll = new Lazy<IRespaldoBusiness>(
            () => new RespaldoBusiness(daoSeguridad, contexto, bitacora, respaldos));
    }

    public IAutenticacionBusiness Autenticacion => _autenticacion.Value;
    public IEquipoBusiness Equipos => _equipos.Value;
    public IOrganizacionBusiness Organizaciones => _organizaciones.Value;
    public IIncidenciaBusiness Incidencias => _incidencias.Value;
    public IPrediccionBusiness Prediccion => _prediccion.Value;
    public IMantenimientoBusiness Mantenimientos => _mantenimientos.Value;
    public IMantenimientoProgramadoBusiness MantenimientosProgramados => _programados.Value;
    public IFacturacionBusiness Facturacion => _facturacion.Value;
    public IUsuarioBusiness Usuarios => _usuarios.Value;
    public IAuditoriaBusiness Auditoria => _auditoria.Value;
    public IIntegracionIaBusiness IntegracionIa => _integracionIa.Value;
    public IRespaldoBusiness Respaldos => _respaldosBll.Value;
    public IIdiomaBusiness Idiomas => _idiomas.Value;
    public IReporteBusiness Reportes => _reportes.Value;
}
