using Microsoft.AspNetCore.RateLimiting;
using PredictIT.Api.Infraestructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictIT.BLL;
using PredictIT.BLL.DTO;

namespace PredictIT.Api.Controllers;

public record LoginRequest(string Username, string Contrasena);

public record CambioOrganizacionRequest(Guid IdOrganizacion);

/// <summary>
/// Acceso al sistema (CU-001).
///
/// El controlador no valida credenciales ni arma tokens: sólo traduce HTTP. La
/// regla vive en la BLL y el servicio de seguridad.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AutenticacionController : ControllerBase
{
    private readonly IFactoryBusiness _negocio;

    public AutenticacionController(IFactoryBusiness negocio) => _negocio = negocio;

    // El limite por origen se aplica solo aca. Es el unico endpoint que se
    // puede llamar sin token, y por eso el unico donde alguien puede probar.
    [EnableRateLimiting(Politicas.IntentosDeLogin)]
    [HttpPost("login")]
    [AllowAnonymous]
    public ActionResult<SesionDto> Login([FromBody] LoginRequest pedido)
    {
        var sesion = _negocio.Autenticacion.IniciarSesion(
            pedido.Username, pedido.Contrasena, IpDelCliente());

        return Ok(sesion);
    }

    /// <summary>Devuelve la sesión del token actual, para rehidratar el frontend al recargar.</summary>
    [HttpGet("sesion")]
    [Authorize]
    public ActionResult<SesionDto> Sesion() => Ok(_negocio.Autenticacion.SesionActual());

    /// <summary>
    /// Cierra la sesión.
    ///
    /// Con JWT el servidor no guarda sesión, así que técnicamente alcanzaría
    /// con que el navegador descarte el token. Este endpoint existe por la
    /// bitácora: sin él el registro de auditoría tiene todas las entradas y
    /// ninguna salida, y no se puede reconstruir cuánto duró una sesión.
    ///
    /// No invalida el token —no hay lista de revocación—, y eso está dicho acá
    /// a propósito para que nadie suponga lo contrario.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        _negocio.Autenticacion.CerrarSesion(IpDelCliente());
        return NoContent();
    }

    /// <summary>Cambio de organización activa. Sólo el perfil Partner.</summary>
    [HttpPost("organizacion")]
    [Authorize]
    public ActionResult<SesionDto> CambiarOrganizacion([FromBody] CambioOrganizacionRequest pedido) =>
        Ok(_negocio.Autenticacion.CambiarOrganizacion(pedido.IdOrganizacion));

    /// <summary>
    /// Dirección del cliente para la bitácora.
    ///
    /// Se prefiere X-Forwarded-For porque en el despliegue previsto hay un proxy
    /// inverso delante y sin eso todos los accesos quedarían registrados con la
    /// dirección del proxy.
    /// </summary>
    private string? IpDelCliente()
    {
        var adelantada = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(adelantada))
        {
            return adelantada.Split(',')[0].Trim();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
