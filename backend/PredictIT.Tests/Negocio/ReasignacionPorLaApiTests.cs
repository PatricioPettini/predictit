using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Negocio;

/// <summary>
/// CP-08 · La reasignación manual, por la API.
///
/// La regla de permisos de la reasignación —el técnico sólo sobre las suyas, el
/// administrador sobre cualquiera— está probada contra la capa de negocio. Lo
/// que eso no prueba es que el endpoint exista y llame a ese método.
///
/// Y no existía. Los atributos `[HttpPut("{id}/tecnico")]` y su
/// `[RequierePatente]` habían quedado huérfanos entre dos métodos, así que
/// decoraban al que seguía —`Transiciones`— y el método `Reasignar` quedaba sin
/// ruta. El efecto era el peor posible: la pantalla llamaba, recibía **200** con
/// la lista de transiciones, y daba la reasignación por hecha. Nada cambiaba.
///
/// Lo destapó activar la generación del XML de documentación, que avisa cuando
/// un comentario queda colgado de algo que no es un miembro. La prueba va acá
/// para que no haga falta ese aviso la próxima vez.
/// </summary>
[Trait("Categoria", "Integracion")]
[Trait("CP", "CP-08")]
[Collection(UsuarioSolicitante.Nombre)]
public class ReasignacionPorLaApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _api;

    public ReasignacionPorLaApiTests(WebApplicationFactory<Program> api) => _api = api;

    private async Task<HttpClient> Entrar(string usuario, string contrasena)
    {
        var cliente = _api.CreateClient();
        var r = await cliente.PostAsJsonAsync("/api/auth/login",
            new { username = usuario, contrasena });
        r.StatusCode.Should().Be(HttpStatusCode.OK, "el usuario del seed tiene que poder entrar");

        var token = JsonDocument.Parse(await r.Content.ReadAsStringAsync())
                                .RootElement.GetProperty("token").GetString();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Una incidencia abierta y con técnico, que es lo que se reasigna.</summary>
    private static async Task<(Guid Id, string Tecnico)> UnaAsignada(HttpClient cliente)
    {
        var lista = await Json(
            await cliente.GetAsync("/api/incidencias?abiertas=true&porPagina=20"));

        foreach (var i in lista.GetProperty("items").EnumerateArray())
        {
            var id = i.GetProperty("id").GetGuid();
            var d = await Json(await cliente.GetAsync($"/api/incidencias/{id}"));
            if (d.TryGetProperty("tecnico", out var t) && t.ValueKind == JsonValueKind.String)
                return (id, t.GetString()!);
        }

        throw new InvalidOperationException(
            "el seed tiene que dejar al menos una incidencia abierta con técnico");
    }

    private static async Task<(Guid Id, string Nombre)> OtroTecnico(HttpClient cliente, string actual)
    {
        var catalogos = await Json(await cliente.GetAsync("/api/mantenimientos/catalogos"));

        foreach (var t in catalogos.GetProperty("tecnicos").EnumerateArray())
        {
            var nombre = t.GetProperty("nombreCompleto").GetString()!;
            if (nombre != actual) return (t.GetProperty("id").GetGuid(), nombre);
        }

        throw new InvalidOperationException("el seed tiene que tener más de un técnico");
    }

    [Fact]
    public async Task Reasignar_por_la_API_cambia_el_tecnico()
    {
        var cliente = await Entrar("admin", "Admin.2026");
        var (id, antes) = await UnaAsignada(cliente);
        var (idOtro, otro) = await OtroTecnico(cliente, antes);

        var r = await cliente.PutAsJsonAsync($"/api/incidencias/{id}/tecnico",
            new { idTecnico = idOtro });

        // 204 y no 200: la reasignación no devuelve cuerpo. Un 200 acá era
        // justamente el síntoma —la ruta caía en el endpoint de transiciones,
        // que responde con una lista y no toca nada—.
        r.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var despues = await Json(await cliente.GetAsync($"/api/incidencias/{id}"));
        despues.GetProperty("tecnico").GetString().Should().Be(otro,
            "reasignar tiene que cambiar el técnico, y no sólo contestar que sí");

        // Se deja como estaba: la base es la misma que usa la demostración.
        await cliente.PutAsJsonAsync($"/api/incidencias/{id}/tecnico",
            new { idTecnico = (await OtroTecnico(cliente, otro)).Id });
    }

    [Fact]
    public async Task Las_transiciones_siguen_en_su_propia_ruta()
    {
        // La otra mitad del defecto: `Transiciones` tenía dos rutas y dos
        // exigencias de patente, la suya y la que le había quedado de arriba.
        var cliente = await Entrar("admin", "Admin.2026");
        var (id, _) = await UnaAsignada(cliente);

        var r = await cliente.GetAsync($"/api/incidencias/{id}/transiciones");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(r)).ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task El_solicitante_no_puede_reasignar()
    {
        var admin = await Entrar("admin", "Admin.2026");
        var (id, actual) = await UnaAsignada(admin);
        var (idOtro, _) = await OtroTecnico(admin, actual);

        var solicitante = await Entrar("solicitante", "Usuario.2026");

        var r = await solicitante.PutAsJsonAsync($"/api/incidencias/{id}/tecnico",
            new { idTecnico = idOtro });

        // Quien reporta una falla no decide quién la atiende. Con los atributos
        // huérfanos esta exigencia estaba puesta sobre otro endpoint.
        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
