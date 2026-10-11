namespace PredictIT.Api.Infraestructura;

/// <summary>
/// Cabeceras de seguridad en toda respuesta de la API (OWASP A05).
///
/// El frontend ya las manda desde nginx, pero la API se puede llamar
/// directamente —Swagger está publicado, y el puerto 8081 queda expuesto para
/// poder mostrarlo—, así que ponerlas sólo en el proxy deja la mitad afuera.
///
/// La política de contenido es restrictiva porque la API no devuelve páginas:
/// devuelve JSON. Lo único que se sirve como HTML es Swagger, y por eso
/// `script-src` y `style-src` admiten lo que Swagger inyecta en línea. Si la
/// API dejara de exponer Swagger, la política podría reducirse a `default-src
/// 'none'`.
/// </summary>
public class CabecerasDeSeguridad(RequestDelegate siguiente, IConfiguration config)
{
    private readonly bool _swagger = config.GetValue("Swagger:Habilitado", false);

    public async Task InvokeAsync(HttpContext contexto)
    {
        var cabeceras = contexto.Response.Headers;

        // No adivinar el tipo de contenido: sin esto, una respuesta con un
        // cuerpo controlado por el usuario puede terminar interpretada como
        // HTML y ejecutarse.
        cabeceras["X-Content-Type-Options"] = "nosniff";

        // La API no se muestra dentro de un marco en ningún lado.
        cabeceras["X-Frame-Options"] = "DENY";
        cabeceras["Referrer-Policy"] = "no-referrer";

        // Nada de cámara, micrófono ni ubicación: la API no los usa y así no
        // los puede pedir una página que la incruste.
        cabeceras["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        cabeceras["Content-Security-Policy"] = _swagger
            ? "default-src 'none'; script-src 'self' 'unsafe-inline'; "
              + "style-src 'self' 'unsafe-inline'; img-src 'self' data:; "
              + "connect-src 'self'; frame-ancestors 'none'; base-uri 'none'"
            : "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

        await siguiente(contexto);
    }
}
