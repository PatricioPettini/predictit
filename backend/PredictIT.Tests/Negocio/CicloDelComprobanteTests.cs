using FluentAssertions;
using PredictIT.BLL;
using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.BLL.Implementations;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;
using PredictIT.Tests.Dobles;

namespace PredictIT.Tests.Negocio;

/// <summary>
/// El ciclo de un comprobante del servicio (RF-17 · CU-021).
///
/// El comprobante nace como borrador y se emite en un segundo paso. Ese corte
/// es el que hace auditable la facturación: hasta la emisión es un cálculo que
/// se puede rehacer, y después es lo que se le reclama a la organización, con
/// número y fecha, que sólo se deshace anulándolo con su motivo.
///
/// Lo que se prueba acá son las reglas que protegen ese corte. Que el importe
/// salga bien del plan es la parte fácil; lo que rompe una facturación es que
/// un emitido se pueda modificar, que un período se facture dos veces o que
/// queden huecos en la numeración.
/// </summary>
[Trait("Categoria", "Negocio")]
public class CicloDelComprobanteTests
{
    private static readonly Guid IdPlan = Guid.NewGuid();

    private static PlanComercial Plan() => new()
    {
        Id = IdPlan,
        Codigo = "ESTANDAR",
        Nombre = "Plan Estándar",
        AbonoMensual = 100_000m,
        EquiposIncluidos = 25,
        PrecioEquipoAdicional = 2_500m,
    };

    private static (FacturacionBusiness Negocio, FacturacionDaoFalso Dao, BitacoraFalsa Bitacora)
        Armar(int equipos = 30, params string[] patentes)
    {
        var facturacion = new FacturacionDaoFalso { Equipos = equipos };
        var contexto = new ContextoFalso().Con(patentes.Length > 0
            ? patentes
            : [Patentes.FacturacionVer, Patentes.FacturacionGestionar]);
        var bitacora = new BitacoraFalsa();

        var negocio = new FacturacionBusiness(
            new FabricaSeguridadFalsa(new UsuarioDaoFalso()),
            contexto,
            new FabricaFalsa(new IncidenciaDaoFalso(), new CatalogoDaoFalso(),
                             facturacion: facturacion,
                             organizaciones: new OrganizacionDaoFalso(Plan())),
            bitacora);

        return (negocio, facturacion, bitacora);
    }

    /// <summary>El mes cerrado, que es el que corresponde facturar.</summary>
    private static string Sugerido =>
        Periodo.AnteriorA(DateOnly.FromDateTime(DateTime.Today)).ToString();

    [Fact]
    public void El_borrador_desglosa_el_abono_y_los_equipos_adicionales()
    {
        var (negocio, _, _) = Armar(equipos: 30);

        var generado = negocio.Generar(null);
        var c = negocio.Comprobante(generado.Id);

        c.Periodo.Should().Be(Sugerido);
        c.Estado.Should().Be(EstadoComprobante.Borrador);
        c.Numero.Should().BeNull("numerar algo que todavía se puede borrar deja huecos");

        c.Lineas.Should().HaveCount(2);
        c.Lineas[0].Importe.Should().Be(100_000m);
        // Cinco equipos por encima de los 25 incluidos, a 2.500 cada uno.
        c.Lineas[1].Cantidad.Should().Be(5);
        c.Lineas[1].Importe.Should().Be(12_500m);

        c.Subtotal.Should().Be(112_500m);
        c.Iva.Should().Be(23_625m);
        c.Total.Should().Be(136_125m);
    }

    [Fact]
    public void Sin_equipos_adicionales_el_comprobante_es_solo_el_abono()
    {
        var (negocio, _, _) = Armar(equipos: 20);

        var c = negocio.Comprobante(negocio.Generar(null).Id);

        c.Lineas.Should().ContainSingle("una línea de cero adicionales diría algo que no pasó");
        c.EquiposAdicionales.Should().Be(0);
        c.Total.Should().Be(121_000m);
    }

    [Fact]
    public void Generar_dos_veces_el_mismo_periodo_no_duplica_nada()
    {
        var (negocio, dao, _) = Armar();

        var primera = negocio.Generar(null);
        var segunda = negocio.Generar(null);

        segunda.Id.Should().Be(primera.Id);
        segunda.YaExistia.Should().BeTrue("sin ese dato, volver a generar parece no haber hecho nada");
        dao.Comprobantes.Should().ContainSingle();
    }

    [Fact]
    public void No_se_factura_un_periodo_que_todavia_no_empezo()
    {
        var (negocio, _, _) = Armar();
        var futuro = Periodo.Mover(Periodo.De(DateOnly.FromDateTime(DateTime.Today)), 2).ToString();

        var acto = () => negocio.Generar(futuro);

        acto.Should().Throw<BusinessException>().WithMessage("*todavía no empezó*");
    }

    [Fact]
    public void Emitir_le_pone_numero_fecha_y_vencimiento()
    {
        var (negocio, _, bitacora) = Armar();
        var borrador = negocio.Generar(null);

        var emitido = negocio.Emitir(borrador.Id);

        emitido.Estado.Should().Be(EstadoComprobante.Emitido);
        emitido.Numero.Should().Be(1);
        emitido.FechaEmision.Should().Be(DateOnly.FromDateTime(DateTime.Today));
        emitido.FechaVencimiento.Should().BeAfter(emitido.FechaEmision!.Value);

        bitacora.Eventos.Should().Contain(e => e.Codigo == TipoEvento.Codigos.ComprobanteEmitido);
    }

    [Fact]
    public void La_numeracion_es_correlativa_y_no_deja_huecos()
    {
        var (negocio, _, _) = Armar();

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var uno = negocio.Generar(Periodo.Mover(Periodo.De(hoy), -3).ToString());
        var dos = negocio.Generar(Periodo.Mover(Periodo.De(hoy), -2).ToString());
        var tres = negocio.Generar(Periodo.Mover(Periodo.De(hoy), -1).ToString());

        negocio.Emitir(uno.Id).Numero.Should().Be(1);
        // El del medio se descarta en borrador: como nunca tuvo número, no deja
        // hueco en la numeración.
        negocio.Anular(dos.Id, "Se facturó por otro canal");
        negocio.Emitir(tres.Id).Numero.Should().Be(2);
    }

    [Fact]
    public void Un_comprobante_emitido_no_se_modifica()
    {
        var (negocio, _, _) = Armar();
        var id = negocio.Generar(null).Id;
        negocio.Emitir(id);

        var acto = () => negocio.AgregarAjuste(id, new AjusteEntradaDto
        {
            Concepto = "Descuento comercial",
            Importe = -10_000m,
        });

        acto.Should().Throw<BusinessException>().WithMessage("*no se modifica*");
    }

    [Fact]
    public void Un_ajuste_negativo_baja_el_total_y_queda_marcado_como_ajuste()
    {
        var (negocio, _, bitacora) = Armar(equipos: 20);
        var id = negocio.Generar(null).Id;

        var c = negocio.AgregarAjuste(id, new AjusteEntradaDto
        {
            Concepto = "Descuento por pago adelantado",
            Importe = -20_000m,
        });

        // Un descuento entra como línea negativa y no como un tipo de
        // comprobante aparte.
        c.Subtotal.Should().Be(80_000m);
        c.Total.Should().Be(96_800m);
        c.Lineas.Should().ContainSingle(l => l.EsAjuste);
        bitacora.Eventos.Should().Contain(e => e.Codigo == TipoEvento.Codigos.ComprobanteAjustado);
    }

    [Fact]
    public void Un_ajuste_sin_explicacion_o_de_cero_se_rechaza()
    {
        var (negocio, _, _) = Armar();
        var id = negocio.Generar(null).Id;

        var sinConcepto = () => negocio.AgregarAjuste(id, new AjusteEntradaDto
        {
            Concepto = "ok",
            Importe = -1000m,
        });
        var deCero = () => negocio.AgregarAjuste(id, new AjusteEntradaDto
        {
            Concepto = "Ajuste del período",
            Importe = 0m,
        });

        sinConcepto.Should().Throw<BusinessException>();
        deCero.Should().Throw<BusinessException>().WithMessage("*no cambia nada*");
    }

    [Fact]
    public void El_pago_se_registra_solo_sobre_lo_emitido()
    {
        var (negocio, _, bitacora) = Armar();
        var id = negocio.Generar(null).Id;

        var sinEmitir = () => negocio.RegistrarPago(id, null);
        sinEmitir.Should().Throw<BusinessException>().WithMessage("*emitido*");

        negocio.Emitir(id);
        var pagado = negocio.RegistrarPago(id, null);

        pagado.Estado.Should().Be(EstadoComprobante.Pagado);
        pagado.FechaPago.Should().Be(DateOnly.FromDateTime(DateTime.Today));
        bitacora.Eventos.Should().Contain(e => e.Codigo == TipoEvento.Codigos.ComprobantePagado);
    }

    [Fact]
    public void Un_pago_no_puede_ser_futuro_ni_anterior_a_la_emision()
    {
        var (negocio, _, _) = Armar();
        var id = negocio.Generar(null).Id;
        negocio.Emitir(id);

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var futuro = () => negocio.RegistrarPago(id, hoy.AddDays(1));
        var previo = () => negocio.RegistrarPago(id, hoy.AddDays(-1));

        futuro.Should().Throw<BusinessException>().WithMessage("*futura*");
        previo.Should().Throw<BusinessException>().WithMessage("*anterior a la emisión*");
    }

    [Fact]
    public void Anular_un_emitido_lo_deja_anulado_con_su_motivo_y_no_lo_borra()
    {
        var (negocio, dao, _) = Armar();
        var id = negocio.Generar(null).Id;
        negocio.Emitir(id);

        negocio.Anular(id, "Se facturó con el plan equivocado");

        // El número ya se usó: hacerlo desaparecer dejaría un hueco que nadie
        // puede explicar.
        var c = negocio.Comprobante(id);
        c.Estado.Should().Be(EstadoComprobante.Anulado);
        c.Numero.Should().Be(1);
        c.Motivo.Should().Contain("plan equivocado");
        dao.Comprobantes.Should().ContainSingle();
    }

    [Fact]
    public void Un_borrador_anulado_se_descarta_entero()
    {
        var (negocio, dao, _) = Armar();
        var id = negocio.Generar(null).Id;

        negocio.Anular(id, "No corresponde facturar este mes");

        // Nunca tuvo número, así que borrarlo no deja hueco. Y el período queda
        // libre para volver a generarlo.
        dao.Comprobantes.Should().BeEmpty();
        negocio.Generar(null).YaExistia.Should().BeFalse();
    }

    [Fact]
    public void No_se_anula_un_comprobante_ya_cobrado()
    {
        var (negocio, _, _) = Armar();
        var id = negocio.Generar(null).Id;
        negocio.Emitir(id);
        negocio.RegistrarPago(id, null);

        var acto = () => negocio.Anular(id, "Estaba mal calculado");

        // Lo cobrado no se borra: se corrige con un ajuste en el período
        // siguiente, que es lo que deja el rastro de los dos hechos.
        acto.Should().Throw<BusinessException>().WithMessage("*ajuste en el período siguiente*");
    }

    [Fact]
    public void Anular_sin_motivo_no_se_puede()
    {
        var (negocio, _, _) = Armar();
        var id = negocio.Generar(null).Id;

        var acto = () => negocio.Anular(id, "   ");

        acto.Should().Throw<BusinessException>().WithMessage("*motivo*");
    }

    [Fact]
    public void Ver_la_facturacion_no_habilita_a_emitirla()
    {
        var (negocio, _, _) = Armar(30, Patentes.FacturacionVer);

        var acto = () => negocio.Generar(null);

        // Quien administra la organización tiene que poder consultar qué le
        // facturaron sin que eso lo habilite a emitir ni a dar por cobrado.
        acto.Should().Throw<PermisoDenegadoException>();
    }

    [Fact]
    public void El_panel_separa_lo_pendiente_de_lo_vencido()
    {
        var (negocio, dao, _) = Armar();
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var vencido = negocio.Generar(Periodo.Mover(Periodo.De(hoy), -2).ToString());
        negocio.Emitir(vencido.Id);
        // Se corre el vencimiento hacia atrás para que quede impago y vencido,
        // que es el caso que el panel tiene que hacer ver.
        dao.Comprobantes.Single(c => c.Id == vencido.Id).FechaVencimiento = hoy.AddDays(-5);

        var alDia = negocio.Generar(Periodo.Mover(Periodo.De(hoy), -1).ToString());
        negocio.Emitir(alDia.Id);

        var panel = negocio.Panel();

        panel.Emitidos.Should().Be(2);
        panel.Vencidos.Should().Be(1);
        panel.TotalVencido.Should().BeLessThan(panel.TotalPendiente);
        panel.PeriodoSugerido.Should().Be(Sugerido);
        panel.PeriodoSugeridoFacturado.Should().BeTrue();
    }
}
