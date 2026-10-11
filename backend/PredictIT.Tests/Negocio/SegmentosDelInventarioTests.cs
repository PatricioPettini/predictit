using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Negocio;

/// <summary>
/// Los tres segmentos del inventario (RF-05).
///
/// No son filtros por columna sino las tres preguntas que alguien se hace
/// parado frente al parque: qué está por romperse, a qué le debo el preventivo
/// y qué ya no tiene garantía. Cada una tiene su número al lado, y el número
/// es una promesa: si dice tres, al pulsarlo tienen que aparecer tres.
///
/// Eso es lo que se prueba acá, y no la aritmética de cada segmento por
/// separado: que el recuento y el listado cuenten lo mismo. Son dos consultas
/// distintas sobre las mismas tablas, y es exactamente donde se separan.
/// </summary>
[Trait("Categoria", "Integracion")]
[Collection(UsuarioSolicitante.Nombre)]
public class SegmentosDelInventarioTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _api;

    public SegmentosDelInventarioTests(WebApplicationFactory<Program> api) => _api = api;

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

    private static async Task<JsonElement> Json(HttpClient cliente, string ruta)
    {
        var r = await cliente.GetAsync(ruta);
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
    }

    public static TheoryData<string, string> LosTres() => new()
    {
        { "riesgoAlto", "riesgoAlto" },
        { "preventivoVencido", "preventivoVencido" },
        { "garantiaVencida", "garantiaVencida" },
    };

    [Theory]
    [MemberData(nameof(LosTres))]
    public async Task El_numero_del_segmento_es_el_del_listado(string segmento, string campo)
    {
        var cliente = await Entrar("admin", "Admin.2026");

        var recuentos = await Json(cliente, "/api/equipos/segmentos");
        var declarado = recuentos.GetProperty(campo).GetInt32();

        var listado = await Json(cliente, $"/api/equipos?segmento={segmento}&porPagina=1");

        listado.GetProperty("total").GetInt32().Should().Be(declarado,
            "el número de la píldora es lo que promete mostrar al pulsarla");
    }

    [Fact]
    public async Task El_total_es_el_del_parque_sin_segmento()
    {
        var cliente = await Entrar("admin", "Admin.2026");

        var recuentos = await Json(cliente, "/api/equipos/segmentos");
        var listado = await Json(cliente, "/api/equipos?porPagina=1");

        recuentos.GetProperty("todos").GetInt32()
            .Should().Be(listado.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Ningun_segmento_es_mas_grande_que_el_parque()
    {
        // Un `EXISTS` mal escrito multiplica filas y da recuentos mayores que
        // el universo. Es barato de comprobar y no depende de los datos.
        var r = await Json(await Entrar("admin", "Admin.2026"), "/api/equipos/segmentos");
        var todos = r.GetProperty("todos").GetInt32();

        foreach (var campo in new[] { "riesgoAlto", "preventivoVencido", "garantiaVencida" })
            r.GetProperty(campo).GetInt32().Should().BeInRange(0, todos);
    }

    [Fact]
    public async Task Los_recuentos_acompanan_a_los_demas_filtros()
    {
        // El número tiene que decir cuántos vería quien pulse la píldora *con
        // los filtros que ya puso*. Con un total del parque fijo, al filtrar
        // por ubicación la píldora seguiría diciendo un número que no existe.
        var cliente = await Entrar("admin", "Admin.2026");

        var sinFiltro = await Json(cliente, "/api/equipos/segmentos");
        var conFiltro = await Json(cliente, "/api/equipos/segmentos?texto=UPS");

        conFiltro.GetProperty("todos").GetInt32()
            .Should().BeLessThan(sinFiltro.GetProperty("todos").GetInt32());

        // Y sigue valiendo la promesa, ahora con el filtro puesto.
        var listado = await Json(cliente, "/api/equipos?texto=UPS&porPagina=1");
        conFiltro.GetProperty("todos").GetInt32()
            .Should().Be(listado.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Un_segmento_inventado_no_recorta_nada()
    {
        // Llega por la dirección, así que puede venir de un enlace viejo o de
        // alguien probando. Se ignora: un 400 dejaría la pantalla sin
        // inventario por un nombre mal escrito.
        var cliente = await Entrar("admin", "Admin.2026");

        var inventado = await Json(cliente, "/api/equipos?segmento=riesgoAlt&porPagina=1");
        var todos = await Json(cliente, "/api/equipos?porPagina=1");

        inventado.GetProperty("total").GetInt32()
            .Should().Be(todos.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task El_solicitante_no_puede_contar_el_parque()
    {
        // Los recuentos dicen cuántos equipos tiene la organización y cuántos
        // están en riesgo. Es la misma información del inventario, resumida.
        var r = await (await Entrar("solicitante", "Usuario.2026"))
            .GetAsync("/api/equipos/segmentos");

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
