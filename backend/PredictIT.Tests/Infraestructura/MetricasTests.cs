using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PredictIT.Tests.Infraestructura;

/// <summary>
/// Las métricas en formato Prometheus (ADR 0015).
///
/// Lo que se prueba es lo que la decisión promete, y en particular la parte que
/// es de seguridad: que sin token configurado el endpoint **no exista**. Las
/// métricas dicen qué rutas hay, con qué frecuencia se usan y cuánto tardan, y
/// eso es material de reconocimiento; un endpoint así abierto por omisión es de
/// las cosas que se dejan prendidas sin querer.
/// </summary>
[Trait("Categoria", "Infraestructura")]
public class MetricasTests
{
    private const string Token = "un-token-largo-solo-para-la-prueba-de-metricas";

    /// <summary>La aplicación con el token puesto, que es como corre en la demostración.</summary>
    private sealed class ApiConMetricas : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Metricas:Token"] = Token }));
    }

    [Fact]
    public async Task Sin_token_configurado_el_endpoint_no_existe()
    {
        // La fábrica sin configurar es el caso por omisión: nadie decidió
        // exponer nada.
        using var api = new WebApplicationFactory<Program>();

        var r = await api.CreateClient().GetAsync("/api/metricas");

        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Con_el_token_equivocado_responde_lo_mismo_que_si_no_existiera()
    {
        using var api = new ApiConMetricas();
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Metricas-Token", "el-token-que-no-es");

        var r = await cliente.GetAsync("/api/metricas");

        // 404 y no 401: un 401 confirma que el endpoint está ahí y que sólo
        // falta la credencial, que es justo lo que no conviene confirmarle a
        // quien está probando.
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Con_el_token_devuelve_las_metricas_del_trabajo()
    {
        using var api = new ApiConMetricas();
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Metricas-Token", Token);

        var r = await cliente.GetAsync("/api/metricas");
        r.StatusCode.Should().Be(HttpStatusCode.OK);

        var texto = await r.Content.ReadAsStringAsync();

        // El tiempo en línea es el que hace verificable el RNF-05, que era el
        // único requisito del documento sin forma de comprobarse.
        texto.Should().Contain("predictit_tiempo_en_linea_segundos");

        // Y los dos mecanismos del ADR 0009 que se pueden observar.
        texto.Should().Contain("predictit_disyuntores_abiertos");
        texto.Should().Contain("predictit_peticiones_limitadas_total");

        // Y las del proceso, que cubren el RNF-09.
        texto.Should().Contain("process_working_set_bytes");

        // El histograma de duración de las peticiones —el del RNF-04— no se
        // afirma acá a propósito. Existe: contra el contenedor sale como
        // `microsoft_aspnetcore_hosting_http_server_request_duration`, con sus
        // baldes por ruta y código. Pero lo publica el adaptador que traduce
        // los instrumentos de .NET, de forma asincrónica y con estado de
        // proceso, y bajo `WebApplicationFactory` —varias aplicaciones en el
        // mismo proceso— aparece o no según el orden en que corran las
        // pruebas. Afirmarlo acá daría una prueba intermitente, que es peor que
        // no tenerla: entrena a ignorar la suite.
    }

    [Fact]
    public async Task El_formato_es_el_que_espera_un_recolector()
    {
        using var api = new ApiConMetricas();
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Metricas-Token", Token);

        var r = await cliente.GetAsync("/api/metricas");

        // Un recolector mira el tipo de contenido para saber cómo leerlo. Si
        // saliera como JSON o sin versión, Prometheus lo descarta sin avisar.
        r.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        r.Content.Headers.ContentType.Parameters
            .Should().Contain(p => p.Name == "version");

        var texto = await r.Content.ReadAsStringAsync();

        // Cada métrica lleva su descripción y su tipo, que es lo que hace
        // legible la página sin tener el código al lado.
        texto.Should().Contain("# HELP predictit_tiempo_en_linea_segundos");
        texto.Should().Contain("# TYPE predictit_tiempo_en_linea_segundos gauge");
    }
}
