namespace PredictIT.Domain.Seguridad;

/// <summary>
/// Usuario del sistema. Vive en la base de servicio.
///
/// <see cref="IdOrganizacion"/> es una referencia lógica a la base de negocio,
/// sin clave foránea: son bases distintas a propósito. Queda en nulo para el
/// perfil Partner, que trabaja sobre varias organizaciones
/// (<see cref="OrganizacionesHabilitadas"/>).
/// </summary>
public class Usuario
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;

    /// <summary>Hash en formato <c>PBKDF2$iteraciones$salt$hash</c>. Nunca texto plano.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string? Telefono { get; set; }
    public bool Activo { get; set; } = true;
    public bool Bloqueado { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime FechaAlta { get; set; }
    public DateTime? UltimoAcceso { get; set; }

    public Guid? IdOrganizacion { get; set; }
    public Guid? IdIdioma { get; set; }

    public IList<Rol> Roles { get; } = new List<Rol>();
    public IList<Guid> OrganizacionesHabilitadas { get; } = new List<Guid>();

    public string NombreCompleto => $"{Nombre} {Apellido}".Trim();

    /// <summary>Todos los permisos del usuario, sumando los de cada rol.</summary>
    public IReadOnlyCollection<Patente> ObtenerPatentes()
    {
        var acumulado = new Dictionary<Guid, Patente>();
        foreach (var rol in Roles)
        {
            foreach (var patente in rol.ObtenerPatentes())
            {
                acumulado[patente.Id] = patente;
            }
        }
        return acumulado.Values.ToList();
    }

    public bool Tiene(string dataKey) =>
        ObtenerPatentes().Any(p => string.Equals(p.DataKey, dataKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Indica si el usuario puede operar sobre esa organización. El Partner puede
    /// sobre las que tiene habilitadas; el resto, sólo sobre la propia.
    /// </summary>
    public bool PuedeOperarSobre(Guid idOrganizacion) =>
        IdOrganizacion == idOrganizacion || OrganizacionesHabilitadas.Contains(idOrganizacion);

    public override string ToString() => Username;
}
