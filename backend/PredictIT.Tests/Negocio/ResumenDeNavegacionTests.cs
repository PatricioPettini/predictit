using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Negocio;

/// <summary>
/// Los contadores de la navegación.
///
/// Son tres números chicos que alimentan las insignias de la barra lateral, y
/// lo que hay que probar de ellos no es la aritmética sino el recorte: cada
/// contador vale para lo que el usuario puede ver, y cuando no puede ver nada
/// viaja en **nulo** y no en cero.
///
/// La distinción importa porque el frontend dibuja la insignia si hay número.
/// Con cero, el solicitante vería un «0» al lado de Activos afirmando que el
/// parque está vacío, que es una afirmación falsa sobre datos que no le
/// corresponden.
/// </summary>
[Trait("Categoria", "Integracion")]
[Collection(UsuarioSolicitante.Nombre)]
public class ResumenDeNavegacionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _api;

    public ResumenDeNavegacionTests(WebApplicationFactory<Program> api) => _api = api;

    private async Task<HttpClient> Entrar(string usuario, string contrasena)
    {
        var cliente = _api.CreateClient();
        var r = await cliente.PostAsJsonAsync("/api/auth/login",
            new { username = usuario, contrasena });
        r.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = JsonDocument.Parse(await r.Content.ReadAsStringAsync())
                                .RootElement.GetProperty("token").GetString();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    private static async Task<JsonElement> Resumen(HttpClient cliente)
    {
        var r = await cliente.GetAsync("/api/organizacion/resumen");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
    }

    /// <summary>
    /// El contador no llegó: la API omite los nulos, así que «ausente» y
    /// «nulo» dicen lo mismo y las dos formas tienen que pasar.
    /// </summary>
    private static void Vacio(JsonElement resumen, string campo)
    {
        if (resumen.TryGetProperty(campo, out var valor))
            valor.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task El_administrador_recibe_los_tres_contadores()
    {
        var resumen = await Resumen(await Entrar("admin", "Admin.2026"));

        resumen.GetProperty("equipos").GetInt32().Should().BeGreaterThan(0);
        resumen.GetProperty("incidenciasAbiertas").ValueKind.Should().Be(JsonValueKind.Number);
        resumen.GetProperty("mantenimientosVencidos").ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public async Task El_conteo_de_equipos_es_el_mismo_que_declara_el_plan()
    {
        // Si los dos números se calcularan por su cuenta, uno de los dos se iba
        // a quedar viejo. Salen del mismo recuento del DAO y esto lo sostiene.
        var cliente = await Entrar("admin", "Admin.2026");

        var resumen = await Resumen(cliente);
        var plan = JsonDocument.Parse(
            await (await cliente.GetAsync("/api/organizacion/plan")).Content.ReadAsStringAsync())
            .RootElement;

        resumen.GetProperty("equipos").GetInt32()
            .Should().Be(plan.GetProperty("equiposAdministrados").GetInt32());
    }

    [Fact]
    public async Task El_solicitante_no_recibe_lo_que_no_puede_ver()
    {
        var resumen = await Resumen(await Entrar("solicitante", "Usuario.2026"));

        // No tiene EQUIPO_VER ni MANTENIMIENTO_VER: esos dos contadores no
        // existen para él. La API omite los nulos, así que el campo no aparece;
        // lo que importa es que no llegue un número, que es lo que el frontend
        // dibujaría.
        Vacio(resumen, "equipos");
        Vacio(resumen, "mantenimientosVencidos");

        // Las incidencias sí, pero las suyas: es el contador que se dibuja al
        // lado de «Mis pedidos».
        resumen.GetProperty("incidenciasAbiertas").ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Theory]
    [InlineData("solicitante", "Usuario.2026")]
    [InlineData("admin", "Admin.2026")]
    public async Task El_contador_es_el_total_del_listado_que_esa_persona_ve(
        string usuario, string contrasena)
    {
        // El contador y el listado son dos caminos distintos al mismo número, y
        // el recorte por perfil vive en el medio de los dos. Compararlos contra
        // sí mismos —y no el de un usuario contra el de otro— es lo que hace
        // que la prueba dependa del recorte y no de cuántas incidencias tenga
        // el seed en ese momento.
        var cliente = await Entrar(usuario, contrasena);

        var contador = (await Resumen(cliente)).GetProperty("incidenciasAbiertas").GetInt32();

        var listado = JsonDocument.Parse(await (await cliente.GetAsync(
                "/api/incidencias?abiertas=true&porPagina=1")).Content.ReadAsStringAsync())
            .RootElement.GetProperty("total").GetInt32();

        contador.Should().Be(listado);
    }

    [Fact]
    public async Task Sin_sesion_no_se_contesta()
    {
        // Los contadores dicen cuántos equipos e incidencias tiene la
        // organización. No es información pública aunque sean tres enteros.
        var r = await _api.CreateClient().GetAsync("/api/organizacion/resumen");

        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
