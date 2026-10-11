using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictIT.Api.Infraestructura;
using PredictIT.BLL;
using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;

namespace PredictIT.Api.Controllers;

/// <summary>Mantenimientos (RF-10 · CU-008).</summary>
[ApiController]
[Route("api/mantenimientos")]
[Authorize]
public class MantenimientosController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [RequierePatente(Patentes.MantenimientoVer)]
    public ActionResult<IReadOnlyList<MantenimientoListaDto>> Listar([FromQuery] Guid? equipo) =>
        Ok(negocio.Mantenimientos.Mantenimientos(equipo));

    [HttpGet("catalogos")]
    [RequierePatente(Patentes.MantenimientoVer)]
    public ActionResult<CatalogosMantenimientoDto> Catalogos() =>
        Ok(negocio.Mantenimientos.CatalogosMantenimiento());

    [HttpPost]
    [RequierePatente(Patentes.MantenimientoRegistrar)]
    public ActionResult<Guid> Registrar([FromBody] MantenimientoEntradaDto entrada) =>
        Ok(negocio.Mantenimientos.RegistrarMantenimiento(entrada));
}

/// <summary>
/// El plan de mantenimiento preventivo y su agenda (RF-16 · CU-019, CU-020).
///
/// Va en su propia ruta y no colgando de mantenimientos porque son recursos
/// distintos: uno es lo que se hizo y otro lo que está previsto.
/// </summary>
[ApiController]
[Route("api/mantenimientos/programados")]
[Authorize]
public class MantenimientosProgramadosController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [RequierePatente(Patentes.MantenimientoVer)]
    public ActionResult<IReadOnlyList<MantenimientoProgramadoDto>> Listar([FromQuery] string? estado) =>
        Ok(negocio.MantenimientosProgramados.Programados(estado));

    [HttpGet("panel")]
    [RequierePatente(Patentes.MantenimientoVer)]
    public ActionResult<PanelProgramadoDto> Panel() =>
        Ok(negocio.MantenimientosProgramados.Panel());

    [HttpPost]
    [RequierePatente(Patentes.MantenimientoPlanificar)]
    public ActionResult<Guid> Programar([FromBody] ProgramarEntradaDto entrada) =>
        Ok(negocio.MantenimientosProgramados.Programar(entrada));

    [HttpPut("{id:guid}")]
    [RequierePatente(Patentes.MantenimientoPlanificar)]
    public IActionResult Reprogramar(Guid id, [FromBody] ReprogramarEntradaDto entrada)
    {
        negocio.MantenimientosProgramados.Reprogramar(id, entrada);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequierePatente(Patentes.MantenimientoPlanificar)]
    public IActionResult Anular(Guid id, [FromQuery] string motivo = "")
    {
        negocio.MantenimientosProgramados.Anular(id, motivo);
        return NoContent();
    }

    /// <summary>Crea las fechas que faltan a partir de los planes activos.</summary>
    [HttpPost("generar")]
    [RequierePatente(Patentes.MantenimientoPlanificar)]
    public ActionResult<GeneracionDto> Generar() =>
        Ok(negocio.MantenimientosProgramados.GenerarDesdePlanes());
}

/// <summary>Los planes de mantenimiento preventivo (RF-16 · CU-019).</summary>
[ApiController]
[Route("api/mantenimientos/planes")]
[Authorize]
public class PlanesMantenimientoController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [RequierePatente(Patentes.MantenimientoVer)]
    public ActionResult<IReadOnlyList<PlanMantenimientoDto>> Listar() =>
        Ok(negocio.MantenimientosProgramados.Planes());

    [HttpPost]
    [RequierePatente(Patentes.MantenimientoPlanificar)]
    public ActionResult<Guid> Crear([FromBody] PlanMantenimientoEntradaDto entrada) =>
        Ok(negocio.MantenimientosProgramados.GuardarPlan(null, entrada));

    [HttpPut("{id:guid}")]
    [RequierePatente(Patentes.MantenimientoPlanificar)]
    public ActionResult<Guid> Actualizar(Guid id, [FromBody] PlanMantenimientoEntradaDto entrada) =>
        Ok(negocio.MantenimientosProgramados.GuardarPlan(id, entrada));
}

/// <summary>
/// La facturación del servicio (RF-17 · CU-021).
///
/// Cuelga de `api/facturacion` y no de `api/organizacion` porque son recursos
/// distintos: uno es con qué plan está la organización, y otro lo que se le
/// facturó mes a mes.
/// </summary>
[ApiController]
[Route("api/facturacion")]
[Authorize]
public class FacturacionController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [RequierePatente(Patentes.FacturacionVer)]
    public ActionResult<IReadOnlyList<ComprobanteDto>> Listar(
        [FromQuery] string? estado, [FromQuery] string? periodo) =>
        Ok(negocio.Facturacion.Comprobantes(estado, periodo));

    [HttpGet("panel")]
    [RequierePatente(Patentes.FacturacionVer)]
    public ActionResult<PanelFacturacionDto> Panel() => Ok(negocio.Facturacion.Panel());

    [HttpGet("{id:guid}")]
    [RequierePatente(Patentes.FacturacionVer)]
    public ActionResult<ComprobanteDto> Detalle(Guid id) => Ok(negocio.Facturacion.Comprobante(id));

    /// <summary>Arma el borrador del período. Sin período, el último mes cerrado.</summary>
    [HttpPost]
    [RequierePatente(Patentes.FacturacionGestionar)]
    public ActionResult<GeneracionComprobanteDto> Generar([FromQuery] string? periodo) =>
        Ok(negocio.Facturacion.Generar(periodo));

    [HttpPost("{id:guid}/ajustes")]
    [RequierePatente(Patentes.FacturacionGestionar)]
    public ActionResult<ComprobanteDto> Ajustar(Guid id, [FromBody] AjusteEntradaDto entrada) =>
        Ok(negocio.Facturacion.AgregarAjuste(id, entrada));

    [HttpPost("{id:guid}/emitir")]
    [RequierePatente(Patentes.FacturacionGestionar)]
    public ActionResult<ComprobanteDto> Emitir(Guid id) => Ok(negocio.Facturacion.Emitir(id));

    [HttpPost("{id:guid}/pago")]
    [RequierePatente(Patentes.FacturacionGestionar)]
    public ActionResult<ComprobanteDto> Pagar(Guid id, [FromQuery] DateOnly? fecha) =>
        Ok(negocio.Facturacion.RegistrarPago(id, fecha));

    [HttpDelete("{id:guid}")]
    [RequierePatente(Patentes.FacturacionGestionar)]
    public IActionResult Anular(Guid id, [FromQuery] string motivo = "")
    {
        negocio.Facturacion.Anular(id, motivo);
        return NoContent();
    }
}

/// <summary>Usuarios, roles y permisos (RF-02, RF-03 · CU-002).</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [RequierePatente(Patentes.UsuarioGestionar)]
    public ActionResult<IReadOnlyList<UsuarioListaDto>> Listar() =>
        Ok(negocio.Usuarios.Usuarios());

    [HttpGet("roles")]
    [RequierePatente(Patentes.UsuarioGestionar)]
    public ActionResult<IReadOnlyList<RolDto>> Roles() => Ok(negocio.Usuarios.Roles());

    /// <summary>El usuario con el detalle de qué patente le da cada rol.</summary>
    [HttpGet("{id:guid}")]
    [RequierePatente(Patentes.UsuarioGestionar)]
    public ActionResult<UsuarioDetalleDto> Detalle(Guid id) =>
        Ok(negocio.Usuarios.Usuario(id));

    [HttpPut("{id:guid}/estado")]
    [RequierePatente(Patentes.UsuarioGestionar)]
    public IActionResult CambiarEstado(Guid id, [FromQuery] bool activo)
    {
        negocio.Usuarios.CambiarEstadoUsuario(id, activo);
        return NoContent();
    }

    [HttpPut("{id:guid}/desbloquear")]
    [RequierePatente(Patentes.UsuarioGestionar)]
    public IActionResult Desbloquear(Guid id)
    {
        negocio.Usuarios.DesbloquearUsuario(id);
        return NoContent();
    }

    [HttpPut("{id:guid}/roles")]
    [RequierePatente(Patentes.RolGestionar)]
    public IActionResult AsignarRoles(Guid id, [FromBody] RolesDto cuerpo)
    {
        negocio.Usuarios.AsignarRoles(id, cuerpo.IdsRol);
        return NoContent();
    }

    public record RolesDto(IReadOnlyList<Guid> IdsRol);
}

/// <summary>
/// Bitácora (Req. Arq. 002).
///
/// Sólo hay lectura. No existe un endpoint para modificar ni borrar, y aunque
/// existiera la base lo rechazaría: la tabla tiene un disparador que impide
/// cualquier UPDATE o DELETE.
/// </summary>
[ApiController]
[Route("api/bitacora")]
[Authorize]
public class BitacoraController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [RequierePatente(Patentes.BitacoraVer)]
    public ActionResult<IReadOnlyList<EntradaBitacoraDto>> Consultar(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] string? tipo,
        [FromQuery] Guid? usuario) =>
        Ok(negocio.Auditoria.Bitacora(new FiltroBitacoraDto
        {
            Desde = desde,
            Hasta = hasta,
            CodigoTipoEvento = string.IsNullOrWhiteSpace(tipo) ? null : tipo,
            IdUsuario = usuario == Guid.Empty ? null : usuario
        }));

    [HttpGet("tipos")]
    [RequierePatente(Patentes.BitacoraVer)]
    public ActionResult<IReadOnlyList<CatalogoItemDto>> Tipos() =>
        Ok(negocio.Auditoria.TiposDeEvento());
}

/// <summary>
/// Historial técnico de la organización.
///
/// Tiene recurso propio y no vive bajo la bitácora: son dos registros
/// distintos. La bitácora audita quién hizo qué en el sistema; el historial
/// cuenta qué le viene pasando al parque —incidencias y mantenimientos de todos
/// los equipos en una sola línea de tiempo—.
/// </summary>
[ApiController]
[Route("api/historial")]
[Authorize]
public class HistorialController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [RequierePatente(Patentes.HistorialVerOrganizacion)]
    public ActionResult<IReadOnlyList<HechoHistorialDto>> Consultar(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta) =>
        Ok(negocio.Mantenimientos.Historial(desde, hasta));
}

/// <summary>
/// Errores del sistema (CU.Arq.007).
///
/// Sale de la misma bitácora que la pantalla de auditoría, filtrada por
/// criticidad y devolviendo la traza. Está en su propio recurso y con su propia
/// patente porque la traza no es información para cualquiera que pueda auditar.
/// </summary>
[ApiController]
[Route("api/configuracion/errores")]
[Authorize]
public class ErroresController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [RequierePatente(Patentes.ErrorVer)]
    public ActionResult<IReadOnlyList<ErrorDto>> Consultar(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] bool advertencias = false) =>
        Ok(negocio.Auditoria.Errores(new FiltroErroresDto
        {
            Desde = desde,
            Hasta = hasta,
            IncluirAdvertencias = advertencias
        }));
}

/// <summary>Configuración de la asignación automática (pantalla 10.5.1.7.7).</summary>
[ApiController]
[Route("api/configuracion/ia")]
[Authorize]
public class ConfiguracionIaController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [RequierePatente(Patentes.IaConfigurar)]
    public ActionResult<PanelIaDto> Panel() => Ok(negocio.IntegracionIa.PanelIa());

    [HttpPut]
    [RequierePatente(Patentes.IaConfigurar)]
    public IActionResult Guardar([FromBody] ConfiguracionIaDto entrada)
    {
        negocio.IntegracionIa.GuardarConfiguracionIa(entrada);
        return NoContent();
    }

    /// <summary>
    /// Carga la clave de API, que se guarda cifrada (CU.Arq.006).
    ///
    /// No hay GET que la devuelva, y eso es deliberado: una credencial que puede
    /// salir del servidor es una credencial que se puede filtrar por cualquiera
    /// de los caminos por los que sale.
    /// </summary>
    [HttpPut("clave")]
    [RequierePatente(Patentes.IaConfigurar)]
    public IActionResult GuardarClave([FromBody] ClaveIaDto cuerpo)
    {
        negocio.IntegracionIa.GuardarClaveIa(cuerpo.Clave);
        return NoContent();
    }

    [HttpDelete("clave")]
    [RequierePatente(Patentes.IaConfigurar)]
    public IActionResult BorrarClave()
    {
        negocio.IntegracionIa.BorrarClaveIa();
        return NoContent();
    }

    public record ClaveIaDto(string Clave);
}

/// <summary>
/// Respaldos (Req. Arq. 003).
///
/// La restauración no está expuesta como endpoint a propósito: es una operación
/// destructiva que corta todas las sesiones abiertas, y hacerla desde un botón
/// de una pantalla web es pedir un accidente. El servicio la implementa y el
/// procedimiento está en el manual de administración.
/// </summary>
[ApiController]
[Route("api/respaldos")]
[Authorize]
public class RespaldosController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [RequierePatente(Patentes.BackupGestionar)]
    public ActionResult<PanelRespaldosDto> Panel() => Ok(negocio.Respaldos.PanelRespaldos());

    [HttpPost]
    [RequierePatente(Patentes.BackupGestionar)]
    public ActionResult<RespaldoDto> Respaldar([FromQuery] string basedatos) =>
        Ok(negocio.Respaldos.Respaldar(basedatos));

    /// <summary>
    /// Restaura un respaldo. Es la operación más destructiva del sistema:
    /// reemplaza la base entera por el contenido del archivo.
    ///
    /// Por eso pide el nombre de la base en el cuerpo además del identificador
    /// del respaldo: un clic accidental no alcanza, hay que escribir sobre qué
    /// se está operando. Un respaldo que nunca se probó restaurando es una
    /// copia, no un respaldo.
    /// </summary>
    [HttpPost("{id:guid}/restaurar")]
    [RequierePatente(Patentes.BackupGestionar)]
    public ActionResult<RespaldoDto> Restaurar(Guid id, [FromBody] ConfirmacionRestauracionDto cuerpo) =>
        Ok(negocio.Respaldos.Restaurar(id, cuerpo.ConfirmoLaBase));

    public record ConfirmacionRestauracionDto(string ConfirmoLaBase);
}
