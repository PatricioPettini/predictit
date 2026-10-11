using FluentAssertions;
using PredictIT.Service.IA;
using Xunit;

namespace PredictIT.Tests.IA;

/// <summary>
/// Disyuntor sobre el proveedor de IA (ADR 0009 · CP-14).
///
/// El reloj es una función que estas pruebas controlan: probar los 60 segundos de
/// espera esperándolos de verdad haría la suite inservible.
/// </summary>
public class DisyuntorTests
{
    private DateTime _ahora = new(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);

    private Disyuntor Nuevo(int fallos = 5, int segundos = 60) =>
        new(fallos, TimeSpan.FromSeconds(segundos), () => _ahora);

    private void Avanzar(int segundos) => _ahora = _ahora.AddSeconds(segundos);

    [Fact]
    public void Arranca_cerrado_y_deja_pasar()
    {
        var d = Nuevo();

        d.Estado.Should().Be(EstadoDisyuntor.Cerrado);
        d.PermiteIntentar().Should().BeTrue();
    }

    [Fact]
    public void Abre_recien_al_quinto_fallo_consecutivo()
    {
        var d = Nuevo(fallos: 5);

        for (var i = 1; i <= 4; i++)
        {
            d.RegistrarFallo().Should().BeFalse($"el fallo {i} no debería abrir todavía");
            d.Estado.Should().Be(EstadoDisyuntor.Cerrado);
        }

        d.RegistrarFallo().Should().BeTrue("el quinto fallo abre el circuito");
        d.Estado.Should().Be(EstadoDisyuntor.Abierto);
        d.PermiteIntentar().Should().BeFalse();
    }

    [Fact]
    public void Un_exito_reinicia_la_cuenta_de_fallos()
    {
        // «Consecutivos» es la clave: cuatro fallos salteados a lo largo del día
        // no son un servicio caído.
        var d = Nuevo(fallos: 5);

        d.RegistrarFallo();
        d.RegistrarFallo();
        d.RegistrarFallo();
        d.RegistrarFallo();
        d.RegistrarExito();

        d.FallosConsecutivos.Should().Be(0);

        d.RegistrarFallo();
        d.Estado.Should().Be(EstadoDisyuntor.Cerrado);
    }

    [Fact]
    public void Abierto_no_deja_pasar_hasta_que_vence_la_espera()
    {
        var d = Nuevo(fallos: 2, segundos: 60);
        d.RegistrarFallo();
        d.RegistrarFallo();

        Avanzar(59);
        d.Estado.Should().Be(EstadoDisyuntor.Abierto);
        d.PermiteIntentar().Should().BeFalse();

        Avanzar(2);
        d.Estado.Should().Be(EstadoDisyuntor.Semiabierto);
    }

    [Fact]
    public void Semiabierto_deja_pasar_una_sola_llamada_de_prueba()
    {
        // Si dejara pasar todas, al vencer la espera se dispararía una avalancha
        // contra un servicio que quizás sigue caído.
        var d = Nuevo(fallos: 2, segundos: 60);
        d.RegistrarFallo();
        d.RegistrarFallo();
        Avanzar(61);

        d.PermiteIntentar().Should().BeTrue("la primera es la llamada de prueba");
        d.PermiteIntentar().Should().BeFalse("la segunda no pasa hasta saber el resultado");
    }

    [Fact]
    public void Si_la_llamada_de_prueba_funciona_el_circuito_cierra()
    {
        var d = Nuevo(fallos: 2, segundos: 60);
        d.RegistrarFallo();
        d.RegistrarFallo();
        Avanzar(61);

        d.PermiteIntentar();
        d.RegistrarExito();

        d.Estado.Should().Be(EstadoDisyuntor.Cerrado);
        d.AbiertoDesde.Should().BeNull();
        d.PermiteIntentar().Should().BeTrue();
    }

    [Fact]
    public void Si_la_llamada_de_prueba_falla_el_circuito_vuelve_a_abrir_y_reinicia_la_espera()
    {
        var d = Nuevo(fallos: 2, segundos: 60);
        d.RegistrarFallo();
        d.RegistrarFallo();
        Avanzar(61);

        d.PermiteIntentar();
        d.RegistrarFallo();

        d.Estado.Should().Be(EstadoDisyuntor.Abierto);

        // La espera se cuenta de nuevo desde este fallo, no desde el original.
        Avanzar(59);
        d.Estado.Should().Be(EstadoDisyuntor.Abierto);
        Avanzar(2);
        d.Estado.Should().Be(EstadoDisyuntor.Semiabierto);
    }

    [Fact]
    public void Abre_una_sola_vez_aunque_se_sigan_registrando_fallos()
    {
        // Devuelve true sólo en la transición: el llamador asienta en bitácora
        // cuando esto es true, y no queremos una línea por intento.
        var d = Nuevo(fallos: 2);

        d.RegistrarFallo();
        d.RegistrarFallo().Should().BeTrue();
        d.RegistrarFallo().Should().BeFalse();
        d.RegistrarFallo().Should().BeFalse();
    }

    [Fact]
    public void Reiniciar_lo_devuelve_al_estado_inicial()
    {
        var d = Nuevo(fallos: 2);
        d.RegistrarFallo();
        d.RegistrarFallo();

        d.Reiniciar();

        d.Estado.Should().Be(EstadoDisyuntor.Cerrado);
        d.PermiteIntentar().Should().BeTrue();
    }
}
