using System.Collections.Concurrent;

namespace PredictIT.Service.IA;

/// <summary>
/// Un disyuntor por organización.
///
/// El disyuntor guarda estado en memoria, así que tiene que vivir más que una
/// petición. Pero uno solo compartido sería un error de aislamiento: si el
/// proveedor falla para una organización —su clave venció, su cuenta se quedó
/// sin crédito— el circuito abriría para todas, y las demás pasarían a
/// asignación de respaldo sin motivo.
///
/// El alcance del ADR 0009 sigue valiendo: es estado de esta instancia de la
/// API. Con más de una instancia, cada una tiene su registro.
/// </summary>
public class RegistroDisyuntores(int fallosParaAbrir = 5, TimeSpan? esperaParaProbar = null)
{
    private readonly ConcurrentDictionary<Guid, Disyuntor> _porOrganizacion = new();

    public Disyuntor Para(Guid idOrganizacion) =>
        _porOrganizacion.GetOrAdd(
            idOrganizacion,
            _ => new Disyuntor(fallosParaAbrir, esperaParaProbar));

    /// <summary>Estado de todos los circuitos abiertos. Sirve para diagnóstico.</summary>
    public IReadOnlyDictionary<Guid, EstadoDisyuntor> Abiertos() =>
        _porOrganizacion
            .Where(par => par.Value.Estado != EstadoDisyuntor.Cerrado)
            .ToDictionary(par => par.Key, par => par.Value.Estado);
}
