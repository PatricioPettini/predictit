using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PredictIT.DAO.Contracts;
using PredictIT.Service.Seguridad;

namespace PredictIT.Api.Infraestructura;

/// <summary>
/// Exige una patente para llegar al endpoint.
///
/// Es una segunda barrera, no la única: la BLL vuelve a exigir la patente antes
/// de operar. Puede parecer redundante y es a propósito —la regla tiene que
/// valer para cualquier punto de entrada, no sólo para la API—, pero acá se
/// corta antes de tocar la base y el rechazo queda en bitácora con el nombre del
/// permiso que faltó.
///
/// Admite varias patentes y alcanza con tener **una**. Hace falta porque el
/// modelo de permisos tiene pares donde ninguna implica a la otra:
/// <c>INCIDENCIA_VER_TODAS</c> e <c>INCIDENCIA_VER_PROPIAS</c> son patentes
/// distintas, y el administrador tiene sólo la primera. Exigir la de «propias»
/// como mínimo le daba 403 al administrador; exigir la amplia se lo daba al
/// solicitante. Quién ve qué lo sigue decidiendo la BLL: acá sólo se corta a
/// quien no tiene ninguna de las dos.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequierePatenteAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _dataKeys;

    public RequierePatenteAttribute(params string[] dataKeys)
    {
        if (dataKeys.Length == 0)
            throw new ArgumentException("Hay que indicar al menos una patente.", nameof(dataKeys));

        _dataKeys = dataKeys;
    }

    public void OnAuthorization(AuthorizationFilterContext ctx)
    {
        var contexto = ctx.HttpContext.RequestServices.GetService(typeof(IContextoSesion)) as IContextoSesion;

        if (contexto is null || contexto.IdUsuario == Guid.Empty)
        {
            ctx.Result = new UnauthorizedObjectResult(new { mensaje = "La sesión no es válida." });
            return;
        }

        if (_dataKeys.Any(contexto.Tiene)) return;

        var faltantes = string.Join(" o ", _dataKeys);

        if (ctx.HttpContext.RequestServices.GetService(typeof(IBitacoraService)) is IBitacoraService bitacora)
        {
            bitacora.RegistrarAccesoDenegado(contexto.Username, faltantes,
                                             contexto.IdUsuario, contexto.IdOrganizacion);
        }

        ctx.Result = new ObjectResult(new
        {
            mensaje = "No tenés permiso para realizar esta acción.",
            patente = faltantes
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
