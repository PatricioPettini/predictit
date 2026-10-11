using FluentAssertions;
using Microsoft.Data.SqlClient;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Dao;

/// <summary>
/// La aplicacion y el motor tienen que estar en la misma hora.
///
/// El sistema sella fechas con dos relojes. Trece columnas del esquema usan
/// `DEFAULT SYSDATETIME()`, o sea el reloj del motor; el resto lo pone la BLL
/// con `DateTime.Now`, o sea el de la aplicacion. Mientras los dos coincidan
/// eso no molesta a nadie.
///
/// Cuando no coinciden, el sintoma es raro y no rompe nada: la pantalla del
/// solicitante mostraba «recibimos tu pedido 15:56» y «se lo asignamos 18:56»
/// para una incidencia registrada y asignada en el mismo segundo. El
/// contenedor de la API traia `TZ=America/Argentina/Buenos_Aires` -con un
/// comentario que explicaba justamente este riesgo- y el del motor no traia
/// ninguna, asi que corria en UTC.
///
/// La prueba compara los dos relojes. No mide precision -para eso estaria NTP-
/// sino que no haya un salto de zona horaria, que es de horas enteras.
/// </summary>
[Trait("Categoria", "Integracion")]
public class RelojUnicoTests
{
    /// <summary>
    /// Un minuto de tolerancia. Alcanza para absorber la latencia de la
    /// consulta y un reloj que derivo, y no alcanza para tapar la diferencia
    /// mas chica entre dos zonas horarias, que es de media hora.
    /// </summary>
    private static readonly TimeSpan Tolerancia = TimeSpan.FromMinutes(1);

    [Fact]
    public void El_reloj_del_motor_y_el_de_la_aplicacion_no_estan_en_zonas_distintas()
    {
        using var conexion = new SqlConnection(EntornoPruebas.CadenaNegocio);
        conexion.Open();

        using var comando = new SqlCommand("SELECT SYSDATETIME();", conexion);

        // Se lee el reloj de la aplicacion pegado al del motor para que la
        // diferencia que se mide sea la de las zonas y no la del tiempo que
        // tardo la consulta.
        var antes = DateTime.Now;
        var delMotor = (DateTime)comando.ExecuteScalar()!;
        var despues = DateTime.Now;

        var diferencia = delMotor < antes ? antes - delMotor : delMotor - despues;
        if (diferencia < TimeSpan.Zero) diferencia = TimeSpan.Zero;

        diferencia.Should().BeLessThan(
            Tolerancia,
            "el motor marca {0} y la aplicacion {1}: si la diferencia es de horas enteras, "
            + "los dos contenedores estan en zonas horarias distintas y las fechas del "
            + "sistema van a contradecirse entre si",
            delMotor, antes);
    }
}
