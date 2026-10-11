using Microsoft.Data.SqlClient;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Seguridad;

namespace PredictIT.Service.Seguridad;

public interface IBitacoraService
{
    void Registrar(string codigoTipoEvento, string descripcion, Guid? idUsuario = null,
                   Guid? idOrganizacion = null, string? entidad = null, Guid? idEntidad = null,
                   string? ip = null, string? traza = null);

    void RegistrarAccesoExitoso(Usuario usuario, Guid idOrganizacion, string? ip);
    void RegistrarIntentoFallido(string username, string? ip, string motivo, Guid? idUsuario = null);
    void RegistrarAccesoDenegado(string username, string dataKey, Guid? idUsuario, Guid? idOrganizacion);
    void RegistrarExcepcion(Exception ex, string? contexto, Guid? idUsuario, Guid? idOrganizacion);

    IList<Bitacora> Consultar(DateTime desde, DateTime hasta, Guid? idOrganizacion,
                              string? codigo, Guid? idUsuario = null);

    /// <summary>Sólo los errores, con su traza (CU.Arq.007).</summary>
    IList<Bitacora> ConsultarErrores(DateTime desde, DateTime hasta, Guid? idOrganizacion,
                                     bool incluirAdvertencias);
}

/// <summary>
/// Escritura de la bitácora (Req. Arq. 002).
///
/// Un fallo al asentar en bitácora **no** interrumpe la operación de negocio:
/// perder un registro de auditoría es malo, pero abortar el alta de un equipo
/// porque no se pudo escribir el log es peor. El fallo se reporta por el logger
/// de la aplicación.
///
/// La inmutabilidad no se garantiza acá: la garantiza un trigger en la base que
/// rechaza cualquier UPDATE o DELETE sobre la tabla.
/// </summary>
public class BitacoraService : IBitacoraService
{
    private readonly IFactoryDaoSeguridad _dao;
    private readonly ILoggerService _logger;

    /// <summary>
    /// Los identificadores de tipo de evento no cambian nunca en una corrida, y
    /// cada registro necesitaría una consulta para resolverlos.
    /// </summary>
    private readonly Dictionary<string, Guid> _tipos = new(StringComparer.OrdinalIgnoreCase);

    public BitacoraService(IFactoryDaoSeguridad dao, ILoggerService logger)
    {
        _dao = dao;
        _logger = logger;
    }

    public void Registrar(string codigoTipoEvento, string descripcion, Guid? idUsuario = null,
                          Guid? idOrganizacion = null, string? entidad = null, Guid? idEntidad = null,
                          string? ip = null, string? traza = null)
    {
        try
        {
            var idTipo = ResolverTipo(codigoTipoEvento);
            if (idTipo is null)
            {
                _logger.Advertencia($"Tipo de evento desconocido en bitácora: {codigoTipoEvento}");
                return;
            }

            Escribir(new Bitacora
            {
                IdUsuario = idUsuario == Guid.Empty ? null : idUsuario,
                IdTipoEvento = idTipo.Value,
                Descripcion = Acotar(descripcion, 1000),
                Entidad = entidad,
                IdEntidad = idEntidad,
                IdOrganizacion = idOrganizacion == Guid.Empty ? null : idOrganizacion,
                Ip = ip,
                Traza = traza
            }, codigoTipoEvento);
        }
        catch (Exception ex)
        {
            _logger.Error($"No se pudo asentar en bitácora el evento {codigoTipoEvento}.", ex);
        }
    }

    public void RegistrarAccesoExitoso(Usuario usuario, Guid idOrganizacion, string? ip) =>
        Registrar(TipoEvento.Codigos.LoginOk,
                  $"Inicio de sesión de {usuario.Username} ({usuario.NombreCompleto}).",
                  usuario.Id, idOrganizacion, "Usuario", usuario.Id, ip);

    public void RegistrarIntentoFallido(string username, string? ip, string motivo,
                                        Guid? idUsuario = null)
    {
        try
        {
            var idTipo = ResolverTipo(TipoEvento.Codigos.LoginFallido);
            if (idTipo is null) return;

            Escribir(new Bitacora
            {
                IdUsuario = idUsuario,
                UsuarioTexto = Acotar(username, 100),
                IdTipoEvento = idTipo.Value,
                Descripcion = Acotar($"Intento de acceso fallido. {motivo}", 1000),
                Ip = ip
            }, TipoEvento.Codigos.LoginFallido);
        }
        catch (Exception ex)
        {
            _logger.Error("No se pudo asentar el intento fallido en bitácora.", ex);
        }
    }

    public void RegistrarAccesoDenegado(string username, string dataKey, Guid? idUsuario,
                                        Guid? idOrganizacion) =>
        Registrar(TipoEvento.Codigos.AccesoDenegado,
                  $"Acceso denegado a {username}: falta la patente {dataKey}.",
                  idUsuario, idOrganizacion);

    public void RegistrarExcepcion(Exception ex, string? contexto, Guid? idUsuario,
                                   Guid? idOrganizacion) =>
        Registrar(TipoEvento.Codigos.ErrorSistema,
                  $"{contexto}: {ex.Message}".TrimStart(':', ' '),
                  idUsuario, idOrganizacion, traza: ex.ToString());

    public IList<Bitacora> Consultar(DateTime desde, DateTime hasta, Guid? idOrganizacion,
                                     string? codigo, Guid? idUsuario = null) =>
        _dao.Bitacora.Consultar(desde, hasta, idOrganizacion, codigo, idUsuario);

    public IList<Bitacora> ConsultarErrores(DateTime desde, DateTime hasta,
                                            Guid? idOrganizacion, bool incluirAdvertencias) =>
        _dao.Bitacora.ConsultarErrores(desde, hasta, idOrganizacion, incluirAdvertencias);

    /// <summary>
    /// Escribe el asiento, reintentando si el fallo es transitorio.
    ///
    /// Todas las operaciones del sistema escriben en la misma tabla, así que
    /// bajo concurrencia un asiento puede perder un lock o ser elegido víctima
    /// de un interbloqueo. Sin reintento eso se traga en el catch de arriba y
    /// el asiento desaparece en silencio: la auditoría queda con un agujero
    /// justo en el momento de más actividad, que es cuando más se la consulta.
    ///
    /// Un INSERT que lanza no llegó a confirmarse, así que reintentarlo no
    /// duplica. Los intentos son pocos y la espera corta a propósito: la
    /// bitácora no puede demorar la operación de negocio que la origina.
    /// </summary>
    private void Escribir(Bitacora entrada, string codigoTipoEvento)
    {
        const int intentos = 3;

        for (var intento = 1; ; intento++)
        {
            try
            {
                _dao.Bitacora.Registrar(entrada);
                if (intento > 1)
                    _logger.Advertencia(
                        $"El asiento de {codigoTipoEvento} se escribió recién en el intento {intento}.");
                return;
            }
            catch (Exception ex) when (intento < intentos && EsTransitorio(ex))
            {
                Thread.Sleep(40 * intento);
            }
        }
    }

    /// <summary>
    /// Si el fallo puede desaparecer solo. Un error de datos —un texto que no
    /// entra, un tipo de evento que no existe— da igual cuántas veces se
    /// reintente, y reintentarlo sólo retrasa la operación de negocio.
    /// </summary>
    private static bool EsTransitorio(Exception ex) => ex switch
    {
        TimeoutException => true,
        // 1205 interbloqueo, 1222 se agotó la espera del lock, -2 tiempo de
        // espera del comando, 4060/40613 base no disponible todavía.
        SqlException sql => sql.Number is 1205 or 1222 or -2 or 4060 or 40613,
        _ => false,
    };

    private Guid? ResolverTipo(string codigo)
    {
        if (_tipos.TryGetValue(codigo, out var cacheado)) return cacheado;

        var id = _dao.Bitacora.IdTipoEventoPorCodigo(codigo);
        if (id is { } encontrado) _tipos[codigo] = encontrado;
        return id;
    }

    /// <summary>
    /// Recorta al largo de la columna.
    ///
    /// Sin esto, un mensaje largo hace fallar el INSERT y se pierde el registro
    /// entero: preferimos un texto truncado a no tener rastro del evento.
    /// </summary>
    private static string Acotar(string? texto, int largo)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        return texto.Length <= largo ? texto : texto[..(largo - 1)] + "…";
    }
}
