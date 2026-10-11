using FluentAssertions;
using PredictIT.Domain.Negocio;

namespace PredictIT.Tests.Negocio;

/// <summary>
/// El período facturable (RF-17).
///
/// Existe como tipo propio y no como dos enteros sueltos porque «2026-09» se
/// arma, se lee y se mueve en varios lugares —el borrador, el panel, el filtro
/// de la pantalla—, y cada uno con su criterio sobre el cero de los meses de un
/// dígito daría períodos que no se cruzan entre sí.
///
/// Lo que se prueba es el cruce de año, que es donde fallan las cuentas de
/// meses escritas a mano.
/// </summary>
[Trait("Categoria", "Negocio")]
public class PeriodoFacturableTests
{
    [Fact]
    public void Se_escribe_con_el_mes_en_dos_digitos()
    {
        new Periodo(2026, 9).ToString().Should().Be("2026-09");
        new Periodo(2026, 12).ToString().Should().Be("2026-12");
    }

    [Theory]
    [InlineData(2026, 1, -1, 2025, 12)]   // hacia atrás cruzando el año
    [InlineData(2026, 12, 1, 2027, 1)]    // hacia adelante cruzando el año
    [InlineData(2026, 9, -11, 2025, 10)]  // los doce meses del panel
    [InlineData(2026, 3, 0, 2026, 3)]
    public void Moverse_entre_meses_cruza_bien_el_ano(
        int anio, int mes, int meses, int esperadoAnio, int esperadoMes)
    {
        var r = Periodo.Mover(new Periodo(anio, mes), meses);

        r.Anio.Should().Be(esperadoAnio);
        r.Mes.Should().Be(esperadoMes);
    }

    [Fact]
    public void El_periodo_a_facturar_es_el_mes_cerrado_y_no_el_corriente()
    {
        // Un mes en curso todavía puede sumar o perder equipos, y el
        // comprobante mide el parque al cierre.
        Periodo.AnteriorA(new DateOnly(2026, 1, 4)).ToString().Should().Be("2025-12");
        Periodo.AnteriorA(new DateOnly(2026, 9, 30)).ToString().Should().Be("2026-08");
    }

    [Fact]
    public void El_cierre_es_el_ultimo_dia_del_mes()
    {
        new Periodo(2026, 2).Cierre.Should().Be(new DateOnly(2026, 2, 28));
        // 2028 es bisiesto: la cuenta sale del calendario y no de una tabla
        // de días por mes escrita a mano.
        new Periodo(2028, 2).Cierre.Should().Be(new DateOnly(2028, 2, 29));
        new Periodo(2026, 12).Cierre.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void El_rango_del_periodo_no_deja_ni_repite_dias()
    {
        var p = new Periodo(2026, 9);

        p.Desde.Should().Be(new DateOnly(2026, 9, 1));
        // `Hasta` es exclusivo y arranca el día uno del siguiente: así dos
        // períodos consecutivos no comparten ningún día ni dejan uno afuera.
        p.Hasta.Should().Be(new DateOnly(2026, 10, 1));
        p.Hasta.Should().Be(Periodo.Mover(p, 1).Desde);
    }

    [Theory]
    [InlineData("2026-09", 2026, 9)]
    [InlineData("2026-01", 2026, 1)]
    public void Lee_el_texto_bien_formado(string texto, int anio, int mes)
    {
        var p = Periodo.Leer(texto);

        p.Should().NotBeNull();
        p!.Value.Anio.Should().Be(anio);
        p.Value.Mes.Should().Be(mes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-13")]   // mes que no existe
    [InlineData("2026-00")]
    [InlineData("2026/09")]   // otro separador
    [InlineData("26-09")]
    [InlineData("2026-9")]    // sin el cero, que es el error de armarlo a mano
    [InlineData("1999-09")]
    public void Rechaza_lo_que_no_es_un_periodo(string? texto)
    {
        // Devuelve null y no lanza: el texto llega de la dirección de la
        // pantalla, y un filtro mal escrito no es un error del sistema.
        Periodo.Leer(texto).Should().BeNull();
    }
}
