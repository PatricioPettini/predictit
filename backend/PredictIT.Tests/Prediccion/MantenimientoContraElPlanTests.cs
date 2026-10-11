using FluentAssertions;
using PredictIT.Domain.Negocio;
using PredictIT.Service.Prediccion;

namespace PredictIT.Tests.Prediccion;

/// <summary>
/// La regla de mantenimiento vencido mide contra el plan cuando hay plan.
///
/// Antes medía siempre contra un umbral escrito en la propia regla —«cada 180
/// días»—, que es una estimación genérica. Con un plan de mantenimiento, la
/// organización dijo cada cuánto le toca a ese equipo, y «vencido» pasa a tener
/// una definición exacta: el trabajo agendado cuya fecha ya pasó.
///
/// El umbral no se va: es lo que sigue funcionando para los equipos sin plan, y
/// además es lo que satura el atraso —un mes de atraso sobre un trimestral no
/// es lo mismo que sobre un plan semanal—.
/// </summary>
[Trait("Categoria", "Prediccion")]
[Trait("CP", "CP-16")]
public class MantenimientoContraElPlanTests
{
    private static readonly DateTime Hoy = new(2026, 9, 11);
    private static readonly Guid IdPreventivo = Guid.NewGuid();

    private static ReglaAlerta Regla(int dias = 180) => new()
    {
        Id = Guid.NewGuid(),
        Nombre = "Mantenimiento vencido",
        Tipo = TipoRegla.MantenimientoVencido,
        Condicion = $"{{\"diasSinMantenimiento\":{dias}}}",
        Peso = 20,
        Activa = true,
    };

    private static ContextoEquipo Contexto(
        DateOnly? programadoPara = null, DateTime? ultimoPreventivo = null)
    {
        var mantenimientos = ultimoPreventivo is { } fecha
            ? new List<Mantenimiento>
            {
                new()
                {
                    Fecha = fecha,
                    IdTipo = IdPreventivo,
                    Tipo = new TipoMantenimiento { Id = IdPreventivo, Nombre = "Preventivo", EsPreventivo = true },
                }
            }
            : [];

        var programados = programadoPara is { } cuando
            ? new List<MantenimientoProgramado>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    IdTipoMantenimiento = IdPreventivo,
                    NombreTipoMantenimiento = "Preventivo semestral",
                    FechaProgramada = cuando,
                    Estado = EstadoProgramado.Programado,
                }
            }
            : [];

        return new ContextoEquipo(
            new Equipo { Id = Guid.NewGuid(), Codigo = "NB-COM-007", FechaAlta = new DateTime(2026, 1, 1) },
            [], mantenimientos, Hoy)
        {
            Programados = programados,
        };
    }

    private static ResultadoRegla Evaluar(ContextoEquipo ctx, ReglaAlerta? regla = null) =>
        new EstrategiaMantenimientoVencido().Evaluar(regla ?? Regla(), ctx);

    [Fact]
    public void Con_un_trabajo_agendado_y_vencido_la_regla_se_cumple()
    {
        var r = Evaluar(Contexto(programadoPara: new DateOnly(2026, 8, 12)));

        r.Cumple.Should().BeTrue();
        r.Motivo.Should().Contain("12/08/2026").And.Contain("30 día(s) de atraso");
        r.Recomendacion.Should().Contain("Ejecutar el mantenimiento agendado");
    }

    [Fact]
    public void Un_trabajo_agendado_a_futuro_no_vence_aunque_haga_mucho_del_ultimo()
    {
        // Es el caso que el umbral suelto resolvía mal: hace 250 días del último
        // preventivo, pero la organización decidió que el próximo va la semana
        // que viene. Eso no es un equipo descuidado.
        var r = Evaluar(Contexto(
            programadoPara: new DateOnly(2026, 9, 18),
            ultimoPreventivo: new DateTime(2026, 1, 4)));

        r.Cumple.Should().BeFalse("el plan dice que todavía no le toca");
    }

    [Fact]
    public void Sin_plan_la_regla_sigue_midiendo_contra_el_umbral()
    {
        var r = Evaluar(Contexto(ultimoPreventivo: new DateTime(2026, 1, 4)));

        r.Cumple.Should().BeTrue("pasaron más de 180 días del último preventivo");
        r.Motivo.Should().Contain("desde el último preventivo");
    }

    [Fact]
    public void El_atraso_se_satura_contra_el_intervalo_del_plan()
    {
        // Treinta días de atraso pesan distinto según cada cuánto toca: sobre un
        // plan de 30 días es un ciclo entero perdido, sobre uno de 365 es poco.
        var corto = Evaluar(Contexto(programadoPara: new DateOnly(2026, 8, 12)), Regla(30));
        var largo = Evaluar(Contexto(programadoPara: new DateOnly(2026, 8, 12)), Regla(365));

        corto.Intensidad.Should().Be(1);
        largo.Intensidad.Should().BeLessThan(0.1);
    }

    [Fact]
    public void El_que_manda_es_el_mas_atrasado_de_los_agendados()
    {
        var ctx = Contexto(programadoPara: new DateOnly(2026, 9, 1));
        var mas = new MantenimientoProgramado
        {
            Id = Guid.NewGuid(),
            IdTipoMantenimiento = IdPreventivo,
            NombreTipoMantenimiento = "Preventivo anual",
            FechaProgramada = new DateOnly(2026, 6, 1),
            Estado = EstadoProgramado.Programado,
        };

        var conDos = ctx with { Programados = [.. ctx.Programados, mas] };

        Evaluar(conDos).Motivo.Should().Contain("01/06/2026",
            "de dos trabajos atrasados, el que define la gravedad es el más viejo");
    }

    [Fact]
    public void Ejecutado_el_trabajo_el_equipo_deja_de_estar_vencido()
    {
        // El contexto lleva sólo los pendientes, así que ejecutar el trabajo lo
        // saca de la lista. Con el preventivo recién hecho, la regla no se
        // cumple ni por el plan ni por el umbral.
        var r = Evaluar(Contexto(ultimoPreventivo: Hoy.AddDays(-2)));

        r.Cumple.Should().BeFalse("el trabajo se hizo; no hay nada vencido");
    }
}
