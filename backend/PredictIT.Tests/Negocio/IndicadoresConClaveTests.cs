using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PredictIT.Tests.Negocio;

/// <summary>
/// Los indicadores del Dashboard viajan como clave del diccionario.
///
/// El RNF de internacionalización dice que sumar un idioma se resuelve
/// traduciendo el diccionario, sin tocar la lógica. Los cuatro indicadores del
/// tablero lo desmentían: la capa de negocio armaba «Equipos activos» y «de 52
/// registrados» en castellano y la pantalla los mostraba tal cual, así que con
/// la interfaz en inglés el tablero seguía en castellano y arreglarlo pedía
/// editar una clase de la BLL.
///
/// Esta prueba fija la forma: lo que sale de la API es una clave —minúsculas,
/// un punto, ámbito y nombre— y los números que la frase intercala van aparte.
/// Si alguien vuelve a escribir una etiqueta acá, se pone en rojo.
/// </summary>
[Trait("Categoria", "Integracion")]
public class IndicadoresConClaveTests(WebApplicationFactory<Program> api)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Regex EsUnaClave = new(@"^[a-z][a-zA-Z0-9]*\.[a-zA-Z0-9]+$");

    private async Task<HttpClient> Autenticado()
    {
        var cliente = api.CreateClient();
        var r = await cliente.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", contrasena = "Admin.2026" });
        var token = JsonDocument.Parse(await r.Content.ReadAsStringAsync())
                                .RootElement.GetProperty("token").GetString();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    [Fact]
    public async Task El_tablero_manda_claves_y_no_frases_armadas()
    {
        var cliente = await Autenticado();
        var tablero = JsonDocument.Parse(await cliente.GetStringAsync("/api/prediccion/dashboard")).RootElement;

        var indicadores = tablero.GetProperty("indicadores").EnumerateArray().ToList();
        indicadores.Should().NotBeEmpty("el tablero tiene indicadores");

        foreach (var i in indicadores)
        {
            var clave = i.GetProperty("clave").GetString();
            clave.Should().MatchRegex(EsUnaClave.ToString(),
                "el rótulo del indicador es una clave del diccionario y no un texto");

            var detalle = i.GetProperty("detalleClave");
            if (detalle.ValueKind is not JsonValueKind.Null)
            {
                detalle.GetString().Should().MatchRegex(EsUnaClave.ToString(),
                    "el detalle también es una clave: la frase la arma el diccionario");
            }
        }
    }

    [Fact]
    public async Task El_numero_del_detalle_viaja_aparte_de_la_frase()
    {
        var cliente = await Autenticado();
        var tablero = JsonDocument.Parse(await cliente.GetStringAsync("/api/prediccion/dashboard")).RootElement;

        // «de {total} registrados»: el total va en `datos` y no pegado al
        // texto, porque en otro idioma el número puede ir en otro lugar de la
        // frase.
        var equipos = tablero.GetProperty("indicadores").EnumerateArray()
            .First(i => i.GetProperty("clave").GetString() == "dash.equiposActivos");

        equipos.GetProperty("detalleClave").GetString().Should().Be("dash.deRegistrados");
        equipos.GetProperty("datos").GetProperty("total").GetString()
               .Should().MatchRegex(@"^\d+$", "el total es un número, no una frase");
    }
}
