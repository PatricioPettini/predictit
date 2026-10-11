using Microsoft.Data.SqlClient;

namespace PredictIT.DAO.Helpers;

/// <summary>
/// El reloj del sistema, para los timestamps que se escriben en la base.
///
/// Existe porque había dos criterios conviviendo: algunas escrituras mandaban
/// `DateTime.Now` desde la aplicación y otras dejaban que el motor aplicara
/// `SYSDATETIME()`. Dentro del contenedor el motor está en UTC, así que las dos
/// mitades del sistema quedaban separadas por tres horas.
///
/// Lo visible era un «hace −1 días» en la pantalla de respaldos. Lo importante
/// es que el motor predictivo compara las fechas de las incidencias contra
/// `DateTime.Now`, y el cálculo de «fuera del objetivo de resolución» sobre un
/// SLA de cuatro horas salía mal.
///
/// Se elige el reloj de la aplicación y no el del motor porque la zona horaria
/// del contenedor es un detalle de infraestructura: cambiar la imagen cambiaría
/// el significado de los datos ya guardados.
/// </summary>
public static class Reloj
{
    public static DateTime Ahora => DateTime.Now;

    /// <summary>El parámetro <c>@ahora</c>, listo para agregar a un comando.</summary>
    public static SqlParameter Parametro(string nombre = "@ahora") => new(nombre, Ahora);
}
