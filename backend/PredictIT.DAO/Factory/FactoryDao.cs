using Microsoft.Data.SqlClient;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Helpers;
using PredictIT.DAO.Implementations;

namespace PredictIT.DAO.Factory;

/// <summary>
/// DAO de la base de servicio: usuarios, permisos y bitácora.
///
/// Está separada de la fábrica de negocio y **no recibe contexto de sesión**,
/// por dos razones. La primera es de diseño: estas tablas no llevan
/// <c>id_organizacion</c>, así que no hay nada que filtrar. La segunda es
/// práctica y se descubrió al ejecutar: el contexto de sesión se construye
/// resolviendo permisos, que necesitan estos DAO. Si dependieran del contexto,
/// la resolución sería circular —contexto -> seguridad -> DAO -> contexto— y la
/// primera petición autenticada quedaba colgada.
/// </summary>
public interface IFactoryDaoSeguridad
{
    IUsuarioDao Usuarios { get; }
    IPermisoDao Permisos { get; }
    IBitacoraDao Bitacora { get; }
    IRespaldoDao Respaldos { get; }
    IIdiomaDao Idiomas { get; }
}

/// <summary>
/// DAO de la base de negocio (Abstract Factory).
///
/// La capa de negocio pide contratos a la fábrica y nunca nombra una clase
/// concreta de acceso a datos. Eso es lo que permite reemplazar la
/// implementación —o el motor— sin tocar la BLL.
/// </summary>
public interface IFactoryDao
{
    IEquipoDao Equipos { get; }
    ICatalogoDao Catalogos { get; }
    IOrganizacionDao Organizaciones { get; }
    IIncidenciaDao Incidencias { get; }
    IMantenimientoDao Mantenimientos { get; }
    IMantenimientoProgramadoDao MantenimientosProgramados { get; }
    IFacturacionDao Facturacion { get; }
    IPrediccionDao Prediccion { get; }
    ITecnicoDao Tecnicos { get; }
    IConfiguracionAsignacionDao ConfiguracionAsignacion { get; }

    IUnitOfWork IniciarUnidadDeTrabajo();
}

public class FactoryDaoSeguridad : IFactoryDaoSeguridad
{
    private readonly Lazy<IUsuarioDao> _usuarios;
    private readonly Lazy<IPermisoDao> _permisos;
    private readonly Lazy<IBitacoraDao> _bitacora;
    private readonly Lazy<IRespaldoDao> _respaldos;
    private readonly Lazy<IIdiomaDao> _idiomas;

    public FactoryDaoSeguridad(ConexionesSql conexiones)
    {
        var servicio = new SqlHelperSeguridad(conexiones);

        _usuarios = new Lazy<IUsuarioDao>(() => new UsuarioDao(servicio));
        _permisos = new Lazy<IPermisoDao>(() => new PermisoDao(servicio));
        _bitacora = new Lazy<IBitacoraDao>(() => new BitacoraDao(servicio));
        _respaldos = new Lazy<IRespaldoDao>(() => new RespaldoDao(servicio));
        _idiomas = new Lazy<IIdiomaDao>(() => new IdiomaDao(servicio));
    }

    public IUsuarioDao Usuarios => _usuarios.Value;
    public IPermisoDao Permisos => _permisos.Value;
    public IBitacoraDao Bitacora => _bitacora.Value;
    public IRespaldoDao Respaldos => _respaldos.Value;
    public IIdiomaDao Idiomas => _idiomas.Value;
}

public class FactoryDao : IFactoryDao
{
    private readonly ConexionesSql _conexiones;

    private readonly Lazy<IEquipoDao> _equipos;
    private readonly Lazy<ICatalogoDao> _catalogos;
    private readonly Lazy<IOrganizacionDao> _organizaciones;
    private readonly Lazy<IIncidenciaDao> _incidencias;
    private readonly Lazy<IMantenimientoDao> _mantenimientos;
    private readonly Lazy<IMantenimientoProgramadoDao> _programados;
    private readonly Lazy<IFacturacionDao> _facturacion;
    private readonly Lazy<IPrediccionDao> _prediccion;
    private readonly Lazy<ITecnicoDao> _tecnicos;
    private readonly Lazy<IConfiguracionAsignacionDao> _configuracionAsignacion;

    public FactoryDao(ConexionesSql conexiones, IContextoSesion contexto)
    {
        _conexiones = conexiones;

        var negocio = new SqlHelper(conexiones);

        // Perezoso a propósito: una petición que sólo consulta activos no tiene
        // por qué construir el DAO de catálogos ni el de organizaciones.
        _equipos = new Lazy<IEquipoDao>(() => new EquipoDao(negocio, contexto));
        _catalogos = new Lazy<ICatalogoDao>(() => new CatalogoDao(negocio, contexto));
        _organizaciones = new Lazy<IOrganizacionDao>(() => new OrganizacionDao(negocio));
        _incidencias = new Lazy<IIncidenciaDao>(() => new IncidenciaDao(negocio, contexto));
        _mantenimientos = new Lazy<IMantenimientoDao>(() => new MantenimientoDao(negocio, contexto));
        _programados = new Lazy<IMantenimientoProgramadoDao>(
            () => new MantenimientoProgramadoDao(negocio, contexto));
        _facturacion = new Lazy<IFacturacionDao>(() => new FacturacionDao(negocio, contexto));
        _prediccion = new Lazy<IPrediccionDao>(() => new PrediccionDao(negocio, contexto));
        _configuracionAsignacion = new Lazy<IConfiguracionAsignacionDao>(
            () => new ConfiguracionAsignacionDao(negocio, contexto));

        // El unico DAO de negocio que tambien toca la base de servicio: los
        // nombres de los tecnicos estan alla. Recibe los dos helpers y cruza en
        // memoria, no entre bases (ADR 0003).
        _tecnicos = new Lazy<ITecnicoDao>(
            () => new TecnicoDao(negocio, new SqlHelperSeguridad(conexiones), contexto));
    }

    public IEquipoDao Equipos => _equipos.Value;
    public ICatalogoDao Catalogos => _catalogos.Value;
    public IOrganizacionDao Organizaciones => _organizaciones.Value;
    public IIncidenciaDao Incidencias => _incidencias.Value;
    public IMantenimientoDao Mantenimientos => _mantenimientos.Value;
    public IMantenimientoProgramadoDao MantenimientosProgramados => _programados.Value;
    public IFacturacionDao Facturacion => _facturacion.Value;
    public IPrediccionDao Prediccion => _prediccion.Value;
    public ITecnicoDao Tecnicos => _tecnicos.Value;
    public IConfiguracionAsignacionDao ConfiguracionAsignacion => _configuracionAsignacion.Value;

    public IUnitOfWork IniciarUnidadDeTrabajo() => new SqlTransactRepository(_conexiones.Negocio);
}

/// <summary>
/// Unidad de trabajo sobre la base de negocio.
///
/// Abarca una sola base: entre negocio y servicio no hay transacción posible y
/// no se usa transacción distribuida a propósito (ADR 0003). Las operaciones que
/// tocan las dos asientan la bitácora *después* del commit de negocio, aceptando
/// que un fallo entre ambos pasos pierda el registro de auditoría pero nunca el
/// dato de negocio.
/// </summary>
public sealed class SqlTransactRepository : IUnitOfWork
{
    private readonly SqlConnection _conexion;
    private readonly SqlTransaction _transaccion;
    private bool _cerrada;

    public SqlTransactRepository(string cadenaConexion)
    {
        _conexion = new SqlConnection(cadenaConexion);
        _conexion.Open();
        _transaccion = _conexion.BeginTransaction();
    }

    public SqlConnection Conexion => _conexion;
    public SqlTransaction Transaccion => _transaccion;

    public void Comprometer()
    {
        if (_cerrada) throw new InvalidOperationException("La unidad de trabajo ya se cerró.");
        _transaccion.Commit();
        _cerrada = true;
    }

    public void Revertir()
    {
        if (_cerrada) return;
        _transaccion.Rollback();
        _cerrada = true;
    }

    public void Dispose()
    {
        // Si nadie comprometió, se revierte: es más seguro perder el trabajo que
        // dejar una transacción a medias tomando bloqueos.
        if (!_cerrada)
        {
            try { _transaccion.Rollback(); } catch { /* la conexión ya podía estar caída */ }
        }
        _transaccion.Dispose();
        _conexion.Dispose();
    }
}
