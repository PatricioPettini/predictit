using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictIT.Api.Infraestructura;
using PredictIT.BLL;
using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;

namespace PredictIT.Api.Controllers;

/// <summary>
/// Incidencias (RF-07 a RF-09 · CU-006, CU-007, CU-013).
///
/// El controlador sólo traduce HTTP: no decide nada. Quién puede ver qué lo
/// resuelve la BLL, porque la regla —«el responsable técnico ve las suyas»— es
/// de negocio y tiene que valer para cualquier punto de entrada.
/// </summary>
[ApiController]
[Route("api/incidencias")]
[Authorize]
public class IncidenciasController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [RequierePatente(Patentes.IncidenciaVerTodas, Patentes.IncidenciaVerPropias)]
    public ActionResult<PaginaDto<IncidenciaListaDto>> Buscar(
        [FromQuery] string? texto,
        [FromQuery] Guid? equipo,
        [FromQuery] Guid? estado,
        [FromQuery] Guid? prioridad,
        [FromQuery] Guid? categoria,
        [FromQuery] Guid? tecnico,
        [FromQuery] bool abiertas = false,
        [FromQuery] bool revision = false,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 20)
    {
        var filtro = new FiltroIncidencias
        {
            Texto = texto,
            IdEquipo = equipo,
            IdEstado = estado,
            IdPrioridad = prioridad,
            IdCategoria = categoria,
            IdTecnico = tecnico,
            SoloAbiertas = abiertas,
            SoloPendientesDeRevision = revision,
            Desde = desde,
            Hasta = hasta,
            Pagina = pagina,
            PorPagina = porPagina
        };

        return Ok(negocio.Incidencias.Buscar(filtro));
    }

    [HttpGet("catalogos")]
    [RequierePatente(Patentes.IncidenciaVerTodas, Patentes.IncidenciaVerPropias)]
    public ActionResult<CatalogosIncidenciaDto> Catalogos() => Ok(negocio.Incidencias.Catalogos());

    [HttpGet("{id:guid}")]
    [RequierePatente(Patentes.IncidenciaVerTodas, Patentes.IncidenciaVerPropias)]
    public ActionResult<IncidenciaDetalleDto> Detalle(Guid id) =>
        Ok(negocio.Incidencias.ObtenerDetalle(id));

    /// <summary>
    /// Guía de reparación sugerida para la incidencia (RF-15, CU-018).
    ///
    /// Devuelve 200 aunque el proveedor no haya respondido: en ese caso la guía
    /// viene marcada como no disponible con el motivo. No es un error de esta
    /// operación que el servicio externo esté caído, y devolver 502 obligaría a
    /// la pantalla a distinguir entre «falló la red» y «falló la IA».
    /// </summary>
    [HttpGet("{id:guid}/guia-reparacion")]
    [RequierePatente(Patentes.IncidenciaVerTodas, Patentes.IncidenciaVerPropias)]
    public ActionResult<GuiaReparacionDto> GuiaReparacion(Guid id) =>
        Ok(negocio.Incidencias.SugerirReparacion(id));

    /// <summary>
    /// Registra la incidencia y dispara triage y asignación (CU-006, CU-007).
    ///
    /// Devuelve 201 con la asignación resuelta, o con la advertencia si hubo que
    /// aplicar el respaldo. Que la asignación automática no funcione no es un
    /// error de esta operación: la incidencia se registró igual.
    /// </summary>
    [HttpPost]
    [RequierePatente(Patentes.IncidenciaRegistrar)]
    public ActionResult<IncidenciaRegistradaDto> Registrar([FromBody] IncidenciaEntradaDto entrada)
    {
        var resultado = negocio.Incidencias.Registrar(entrada);
        return CreatedAtAction(nameof(Detalle), new { id = resultado.Id }, resultado);
    }

    /// <summary>
    /// Qué transiciones de estado admite la incidencia ahora.
    ///
    /// La pantalla la consulta para ofrecer sólo lo posible. La máquina de
    /// estados está en el negocio: acá no se decide nada.
    /// </summary>
    [HttpGet("{id:guid}/transiciones")]
    [RequierePatente(Patentes.IncidenciaVerTodas, Patentes.IncidenciaVerPropias)]
    public ActionResult<IReadOnlyList<TransicionDto>> Transiciones(Guid id) =>
        Ok(negocio.Incidencias.TransicionesDe(id));

    /// <summary>
    /// Avanza la atención: mueve el estado y guarda diagnóstico y solución
    /// (CU de atención de incidencias).
    /// </summary>
    [HttpPut("{id:guid}/atencion")]
    [RequierePatente(Patentes.IncidenciaAtender)]
    public IActionResult Atender(Guid id, [FromBody] AtencionDto entrada)
    {
        negocio.Incidencias.Atender(id, entrada);
        return NoContent();
    }

    /// <summary>
    /// Reasignación manual de la incidencia a otro técnico (CU-013).
    ///
    /// El Responsable Técnico sólo puede reasignar las que tiene asignadas; el
    /// Administrador, cualquiera. Las dos patentes se exigen juntas porque son
    /// alternativas, y cuál de las dos alcanza lo decide el negocio.
    /// </summary>
    [HttpPut("{id:guid}/tecnico")]
    [RequierePatente(Patentes.IncidenciaReasignarTodas, Patentes.IncidenciaReasignarPropias)]
    public IActionResult Reasignar(Guid id, [FromBody] ReasignacionDto cuerpo)
    {
        negocio.Incidencias.Reasignar(id, cuerpo.IdTecnico);
        return NoContent();
    }

    public record ReasignacionDto(Guid IdTecnico);
}

/// <summary>
/// Análisis predictivo y estado del proveedor de IA
/// (RF-11, RF-12 · CU-010, CU-011).
/// </summary>
[ApiController]
[Route("api/prediccion")]
[Authorize]
public class PrediccionController(IFactoryBusiness negocio) : ControllerBase
{
    /// <summary>
    /// Distribución, ranking y alertas, sin reevaluar. Abrir la pantalla no
    /// tiene por qué recalcular el parque entero.
    /// </summary>
    [HttpGet("panel")]
    [RequierePatente(Patentes.AlertaVer)]
    public ActionResult<PanelPredictivoDto> Panel() => Ok(negocio.Prediccion.Panel());

    [HttpGet("alertas")]
    [RequierePatente(Patentes.AlertaVer)]
    public ActionResult<IReadOnlyList<AlertaDto>> Alertas() =>
        Ok(negocio.Prediccion.AlertasActivas());

    [HttpPut("alertas/{id:guid}")]
    [RequierePatente(Patentes.AlertaVer)]
    public IActionResult Atender(Guid id, [FromQuery] bool descartar = false)
    {
        negocio.Prediccion.AtenderAlerta(id, descartar);
        return NoContent();
    }

    /// <summary>Riesgo de un equipo, con el aporte de cada regla al score.</summary>
    [HttpGet("equipos/{id:guid}")]
    [RequierePatente(Patentes.AlertaVer)]
    public ActionResult<RiesgoEquipoDto> Riesgo(Guid id) =>
        Ok(negocio.Prediccion.EvaluarEquipo(id));

    /// <summary>
    /// Evalúa el parque completo (CU-010).
    ///
    /// Es POST y no GET porque escribe: guarda una evaluación por equipo y
    /// genera las alertas nuevas.
    /// </summary>
    [HttpPost("evaluar")]
    [RequierePatente(Patentes.AlertaVer)]
    public ActionResult<ResultadoEvaluacionDto> Evaluar() =>
        Ok(negocio.Prediccion.EvaluarParque());

    [HttpGet("reglas")]
    [RequierePatente(Patentes.AlertaVer)]
    public ActionResult<IReadOnlyList<ReglaDto>> Reglas() => Ok(negocio.Prediccion.Reglas());

    [HttpPut("reglas")]
    [RequierePatente(Patentes.ReglaGestionar)]
    public ActionResult<Guid> GuardarRegla([FromBody] ReglaDto regla) =>
        Ok(negocio.Prediccion.GuardarRegla(regla));

    /// <summary>Indicadores del Dashboard (RF-13).</summary>
    [HttpGet("dashboard")]
    [RequierePatente(Patentes.DashboardVer)]
    public ActionResult<DashboardDto> Dashboard() => Ok(negocio.Prediccion.Dashboard());

    /// <summary>Estado del proveedor y del disyuntor (ADR 0009).</summary>
    [HttpGet("ia")]
    [RequierePatente(Patentes.AlertaVer)]
    public ActionResult<EstadoIaDto> EstadoIa() => Ok(negocio.Prediccion.EstadoDeLaIa());
    /// <summary>
    /// Rendimiento de cada regla del motor (H-48).
    ///
    /// Sirve para responder «¿este umbral está bien puesto?» con el histórico
    /// en lugar de a ojo.
    /// </summary>
    [HttpGet("calibracion")]
    public ActionResult<IReadOnlyList<CalibracionReglaDto>> Calibracion() =>
        Ok(negocio.Prediccion.Calibracion());

}
