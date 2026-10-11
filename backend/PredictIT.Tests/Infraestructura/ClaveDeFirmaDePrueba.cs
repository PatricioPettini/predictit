using System.Runtime.CompilerServices;

namespace PredictIT.Tests.Infraestructura;

/// <summary>
/// La clave con la que firma la API cuando la levanta una prueba.
///
/// Las pruebas que levantan la aplicación real con
/// <c>WebApplicationFactory</c> arrancan el <c>Program</c> de verdad, y ese se
/// niega a arrancar sin clave de firma —y hace bien: un valor por defecto en
/// producción sería una clave conocida—. En la máquina de desarrollo la clave
/// venía de <c>appsettings.Development.json</c>, que está fuera del control de
/// versiones, así que en un clon limpio del repositorio siete pruebas fallaban
/// con «Falta la clave de firma del token» sin que hubiera nada roto.
///
/// Acá se pone una de usar y tirar, y sólo si el entorno no trae la suya: la
/// integración continua sigue inyectando la propia y quien tenga configurada
/// una la conserva. El resguardo de <c>Program</c> queda intacto, que es lo que
/// importa; lo que cambia es que correr la suite deja de pedir un secreto.
/// </summary>
internal static class ClaveDeFirmaDePrueba
{
    private const string Variable = "PREDICTIT_JWT_CLAVE";

    // Larga a propósito: HS256 exige 32 bytes y el arranque lo verifica.
    private const string DeUsarYTirar =
        "clave-de-pruebas-automatizadas-no-usar-en-produccion";

    [ModuleInitializer]
    internal static void Poner()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
        {
            Environment.SetEnvironmentVariable(Variable, DeUsarYTirar);
        }
    }
}
