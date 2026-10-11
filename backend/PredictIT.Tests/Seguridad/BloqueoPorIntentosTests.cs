using FluentAssertions;
using PredictIT.DAO.Factory;
using PredictIT.DAO.Helpers;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Seguridad;

/// <summary>
/// CP-02 — Bloqueo del usuario tras cinco intentos fallidos.
///
/// El bloqueo vive en el <c>UPDATE</c> del DAO y no en la capa de negocio, y eso
/// es a propósito: el contador y la marca se mueven en la misma sentencia, así
/// que dos intentos simultáneos no pueden dejar el contador en 5 y la marca en
/// falso. Probarlo desde acá es probar exactamente eso.
///
/// La prueba deja el usuario como lo encontró. No hay «usuario de prueba»
/// dedicado a propósito: el que importa es el del seed, porque es el que el
/// evaluador va a usar.
/// </summary>
[Trait("Categoria", "Seguridad")]
[Trait("CP", "CP-02")]
[Collection(UsuarioSolicitante.Nombre)]
public class BloqueoPorIntentosTests
{
    private static IFactoryDaoSeguridad Dao() =>
        new FactoryDaoSeguridad(new ConexionesSql(
            EntornoPruebas.CadenaNegocio, EntornoPruebas.CadenaServicio));

    private const string Username = "solicitante";

    [Fact]
    public void A_los_cinco_intentos_fallidos_el_usuario_queda_bloqueado()
    {
        var dao = Dao();
        var usuario = dao.Usuarios.PorUsername(Username);
        usuario.Should().NotBeNull("es un usuario del seed");

        // Se parte de cero por si una corrida anterior dejó intentos contados.
        dao.Usuarios.Desbloquear(usuario!.Id);

        try
        {
            for (var intento = 1; intento <= 4; intento++)
            {
                dao.Usuarios.RegistrarAccesoFallido(usuario.Id);

                var parcial = dao.Usuarios.PorUsername(Username)!;
                parcial.IntentosFallidos.Should().Be(intento);
                parcial.Bloqueado.Should().BeFalse(
                    $"con {intento} intentos todavía no corresponde bloquear");
            }

            dao.Usuarios.RegistrarAccesoFallido(usuario.Id);

            var final = dao.Usuarios.PorUsername(Username)!;
            final.IntentosFallidos.Should().Be(5);
            final.Bloqueado.Should().BeTrue("el quinto intento es el que bloquea");
        }
        finally
        {
            // Se desbloquea siempre: una prueba que deja al usuario de
            // demostración bloqueado rompe la demostración, y el fallo aparece
            // en otra parte.
            dao.Usuarios.Desbloquear(usuario.Id);
        }
    }

    [Fact]
    public void Desbloquear_pone_el_contador_en_cero()
    {
        var dao = Dao();
        var usuario = dao.Usuarios.PorUsername(Username)!;

        dao.Usuarios.RegistrarAccesoFallido(usuario.Id);
        dao.Usuarios.RegistrarAccesoFallido(usuario.Id);

        dao.Usuarios.Desbloquear(usuario.Id);

        var despues = dao.Usuarios.PorUsername(Username)!;
        despues.Bloqueado.Should().BeFalse();
        // El contador tiene que volver a cero y no sólo la marca: si quedara en
        // dos, el próximo error del usuario lo bloquearía al tercer intento.
        despues.IntentosFallidos.Should().Be(0);
    }

    [Fact]
    public void Un_acceso_exitoso_borra_los_intentos_previos()
    {
        var dao = Dao();
        var usuario = dao.Usuarios.PorUsername(Username)!;

        dao.Usuarios.Desbloquear(usuario.Id);
        dao.Usuarios.RegistrarAccesoFallido(usuario.Id);
        dao.Usuarios.RegistrarAccesoFallido(usuario.Id);

        dao.Usuarios.RegistrarAccesoExitoso(usuario.Id);

        var despues = dao.Usuarios.PorUsername(Username)!;
        // Sin esto, cinco errores repartidos a lo largo de un mes bloquearían a
        // alguien que entra todos los días. El contador cuenta intentos
        // seguidos, no intentos totales.
        despues.IntentosFallidos.Should().Be(0);
        despues.UltimoAcceso.Should().NotBeNull();
    }
}
