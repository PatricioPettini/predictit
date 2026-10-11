using PredictIT.BLL.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Seguridad;
using PredictIT.DAO.Contracts;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// Lo que comparten las cinco áreas de administración.
///
/// Es una clase base y no una clase de utilidades sueltas porque las tres cosas
/// que hay acá dependen del contexto de sesión: el control de patente, y la
/// resolución de nombres de usuario dentro de la organización. Pasarles el
/// contexto por parámetro en cada llamada sería el mismo acoplamiento escrito
/// de forma más larga.
///
/// No tiene nada más. Cuando se partió <c>AdministracionBusiness</c> en cinco,
/// la tentación fue dejar acá todo lo que usaba más de una: así es como una
/// clase base termina siendo la clase vieja con otro nombre.
/// </summary>
public abstract class BaseAdministracion(
    IFactoryDaoSeguridad seguridad,
    IContextoSesion contexto)
{
    protected IFactoryDaoSeguridad Seguridad { get; } = seguridad;
    protected IContextoSesion Contexto { get; } = contexto;

    protected void Exigir(string patente)
    {
        if (!Contexto.Tiene(patente)) throw new PermisoDenegadoException(patente);
    }

    /// <summary>
    /// Identificador de usuario a nombre, para toda la organización.
    ///
    /// Se trae el mapa entero de una vez en lugar de consultar por cada fila:
    /// la bitácora y el historial listan cientos de registros y la alternativa
    /// es una consulta por renglón.
    /// </summary>
    protected IDictionary<Guid, string> MapaDeUsuarios() =>
        Seguridad.Usuarios
            .PorOrganizacion(Contexto.IdOrganizacion)
            .ToDictionary(u => u.Id, u => u.NombreCompleto);

    protected static string? NombreDe(IDictionary<Guid, string> mapa, Guid? id) =>
        id is { } valor && mapa.TryGetValue(valor, out var nombre) ? nombre : null;

    protected static string? Recortado(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
