using System.Text.Json;
using PredictIT.BLL;
using PredictIT.DAO.Contracts;
using PredictIT.Service.Seguridad;

namespace PredictIT.Api.Infraestructura;

/// <summary>
/// Traduce las excepciones a respuestas HTTP (Req. Arq. 004).
///
/// La distinción importa: una <see cref="BusinessException"/> lleva un mensaje
/// escrito para el usuario y se devuelve tal cual. Cualquier otra excepción se
/// devuelve con un texto genérico, porque no se puede saber si el mensaje
/// original expone detalles internos —un nombre de tabla, una ruta, una cadena
/// de conexión—. El detalle completo va a la bitácora, no a la respuesta.
/// </summary>
public class ManejadorDeExcepciones
{
    private readonly RequestDelegate _siguiente;
    private readonly ILogger<ManejadorDeExcepciones> _log;

    public ManejadorDeExcepciones(RequestDelegate siguiente, ILogger<ManejadorDeExcepciones> log)
    {
        _siguiente = siguiente;
        _log = log;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _siguiente(ctx);
        }
        catch (BusinessException ex)
        {
            await Responder(ctx, StatusCodes.Status400BadRequest,
                            new { mensaje = ex.Message, campo = ex.Campo, codigo = ex.Codigo });
        }
        catch (PermisoDenegadoException ex)
        {
            Asentar(ctx, b => b.RegistrarAccesoDenegado(
                UsuarioDe(ctx)?.Username ?? "(desconocido)", ex.DataKey,
                UsuarioDe(ctx)?.IdUsuario, UsuarioDe(ctx)?.IdOrganizacion));

            await Responder(ctx, StatusCodes.Status403Forbidden,
                            new { mensaje = ex.Message, patente = ex.DataKey });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Excepción no controlada en {Ruta}", ctx.Request.Path);

            Asentar(ctx, b => b.RegistrarExcepcion(ex, ctx.Request.Path,
                                                   UsuarioDe(ctx)?.IdUsuario,
                                                   UsuarioDe(ctx)?.IdOrganizacion));

            await Responder(ctx, StatusCodes.Status500InternalServerError, new
            {
                mensaje = "Ocurrió un error inesperado. El problema quedó registrado."
            });
        }
    }

    private static IContextoSesion? UsuarioDe(HttpContext ctx) =>
        ctx.RequestServices.GetService(typeof(IContextoSesion)) as IContextoSesion;

    /// <summary>
    /// Asienta en bitácora sin dejar que un fallo al hacerlo tape la excepción
    /// original, que es la que hay que reportar.
    /// </summary>
    private void Asentar(HttpContext ctx, Action<IBitacoraService> accion)
    {
        try
        {
            if (ctx.RequestServices.GetService(typeof(IBitacoraService)) is IBitacoraService b)
            {
                accion(b);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo asentar la excepción en bitácora.");
        }
    }

    private static async Task Responder(HttpContext ctx, int codigo, object cuerpo)
    {
        if (ctx.Response.HasStarted) return;

        ctx.Response.Clear();
        ctx.Response.StatusCode = codigo;
        ctx.Response.ContentType = "application/json; charset=utf-8";

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(cuerpo, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        }));
    }
}
