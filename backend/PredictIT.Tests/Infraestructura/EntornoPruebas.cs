namespace PredictIT.Tests.Infraestructura;

/// <summary>
/// Punto único desde donde las pruebas obtienen las cadenas de conexión.
///
/// Se conectan como <c>predictit_app</c>, el mismo usuario que usa la
/// aplicación, y no como <c>sa</c>. La diferencia no es cosmética: corriendo
/// como administrador del motor, un permiso que la aplicación no tiene pasa
/// desapercibido en las pruebas y aparece recién en producción.
///
/// La contraseña sale de PREDICTIT_DB_PASSWORD, o se pasa la cadena entera en
/// PREDICTIT_CNN_NEGOCIO / PREDICTIT_CNN_SERVICIO, que es lo que hace el
/// pipeline. El usuario lo crea <c>db/06-usuario-de-aplicacion.sql</c>.
/// </summary>
public static class EntornoPruebas
{
    private const string Plantilla =
        "Server=localhost,1433;Database=PredictIT_{0};User Id=predictit_app;"
        + "Password={1};TrustServerCertificate=True;Encrypt=True;";

    public static string CadenaNegocio =>
        Environment.GetEnvironmentVariable("PREDICTIT_CNN_NEGOCIO") ?? Armar("Negocio");

    public static string CadenaServicio =>
        Environment.GetEnvironmentVariable("PREDICTIT_CNN_SERVICIO") ?? Armar("Servicio");

    private static string Armar(string nombreBase)
    {
        var clave = Environment.GetEnvironmentVariable("PREDICTIT_DB_PASSWORD");

        if (string.IsNullOrWhiteSpace(clave))
            throw new InvalidOperationException(
                "Falta PREDICTIT_DB_PASSWORD. Es la contraseña del usuario de la "
                + "aplicación, la misma que está en .env; la crea "
                + "db/06-usuario-de-aplicacion.sql. Alternativa: pasar la cadena "
                + "entera en PREDICTIT_CNN_NEGOCIO y PREDICTIT_CNN_SERVICIO.");

        return string.Format(Plantilla, nombreBase, clave);
    }
}
