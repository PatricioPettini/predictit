namespace PredictIT.Domain.Seguridad;

/// <summary>
/// Agrupación de permisos. Es el compuesto: puede contener patentes y otras
/// familias, lo que permite armar perfiles sin repetir permiso por permiso.
///
/// El caso que ejercita el anidamiento en este sistema es OPERACION_TECNICA,
/// que contiene a CONSULTA_TECNICA: quien atiende incidencias necesita todo lo
/// que necesita quien sólo consulta, más lo propio.
/// </summary>
public class Familia : ComponenteSeguridad
{
    private readonly List<ComponenteSeguridad> _hijos = new();

    public IReadOnlyList<ComponenteSeguridad> Hijos => _hijos;

    public void Agregar(ComponenteSeguridad hijo)
    {
        if (hijo is null) throw new ArgumentNullException(nameof(hijo));
        if (hijo.Id == Id) throw new InvalidOperationException(
            $"La familia '{Nombre}' no puede contenerse a sí misma.");
        _hijos.Add(hijo);
    }

    public void Quitar(Guid idHijo) => _hijos.RemoveAll(h => h.Id == idHijo);

    internal override void Recolectar(IDictionary<Guid, Patente> acumulado, ISet<Guid> visitados)
    {
        if (!visitados.Add(Id))
        {
            // Ya se recorrió esta familia en esta misma resolución: hay un ciclo
            // o un diamante. Cortar acá es correcto en los dos casos.
            return;
        }

        foreach (var hijo in _hijos)
        {
            hijo.Recolectar(acumulado, visitados);
        }
    }

    public override string ToString() => $"{Nombre} ({_hijos.Count} hijos)";
}
