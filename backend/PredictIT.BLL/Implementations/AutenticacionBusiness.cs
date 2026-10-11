using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// Inicio de sesión y cambio de organización (CU-001).
///
/// La validación de credenciales la hace el servicio de seguridad; acá se arma
/// el DTO de sesión con el token y lo que el frontend necesita para dibujar el
/// menú: los permisos.
/// </summary>
public class AutenticacionBusiness : IAutenticacionBusiness
{
    private readonly ISeguridadService _seguridad;
    private readonly ITokenService _tokens;
    private readonly IFactoryDao _dao;
    private readonly IFactoryDaoSeguridad _daoSeguridad;
    private readonly IContextoSesion _contexto;
    private readonly IBitacoraService _bitacora;

    public AutenticacionBusiness(ISeguridadService seguridad, ITokenService tokens, IFactoryDao dao,
                                 IFactoryDaoSeguridad daoSeguridad, IContextoSesion contexto,
                                 IBitacoraService bitacora)
    {
        _seguridad = seguridad;
        _daoSeguridad = daoSeguridad;
        _tokens = tokens;
        _dao = dao;
        _contexto = contexto;
        _bitacora = bitacora;
    }

    public SesionDto IniciarSesion(string username, string contrasena, string? ip)
    {
        var auth = _seguridad.Autenticar(username, contrasena, ip);

        // El mensaje ya viene armado por el servicio y es deliberadamente
        // ambiguo para credenciales inválidas: no hay que distinguir "no existe"
        // de "contraseña incorrecta".
        //
        // El código acompaña al mensaje sin decir nada que el mensaje no diga
        // ya: los dos casos que colapsan en `credenciales` siguen colapsados.
        if (!auth.Exitosa)
            throw new BusinessException(auth.Mensaje) { Codigo = CodigoDe(auth.Resultado) };

        return Armar(auth.Usuario!, auth.Contexto!);
    }

    /// <summary>
    /// Codigo estable del motivo, para que el frontend pueda tratarlos
    /// distinto. `CredencialesInvalidas` cubre tanto el usuario que no existe
    /// como la contrasena equivocada, igual que el mensaje: el codigo no
    /// distingue lo que el mensaje decidio no distinguir.
    /// </summary>
    private static string CodigoDe(ResultadoAutenticacion resultado) => resultado switch
    {
        ResultadoAutenticacion.CredencialesInvalidas => "credenciales",
        ResultadoAutenticacion.UsuarioBloqueado => "bloqueado",
        ResultadoAutenticacion.UsuarioInactivo => "inactivo",
        ResultadoAutenticacion.SinOrganizacion => "sin-organizacion",
        _ => "fallo"
    };

    public SesionDto CambiarOrganizacion(Guid idOrganizacion)
    {
        if (!_contexto.Tiene(Patentes.OrganizacionCambiar))
            throw new PermisoDenegadoException(Patentes.OrganizacionCambiar);

        var contexto = _seguridad.ReconstruirContexto(_contexto.IdUsuario, idOrganizacion)
                       ?? throw new BusinessException("No tenés habilitada esa organización.");

        var usuario = _daoSeguridad.Usuarios.GetById(_contexto.IdUsuario)
                      ?? throw new BusinessException("No se encontró el usuario de la sesión.");

        _bitacora.Registrar(TipoEvento.Codigos.Modificacion,
            $"{usuario.Username} cambió a la organización {idOrganizacion}.",
            usuario.Id, idOrganizacion);

        return Armar(usuario, contexto);
    }

    public void CerrarSesion(string? ip)
    {
        // Sin sesión no hay nada que asentar, y tampoco es un error: puede
        // llegar de un token ya expirado.
        if (_contexto.IdUsuario == Guid.Empty) return;

        _bitacora.Registrar(TipoEvento.Codigos.Logout,
            $"Cierre de sesión de {_contexto.Username}.",
            _contexto.IdUsuario, _contexto.IdOrganizacion, "Usuario", _contexto.IdUsuario, ip);
    }

    public SesionDto SesionActual()
    {
        var usuario = _daoSeguridad.Usuarios.GetById(_contexto.IdUsuario)
                      ?? throw new BusinessException("La sesión no es válida.");

        var contexto = _seguridad.ReconstruirContexto(_contexto.IdUsuario, _contexto.IdOrganizacion)
                       ?? throw new BusinessException("La sesión no es válida.");

        return Armar(usuario, contexto);
    }

    private SesionDto Armar(Usuario usuario, ContextoSesion contexto)
    {
        var organizacion = _dao.Organizaciones.GetById(contexto.IdOrganizacion)
                           ?? throw new BusinessException("No se encontró la organización.");

        var (token, expira) = _tokens.Emitir(usuario.Id, usuario.Username, contexto.IdOrganizacion);

        // El Partner ve el selector de organizaciones; el resto, una sola.
        var disponibles = usuario.OrganizacionesHabilitadas.Count > 0
            ? usuario.OrganizacionesHabilitadas
                .Select(id => _dao.Organizaciones.GetById(id))
                .Where(o => o is not null)
                .Select(o => Mapear(o!))
                .ToList()
            : new List<OrganizacionDto> { Mapear(organizacion) };

        return new SesionDto(
            usuario.Id,
            usuario.Username,
            usuario.NombreCompleto,
            usuario.Email,
            usuario.Roles.Select(r => r.Nombre).ToList(),
            contexto.Patentes.ToList(),
            Mapear(organizacion),
            disponibles,
            token,
            expira);
    }

    private static OrganizacionDto Mapear(Domain.Negocio.Organizacion o) => new(
        o.Id, o.RazonSocial, o.NombreCorto, o.Cuit,
        o.Plan is null
            ? null
            : new PlanDto(o.Plan.Codigo, o.Plan.Nombre, o.Plan.AbonoMensual,
                          o.Plan.EquiposIncluidos, o.Plan.PrecioEquipoAdicional, o.Plan.Soporte));
}
