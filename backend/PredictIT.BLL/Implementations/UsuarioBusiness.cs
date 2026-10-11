using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.IA;
using PredictIT.Service.Respaldos;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// Usuarios, roles y bloqueos (RF-02, CU-002).
///
/// Toda búsqueda de usuario pasa por la organización de la sesión y no por el
/// identificador: si no, alcanzaría con tener un id para operar sobre el
/// usuario de otro cliente.
/// </summary>
public class UsuarioBusiness(
    IFactoryDaoSeguridad seguridad,
    IContextoSesion contexto,
    IFactoryDao dao,
    IBitacoraService bitacora)
    : BaseAdministracion(seguridad, contexto), IUsuarioBusiness
{
    // ------------------------------------------------------------- usuarios

    public IReadOnlyList<UsuarioListaDto> Usuarios()
    {
        Exigir(Patentes.UsuarioGestionar);

        var usuarios = Seguridad.Usuarios.PorOrganizacion(Contexto.IdOrganizacion);

        // La carga de trabajo sale de negocio; los usuarios, de servicio. Se
        // cruzan en memoria (ADR 0003).
        var carga = dao.Tecnicos.CandidatosPara(idEquipo: null)
            .ToDictionary(t => t.IdUsuario, t => t.IncidenciasAbiertas);

        return usuarios.Select(u => ADto(u, carga)).ToList();
    }

    public UsuarioDetalleDto Usuario(Guid id)
    {
        Exigir(Patentes.UsuarioGestionar);

        var usuario = BuscarDeLaOrganizacion(id);
        var carga = dao.Tecnicos.CandidatosPara(idEquipo: null)
            .ToDictionary(t => t.IdUsuario, t => t.IncidenciasAbiertas);

        // De dónde viene cada patente: es lo que hace explicable el permiso.
        // Sin esto, «por qué este usuario puede hacer esto» no tiene respuesta.
        var concedidas = new List<PatenteConcedidaDto>();
        foreach (var rol in usuario.Roles)
        {
            foreach (var patente in rol.ObtenerPatentes())
            {
                concedidas.Add(new PatenteConcedidaDto(
                    patente.DataKey, patente.Nombre, rol.Nombre,
                    patente.TipoAcceso.ToString().ToUpperInvariant()));
            }
        }

        var unicas = concedidas
            .GroupBy(p => p.DataKey)
            .Select(g => g.Count() == 1
                ? g.First()
                // Una patente puede llegar por más de un rol. Se dice, en lugar
                // de mostrar el primero y ocultar el resto.
                : new PatenteConcedidaDto(g.Key, g.First().Nombre,
                    string.Join(", ", g.Select(x => x.Origen).Distinct()),
                    g.First().TipoAcceso))
            .OrderBy(p => p.DataKey)
            .ToList();

        return new UsuarioDetalleDto(ADto(usuario, carga), unicas);
    }

    public IReadOnlyList<RolDto> Roles()
    {
        Exigir(Patentes.UsuarioGestionar);

        return Seguridad.Permisos.TodosLosRoles()
            .Select(r => new RolDto(r.Id, r.Nombre, r.Descripcion, r.EsSistema,
                                    r.ObtenerPatentes().Count))
            .ToList();
    }

    public void CambiarEstadoUsuario(Guid id, bool activo)
    {
        Exigir(Patentes.UsuarioGestionar);

        var usuario = BuscarDeLaOrganizacion(id);

        // Nadie se desactiva a sí mismo: quedaría afuera del sistema sin nadie
        // que pueda volver a habilitarlo si es el único administrador.
        if (id == Contexto.IdUsuario && !activo)
            throw new BusinessException("No podés desactivar tu propio usuario.");

        Seguridad.Usuarios.CambiarEstado(id, activo);

        bitacora.Registrar(TipoEvento.Codigos.Modificacion,
            $"Usuario {usuario.Username} " + (activo ? "activado" : "desactivado") + ".");
    }

    public void DesbloquearUsuario(Guid id)
    {
        Exigir(Patentes.UsuarioGestionar);

        var usuario = BuscarDeLaOrganizacion(id);
        Seguridad.Usuarios.Desbloquear(id);

        bitacora.Registrar(TipoEvento.Codigos.Modificacion,
            $"Usuario {usuario.Username} desbloqueado tras {usuario.IntentosFallidos} intentos fallidos.");
    }

    public void AsignarRoles(Guid id, IReadOnlyList<Guid> idsRol)
    {
        Exigir(Patentes.RolGestionar);

        var usuario = BuscarDeLaOrganizacion(id);
        var roles = Seguridad.Permisos.TodosLosRoles();

        var desconocidos = idsRol.Where(r => roles.All(x => x.Id != r)).ToList();
        if (desconocidos.Count > 0)
            throw BusinessException.De("roles", "Alguno de los roles indicados no existe.");

        // Un usuario sin rol no puede hacer nada, ni siquiera ver una pantalla.
        // Es más probable que sea un error del formulario que una intención.
        if (idsRol.Count == 0)
        {
            throw BusinessException.De("roles",
                "El usuario tiene que tener al menos un rol. Para quitarle el acceso, desactivalo.");
        }

        // Quitarse a sí mismo la gestión de usuarios deja el sistema sin nadie
        // que pueda volver a otorgarla.
        if (id == Contexto.IdUsuario)
        {
            var nuevas = roles.Where(r => idsRol.Contains(r.Id))
                .SelectMany(r => r.ObtenerPatentes())
                .Select(p => p.DataKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!nuevas.Contains(Patentes.UsuarioGestionar))
            {
                throw new BusinessException(
                    "Con esos roles perderías la gestión de usuarios y no podrías revertirlo.");
            }
        }

        Seguridad.Usuarios.AsignarRoles(id, idsRol);

        var nombres = roles.Where(r => idsRol.Contains(r.Id)).Select(r => r.Nombre);
        bitacora.Registrar(TipoEvento.Codigos.Modificacion,
            $"Roles de {usuario.Username} cambiados a: {string.Join(", ", nombres)}.");
    }

    // ------------------------------------------------------------- privados

    private Usuario BuscarDeLaOrganizacion(Guid id)
    {
        var usuario = Seguridad.Usuarios.PorOrganizacion(Contexto.IdOrganizacion)
            .FirstOrDefault(u => u.Id == id);

        // Se busca dentro de la organización y no por identificador: si no,
        // alcanzaría el id para operar sobre un usuario de otro cliente.
        if (usuario is null)
            throw new BusinessException("El usuario no existe o no pertenece a tu organización.");

        usuario.Roles.Clear();
        foreach (var rol in Seguridad.Permisos.RolesDe(usuario.Id)) usuario.Roles.Add(rol);

        return usuario;
    }

    private UsuarioListaDto ADto(Usuario u, IDictionary<Guid, int> carga)
    {
        var roles = u.Roles.Count > 0 ? u.Roles : Seguridad.Permisos.RolesDe(u.Id);

        return new UsuarioListaDto(
            u.Id, u.Username, u.NombreCompleto, u.Email, u.Telefono,
            u.Activo, u.Bloqueado, u.IntentosFallidos, u.FechaAlta, u.UltimoAcceso,
            roles.Select(r => r.Nombre).ToList(),
            roles.SelectMany(r => r.ObtenerPatentes()).Select(p => p.Id).Distinct().Count(),
            carga.TryGetValue(u.Id, out var n) ? n : 0);
    }
}
