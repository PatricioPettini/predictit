using FluentAssertions;
using PredictIT.Service.IA;

namespace PredictIT.Tests.IA;

/// <summary>
/// La guía de reparación es asistencia al técnico, no una acción del sistema.
/// Lo que se prueba acá es que esa promesa se sostenga: que los pasos vengan en
/// un orden que no rompa nada, que el riesgo esté declarado donde existe, y que
/// el historial del equipo pese cuando lo hay.
/// </summary>
public class GuiaReparacionTests
{
    private static ContextoReparacion Caso(
        string titulo = "La impresora no imprime",
        string descripcion = "Mando a imprimir y no sale nada, la impresora tiene luz verde",
        string? categoria = "Periféricos",
        IReadOnlyList<AntecedenteEquipo>? antecedentes = null,
        int? antiguedad = null) => new()
        {
            TituloIncidencia = titulo,
            DescripcionIncidencia = descripcion,
            Categoria = categoria,
            Prioridad = "Media",
            CodigoEquipo = "PC-014",
            TipoEquipo = "Computadora de escritorio",
            SistemaOperativo = "Windows 11",
            AntiguedadMeses = antiguedad,
            Antecedentes = antecedentes ?? [],
        };

    [Fact]
    public async Task Devuelve_pasos_numerados_sin_saltos()
    {
        var resultado = await new ProveedorIASimulado().SugerirReparacionAsync(Caso());

        resultado.Ok.Should().BeTrue();
        var pasos = resultado.Valor!.Pasos;
        pasos.Should().NotBeEmpty();
        pasos.Select(p => p.Orden).Should().Equal(Enumerable.Range(1, pasos.Count));
        pasos.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Titulo));
    }

    [Fact]
    public async Task Los_pasos_con_riesgo_van_despues_de_los_que_no_lo_tienen()
    {
        // Es la regla que hace utilizable la guía: un procedimiento que arranca
        // apagando el equipo o vaciando la cola de impresión es peor que ninguno.
        foreach (var categoria in new[] { "Red", "Hardware", "Software", "Periféricos", "Energía", "Rendimiento" })
        {
            var resultado = await new ProveedorIASimulado().SugerirReparacionAsync(
                Caso(categoria: categoria));

            var pasos = resultado.Valor!.Pasos;
            var primeroConRiesgo = pasos
                .Select((p, i) => (p, i))
                .Where(x => !string.IsNullOrWhiteSpace(x.p.Riesgo))
                .Select(x => (int?)x.i)
                .FirstOrDefault();

            if (primeroConRiesgo is { } corte)
            {
                pasos.Skip(corte).Should().OnlyContain(
                    p => !string.IsNullOrWhiteSpace(p.Riesgo),
                    $"en «{categoria}» los pasos con riesgo tienen que quedar al final");
            }
        }
    }

    [Fact]
    public async Task Empieza_por_el_antecedente_cuando_el_equipo_ya_tuvo_la_falla()
    {
        var resultado = await new ProveedorIASimulado().SugerirReparacionAsync(Caso(
            antecedentes: [new AntecedenteEquipo(41, "No imprimía", "Cola trabada",
                                                 "Se reinició el servicio de cola de impresión")]));

        var primero = resultado.Valor!.Pasos[0];
        primero.Titulo.Should().Contain("#41");
        primero.Detalle.Should().Contain("cola de impresión");
        // Con historial la confianza sube: hay algo concreto sobre esta máquina.
        resultado.Valor.Confianza.Should().BeGreaterThan(0.5m);
    }

    [Fact]
    public async Task Sin_categoria_la_deduce_del_texto()
    {
        var resultado = await new ProveedorIASimulado().SugerirReparacionAsync(Caso(
            titulo: "La máquina está lentísima",
            descripcion: "Tarda mucho en abrir todo, se cuelga cada dos por tres",
            categoria: null));

        resultado.Ok.Should().BeTrue();
        resultado.Valor!.Resumen.Should().Contain("rendimiento");
    }

    [Fact]
    public async Task Sin_categoria_ni_pistas_devuelve_la_guia_generica_y_lo_dice()
    {
        var resultado = await new ProveedorIASimulado().SugerirReparacionAsync(Caso(
            titulo: "Consulta", descripcion: "Necesito ayuda", categoria: null));

        resultado.Ok.Should().BeTrue();
        resultado.Valor!.Pasos.Should().NotBeEmpty();
        resultado.Valor.Resumen.Should().Contain("no alcanzó");
        // La confianza baja es parte de la respuesta: decir 0,9 acá sería mentir.
        resultado.Valor.Confianza.Should().BeLessThan(0.5m);
    }

    [Fact]
    public async Task Un_equipo_pasado_de_vida_util_avisa_antes_de_gastar_horas()
    {
        var resultado = await new ProveedorIASimulado().SugerirReparacionAsync(Caso(
            categoria: "Hardware", antiguedad: 62));

        resultado.Valor!.Resumen.Should().Contain("reposición");
    }

    [Fact]
    public async Task Siempre_dice_cuando_escalar()
    {
        var resultado = await new ProveedorIASimulado().SugerirReparacionAsync(Caso());
        resultado.Valor!.CuandoEscalar.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Es_deterministico()
    {
        // Sin esto no habría caso de prueba posible, que es la razón por la que
        // el proveedor simulado existe.
        var a = await new ProveedorIASimulado().SugerirReparacionAsync(Caso());
        var b = await new ProveedorIASimulado().SugerirReparacionAsync(Caso());

        a.Valor!.Pasos.Select(p => p.Titulo)
            .Should().Equal(b.Valor!.Pasos.Select(p => p.Titulo));
    }

    [Fact]
    public async Task Cuando_el_proveedor_esta_caido_devuelve_el_motivo_y_no_lanza()
    {
        var resultado = await new ProveedorIACaido().SugerirReparacionAsync(Caso());

        resultado.Ok.Should().BeFalse();
        resultado.Falla.Should().Be(MotivoFalla.Timeout);
        resultado.Detalle.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task El_disyuntor_cubre_tambien_la_guia()
    {
        // Si el circuito abierto no alcanzara a esta operación, una caída del
        // proveedor seguiría golpeándolo una vez por cada incidencia abierta.
        var disyuntor = new Disyuntor(fallosParaAbrir: 2);
        var proveedor = new ProveedorIAConDisyuntor(new ProveedorIACaido(), disyuntor);

        await proveedor.SugerirReparacionAsync(Caso());
        await proveedor.SugerirReparacionAsync(Caso());
        var tercera = await proveedor.SugerirReparacionAsync(Caso());

        disyuntor.Estado.Should().Be(EstadoDisyuntor.Abierto);
        tercera.Falla.Should().Be(MotivoFalla.CircuitoAbierto);
    }
}
