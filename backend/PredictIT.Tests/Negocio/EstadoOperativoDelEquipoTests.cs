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
/// El cambio de estado operativo y la baja de un equipo (RF-06, CU-005,
/// CU-015).
///
/// Son dos operaciones distintas con dos patentes distintas, y la diferencia no
/// es burocrática: dar de baja saca el equipo del parque, y cambiar el estado
/// lo deja adentro pero fuera de la evaluación del motor predictivo. La
/// confusión entre las dos es lo que estas pruebas fijan.
/// </summary>
public class EstadoOperativoDelEquipoTests
{
    private readonly EquipoDaoFalso _equipos = new();
    private readonly ContextoFalso _contexto = new();
    private readonly BitacoraFalsa _bitacora = new();
    private readonly PrediccionFalsa _prediccion = new();

    private EquipoBusiness Negocio() => new(
        new FabricaFalsa(new IncidenciaDaoFalso(), new CatalogoDaoFalso(), _equipos),
        new FabricaSeguridadFalsa(new UsuarioDaoFalso()), _contexto, _bitacora,
        prediccion: () => _prediccion);

    private Guid UnEquipoOperativo() => _equipos.Insert(new Equipo
    {
        Codigo = "NB-COM-007",
        IdEstadoEquipo = CatalogoDaoFalso.IdOperativo
    });

    [Fact]
    public void Cambiar_el_estado_pide_su_propia_patente()
    {
        var id = UnEquipoOperativo();
        // Gestionar equipos no alcanza: cambiar el estado tiene la suya.
        _contexto.Con(Patentes.EquipoGestionar);

        Negocio().Invoking(n => n.CambiarEstado(id, new CambioEstadoEquipoDto
                 {
                     IdEstado = CatalogoDaoFalso.IdEnReparacion
                 }))
                 .Should().Throw<PermisoDenegadoException>();

        _equipos.CambiosDeEstado.Should().BeEmpty();
    }

    [Fact]
    public void El_cambio_valido_se_aplica_y_dispara_la_reevaluacion()
    {
        var id = UnEquipoOperativo();
        _contexto.Con(Patentes.EquipoCambiarEstado);

        Negocio().CambiarEstado(id, new CambioEstadoEquipoDto
        {
            IdEstado = CatalogoDaoFalso.IdEnReparacion,
            Motivo = "Se lo llevó el service"
        });

        _equipos.CambiosDeEstado.Should().ContainSingle()
                .Which.IdEstado.Should().Be(CatalogoDaoFalso.IdEnReparacion);

        // Un equipo que deja de estar operativo sale de la evaluación: el
        // riesgo del parque cambió y hay que recalcularlo.
        _prediccion.Reevaluados.Should().ContainSingle().Which.Should().Be(id);
    }

    [Fact]
    public void El_motivo_queda_escrito_en_la_bitacora_con_los_dos_estados()
    {
        var id = UnEquipoOperativo();
        _contexto.Con(Patentes.EquipoCambiarEstado);

        Negocio().CambiarEstado(id, new CambioEstadoEquipoDto
        {
            IdEstado = CatalogoDaoFalso.IdEnReparacion,
            Motivo = "Se lo llevó el service"
        });

        var evento = _bitacora.Eventos.Should().ContainSingle().Subject;
        evento.Descripcion.Should().Contain("Operativo")
              .And.Contain("En reparación")
              .And.Contain("Se lo llevó el service");
    }

    [Fact]
    public void Poner_el_estado_que_ya_tiene_se_rechaza()
    {
        // No es una tontería: sin esto la bitácora se llena de cambios que no
        // cambiaron nada y el historial deja de servir para leer qué pasó.
        var id = UnEquipoOperativo();
        _contexto.Con(Patentes.EquipoCambiarEstado);

        Negocio().Invoking(n => n.CambiarEstado(id, new CambioEstadoEquipoDto
                 {
                     IdEstado = CatalogoDaoFalso.IdOperativo
                 }))
                 .Should().Throw<BusinessException>()
                 .Which.Campo.Should().Be("idEstado");
    }

    [Fact]
    public void Un_estado_que_no_existe_se_rechaza()
    {
        var id = UnEquipoOperativo();
        _contexto.Con(Patentes.EquipoCambiarEstado);

        Negocio().Invoking(n => n.CambiarEstado(id, new CambioEstadoEquipoDto
                 {
                     IdEstado = Guid.NewGuid()
                 }))
                 .Should().Throw<BusinessException>()
                 .Which.Campo.Should().Be("idEstado");
    }

    [Fact]
    public void Dar_de_baja_por_la_puerta_del_cambio_de_estado_no_se_permite()
    {
        // La baja tiene su propia operación porque además valida que el equipo
        // no tenga incidencias abiertas. Llegar a «Dado de baja» por acá se
        // saltearía esa validación, y el mensaje lo explica en lugar de
        // limitarse a negar.
        var id = UnEquipoOperativo();
        _contexto.Con(Patentes.EquipoCambiarEstado);

        Negocio().Invoking(n => n.CambiarEstado(id, new CambioEstadoEquipoDto
                 {
                     IdEstado = CatalogoDaoFalso.IdDadoDeBaja
                 }))
                 .Should().Throw<BusinessException>()
                 .Which.Message.Should().Contain("baja");

        _equipos.CambiosDeEstado.Should().BeEmpty();
    }

    [Fact]
    public void La_baja_borra_y_queda_asentada()
    {
        var id = UnEquipoOperativo();
        _contexto.Con(Patentes.EquipoGestionar);

        Negocio().DarDeBaja(id);

        _equipos.Borrados.Should().ContainSingle().Which.Should().Be(id);
        _bitacora.Eventos.Should().ContainSingle()
                 .Which.Descripcion.Should().Contain("NB-COM-007");
    }

    [Fact]
    public void No_se_puede_dar_de_baja_un_equipo_de_otra_organizacion()
    {
        _contexto.Con(Patentes.EquipoGestionar);

        Negocio().Invoking(n => n.DarDeBaja(Guid.NewGuid()))
                 .Should().Throw<BusinessException>()
                 .Which.Message.Should().Contain("organización");

        _equipos.Borrados.Should().BeEmpty();
    }
}
