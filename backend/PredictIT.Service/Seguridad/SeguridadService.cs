using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Seguridad;

namespace PredictIT.Service.Seguridad;

/// <summary>Motivo por el que un intento de autenticación no prosperó.</summary>
public enum ResultadoAutenticacion
{
    Ok,
    CredencialesInvalidas,
    UsuarioInactivo,
    UsuarioBloqueado,
    SinOrganizacion
}

public class Autenticacion
{
    public ResultadoAutenticacion Resultado { get; init; }
    public Usuario? Usuario { get; init; }
    public ContextoSesion? Contexto { get; init; }

    public bool Exitosa => Resultado == ResultadoAutenticacion.Ok;

    /// <summary>
    /// Mensaje para mostrarle al usuario.
    ///
    /// Credenciales inválidas devuelve el mismo texto tanto si el usuario no
    /// existe como si la contraseña está mal: distinguirlos le confirmaría a un
    /// atacante qué nombres de usuario son válidos.
    /// </summary>
    public string Mensaje => Resultado switch
    {
        ResultadoAutenticacion.Ok => "Acceso concedido.",
        ResultadoAutenticacion.CredencialesInvalidas => "Los datos ingresados no son correctos.",
        ResultadoAutenticacion.UsuarioInactivo => "El usuario se encuentra inactivo.",
        ResultadoAutenticacion.UsuarioBloqueado =>
            "El usuario está bloqueado por intentos fallidos. Comunicate con el administrador.",
        ResultadoAutenticacion.SinOrganizacion =>
            "El usuario no tiene ninguna organización asignada.",
        _ => "No fue posible iniciar sesión."
    };
}

public interface ISeguridadService
{
    Autenticacion Autenticar(string username, string contrasena, string? ip = null);
    ContextoSesion? ReconstruirContexto(Guid idUsuario, Guid idOrganizacion);
    IReadOnlyCollection<Patente> PatentesDe(Guid idUsuario);
}

/// <summary>
/// Autenticación y resolución de permisos.
///
/// La resolución del árbol de permisos la hace el Composite del dominio; acá
/// sólo se lo arma desde la base y se lo consulta.
/// </summary>
public class SeguridadService : ISeguridadService
{
    private readonly IFactoryDaoSeguridad _dao;
    private readonly IBitacoraService _bitacora;

    public SeguridadService(IFactoryDaoSeguridad dao, IBitacoraService bitacora)
    {
        _dao = dao;
        _bitacora = bitacora;
    }

    public Autenticacion Autenticar(string username, string contrasena, string? ip = null)
    {
        var usuario = string.IsNullOrWhiteSpace(username) ? null : _dao.Usuarios.PorUsername(username.Trim());

        if (usuario is null)
        {
            // No hay usuario que referenciar, así que se guarda lo tecleado: sirve
            // para detectar si alguien está probando nombres de usuario.
            _bitacora.RegistrarIntentoFallido(username, ip, "El usuario no existe.");
            return Fallo(ResultadoAutenticacion.CredencialesInvalidas);
        }

        if (usuario.Bloqueado)
        {
            _bitacora.RegistrarIntentoFallido(username, ip, "Usuario bloqueado.", usuario.Id);
            return Fallo(ResultadoAutenticacion.UsuarioBloqueado, usuario);
        }

        if (!HashContrasena.Verificar(contrasena, usuario.PasswordHash))
        {
            _dao.Usuarios.RegistrarAccesoFallido(usuario.Id);
            _bitacora.RegistrarIntentoFallido(username, ip, "Contraseña incorrecta.", usuario.Id);
            return Fallo(ResultadoAutenticacion.CredencialesInvalidas, usuario);
        }

        // El orden importa: primero la contraseña y después el estado. Si se
        // comprobara el estado antes, un atacante podría distinguir un usuario
        // inactivo de uno inexistente sin conocer la contraseña.
        if (!usuario.Activo)
        {
            _bitacora.RegistrarIntentoFallido(username, ip, "Usuario inactivo.", usuario.Id);
            return Fallo(ResultadoAutenticacion.UsuarioInactivo, usuario);
        }

        CargarPermisos(usuario);

        var idOrganizacion = ResolverOrganizacion(usuario);
        if (idOrganizacion is null)
        {
            return Fallo(ResultadoAutenticacion.SinOrganizacion, usuario);
        }

        var contexto = new ContextoSesion(
            usuario.Id, usuario.Username, idOrganizacion.Value,
            usuario.ObtenerPatentes().Select(p => p.DataKey),
            usuario.IdIdioma);

        _dao.Usuarios.RegistrarAccesoExitoso(usuario.Id);
        RederivarSiHaceFalta(usuario, contrasena);
        _bitacora.RegistrarAccesoExitoso(usuario, idOrganizacion.Value, ip);

        return new Autenticacion
        {
            Resultado = ResultadoAutenticacion.Ok,
            Usuario = usuario,
            Contexto = contexto
        };
    }

    /// <summary>
    /// Vuelve a derivar el hash si quedó con menos iteraciones de las que se
    /// usan hoy.
    ///
    /// Es el único momento en que se puede: la contraseña en claro existe acá y
    /// en ningún otro lado. Subir las iteraciones sin esto deja a los usuarios
    /// creados antes con el hash viejo para siempre, y el cambio no protege a
    /// nadie.
    ///
    /// Si falla no se corta la sesión: el usuario ya se autenticó bien y su
    /// hash sigue siendo válido. Queda el aviso en el registro.
    /// </summary>
    private void RederivarSiHaceFalta(Usuario usuario, string contrasena)
    {
        if (!HashContrasena.NecesitaRehash(usuario.PasswordHash)) return;

        try
        {
            var nuevo = HashContrasena.Derivar(contrasena);
            _dao.Usuarios.ActualizarHash(usuario.Id, nuevo);
            usuario.PasswordHash = nuevo;
        }
        catch (Exception ex)
        {
            _bitacora.RegistrarExcepcion(
                ex, "No se pudo volver a derivar el hash de " + usuario.Username,
                usuario.Id, null);
        }
    }

    public ContextoSesion? ReconstruirContexto(Guid idUsuario, Guid idOrganizacion)
    {
        var usuario = _dao.Usuarios.GetById(idUsuario);
        if (usuario is null || !usuario.Activo || usuario.Bloqueado) return null;

        CargarPermisos(usuario);

        // Que el token diga una organización no alcanza: hay que confirmar que el
        // usuario siga habilitado sobre ella. Un Partner al que le revocaron un
        // cliente conserva el token viejo.
        if (!usuario.PuedeOperarSobre(idOrganizacion)) return null;

        return new ContextoSesion(usuario.Id, usuario.Username, idOrganizacion,
                                  usuario.ObtenerPatentes().Select(p => p.DataKey),
                                  usuario.IdIdioma);
    }

    public IReadOnlyCollection<Patente> PatentesDe(Guid idUsuario)
    {
        var usuario = _dao.Usuarios.GetById(idUsuario);
        if (usuario is null) return Array.Empty<Patente>();
        CargarPermisos(usuario);
        return usuario.ObtenerPatentes();
    }

    private void CargarPermisos(Usuario usuario)
    {
        foreach (var rol in _dao.Permisos.RolesDe(usuario.Id))
        {
            usuario.Roles.Add(rol);
        }
        foreach (var idOrg in _dao.Permisos.OrganizacionesDe(usuario.Id))
        {
            usuario.OrganizacionesHabilitadas.Add(idOrg);
        }
    }

    /// <summary>
    /// Organización con la que arranca la sesión.
    ///
    /// El Partner no pertenece a ninguna: se le abre la primera que tenga
    /// habilitada y después puede cambiarla si tiene la patente para eso.
    /// </summary>
    private static Guid? ResolverOrganizacion(Usuario usuario)
    {
        if (usuario.IdOrganizacion is { } propia && propia != Guid.Empty) return propia;

        var primera = usuario.OrganizacionesHabilitadas.FirstOrDefault();
        return primera == Guid.Empty ? null : primera;
    }

    private static Autenticacion Fallo(ResultadoAutenticacion resultado, Usuario? usuario = null) =>
        new() { Resultado = resultado, Usuario = usuario };
}
