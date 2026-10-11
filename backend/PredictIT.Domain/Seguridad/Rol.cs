namespace PredictIT.Domain.Seguridad;

/// <summary>
/// Perfil que se le asigna al usuario. Agrupa familias y, cuando hace falta,
/// patentes sueltas.
///
/// La patente suelta no es un atajo: el Partner necesita ORGANIZACION_CAMBIAR y
/// ningún otro perfil, así que armarle una familia de un solo permiso sería
/// ruido. Por eso el rol puede colgar de las dos cosas.
/// </summary>
public class Rol
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Los roles del documento no se pueden borrar desde la interfaz.</summary>
    public bool EsSistema { get; set; }

    public IList<Familia> Familias { get; } = new List<Familia>();
    public IList<Patente> PatentesDirectas { get; } = new List<Patente>();

    /// <summary>Todos los permisos que concede el rol, con el anidamiento resuelto.</summary>
    public IReadOnlyCollection<Patente> ObtenerPatentes()
    {
        var acumulado = new Dictionary<Guid, Patente>();
        foreach (var familia in Familias)
        {
            foreach (var patente in familia.ObtenerPatentes())
            {
                acumulado[patente.Id] = patente;
            }
        }
        foreach (var patente in PatentesDirectas)
        {
            acumulado[patente.Id] = patente;
        }
        return acumulado.Values.ToList();
    }

    public override string ToString() => Nombre;
}
