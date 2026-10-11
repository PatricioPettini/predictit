using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictIT.Api.Infraestructura;
using PredictIT.BLL;
using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;

namespace PredictIT.Api.Controllers;

/// <summary>
/// Activos informáticos (RF-04, RF-05, RF-06 · CU-003, CU-004, CU-005).
///
/// Ningún endpoint recibe el identificador de organización: se resuelve del
/// token. Es la razón por la que un usuario no puede pedir equipos de otra
/// organización ni cambiando la petición a mano (ADR 0005).
/// </summary>
[ApiController]
[Route("api/equipos")]
[Authorize]
public class EquiposController : ControllerBase
{
    private readonly IFactoryBusiness _negocio;

    public EquiposController(IFactoryBusiness negocio) => _negocio = negocio;

    [HttpGet]
    [RequierePatente(Patentes.EquipoVer)]
    public ActionResult<PaginaDto<EquipoListaDto>> Buscar(
        [FromQuery] string? texto,
        [FromQuery] Guid? tipo,
        [FromQuery] Guid? estado,
        [FromQuery] Guid? ubicacion,
        [FromQuery] Guid? responsable,
        [FromQuery] string? segmento,
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 20)
    {
        // Un segmento que no existe se ignora en vez de rechazarse: es un
        // recorte del listado y no un dato del usuario, y un 400 por un enlace
        // viejo deja la pantalla sin inventario para decir «riesgoAlt».
        var filtro = ArmarFiltro(texto, tipo, estado, ubicacion, responsable, segmento,
                                 pagina, porPagina);

        return Ok(_negocio.Equipos.Buscar(filtro));
    }

    /// <summary>
    /// Cuantos equipos hay en cada segmento del inventario (RF-05).
    ///
    /// Va aparte del listado y no adentro: el listado se pide de nuevo al pasar
    /// de pagina, y los recuentos no cambian por cambiar de pagina.
    /// </summary>
    [HttpGet("segmentos")]
    [RequierePatente(Patentes.EquipoVer)]
    public ActionResult<SegmentosDto> RecuentoDeSegmentos(
        [FromQuery] string? texto,
        [FromQuery] Guid? tipo,
        [FromQuery] Guid? estado,
        [FromQuery] Guid? ubicacion,
        [FromQuery] Guid? responsable) =>
        Ok(_negocio.Equipos.Segmentos(
            ArmarFiltro(texto, tipo, estado, ubicacion, responsable, null, 1, 1)));

    private static FiltroEquipos ArmarFiltro(
        string? texto, Guid? tipo, Guid? estado, Guid? ubicacion, Guid? responsable,
        string? segmento, int pagina, int porPagina) =>
        new()
        {
            Texto = texto,
            IdTipoEquipo = tipo,
            IdEstadoEquipo = estado,
            IdUbicacion = ubicacion,
            IdResponsable = responsable,
            Segmento = Segmentos.Existe(segmento) ? segmento : null,
            Pagina = pagina,
            PorPagina = porPagina
        };

    /// <summary>
    /// Los equipos de los que el usuario es responsable. Es la pantalla del
    /// módulo del cliente: no requiere la patente de inventario.
    /// </summary>
    [HttpGet("mios")]
    [RequierePatente(Patentes.HistorialVerPropio, Patentes.EquipoVer)]
    public ActionResult<PaginaDto<EquipoListaDto>> Mios() => Ok(_negocio.Equipos.MisEquipos());

    [HttpGet("catalogos")]
    [RequierePatente(Patentes.EquipoVer)]
    public ActionResult<CatalogosEquipoDto> Catalogos() => Ok(_negocio.Equipos.Catalogos());

    [HttpGet("{id:guid}")]
    [RequierePatente(Patentes.EquipoVer)]
    public ActionResult<EquipoDetalleDto> Detalle(Guid id) =>
        Ok(_negocio.Equipos.ObtenerDetalle(id));

    [HttpPost]
    [RequierePatente(Patentes.EquipoGestionar)]
    public ActionResult<object> Registrar([FromBody] EquipoEntradaDto entrada)
    {
        var id = _negocio.Equipos.Registrar(entrada);
        return CreatedAtAction(nameof(Detalle), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequierePatente(Patentes.EquipoGestionar)]
    public IActionResult Actualizar(Guid id, [FromBody] EquipoEntradaDto entrada)
    {
        _negocio.Equipos.Actualizar(id, entrada);
        return NoContent();
    }

    /// <summary>
    /// Baja del equipo. Es lógica: pasa al estado "Dado de baja" y conserva su
    /// historial técnico, que es lo que alimenta al motor predictivo.
    /// </summary>
    /// <summary>
    /// Cambia el estado operativo del equipo.
    ///
    /// Tiene patente propia y no la de gestionar: marcar un equipo «en
    /// reparación» es una operación del día a día del técnico, y editar su
    /// ficha no.
    /// </summary>
    [HttpPut("{id:guid}/estado")]
    [RequierePatente(Patentes.EquipoCambiarEstado)]
    public IActionResult CambiarEstado(Guid id, [FromBody] CambioEstadoEquipoDto entrada)
    {
        _negocio.Equipos.CambiarEstado(id, entrada);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequierePatente(Patentes.EquipoGestionar)]
    public IActionResult DarDeBaja(Guid id)
    {
        _negocio.Equipos.DarDeBaja(id);
        return NoContent();
    }
}

/// <summary>Datos de la organización y situación de su plan comercial.</summary>
[ApiController]
[Route("api/organizacion")]
[Authorize]
public class OrganizacionController : ControllerBase
{
    private readonly IFactoryBusiness _negocio;

    public OrganizacionController(IFactoryBusiness negocio) => _negocio = negocio;

    [HttpGet]
    public ActionResult<OrganizacionDto> Actual() => Ok(_negocio.Organizaciones.Actual());

    [HttpGet("plan")]
    [RequierePatente(Patentes.OrganizacionGestionar)]
    public ActionResult<SituacionPlanDto> Plan() => Ok(_negocio.Organizaciones.SituacionDelPlan());

    /// <summary>
    /// Cuanto se uso el servicio de IA en el mes corriente.
    ///
    /// Sale de la bitacora, que ya asienta cada consulta: un contador propio
    /// seria un segundo numero que puede contradecir al registro de auditoria.
    /// </summary>
    [HttpGet("consumo-ia")]
    [RequierePatente(Patentes.OrganizacionGestionar)]
    public ActionResult<ConsumoIaDto> ConsumoIa() => Ok(_negocio.Organizaciones.ConsumoDeIa());

    /// <summary>
    /// Los contadores que la navegacion muestra al lado de cada seccion.
    ///
    /// No lleva patente: cada contador viene en nulo si el usuario no puede
    /// verlo, y quien decide eso es la BLL. Exigir una patente aca dejaria
    /// afuera al solicitante, que tiene su propio contador de pedidos.
    /// </summary>
    [HttpGet("resumen")]
    public ActionResult<ResumenNavegacionDto> Resumen() =>
        Ok(_negocio.Organizaciones.ResumenDeNavegacion());
}
