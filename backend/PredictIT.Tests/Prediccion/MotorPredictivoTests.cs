using FluentAssertions;
using PredictIT.Domain.Negocio;
using PredictIT.Service.Prediccion;
using Xunit;

namespace PredictIT.Tests.Prediccion;

/// <summary>
/// Motor de análisis predictivo (RF-11, RF-12, CU-010 · CP-10).
///
/// Todas las pruebas fijan la fecha de referencia. El motor no lee el reloj, así
/// que el escenario es exacto y el resultado no cambia según el día en que se
/// corran las pruebas.
/// </summary>
public class MotorPredictivoTests
{
    private static readonly DateTime Hoy = new(2026, 9, 1, 12, 0, 0);
    private static readonly Guid OrgId = Guid.Parse("A0000000-0000-0000-0000-000000000001");

    private static readonly TipoMantenimiento Preventivo =
        new() { Id = Guid.NewGuid(), Nombre = "Preventivo", EsPreventivo = true };

    private static readonly TipoMantenimiento Correctivo =
        new() { Id = Guid.NewGuid(), Nombre = "Correctivo", EsPreventivo = false };

    private static Equipo Equipo(
        int criticidad = 2,
        DateOnly? adquisicion = null,
        DateOnly? finGarantia = null,
        DateTime? alta = null) => new()
        {
            Id = Guid.NewGuid(),
            IdOrganizacion = OrgId,
            Codigo = "PC-ADM-014",
            Criticidad = criticidad,
            FechaAdquisicion = adquisicion,
            FechaFinGarantia = finGarantia,
            FechaAlta = alta ?? Hoy.AddYears(-1),
        };

    private static Incidencia Incidencia(int diasAtras, Guid? categoria = null) => new()
    {
        Id = Guid.NewGuid(),
        IdOrganizacion = OrgId,
        Fecha = Hoy.AddDays(-diasAtras),
        IdCategoria = categoria,
        Categoria = categoria is null ? null : new CategoriaIncidencia { Id = categoria.Value, Nombre = "Red" },
    };

    private static Mantenimiento Mant(int diasAtras, TipoMantenimiento tipo) => new()
    {
        Id = Guid.NewGuid(),
        IdOrganizacion = OrgId,
        Fecha = Hoy.AddDays(-diasAtras),
        IdTipo = tipo.Id,
        Tipo = tipo,
    };

    private static ReglaAlerta Regla(TipoRegla tipo, string condicion, int peso = 25) => new()
    {
        Id = Guid.NewGuid(),
        IdOrganizacion = OrgId,
        Nombre = tipo.ToString(),
        Tipo = tipo,
        Condicion = condicion,
        Peso = peso,
        Activa = true,
    };

    private static ContextoEquipo Ctx(
        Equipo equipo,
        IEnumerable<Incidencia>? incidencias = null,
        IEnumerable<Mantenimiento>? mantenimientos = null) =>
        new(equipo,
            (incidencias ?? []).ToList(),
            (mantenimientos ?? []).ToList(),
            Hoy);

    // ------------------------------------------------------- reglas de a una

    [Fact]
    public void RecurrenciaDeFallas_cuenta_solo_dentro_de_la_ventana()
    {
        var regla = Regla(TipoRegla.RecurrenciaFallas, """{"minIncidencias":3,"ventanaDias":30}""");
        var ctx = Ctx(Equipo(), [
            Incidencia(5), Incidencia(10), Incidencia(25),
            Incidencia(45),   // fuera de la ventana: no cuenta
        ]);

        var r = new EstrategiaRecurrenciaFallas().Evaluar(regla, ctx);

        r.Cumple.Should().BeTrue();
        r.Motivo.Should().Contain("3 incidencias");
    }

    [Fact]
    public void RecurrenciaDeFallas_no_dispara_por_debajo_del_umbral_pero_aporta_al_score()
    {
        var regla = Regla(TipoRegla.RecurrenciaFallas, """{"minIncidencias":3,"ventanaDias":30}""");
        var ctx = Ctx(Equipo(), [Incidencia(5), Incidencia(10)]);

        var r = new EstrategiaRecurrenciaFallas().Evaluar(regla, ctx);

        // No amerita alerta todavía, pero dos de tres no es lo mismo que cero:
        // el score tiene que reflejar la tendencia antes de que se cruce el umbral.
        r.Cumple.Should().BeFalse();
        r.Intensidad.Should().BeApproximately(2d / 3d, 0.001);
    }

    [Fact]
    public void Acumulacion_con_mismaCategoria_agrupa_y_toma_la_categoria_mas_repetida()
    {
        var red = Guid.NewGuid();
        var otra = Guid.NewGuid();
        var regla = Regla(TipoRegla.AcumulacionIncidencias,
            """{"minIncidencias":2,"ventanaDias":90,"mismaCategoria":true}""");

        var ctx = Ctx(Equipo(), [
            Incidencia(5, red), Incidencia(20, red),
            Incidencia(30, otra),
        ]);

        var r = new EstrategiaAcumulacionIncidencias().Evaluar(regla, ctx);

        r.Cumple.Should().BeTrue();
        r.Motivo.Should().Contain("2 incidencias");
    }

    [Fact]
    public void Acumulacion_con_mismaCategoria_no_agrupa_las_incidencias_sin_clasificar()
    {
        // Tres incidencias sin categoría no son «la misma falla tres veces»: es
        // el dato que falta. Agruparlas generaría una alerta inventada.
        var regla = Regla(TipoRegla.AcumulacionIncidencias,
            """{"minIncidencias":2,"ventanaDias":90,"mismaCategoria":true}""");

        var ctx = Ctx(Equipo(), [Incidencia(5), Incidencia(20), Incidencia(30)]);

        var r = new EstrategiaAcumulacionIncidencias().Evaluar(regla, ctx);

        r.Cumple.Should().BeFalse();
    }

    [Fact]
    public void Acumulacion_sin_mismaCategoria_cuenta_todas()
    {
        var regla = Regla(TipoRegla.AcumulacionIncidencias,
            """{"minIncidencias":2,"ventanaDias":90,"mismaCategoria":false}""");

        var ctx = Ctx(Equipo(), [Incidencia(5), Incidencia(20)]);

        new EstrategiaAcumulacionIncidencias().Evaluar(regla, ctx).Cumple.Should().BeTrue();
    }

    [Fact]
    public void MantenimientoVencido_ignora_los_correctivos()
    {
        // Un correctivo es la reparación posterior a la falla. Si contara como
        // mantenimiento, el equipo que más se rompe sería el que nunca avisa.
        var regla = Regla(TipoRegla.MantenimientoVencido, """{"diasSinMantenimiento":180}""");
        var ctx = Ctx(
            Equipo(alta: Hoy.AddDays(-400)),
            mantenimientos: [Mant(10, Correctivo)]);

        var r = new EstrategiaMantenimientoVencido().Evaluar(regla, ctx);

        r.Cumple.Should().BeTrue();
        r.Motivo.Should().Contain("sin preventivo registrado");
    }

    [Fact]
    public void MantenimientoVencido_cuenta_desde_el_ultimo_preventivo()
    {
        var regla = Regla(TipoRegla.MantenimientoVencido, """{"diasSinMantenimiento":180}""");
        var ctx = Ctx(
            Equipo(alta: Hoy.AddDays(-400)),
            mantenimientos: [Mant(300, Preventivo), Mant(30, Preventivo)]);

        var r = new EstrategiaMantenimientoVencido().Evaluar(regla, ctx);

        r.Cumple.Should().BeFalse();
        r.Motivo.Should().Contain("30 días");
    }

    [Fact]
    public void Antiguedad_no_aplica_sin_fecha_de_adquisicion()
    {
        var regla = Regla(TipoRegla.AntiguedadEquipo, """{"aniosUmbral":4}""");

        var r = new EstrategiaAntiguedadEquipo().Evaluar(regla, Ctx(Equipo(adquisicion: null)));

        r.Aplicable.Should().BeFalse();
        r.Cumple.Should().BeFalse();
    }

    [Fact]
    public void Antiguedad_dispara_pasado_el_umbral()
    {
        var regla = Regla(TipoRegla.AntiguedadEquipo, """{"aniosUmbral":4}""");
        var ctx = Ctx(Equipo(adquisicion: DateOnly.FromDateTime(Hoy.AddYears(-5))));

        var r = new EstrategiaAntiguedadEquipo().Evaluar(regla, ctx);

        r.Cumple.Should().BeTrue();
        r.Intensidad.Should().Be(1);   // topeado en 1: cinco años no valen 1,25
    }

    [Fact]
    public void Garantia_vencida_aporta_el_maximo()
    {
        var regla = Regla(TipoRegla.GarantiaPorVencer, """{"diasAviso":60}""");
        var ctx = Ctx(Equipo(finGarantia: DateOnly.FromDateTime(Hoy.AddDays(-100))));

        var r = new EstrategiaGarantiaPorVencer().Evaluar(regla, ctx);

        r.Cumple.Should().BeTrue();
        r.Intensidad.Should().Be(1);
        r.Motivo.Should().Contain("vencida hace 100 días");
    }

    [Fact]
    public void Garantia_aporta_mas_cuanto_mas_cerca_del_vencimiento()
    {
        var regla = Regla(TipoRegla.GarantiaPorVencer, """{"diasAviso":60}""");

        var lejos = new EstrategiaGarantiaPorVencer().Evaluar(
            regla, Ctx(Equipo(finGarantia: DateOnly.FromDateTime(Hoy.AddDays(50)))));
        var cerca = new EstrategiaGarantiaPorVencer().Evaluar(
            regla, Ctx(Equipo(finGarantia: DateOnly.FromDateTime(Hoy.AddDays(5)))));

        lejos.Cumple.Should().BeTrue();
        cerca.Intensidad.Should().BeGreaterThan(lejos.Intensidad);
    }

    [Fact]
    public void Garantia_vigente_lejos_del_aviso_no_dispara()
    {
        var regla = Regla(TipoRegla.GarantiaPorVencer, """{"diasAviso":60}""");
        var ctx = Ctx(Equipo(finGarantia: DateOnly.FromDateTime(Hoy.AddDays(300))));

        var r = new EstrategiaGarantiaPorVencer().Evaluar(regla, ctx);

        r.Cumple.Should().BeFalse();
        r.Intensidad.Should().Be(0);
    }

    // -------------------------------------------------------------- el motor

    [Fact]
    public void Un_equipo_sano_da_riesgo_bajo()
    {
        var equipo = Equipo(
            adquisicion: DateOnly.FromDateTime(Hoy.AddMonths(-6)),
            finGarantia: DateOnly.FromDateTime(Hoy.AddYears(2)),
            alta: Hoy.AddMonths(-6));

        var riesgo = new MotorPredictivo().Evaluar(
            Ctx(equipo, mantenimientos: [Mant(15, Preventivo)]),
            ReglasDelSeed());

        riesgo.Nivel.Should().Be(NivelRiesgo.Bajo);
        riesgo.Disparadas.Should().BeEmpty();
    }

    [Fact]
    public void Un_equipo_viejo_con_fallas_y_sin_mantenimiento_da_riesgo_alto()
    {
        var equipo = Equipo(
            criticidad: 4,
            adquisicion: DateOnly.FromDateTime(Hoy.AddYears(-6)),
            finGarantia: DateOnly.FromDateTime(Hoy.AddYears(-2)),
            alta: Hoy.AddYears(-6));

        var categoria = Guid.NewGuid();
        var riesgo = new MotorPredictivo().Evaluar(
            Ctx(equipo, [
                Incidencia(2, categoria), Incidencia(8, categoria),
                Incidencia(15, categoria), Incidencia(22, categoria),
            ]),
            ReglasDelSeed());

        riesgo.Nivel.Should().Be(NivelRiesgo.Alto);
        riesgo.Disparadas.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void El_score_nunca_se_va_de_cero_a_cien()
    {
        var equipo = Equipo(
            criticidad: 4,
            adquisicion: DateOnly.FromDateTime(Hoy.AddYears(-20)),
            finGarantia: DateOnly.FromDateTime(Hoy.AddYears(-15)),
            alta: Hoy.AddYears(-20));

        var cat = Guid.NewGuid();
        var muchas = Enumerable.Range(1, 40).Select(d => Incidencia(d, cat)).ToList();

        var riesgo = new MotorPredictivo().Evaluar(Ctx(equipo, muchas), ReglasDelSeed());

        riesgo.Score.Should().BeInRange(0, 100);
    }

    [Fact]
    public void Las_reglas_inactivas_no_se_evaluan()
    {
        var regla = Regla(TipoRegla.RecurrenciaFallas, """{"minIncidencias":1,"ventanaDias":30}""");
        regla.Activa = false;

        var riesgo = new MotorPredictivo().Evaluar(Ctx(Equipo(), [Incidencia(1)]), [regla]);

        riesgo.Resultados.Should().BeEmpty();
        riesgo.Score.Should().Be(0);
    }

    [Fact]
    public void Una_regla_con_json_roto_no_tumba_la_evaluacion()
    {
        // La condición la edita el usuario desde la pantalla de reglas. Un JSON
        // mal escrito tiene que invalidar esa regla, no el análisis del parque.
        var rota = Regla(TipoRegla.RecurrenciaFallas, "{esto no es json");
        var buena = Regla(TipoRegla.AntiguedadEquipo, """{"aniosUmbral":4}""");

        var equipo = Equipo(adquisicion: DateOnly.FromDateTime(Hoy.AddYears(-5)));
        var riesgo = new MotorPredictivo().Evaluar(Ctx(equipo), [rota, buena]);

        riesgo.Resultados.Should().HaveCount(2);
        riesgo.Resultados.Single(r => r.Regla == rota).Aplicable.Should().BeFalse();
        riesgo.Resultados.Single(r => r.Regla == buena).Cumple.Should().BeTrue();
        riesgo.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Una_regla_no_aplicable_no_diluye_el_score()
    {
        // El caso concreto: dos reglas, una dispara al máximo y la otra no se
        // puede evaluar por falta de datos. Si la no aplicable contara como
        // cero, el score sería la mitad, y el equipo con la ficha incompleta
        // aparecería menos riesgoso que el que está bien cargado.
        var antiguedad = Regla(TipoRegla.AntiguedadEquipo, """{"aniosUmbral":4}""", peso: 50);
        var garantia = Regla(TipoRegla.GarantiaPorVencer, """{"diasAviso":60}""", peso: 50);

        var equipo = Equipo(
            criticidad: 3,   // factor 1.0, para leer el score sin corrección
            adquisicion: DateOnly.FromDateTime(Hoy.AddYears(-8)),
            finGarantia: null);   // la regla de garantía no aplica

        var riesgo = new MotorPredictivo().Evaluar(Ctx(equipo), [antiguedad, garantia]);

        riesgo.Score.Should().Be(100);
    }

    [Fact]
    public void La_criticidad_amplifica_el_score_pero_no_lo_inventa()
    {
        var reglas = ReglasDelSeed();

        // Mismo escenario de fallas, criticidad distinta.
        Equipo Roto(int crit) => Equipo(
            criticidad: crit,
            adquisicion: DateOnly.FromDateTime(Hoy.AddYears(-5)),
            finGarantia: DateOnly.FromDateTime(Hoy.AddYears(-1)),
            alta: Hoy.AddYears(-5));

        var cat = Guid.NewGuid();
        List<Incidencia> fallas = [Incidencia(3, cat), Incidencia(9, cat), Incidencia(20, cat)];

        var monitor = new MotorPredictivo().Evaluar(Ctx(Roto(1), fallas), reglas);
        var servidor = new MotorPredictivo().Evaluar(Ctx(Roto(4), fallas), reglas);

        servidor.Score.Should().BeGreaterThan(monitor.Score);
    }

    [Fact]
    public void La_criticidad_sola_no_pone_en_riesgo_a_un_equipo_sano()
    {
        // La criticidad multiplica, no suma: si sumara sobre una base fija, el
        // servidor nuevo y sano aparecería siempre en riesgo.
        //
        // No se exige score exactamente 0: el equipo tiene un mes de antigüedad
        // y cinco días desde el preventivo, y el score es continuo, así que
        // corresponde que dé un valor mínimo distinto de cero. Lo que importa es
        // que sea despreciable y que no dispare ninguna alerta.
        Equipo Sano(int crit) => Equipo(
            criticidad: crit,
            adquisicion: DateOnly.FromDateTime(Hoy.AddMonths(-1)),
            finGarantia: DateOnly.FromDateTime(Hoy.AddYears(3)),
            alta: Hoy.AddMonths(-1));

        var monitor = new MotorPredictivo().Evaluar(
            Ctx(Sano(1), mantenimientos: [Mant(5, Preventivo)]), ReglasDelSeed());
        var servidor = new MotorPredictivo().Evaluar(
            Ctx(Sano(4), mantenimientos: [Mant(5, Preventivo)]), ReglasDelSeed());

        servidor.Nivel.Should().Be(NivelRiesgo.Bajo);
        servidor.Disparadas.Should().BeEmpty();
        servidor.Score.Should().BeLessThan(5);

        // Y el servidor crítico no puede quedar por debajo del monitor.
        servidor.Score.Should().BeGreaterThanOrEqualTo(monitor.Score);
    }

    [Fact]
    public void Los_pesos_se_normalizan_aunque_no_sumen_cien()
    {
        // Los pesos los configura la organización. Nada garantiza que sumen 100,
        // y el score tiene que seguir siendo una escala de 0 a 100.
        var a = Regla(TipoRegla.AntiguedadEquipo, """{"aniosUmbral":4}""", peso: 7);
        var b = Regla(TipoRegla.GarantiaPorVencer, """{"diasAviso":60}""", peso: 3);

        var equipo = Equipo(
            criticidad: 3,
            adquisicion: DateOnly.FromDateTime(Hoy.AddYears(-9)),
            finGarantia: DateOnly.FromDateTime(Hoy.AddYears(-1)));

        new MotorPredictivo().Evaluar(Ctx(equipo), [a, b]).Score.Should().Be(100);
    }

    [Fact]
    public void Sin_reglas_configuradas_el_score_es_cero_y_no_falla()
    {
        var riesgo = new MotorPredictivo().Evaluar(Ctx(Equipo()), []);

        riesgo.Score.Should().Be(0);
        riesgo.Nivel.Should().Be(NivelRiesgo.Bajo);
    }

    /// <summary>Las cinco reglas del Estudio Pettini, tal como están en el seed.</summary>
    private static List<ReglaAlerta> ReglasDelSeed() =>
    [
        Regla(TipoRegla.RecurrenciaFallas, """{"minIncidencias":3,"ventanaDias":30}""", 30),
        Regla(TipoRegla.AcumulacionIncidencias, """{"minIncidencias":2,"ventanaDias":90,"mismaCategoria":true}""", 25),
        Regla(TipoRegla.MantenimientoVencido, """{"diasSinMantenimiento":180}""", 20),
        Regla(TipoRegla.AntiguedadEquipo, """{"aniosUmbral":4}""", 15),
        Regla(TipoRegla.GarantiaPorVencer, """{"diasAviso":60}""", 10),
    ];
}
