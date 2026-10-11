using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// Datos de la organización y situación de su plan comercial.
///
/// El cálculo del plan está acá y no en el frontend a propósito: es el número
/// que sostiene la justificación económica del capítulo 6 del trabajo, y tiene
/// que dar lo mismo desde cualquier consumidor.
/// </summary>
public class OrganizacionBusiness : IOrganizacionBusiness
{
    /// <summary>Hasta dónde se busca el umbral de conveniencia del otro plan.</summary>
    private const int TopeBusquedaUmbral = 2000;

    private readonly IFactoryDao _dao;
    // El consumo de IA se cuenta sobre la bitacora, que vive en la base de
    // servicio: es el unico motivo por el que esta clase necesita las dos.
    private readonly IFactoryDaoSeguridad _daoSeguridad;
    private readonly IContextoSesion _contexto;

    public OrganizacionBusiness(IFactoryDao dao, IFactoryDaoSeguridad daoSeguridad,
                                IContextoSesion contexto)
    {
        _dao = dao;
        _daoSeguridad = daoSeguridad;
        _contexto = contexto;
    }

    public OrganizacionDto Actual()
    {
        var org = _dao.Organizaciones.GetById(_contexto.IdOrganizacion)
                  ?? throw new BusinessException("No se encontró la organización de la sesión.");
        return Mapear(org);
    }

    public SituacionPlanDto SituacionDelPlan()
    {
        var plan = _dao.Organizaciones.PlanDe(_contexto.IdOrganizacion)
                   ?? throw new BusinessException("La organización no tiene un plan asignado.");

        var equipos = _dao.Equipos.ContarPorOrganizacion();
        var adicionales = Math.Max(0, equipos - plan.EquiposIncluidos);
        var costoAdicionales = adicionales * plan.PrecioEquipoAdicional;

        return new SituacionPlanDto(
            MapearPlan(plan),
            equipos,
            plan.EquiposIncluidos,
            adicionales,
            plan.AbonoMensual,
            costoAdicionales,
            plan.AbonoMensual + costoAdicionales,
            Comparar(plan, equipos));
    }

    /// <summary>
    /// Consumo del servicio de IA en el mes corriente.
    ///
    /// El periodo es el mes calendario porque es el que factura el proveedor y
    /// el que la organización compara contra su abono. Arranca el día uno y
    /// termina el día uno del siguiente, sin incluirlo: contar «hasta hoy»
    /// haría que el mismo mes diera distinto según a qué hora se mire.
    /// </summary>
    public ConsumoIaDto ConsumoDeIa()
    {
        var hoy = DateTime.Now;
        var desde = new DateTime(hoy.Year, hoy.Month, 1);
        var hasta = desde.AddMonths(1);

        var codigos = new[]
        {
            TipoEvento.Codigos.ClasificacionIa,
            TipoEvento.Codigos.ClasificacionHeuristica,
            TipoEvento.Codigos.AsignacionIa,
            TipoEvento.Codigos.AsignacionHeuristica,
            TipoEvento.Codigos.AsignacionRespaldo,
            TipoEvento.Codigos.GuiaReparacionIa,
            TipoEvento.Codigos.GuiaReparacionHeuristica
        };

        var conteo = _daoSeguridad.Bitacora.ContarPorTipo(
            desde, hasta, _contexto.IdOrganizacion, codigos);

        int De(string codigo) => conteo.TryGetValue(codigo, out var n) ? n : 0;

        return new ConsumoIaDto(
            desde, hasta,
            De(TipoEvento.Codigos.ClasificacionIa),
            De(TipoEvento.Codigos.ClasificacionHeuristica),
            De(TipoEvento.Codigos.AsignacionIa),
            De(TipoEvento.Codigos.AsignacionHeuristica),
            De(TipoEvento.Codigos.AsignacionRespaldo),
            De(TipoEvento.Codigos.GuiaReparacionIa),
            De(TipoEvento.Codigos.GuiaReparacionHeuristica));
    }

    public ResumenNavegacionDto ResumenDeNavegacion()
    {
        return new ResumenNavegacionDto(
            _contexto.Tiene(Patentes.EquipoVer)
                ? _dao.Equipos.ContarPorOrganizacion()
                : null,
            IncidenciasAbiertas(),
            _contexto.Tiene(Patentes.MantenimientoVer)
                ? _dao.MantenimientosProgramados.PendientesDeLaOrganizacion()
                      .Count(p => p.FechaProgramada < DateOnly.FromDateTime(DateTime.Now))
                : null);
    }

    /// <summary>
    /// Las incidencias abiertas que le corresponde ver a quien pregunta.
    ///
    /// Se pide una sola fila y se usa el total: la búsqueda ya resuelve el
    /// recuento en la base, y traer las doscientas incidencias para contarlas
    /// en memoria sería leer el listado entero en cada pantalla.
    /// </summary>
    private int? IncidenciasAbiertas()
    {
        var filtro = new FiltroIncidencias { SoloAbiertas = true, PorPagina = 1 };

        if (_contexto.Tiene(Patentes.IncidenciaVerTodas))
        {
            // Nada más que filtrar: la organización ya la aplica el DAO.
        }
        else if (_contexto.Tiene(Patentes.IncidenciaVerPropias))
        {
            filtro.IdInvolucrado = _contexto.IdUsuario;
        }
        else
        {
            return null;
        }

        return _dao.Incidencias.Buscar(filtro).Total;
    }

    /// <summary>
    /// Compara el plan actual contra el más conveniente de los otros.
    ///
    /// Devuelve el resultado honesto: si el plan actual sigue siendo el más
    /// barato, lo dice y aclara desde cuántos equipos convendría cambiar. Un
    /// panel que empujara siempre al plan más caro sería trivial de escribir;
    /// esto es lo que sostiene el argumento de transparencia del capítulo 6.
    /// </summary>
    private PlanAlternativoDto? Comparar(PlanComercial actual, int equipos)
    {
        // El plan Partner no se compara: no es una alternativa para una PyME, es
        // el esquema del canal.
        var candidatos = PlanesComparables()
            .Where(p => p.Id != actual.Id && p.Codigo != "PARTNER")
            .ToList();

        if (candidatos.Count == 0) return null;

        var mejor = candidatos
            .OrderBy(p => p.CostoMensualPara(equipos))
            .First();

        var costoActual = actual.CostoMensualPara(equipos);
        var costoMejor = mejor.CostoMensualPara(equipos);

        return new PlanAlternativoDto(
            mejor.Nombre,
            costoMejor,
            costoMejor - costoActual,
            costoMejor < costoActual,
            UmbralDeConveniencia(actual, mejor, equipos));
    }

    /// <summary>
    /// Cantidad de equipos a partir de la cual el otro plan sale más barato.
    ///
    /// Se busca iterando y no resolviendo la ecuación porque el costo es una
    /// función por tramos: hasta los equipos incluidos es plano y después crece.
    /// Con el tope de búsqueda el costo es despreciable y el resultado es exacto.
    /// </summary>
    private static int UmbralDeConveniencia(PlanComercial actual, PlanComercial otro, int desde)
    {
        for (var n = Math.Max(1, desde); n <= TopeBusquedaUmbral; n++)
        {
            if (otro.CostoMensualPara(n) < actual.CostoMensualPara(n)) return n;
        }
        return 0;   // 0 = nunca conviene dentro del rango razonable
    }

    private IList<PlanComercial>? _planes;

    /// <summary>
    /// Los planes son un catálogo de tres filas: se traen todos una vez y se
    /// comparan en memoria, que es más claro que una consulta por candidato.
    /// </summary>
    private IList<PlanComercial> PlanesComparables() =>
        _planes ??= _dao.Organizaciones.TodosLosPlanes();

    private static OrganizacionDto Mapear(Organizacion o) => new(
        o.Id, o.RazonSocial, o.NombreCorto, o.Cuit,
        o.Plan is null ? null : MapearPlan(o.Plan));

    private static PlanDto MapearPlan(PlanComercial p) => new(
        p.Codigo, p.Nombre, p.AbonoMensual, p.EquiposIncluidos, p.PrecioEquipoAdicional, p.Soporte);
}
