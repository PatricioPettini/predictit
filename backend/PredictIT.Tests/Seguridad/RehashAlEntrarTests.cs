using FluentAssertions;
using PredictIT.DAO.Factory;
using PredictIT.DAO.Helpers;
using PredictIT.Service.Seguridad;
using PredictIT.Tests.Dobles;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Seguridad;

/// <summary>
/// A02 — El hash viejo se vuelve a derivar cuando el usuario entra.
///
/// Subir las iteraciones de PBKDF2 no sirve de nada por sí solo: los hashes ya
/// guardados se siguen verificando con las iteraciones con las que se
/// derivaron, así que los usuarios creados antes se quedan con el hash débil
/// para siempre. La única oportunidad de rehacerlo sin pedirle a nadie que
/// cambie la contraseña es el momento en que la escribe, y eso es el login.
///
/// La prueba deja el usuario como lo encontró: guarda el hash original y lo
/// restaura, porque el que importa es el del seed y lo va a usar el evaluador.
/// </summary>
[Trait("Categoria", "Seguridad")]
public class RehashAlEntrarTests
{
    // `tecnico3` y no `solicitante`: las pruebas de bloqueo cuentan intentos
    // fallidos sobre ese usuario y estas lo desbloquean, y las clases corren en
    // paralelo. Compartir usuario las hacía fallar a las dos por turnos.
    private const string Username = "tecnico3";
    private const string Contrasena = "Tecnico.2026";

    private static IFactoryDaoSeguridad Dao() =>
        new FactoryDaoSeguridad(new ConexionesSql(
            EntornoPruebas.CadenaNegocio, EntornoPruebas.CadenaServicio));

    private static int IteracionesDe(string hash) => int.Parse(hash.Split('$')[1]);

    [Fact]
    public void Un_hash_con_pocas_iteraciones_se_rehace_al_iniciar_sesion()
    {
        var dao = Dao();
        var usuario = dao.Usuarios.PorUsername(Username);
        usuario.Should().NotBeNull("es un usuario del seed");

        var original = usuario!.PasswordHash;
        dao.Usuarios.Desbloquear(usuario.Id);

        try
        {
            // Se lo deja con un hash deliberadamente débil, como el de alguien
            // creado antes de subir el parámetro.
            var debil = HashContrasena.Derivar(Contrasena, iteraciones: 1_000);
            dao.Usuarios.ActualizarHash(usuario.Id, debil);

            var servicio = new SeguridadService(dao, new BitacoraFalsa());
            servicio.Autenticar(Username, Contrasena).Exitosa
                    .Should().BeTrue("la contraseña es la correcta, sólo cambia el costo");

            var despues = dao.Usuarios.PorUsername(Username)!.PasswordHash;
            IteracionesDe(despues).Should().BeGreaterThan(
                IteracionesDe(debil), "entrar tiene que dejar el hash con el costo de hoy");
            HashContrasena.NecesitaRehash(despues).Should().BeFalse();

            // Y la contraseña sigue siendo la misma: lo que cambió es el costo.
            HashContrasena.Verificar(Contrasena, despues).Should().BeTrue();
        }
        finally
        {
            dao.Usuarios.ActualizarHash(usuario.Id, original);
            dao.Usuarios.Desbloquear(usuario.Id);
        }
    }

    [Fact]
    public void Un_hash_ya_al_dia_no_se_toca()
    {
        var dao = Dao();
        var usuario = dao.Usuarios.PorUsername(Username)!;
        var original = usuario.PasswordHash;
        dao.Usuarios.Desbloquear(usuario.Id);

        try
        {
            var alDia = HashContrasena.Derivar(Contrasena);
            dao.Usuarios.ActualizarHash(usuario.Id, alDia);

            new SeguridadService(dao, new BitacoraFalsa())
                .Autenticar(Username, Contrasena).Exitosa.Should().BeTrue();

            dao.Usuarios.PorUsername(Username)!.PasswordHash
               .Should().Be(alDia, "sin necesidad de rehacerlo, el hash no se toca");
        }
        finally
        {
            dao.Usuarios.ActualizarHash(usuario.Id, original);
            dao.Usuarios.Desbloquear(usuario.Id);
        }
    }
}
