using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Service.IA;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.IA;

/// <summary>
/// CP-15 · La guía de reparación con el proveedor caído.
///
/// Hasta acá el camino de fallo estaba probado en la capa de servicio: que
/// <see cref="ProveedorIACaido"/> devuelve un resultado con error y que el
/// disyuntor lo cuenta. Eso no alcanza para el caso de prueba, porque lo que
/// el CP-15 afirma es sobre el recorrido completo: que la API contesta 200 y
/// no un 5xx, que dice por qué, que la incidencia no queda tocada y que el
/// intento queda asentado.
///
/// Devolver 200 con la guía marcada como no disponible es deliberado y es lo
/// que se comprueba acá: que el servicio externo esté caído no es un error de
/// esta operación, y un 502 obligaría a la pantalla a distinguir «falló la
/// red» de «falló la IA» para mostrar lo mismo.
///
/// El proveedor se reemplaza en el contenedor de dependencias y no con la
/// variable `PREDICTIT_IA_FALLAR_CADA`, que es lo que se usa para demostrarlo
/// a mano sobre el sistema corriendo: una variable de entorno es del proceso
/// entero y se llevaría puestas las demás pruebas que corren al mismo tiempo.
/// </summary>
[Trait("Categoria", "Integracion")]
[Trait("CP", "CP-15")]
public class GuiaConProveedorCaidoTests : IClassFixture<GuiaConProveedorCaidoTests.ApiSinIa>
{
    /// <summary>La aplicación real con el proveedor de IA que nunca responde.</summary>
    public class ApiSinIa : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices(servicios =>
            {
                servicios.RemoveAll<IProveedorIA>();
                servicios.AddScoped<IProveedorIA>(_ => new ProveedorIACaido());
            });
        }
    }

    private readonly ApiSinIa _api;

    public GuiaConProveedorCaidoTests(ApiSinIa api) => _api = api;

    private static IFactoryDaoSeguridad Seguridad() =>
        new FactoryDaoSeguridad(new DAO.Helpers.ConexionesSql(
            EntornoPruebas.CadenaNegocio, EntornoPruebas.CadenaServicio));

    /// <summary>Entra como técnico, que es quien pide la guía.</summary>
    private async Task<HttpClient> Autenticado()
    {
        var cliente = _api.CreateClient();
        var r = await cliente.PostAsJsonAsync("/api/auth/login",
            new { username = "tecnico1", contrasena = "Tecnico.2026" });
        r.StatusCode.Should().Be(HttpStatusCode.OK, "el técnico del seed tiene que poder entrar");

        var token = JsonDocument.Parse(await r.Content.ReadAsStringAsync())
                                .RootElement.GetProperty("token").GetString();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    /// <summary>Una incidencia abierta cualquiera de la organización del técnico.</summary>
    private static async Task<(Guid Id, string Estado, string? Diagnostico)> UnaIncidencia(HttpClient cliente)
    {
        var lista = JsonDocument.Parse(
            await cliente.GetStringAsync("/api/incidencias?abiertas=true&porPagina=1"));
        var item = lista.RootElement.GetProperty("items")[0];
        var id = item.GetProperty("id").GetGuid();

        var detalle = JsonDocument.Parse(await cliente.GetStringAsync($"/api/incidencias/{id}")).RootElement;
        return (id,
                detalle.GetProperty("estado").GetString()!,
                detalle.TryGetProperty("diagnostico", out var d) ? d.GetString() : null);
    }

    [Fact]
    public async Task Con_el_proveedor_caido_la_guia_llega_con_el_motivo_y_no_como_error()
    {
        var cliente = await Autenticado();
        var (id, estadoAntes, diagnosticoAntes) = await UnaIncidencia(cliente);

        var respuesta = await cliente.GetAsync($"/api/incidencias/{id}/guia-reparacion");

        // 1 · La operación no falla. Que el proveedor esté caído es una
        //     respuesta posible, no un error del sistema.
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var guia = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;

        // 2 · Viene marcada como no disponible y con el motivo. Una lista vacía
        //     dejaría al técnico pensando que el sistema no tiene nada para
        //     sugerir, que es distinto de que el proveedor no haya contestado.
        guia.GetProperty("disponible").GetBoolean().Should().BeFalse();
        guia.GetProperty("motivo").GetString().Should().NotBeNullOrWhiteSpace();
        guia.GetProperty("pasos").GetArrayLength().Should().Be(0);

        // 3 · La incidencia no quedó tocada: pedir una guía es una consulta.
        var (_, estadoDespues, diagnosticoDespues) = await UnaIncidencia(cliente);
        estadoDespues.Should().Be(estadoAntes);
        diagnosticoDespues.Should().Be(diagnosticoAntes);
    }

    [Fact]
    public async Task El_intento_fallido_queda_asentado_en_la_bitacora()
    {
        var cliente = await Autenticado();
        var (id, _, _) = await UnaIncidencia(cliente);

        var desde = DateTime.Now.AddMinutes(-1);
        (await cliente.GetAsync($"/api/incidencias/{id}/guia-reparacion"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Se busca por la entidad y no por el código de evento: lo que el caso
        // de prueba afirma es que la consulta quedó registrada, y de qué clase
        // fue lo dice el propio asiento.
        var enLaVentana = Seguridad().Bitacora
            .Consultar(desde, DateTime.Now.AddMinutes(1), Datos.Estudio, null);
        var entradas = enLaVentana.Where(b => b.IdEntidad == id).ToList();

        // El detalle del mensaje no es adorno: la suite corre en paralelo y
        // esta prueba lee una tabla que escriben todas las demás. Si alguna vez
        // falla, hay que poder distinguir «no se escribió nada» de «se escribió
        // sobre otra entidad» sin tener que reproducir la corrida.
        entradas.Should().NotBeEmpty(
            "pedir la guía tiene que quedar asentado aunque el proveedor no responda: "
            + "si no, no hay forma de saber cuántas veces se intentó. "
            + $"En la ventana hubo {enLaVentana.Count} asiento(s) de la organización "
            + $"y ninguno sobre la incidencia {id}");
    }
}
