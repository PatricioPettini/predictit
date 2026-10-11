using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictIT.Api.Infraestructura;
using PredictIT.BLL;
using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;

namespace PredictIT.Api.Controllers;

/// <summary>
/// Idiomas y traducciones (Req. Arq. 001 · CU.Arq.004 Traducir).
///
/// El listado y el diccionario son **anónimos**, y es la única excepción a
/// `[Authorize]` en todo el sistema además del propio login. El motivo es
/// concreto: la pantalla de inicio de sesión se traduce, y para traducirla hay
/// que tener el diccionario antes de que exista una sesión.
///
/// No expone nada sensible: son los textos de la interfaz, los mismos que
/// cualquiera ve al abrir la aplicación.
/// </summary>
[ApiController]
[Route("api/idiomas")]
public class IdiomasController(IFactoryBusiness negocio) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<IReadOnlyList<IdiomaDto>> Listar() => Ok(negocio.Idiomas.Idiomas());

    /// <summary>
    /// El diccionario. Sin `codigo` devuelve el del usuario de la sesión, y si
    /// no hay sesión el idioma por defecto.
    /// </summary>
    [HttpGet("traducciones")]
    [AllowAnonymous]
    public ActionResult<DiccionarioDto> Traducciones([FromQuery] string? codigo) =>
        Ok(negocio.Idiomas.Diccionario(codigo));

    /// <summary>Guarda la preferencia del usuario de la sesión.</summary>
    [HttpPut("preferencia")]
    [Authorize]
    public IActionResult Preferencia([FromBody] PreferenciaDto cuerpo)
    {
        negocio.Idiomas.CambiarIdioma(cuerpo.Codigo);
        return NoContent();
    }

    /// <summary>
    /// Cobertura de traducción por idioma. Es el control que hace visible una
    /// clave sin traducir, que de otro modo pasa desapercibida porque el
    /// diccionario cae al idioma por defecto.
    /// </summary>
    [HttpGet("cobertura")]
    [Authorize]
    [RequierePatente(Patentes.IdiomaGestionar)]
    public ActionResult<CoberturaIdiomaDto> Cobertura() => Ok(negocio.Idiomas.Cobertura());

    public record PreferenciaDto(string Codigo);
}
