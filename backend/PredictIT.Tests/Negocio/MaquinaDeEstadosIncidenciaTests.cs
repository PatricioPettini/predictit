using FluentAssertions;
using PredictIT.BLL;
using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.BLL.Implementations;
using PredictIT.Domain.Negocio;
using PredictIT.Service.Seguridad;
using PredictIT.Tests.Dobles;
using Xunit;

namespace PredictIT.Tests.Negocio;

/// <summary>
/// La máquina de estados de la incidencia (RF-07, CU-008).
///
/// Se prueba contra dobles en memoria y no contra la base: lo que hay que
/// verificar acá es la decisión, no la escritura. Las pruebas de integración
/// contra SQL Server ya cubren que el UPDATE llegue; ninguna cubría que la
/// decisión fuera la correcta, y por eso la cobertura de la capa de negocio
/// estaba en 2,3 % (H-71).
///
/// El valor concreto: cada una de estas reglas es una frase del documento —«sólo
/// la puede atender el técnico asignado», «resolver exige diagnóstico y
/// solución», «anular es un acto administrativo»— y hasta acá ninguna estaba
/// verificada por nada más que la lectura del código.
/// </summary>
public class MaquinaDeEstadosIncidenciaTests
{
    private static readonly Guid IdTecnico = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid IdOtroTecnico = Guid.Parse("22222222-0000-0000-0000-000000000002");

    private readonly IncidenciaDaoFalso _incidencias = new();
    private readonly CatalogoDaoFalso _catalogos = new();
    private readonly ContextoFalso _contexto = new() { IdUsuario = IdTecnico };
    private readonly BitacoraFalsa _bitacora = new();
    private readonly PrediccionFalsa _prediccion = new();

    private IncidenciaBusiness Negocio() => new(
        new FabricaFalsa(_incidencias, _catalogos),
        seguridad: null!,          // la atención no toca la base de seguridad
        _contexto,
        _bitacora,
        proveedorIa: null!,        // ni al proveedor de IA
        prediccion: () => _prediccion);

    private Incidencia Incidencia(Guid estado, Guid? tecnico = null,
                                  string? diagnostico = null, string? solucion = null) =>
        _incidencias.Agregar(new Incidencia
        {
            Id = Guid.NewGuid(),
            Numero = 1,
            Titulo = "La notebook no enciende",
            IdEstado = estado,
            IdTecnico = tecnico,
            Diagnostico = diagnostico,
            Solucion = solucion
        });

    // ------------------------------------------------------------ transiciones

    [Theory]
    [InlineData("Asignada", "En curso")]
    [InlineData("Asignada", "Anulada")]
    [InlineData("Pendiente de asignación", "Anulada")]
    [InlineData("En curso", "En espera de repuesto")]
    [InlineData("En curso", "Resuelta")]
    [InlineData("En espera de repuesto", "En curso")]
    [InlineData("Resuelta", "Cerrada")]
    [InlineData("Resuelta", "En curso")]     // reapertura
    public void Las_transiciones_del_ciclo_de_vida_se_permiten(string desde, string hasta)
    {
        var i = Incidencia(Id(desde), IdTecnico, "algo se quemó", "se cambió la fuente");
        _contexto.Con(Patentes.IncidenciaAtender, Patentes.IncidenciaReasignarTodas);

        var accion = () => Negocio().Atender(i.Id, new AtencionDto { Estado = hasta });

        accion.Should().NotThrow();
        i.IdEstado.Should().Be(Id(hasta));
    }

    [Theory]
    [InlineData("Asignada", "Resuelta")]     // no se resuelve sin atenderla
    [InlineData("Asignada", "Cerrada")]
    [InlineData("Pendiente de asignación", "En curso")]   // primero se asigna
    [InlineData("Cerrada", "En curso")]      // cerrada es final
    [InlineData("Anulada", "Asignada")]
    [InlineData("En curso", "Asignada")]     // no se vuelve atrás
    public void Las_transiciones_que_saltean_el_ciclo_se_rechazan(string desde, string hasta)
    {
        var i = Incidencia(Id(desde), IdTecnico, "diagnóstico", "solución");
        _contexto.Con(Patentes.IncidenciaAtender, Patentes.IncidenciaReasignarTodas);

        var accion = () => Negocio().Atender(i.Id, new AtencionDto { Estado = hasta });

        accion.Should().Throw<BusinessException>()
              .WithMessage($"*no puede pasar a «{hasta}»*");
        _incidencias.Atendidas.Should().BeEmpty("una transición rechazada no escribe nada");
    }

    [Fact]
    public void Un_estado_que_no_existe_se_rechaza_sin_tocar_la_incidencia()
    {
        var i = Incidencia(CatalogoDaoFalso.IdAsignada, IdTecnico);
        _contexto.Con(Patentes.IncidenciaAtender);

        var accion = () => Negocio().Atender(i.Id, new AtencionDto { Estado = "Archivada" });

        accion.Should().Throw<BusinessException>().WithMessage("*Ese estado no existe*");
    }

    // ------------------------------------------------------- quién puede qué

    [Fact]
    public void Sin_la_patente_de_atender_no_se_atiende()
    {
        var i = Incidencia(CatalogoDaoFalso.IdAsignada, IdTecnico);

        var accion = () => Negocio().Atender(i.Id, new AtencionDto { Estado = "En curso" });

        accion.Should().Throw<PermisoDenegadoException>();
    }

    [Fact]
    public void Un_tecnico_no_puede_atender_la_incidencia_de_otro()
    {
        var i = Incidencia(CatalogoDaoFalso.IdAsignada, IdOtroTecnico);
        _contexto.Con(Patentes.IncidenciaAtender);

        var accion = () => Negocio().Atender(i.Id, new AtencionDto { Estado = "En curso" });

        accion.Should().Throw<BusinessException>()
              .WithMessage("*Sólo la puede atender el técnico asignado*");
    }

    [Fact]
    public void Anular_pide_la_patente_administrativa_y_no_la_de_atender()
    {
        // Anular cancela el trabajo de otro: por eso no alcanza con atender,
        // ni siquiera siendo el técnico asignado.
        var i = Incidencia(CatalogoDaoFalso.IdAsignada, IdTecnico);
        _contexto.Con(Patentes.IncidenciaAtender);

        var accion = () => Negocio().Atender(i.Id, new AtencionDto { Estado = "Anulada" });

        accion.Should().Throw<PermisoDenegadoException>();
    }

    [Fact]
    public void Quien_reasigna_cualquiera_puede_anular_la_de_otro()
    {
        var i = Incidencia(CatalogoDaoFalso.IdAsignada, IdOtroTecnico);
        _contexto.Con(Patentes.IncidenciaAtender, Patentes.IncidenciaReasignarTodas);

        Negocio().Atender(i.Id, new AtencionDto { Estado = "Anulada" });

        i.IdEstado.Should().Be(CatalogoDaoFalso.IdAnulada);
    }

    // -------------------------------------------------- reglas de cada estado

    [Fact]
    public void No_se_empieza_a_atender_una_incidencia_sin_tecnico()
    {
        var i = Incidencia(CatalogoDaoFalso.IdAsignada, tecnico: null);
        _contexto.Con(Patentes.IncidenciaAtender, Patentes.IncidenciaReasignarTodas);

        var accion = () => Negocio().Atender(i.Id, new AtencionDto { Estado = "En curso" });

        accion.Should().Throw<BusinessException>()
              .WithMessage("*Hace falta un técnico asignado*");
    }

    [Theory]
    [InlineData(null, "se cambió la fuente", "diagnostico")]
    [InlineData("se quemó la fuente", null, "solucion")]
    public void Resolver_exige_contar_que_se_encontro_y_que_se_hizo(
        string? diagnostico, string? solucion, string campoEsperado)
    {
        var i = Incidencia(CatalogoDaoFalso.IdEnCurso, IdTecnico);
        _contexto.Con(Patentes.IncidenciaAtender);

        var accion = () => Negocio().Atender(i.Id, new AtencionDto
        {
            Estado = "Resuelta", Diagnostico = diagnostico, Solucion = solucion
        });

        accion.Should().Throw<BusinessException>()
              .Which.Campo.Should().Be(campoEsperado);
    }

    [Fact]
    public void Resolver_sirve_con_lo_ya_guardado_y_no_hace_falta_repetirlo()
    {
        var i = Incidencia(CatalogoDaoFalso.IdEnCurso, IdTecnico,
                           diagnostico: "se quemó la fuente", solucion: "se cambió");
        _contexto.Con(Patentes.IncidenciaAtender);

        Negocio().Atender(i.Id, new AtencionDto { Estado = "Resuelta" });

        i.IdEstado.Should().Be(CatalogoDaoFalso.IdResuelta);
    }

    // ----------------------------------------------------- fecha de resolución

    [Fact]
    public void Resolver_sella_la_fecha_de_resolucion()
    {
        var i = Incidencia(CatalogoDaoFalso.IdEnCurso, IdTecnico, "d", "s");
        _contexto.Con(Patentes.IncidenciaAtender);

        Negocio().Atender(i.Id, new AtencionDto { Estado = "Resuelta" });

        i.FechaResolucion.Should().NotBeNull();
    }

    [Fact]
    public void Reabrir_borra_la_fecha_de_resolucion()
    {
        // De esa fecha sale el tiempo de reparación. Una incidencia reabierta
        // que conserve la fecha vieja miente el indicador.
        var i = Incidencia(CatalogoDaoFalso.IdResuelta, IdTecnico, "d", "s");
        i.FechaResolucion = new DateTime(2026, 3, 1, 10, 0, 0);
        _contexto.Con(Patentes.IncidenciaAtender);

        Negocio().Atender(i.Id, new AtencionDto { Estado = "En curso" });

        i.FechaResolucion.Should().BeNull();
    }

    [Fact]
    public void Cerrar_conserva_la_fecha_en_que_se_resolvio()
    {
        var resuelta = new DateTime(2026, 3, 1, 10, 0, 0);
        var i = Incidencia(CatalogoDaoFalso.IdResuelta, IdTecnico, "d", "s");
        i.FechaResolucion = resuelta;
        _contexto.Con(Patentes.IncidenciaAtender);

        Negocio().Atender(i.Id, new AtencionDto { Estado = "Cerrada" });

        i.FechaResolucion.Should().Be(resuelta);
    }

    // --------------------------------------------------------- transiciones/UI

    [Fact]
    public void Las_transiciones_ofrecidas_son_las_que_la_maquina_permite()
    {
        var i = Incidencia(CatalogoDaoFalso.IdAsignada, IdTecnico);
        _contexto.Con(Patentes.IncidenciaVerTodas);

        var destinos = Negocio().TransicionesDe(i.Id).Select(t => t.Estado);

        destinos.Should().BeEquivalentTo("En curso", "Anulada");
    }

    [Fact]
    public void La_transicion_imposible_se_ofrece_con_el_motivo_y_no_se_esconde()
    {
        // Un botón que desaparece se lee como un bug; uno deshabilitado con el
        // motivo se lee como una regla.
        var i = Incidencia(CatalogoDaoFalso.IdAsignada, tecnico: null);
        _contexto.Con(Patentes.IncidenciaVerTodas, Patentes.IncidenciaReasignarTodas);

        var enCurso = Negocio().TransicionesDe(i.Id).Single(t => t.Estado == "En curso");

        enCurso.Impedimento.Should().Contain("técnico asignado");
    }

    [Fact]
    public void Una_incidencia_cerrada_no_ofrece_ninguna_transicion()
    {
        var i = Incidencia(CatalogoDaoFalso.IdCerrada, IdTecnico);
        _contexto.Con(Patentes.IncidenciaVerTodas);

        Negocio().TransicionesDe(i.Id).Should().BeEmpty();
    }

    private static Guid Id(string estado) => estado switch
    {
        "Pendiente de asignación" => CatalogoDaoFalso.IdPendiente,
        "Asignada" => CatalogoDaoFalso.IdAsignada,
        "En curso" => CatalogoDaoFalso.IdEnCurso,
        "En espera de repuesto" => CatalogoDaoFalso.IdEsperaRepuesto,
        "Resuelta" => CatalogoDaoFalso.IdResuelta,
        "Cerrada" => CatalogoDaoFalso.IdCerrada,
        "Anulada" => CatalogoDaoFalso.IdAnulada,
        _ => throw new ArgumentException(estado)
    };
}
