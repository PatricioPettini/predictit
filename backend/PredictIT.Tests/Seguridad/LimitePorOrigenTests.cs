using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PredictIT.Api.Infraestructura;

namespace PredictIT.Tests.Seguridad;

/// <summary>
/// CP-02b · El login corta por dirección de origen, además de por usuario.
///
/// El bloqueo por usuario ya estaba probado y no cubre el caso que importa
/// acá. Un ataque que prueba la misma contraseña contra muchos usuarios nunca
/// junta cinco intentos en una sola cuenta, así que ese bloqueo no dispara
/// nunca: lo que lo corta es el límite por origen.
/// </summary>
public class LimitePorOrigenTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _api;

    public LimitePorOrigenTests(WebApplicationFactory<Program> api) => _api = api;

    [Fact]
    public async Task El_login_rechaza_despues_de_muchos_intentos_desde_el_mismo_origen()
    {
        var cliente = _api.CreateClient();

        // Cada intento usa un usuario distinto: es el ataque que el bloqueo por
        // cuenta no ve. Si el límite por origen no estuviera, los veinte
        // pasarían y ninguno daría 429.
        var codigos = new List<HttpStatusCode>();

        for (var i = 0; i < 20; i++)
        {
            var r = await cliente.PostAsJsonAsync("/api/auth/login", new
            {
                username = $"inexistente{i}",
                contrasena = "Cualquiera.2026"
            });

            codigos.Add(r.StatusCode);
            if (r.StatusCode == HttpStatusCode.TooManyRequests) break;
        }

        codigos.Should().Contain(HttpStatusCode.TooManyRequests,
            "veinte intentos en menos de un minuto tienen que dar con el límite por origen");
    }

    [Fact]
    public void La_politica_del_limite_tiene_un_nombre_compartido()
    {
        // El atributo del controlador y la configuración de Program.cs tienen
        // que nombrar la misma política. Si se separaran, la política dejaría
        // de aplicarse sin que nada falle: no rompe el arranque, no rompe
        // ninguna prueba, y el endpoint queda sin límite.
        Politicas.IntentosDeLogin.Should().NotBeNullOrWhiteSpace();

        var atributo = typeof(PredictIT.Api.Controllers.AutenticacionController)
            .GetMethod(nameof(PredictIT.Api.Controllers.AutenticacionController.Login))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), false)
            .Cast<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()
            .SingleOrDefault();

        atributo.Should().NotBeNull("el login tiene que declarar el límite por origen");
        atributo!.PolicyName.Should().Be(Politicas.IntentosDeLogin);
    }
}
