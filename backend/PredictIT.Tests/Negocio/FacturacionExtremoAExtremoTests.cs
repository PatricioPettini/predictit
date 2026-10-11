using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PredictIT.Domain.Negocio;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Negocio;

/// <summary>
/// CP-17 · La facturación del servicio contra la base real.
///
/// Las reglas del ciclo —que emitir congele, que no se duplique el período, que
/// no se anule lo cobrado— están probadas contra dobles, que es donde
/// corresponde. Lo que eso no prueba es el SQL: que las líneas se guarden y se
/// lean en orden, que el desglose sobreviva a la ida y vuelta, que el filtro
/// por organización esté en la consulta y que el correlativo salga de la tabla.
///
/// Se factura un período viejo y se lo descarta al final. Va sobre un período
/// que nadie más usa a propósito: la base de pruebas es la misma que la de
/// demostración, y una prueba que pise el mes en curso rompería lo que la
/// pantalla muestra.
/// </summary>
[Trait("Categoria", "Integracion")]
[Trait("CP", "CP-17")]
public class FacturacionExtremoAExtremoTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _api;

    public FacturacionExtremoAExtremoTests(WebApplicationFactory<Program> api) => _api = api;

    /// <summary>Un período lejano, para no cruzarse con los datos de demostración.</summary>
    private static string PeriodoDePrueba =>
        Periodo.Mover(Periodo.De(DateOnly.FromDateTime(DateTime.Today)), -30).ToString();

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

    [Fact]
    public async Task El_comprobante_se_guarda_con_su_desglose_y_se_lee_igual()
    {
        var cliente = await Entrar("admin", "Admin.2026");
        var periodo = PeriodoDePrueba;
        Guid id = default;

        try
        {
            var generado = await Json(
                await cliente.PostAsync($"/api/facturacion?periodo={periodo}", null));

            id = generado.GetProperty("id").GetGuid();
            generado.GetProperty("yaExistia").GetBoolean().Should().BeFalse();

            var c = await Json(await cliente.GetAsync($"/api/facturacion/{id}"));

            c.GetProperty("periodo").GetString().Should().Be(periodo);
            c.GetProperty("estado").GetString().Should().Be(EstadoComprobante.Borrador);
            // La API omite las propiedades nulas, así que «sin número» es que
            // la propiedad no venga, o que venga en null.
            var numero = c.TryGetProperty("numero", out var n) ? n.ValueKind : JsonValueKind.Undefined;
            numero.Should().BeOneOf([JsonValueKind.Undefined, JsonValueKind.Null],
                "un borrador todavía no tiene número");

            // El desglose tiene que volver de la base tal como se guardó, y con
            // el total siendo la suma de lo que se muestra.
            var lineas = c.GetProperty("lineas");
            lineas.GetArrayLength().Should().BeGreaterThan(0);

            var subtotal = c.GetProperty("subtotal").GetDecimal();
            var sumaLineas = lineas.EnumerateArray().Sum(l => l.GetProperty("importe").GetDecimal());
            sumaLineas.Should().Be(subtotal);

            var iva = c.GetProperty("iva").GetDecimal();
            c.GetProperty("total").GetDecimal().Should().Be(subtotal + iva);

            // La restricción de unicidad del período está en la base, no sólo
            // en el negocio: dos pedidos simultáneos pasarían los dos por la
            // validación.
            var repetido = await Json(
                await cliente.PostAsync($"/api/facturacion?periodo={periodo}", null));
            repetido.GetProperty("yaExistia").GetBoolean().Should().BeTrue();
            repetido.GetProperty("id").GetGuid().Should().Be(id);

            // Y aparece en el listado filtrado por período, que es otra consulta.
            var listado = await Json(
                await cliente.GetAsync($"/api/facturacion?periodo={periodo}"));
            listado.GetArrayLength().Should().Be(1);
        }
        finally
        {
            // Se descarta el borrador: nunca tuvo número, así que no deja hueco
            // en la numeración y la corrida siguiente arranca igual que ésta.
            if (id != default)
            {
                await cliente.DeleteAsync(
                    $"/api/facturacion/{id}?motivo=Prueba automatizada del CP-17");
            }
        }
    }

    [Fact]
    public async Task El_ajuste_sobrevive_a_la_ida_y_vuelta_a_la_base()
    {
        var cliente = await Entrar("admin", "Admin.2026");
        var periodo = Periodo.Mover(Periodo.De(DateOnly.FromDateTime(DateTime.Today)), -31).ToString();
        Guid id = default;

        try
        {
            id = (await Json(await cliente.PostAsync($"/api/facturacion?periodo={periodo}", null)))
                 .GetProperty("id").GetGuid();

            var antes = (await Json(await cliente.GetAsync($"/api/facturacion/{id}")))
                        .GetProperty("total").GetDecimal();

            await cliente.PostAsJsonAsync($"/api/facturacion/{id}/ajustes",
                new { concepto = "Descuento por pago adelantado", importe = -15000m });

            // Se relee de la base y no se mira la respuesta del POST: lo que
            // interesa es que la línea nueva haya quedado guardada.
            var c = await Json(await cliente.GetAsync($"/api/facturacion/{id}"));

            c.GetProperty("total").GetDecimal().Should().BeLessThan(antes);
            c.GetProperty("lineas").EnumerateArray()
             .Should().Contain(l => l.GetProperty("esAjuste").GetBoolean());
        }
        finally
        {
            if (id != default)
            {
                await cliente.DeleteAsync(
                    $"/api/facturacion/{id}?motivo=Prueba automatizada del CP-17");
            }
        }
    }

    [Fact]
    public async Task Dos_borradores_de_meses_distintos_conviven()
    {
        // El correlativo es único por organización, pero sólo entre los que
        // tienen número. Con un UNIQUE sobre (organización, número) SQL Server
        // trata el NULL como un valor más, así que el segundo borrador rompía
        // con un error del motor. Se comprueba contra la base porque la
        // restricción vive ahí y ningún doble la reproduce.
        var cliente = await Entrar("admin", "Admin.2026");
        var hoy = Periodo.De(DateOnly.FromDateTime(DateTime.Today));
        var uno = Periodo.Mover(hoy, -32).ToString();
        var otro = Periodo.Mover(hoy, -33).ToString();

        Guid idUno = default, idOtro = default;

        try
        {
            idUno = (await Json(await cliente.PostAsync($"/api/facturacion?periodo={uno}", null)))
                    .GetProperty("id").GetGuid();

            var respuesta = await cliente.PostAsync($"/api/facturacion?periodo={otro}", null);
            respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
                "dos meses sin facturar son dos borradores, y ninguno tiene número todavía");

            idOtro = (await Json(respuesta)).GetProperty("id").GetGuid();
            idOtro.Should().NotBe(idUno);
        }
        finally
        {
            foreach (var id in new[] { idUno, idOtro }.Where(x => x != default))
            {
                await cliente.DeleteAsync(
                    $"/api/facturacion/{id}?motivo=Prueba automatizada del CP-17");
            }
        }
    }

    [Fact]
    public async Task Un_tecnico_no_ve_ni_toca_la_facturacion()
    {
        var cliente = await Entrar("tecnico1", "Tecnico.2026");

        // El técnico atiende equipos; lo que la organización paga por el
        // servicio no es asunto suyo.
        (await cliente.GetAsync("/api/facturacion")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await cliente.PostAsync("/api/facturacion", null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }
}
