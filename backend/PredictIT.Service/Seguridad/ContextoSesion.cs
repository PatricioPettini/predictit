using PredictIT.DAO.Contracts;

namespace PredictIT.Service.Seguridad;

/// <summary>
/// Contexto de la petición en curso: quién opera y sobre qué organización.
///
/// Se construye una vez al autenticar y se resuelve del token en cada petición.
/// El identificador de organización deliberadamente no viaja en el cuerpo ni en
/// la URL: si viajara, un controlador podría pedir datos de otra organización
/// (ADR 0005).
/// </summary>
public class ContextoSesion : IContextoSesion
{
    private readonly HashSet<string> _patentes;

    public ContextoSesion(Guid idUsuario, string username, Guid idOrganizacion,
                          IEnumerable<string> patentes, Guid? idIdioma = null)
    {
        IdUsuario = idUsuario;
        Username = username;
        IdOrganizacion = idOrganizacion;
        IdIdioma = idIdioma;
        _patentes = new HashSet<string>(patentes, StringComparer.OrdinalIgnoreCase);
    }

    public Guid IdUsuario { get; }
    public string Username { get; }
    public Guid IdOrganizacion { get; }
    public Guid? IdIdioma { get; }

    public IReadOnlyCollection<string> Patentes => _patentes;

    public bool Tiene(string dataKey) => _patentes.Contains(dataKey);

    /// <summary>
    /// Contexto sin usuario, para lo que corre antes de autenticar —el propio
    /// login— y para los procesos en segundo plano.
    /// </summary>
    public static ContextoSesion Anonimo() =>
        new(Guid.Empty, "(anónimo)", Guid.Empty, Array.Empty<string>());

    /// <summary>
    /// Contexto de un proceso automático sobre una organización: el barrido
    /// predictivo programado.
    ///
    /// Lleva sólo las patentes que ese proceso necesita y ninguna más. No es un
    /// contexto «de superusuario»: si mañana el barrido intentara hacer algo
    /// distinto, le faltaría el permiso y fallaría, que es lo que se busca.
    ///
    /// El usuario queda vacío a propósito. La bitácora tiene que decir que lo
    /// hizo el sistema y no atribuírselo a una persona que no estaba.
    /// </summary>
    public static ContextoSesion Sistema(Guid idOrganizacion) =>
        new(Guid.Empty, "(sistema)", idOrganizacion, new[] { "ALERTA_VER" });
}
