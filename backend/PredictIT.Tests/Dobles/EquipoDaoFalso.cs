using PredictIT.DAO.Contracts;
using PredictIT.Domain.Negocio;

namespace PredictIT.Tests.Dobles;

/// <summary>
/// Un DAO de equipos en memoria, para probar las reglas del negocio sin base.
///
/// Lo que importa de estas pruebas es el <b>criterio</b>: qué rechaza el alta,
/// qué cambio de estado no se permite y qué queda asentado. Nada de eso
/// necesita SQL Server, y probarlo contra la base lo vuelve lento y frágil por
/// razones que no tienen que ver con la regla.
///
/// Los métodos que la prueba no debería llegar a usar tiran en lugar de
/// devolver un valor por defecto: un doble que contesta cualquier cosa hace
/// pasar pruebas que no probaron nada.
/// </summary>
public class EquipoDaoFalso : IEquipoDao
{
    public List<Equipo> Equipos { get; } = [];

    /// <summary>Los cambios de estado que se pidieron, en orden.</summary>
    public List<(Guid IdEquipo, Guid IdEstado)> CambiosDeEstado { get; } = [];

    public List<Guid> Borrados { get; } = [];

    public Guid Insert(Equipo entidad)
    {
        entidad.Id = entidad.Id == Guid.Empty ? Guid.NewGuid() : entidad.Id;
        Equipos.Add(entidad);
        return entidad.Id;
    }

    public void Update(Equipo entidad)
    {
        var i = Equipos.FindIndex(e => e.Id == entidad.Id);
        if (i >= 0) Equipos[i] = entidad;
    }

    public void Delete(Guid id) => Borrados.Add(id);

    public Equipo? GetById(Guid id) => Equipos.FirstOrDefault(e => e.Id == id);

    public IList<Equipo> GetAll() => Equipos;

    public bool ExisteCodigo(string codigo, Guid? excepto = null) =>
        Equipos.Any(e => e.Codigo == codigo && e.Id != excepto);

    public bool ExisteNumeroSerie(string numeroSerie, Guid? excepto = null) =>
        Equipos.Any(e => e.NumeroSerie == numeroSerie && e.Id != excepto);

    public void CambiarEstado(Guid idEquipo, Guid idEstado)
    {
        CambiosDeEstado.Add((idEquipo, idEstado));
        var equipo = GetById(idEquipo);
        if (equipo is not null) equipo.IdEstadoEquipo = idEstado;
    }

    public int ContarPorOrganizacion() => Equipos.Count;

    public PaginaDe<Equipo> Buscar(FiltroEquipos filtro) => throw Sin(nameof(Buscar));

    public RecuentoSegmentos ContarSegmentos(FiltroEquipos filtro) =>
        throw Sin(nameof(ContarSegmentos));

    private static NotSupportedException Sin(string miembro) =>
        new($"EquipoDaoFalso.{miembro} no está preparado: la prueba no debería llegar acá.");
}
