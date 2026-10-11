namespace PredictIT.Domain.Seguridad;

/// <summary>
/// Raíz del árbol de permisos (patrón Composite).
///
/// Una <see cref="Patente"/> es una hoja: representa un permiso atómico.
/// Una <see cref="Familia"/> es un compuesto: agrupa patentes y otras familias.
/// Quien consume el árbol pregunta lo mismo a los dos —"¿qué permisos te
/// cuelgan?"— sin saber con cuál está hablando.
/// </summary>
public abstract class ComponenteSeguridad
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>
    /// Permisos atómicos que este componente concede, resolviendo el anidamiento.
    /// </summary>
    public IReadOnlyCollection<Patente> ObtenerPatentes()
    {
        var acumulado = new Dictionary<Guid, Patente>();
        // El conjunto de visitados corta los ciclos: nada impide a nivel de base
        // de datos armar una familia A que contenga a B y B que contenga a A, y
        // sin esta guarda la recursión no termina.
        Recolectar(acumulado, new HashSet<Guid>());
        return acumulado.Values.ToList();
    }

    internal abstract void Recolectar(IDictionary<Guid, Patente> acumulado, ISet<Guid> visitados);

    /// <summary>Indica si el componente concede la patente identificada por su clave.</summary>
    public bool Concede(string dataKey) =>
        ObtenerPatentes().Any(p => string.Equals(p.DataKey, dataKey, StringComparison.OrdinalIgnoreCase));
}
