using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictIT.Api.Infraestructura;
using PredictIT.BLL;
using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;

namespace PredictIT.Api.Controllers;

/// <summary>
/// Reportes exportables a PDF (RF-14).
///
/// Devuelve el archivo y no una ruta: el reporte no se guarda en el servidor.
/// Un reporte guardado envejece, y alguien termina abriendo el de la semana
/// pasada creyendo que es el de hoy.
/// </summary>
[ApiController]
[Route("api/reportes")]
[Authorize]
public class ReportesController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [RequierePatente(Patentes.ReporteGenerar)]
    public ActionResult<IReadOnlyList<CatalogoItemDto>> Disponibles() =>
        Ok(negocio.Reportes.Disponibles());

    /// <summary>
    /// Genera un reporte. Los filtros van por query para que la URL del
    /// reporte sea compartible y reproducible: el mismo enlace da el mismo
    /// recorte.
    /// </summary>
    [HttpGet("{reporte}")]
    [RequierePatente(Patentes.ReporteGenerar)]
    public IActionResult Generar(
        string reporte,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] Guid? estado,
        [FromQuery] Guid? tecnico)
    {
        var pdf = negocio.Reportes.Generar(reporte, new FiltroReporteDto
        {
            Desde = desde,
            Hasta = hasta,
            IdEstado = estado == Guid.Empty ? null : estado,
            IdTecnico = tecnico == Guid.Empty ? null : tecnico
        });

        return File(pdf.Contenido, pdf.TipoContenido, pdf.NombreArchivo);
    }
}
