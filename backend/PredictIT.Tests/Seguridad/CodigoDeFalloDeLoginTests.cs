using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PredictIT.DAO.Factory;
using PredictIT.DAO.Helpers;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Seguridad;

/// <summary>
/// El fallo de login viaja con un codigo estable, y ese codigo no dice mas de
/// lo que dice el mensaje.
///
/// La pantalla de ingreso trata distinto a las credenciales invalidas que a la
/// cuenta bloqueada: la primera es roja y vive adentro del formulario porque se
/// puede corregir ahi mismo, la segunda es ambar y lo reemplaza porque ahi no
/// hay nada que corregir. Para elegir entre las dos hace falta un dato que se
/// pueda comparar, y el mensaje no sirve: cambia con el idioma.
///
/// Lo que estas pruebas cuidan es que el codigo no se convierta en un canal
/// lateral. El mensaje de credenciales invalidas es deliberadamente ambiguo
/// —no distingue "el usuario no existe" de "la contrasena esta mal", porque
/// distinguirlos le confirmaria a un atacante que nombres son validos— y el
/// codigo tiene que colapsar exactamente igual. Si alguien alguna vez separa
/// esos dos casos en el enum, esta prueba se pone en rojo.
/// </summary>
[Trait("Categoria", "Seguridad")]
[Trait("CP", "CP-02")]
[Collection(UsuarioSolicitante.Nombre)]
public class CodigoDeFalloDeLoginTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _api;

    public CodigoDeFalloDeLoginTests(WebApplicationFactory<Program> api) => _api = api;

    private static IFactoryDaoSeguridad Dao() =>
        new FactoryDaoSeguridad(new ConexionesSql(
            EntornoPruebas.CadenaNegocio, EntornoPruebas.CadenaServicio));

    private async Task<(string? codigo, string? mensaje)> Intentar(
        HttpClient cliente, string usuario, string clave)
    {
        var r = await cliente.PostAsJsonAsync("/api/auth/login",
                                              new { username = usuario, contrasena = clave });
        var cuerpo = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
        return (cuerpo.TryGetProperty("codigo", out var c) ? c.GetString() : null,
                cuerpo.TryGetProperty("mensaje", out var m) ? m.GetString() : null);
    }

    [Fact]
    public async Task El_usuario_que_no_existe_y_la_contrasena_equivocada_dan_el_mismo_codigo()
    {
        var cliente = _api.CreateClient();

        var inexistente = await Intentar(cliente, "no-existe-este-usuario", "Cualquiera.2026");
        var claveMal = await Intentar(cliente, "admin", "NoEsLaClave.2026");

        inexistente.codigo.Should().Be("credenciales");
        claveMal.codigo.Should().Be("credenciales");

        // Y el mensaje tampoco los separa: el codigo acompana al mensaje, no lo
        // contradice.
        claveMal.mensaje.Should().Be(inexistente.mensaje);
    }

    [Fact]
    public async Task La_cuenta_bloqueada_se_distingue_de_las_credenciales_invalidas()
    {
        var cliente = _api.CreateClient();
        var dao = Dao();
        var usuario = dao.Usuarios.PorUsername("solicitante");
        usuario.Should().NotBeNull("es un usuario del seed");

        // El bloqueo se provoca por el camino real —el UPDATE del DAO que mueve
        // contador y marca en la misma sentencia— y no escribiendo la columna a
        // mano, para que la prueba falle si ese camino cambia.
        dao.Usuarios.Desbloquear(usuario!.Id);
        try
        {
            for (var intento = 1; intento <= 5; intento++)
                dao.Usuarios.RegistrarAccesoFallido(usuario.Id);

            var bloqueado = await Intentar(cliente, "solicitante", "Usuario.2026");

            bloqueado.codigo.Should().Be("bloqueado");
            bloqueado.mensaje.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            // La prueba deja el usuario como lo encontro: es del seed y el
            // evaluador lo va a usar.
            dao.Usuarios.Desbloquear(usuario.Id);
        }
    }
}
