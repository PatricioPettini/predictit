using FluentAssertions;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.Seguridad;
using PredictIT.Tests.Dobles;

namespace PredictIT.Tests.Seguridad;

/// <summary>
/// La bitácora no pierde asientos por un fallo pasajero de la base.
///
/// Todas las operaciones del sistema escriben en la misma tabla, así que bajo
/// concurrencia SQL Server puede devolver un interbloqueo o una espera de lock
/// agotada. El servicio se traga cualquier excepción a propósito —no se aborta
/// el alta de un equipo porque no se pudo escribir el log—, y esa decisión, sin
/// reintento, convierte un fallo pasajero en un agujero de auditoría silencioso
/// justo en los momentos de más actividad, que son los que más se auditan.
///
/// Se comprueba contra un doble y no contra la base real porque provocar un
/// interbloqueo de verdad exige montar contención a propósito y deja una prueba
/// que depende del azar del motor.
/// </summary>
[Trait("Categoria", "Seguridad")]
public class BitacoraNoPierdeAsientosTests
{
    private static (BitacoraService Servicio, BitacoraDaoFalso Dao, LoggerFalso Log) Armar(int fallas)
    {
        var dao = new BitacoraDaoFalso { FallasSeguidas = fallas };
        var log = new LoggerFalso();
        var servicio = new BitacoraService(
            new FabricaSeguridadFalsa(new UsuarioDaoFalso(), dao), log);

        return (servicio, dao, log);
    }

    private static void Asentar(BitacoraService servicio) =>
        servicio.Registrar(TipoEvento.Codigos.LoginOk, "Prueba de reintento.",
                           Guid.NewGuid(), Guid.NewGuid(), "Usuario", Guid.NewGuid());

    [Fact]
    public void Un_fallo_pasajero_no_se_lleva_el_asiento()
    {
        var (servicio, dao, log) = Armar(fallas: 2);

        Asentar(servicio);

        dao.Escritas.Should().HaveCount(1, "el tercer intento tiene que haber entrado");
        dao.Intentos.Should().Be(3);
        log.Errores.Should().BeEmpty("no hubo nada que reportar: el asiento está");
        log.Advertencias.Should().ContainSingle()
           .Which.Should().Contain("intento 3",
               "que haya hecho falta reintentar es en sí una señal de contención");
    }

    [Fact]
    public void Si_no_hay_forma_de_escribirlo_la_operacion_sigue_y_queda_reportado()
    {
        var (servicio, dao, log) = Armar(fallas: 99);

        // No lanza: la operación de negocio que originó el asiento no se cae
        // porque la auditoría no haya podido escribirse.
        Asentar(servicio);

        dao.Intentos.Should().Be(3, "se reintenta, pero no para siempre");
        dao.Escritas.Should().BeEmpty();
        log.Errores.Should().ContainSingle()
           .Which.Mensaje.Should().Contain(TipoEvento.Codigos.LoginOk,
               "el log tiene que decir qué evento se perdió, o no sirve para nada");
    }

    [Fact]
    public void Un_error_permanente_no_se_reintenta()
    {
        var (servicio, dao, log) = Armar(fallas: 99);
        // Un texto que no entra en la columna falla igual las tres veces, y
        // reintentarlo sólo retrasa la operación de negocio.
        dao.Falla = () => new InvalidOperationException("La columna no admite ese valor.");

        Asentar(servicio);

        dao.Intentos.Should().Be(1);
        log.Errores.Should().ContainSingle();
    }
}
