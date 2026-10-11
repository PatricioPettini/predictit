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
/// Lo que el alta y la modificación de un equipo rechazan antes de escribir
/// (RF-03, RF-04, CU-003).
///
/// Se prueba contra dobles y no contra la base por lo mismo que el alta de
/// incidencia: lo que importa acá es el criterio —qué se rechaza y con qué
/// mensaje—, y eso no necesita SQL Server. Las que sí necesitan base son las
/// del DAO, y están aparte.
///
/// Cada límite tiene un motivo que no se ve en el número, y por eso cada
/// prueba lo nombra: una garantía anterior a la compra hace que el motor
/// predictivo calcule mal su regla, y una fecha de adquisición en el futuro
/// deja la antigüedad en negativo.
/// </summary>
public class AltaDeEquipoTests
{
    private readonly EquipoDaoFalso _equipos = new();
    private readonly ContextoFalso _contexto = new();
    private readonly BitacoraFalsa _bitacora = new();

    private EquipoBusiness Negocio() => new(
        new FabricaFalsa(new IncidenciaDaoFalso(), new CatalogoDaoFalso(), _equipos),
        new FabricaSeguridadFalsa(new UsuarioDaoFalso()), _contexto, _bitacora,
        prediccion: () => new PrediccionFalsa());

    private static EquipoEntradaDto Valido() => new()
    {
        Codigo = "NB-COM-100",
        NumeroSerie = "SN-NB-COM-100",
        IdTipoEquipo = CatalogoDaoFalso.IdTipoNotebook,
        IdEstadoEquipo = CatalogoDaoFalso.IdOperativo,
        IdUbicacion = CatalogoDaoFalso.IdUbicacionComercial,
        Criticidad = 2
    };

    [Fact]
    public void Sin_la_patente_no_se_puede_dar_de_alta()
    {
        // El contexto arranca sin permisos: es el caso de un solicitante
        // llamando al endpoint a mano.
        var accion = () => Negocio().Registrar(Valido());

        accion.Should().Throw<PermisoDenegadoException>();
        _equipos.Equipos.Should().BeEmpty("no tiene que haber escrito nada");
    }

    [Fact]
    public void El_alta_valida_guarda_y_queda_asentada()
    {
        _contexto.Con(Patentes.EquipoGestionar);

        var id = Negocio().Registrar(Valido());

        _equipos.Equipos.Should().ContainSingle().Which.Codigo.Should().Be("NB-COM-100");
        id.Should().NotBeEmpty();
        _bitacora.Eventos.Should().ContainSingle()
                 .Which.Descripcion.Should().Contain("NB-COM-100");
    }

    [Fact]
    public void El_codigo_se_recorta_antes_de_guardarse()
    {
        _contexto.Con(Patentes.EquipoGestionar);
        var entrada = Valido();
        entrada.Codigo = "  NB-COM-101  ";

        Negocio().Registrar(entrada);

        _equipos.Equipos[0].Codigo.Should().Be("NB-COM-101",
            "un espacio al final vuelve único un código que ya existe");
    }

    [Theory]
    [InlineData("", "Codigo")]
    [InlineData("   ", "Codigo")]
    public void Sin_codigo_de_inventario_no_hay_alta(string codigo, string campo)
    {
        _contexto.Con(Patentes.EquipoGestionar);
        var entrada = Valido();
        entrada.Codigo = codigo;

        Negocio().Invoking(n => n.Registrar(entrada))
                 .Should().Throw<BusinessException>()
                 .Which.Campo.Should().Be(campo);
    }

    [Fact]
    public void Un_codigo_repetido_se_rechaza_con_el_codigo_en_el_mensaje()
    {
        _contexto.Con(Patentes.EquipoGestionar);
        _equipos.Insert(new Equipo { Codigo = "NB-COM-100" });

        Negocio().Invoking(n => n.Registrar(Valido()))
                 .Should().Throw<BusinessException>()
                 .Where(e => e.Campo == "Codigo" && e.Message.Contains("NB-COM-100"));
    }

    [Fact]
    public void Un_numero_de_serie_repetido_se_rechaza()
    {
        _contexto.Con(Patentes.EquipoGestionar);
        _equipos.Insert(new Equipo { Codigo = "OTRO", NumeroSerie = "SN-NB-COM-100" });

        Negocio().Invoking(n => n.Registrar(Valido()))
                 .Should().Throw<BusinessException>()
                 .Which.Campo.Should().Be("NumeroSerie");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void La_criticidad_va_de_uno_a_cuatro(int criticidad)
    {
        _contexto.Con(Patentes.EquipoGestionar);
        var entrada = Valido();
        entrada.Criticidad = criticidad;

        Negocio().Invoking(n => n.Registrar(entrada))
                 .Should().Throw<BusinessException>()
                 .Which.Campo.Should().Be("Criticidad");
    }

    [Fact]
    public void La_garantia_no_puede_vencer_antes_de_la_compra()
    {
        // Es un error de carga frecuente y silencioso: con la garantía vencida
        // «antes» de comprar el equipo, la regla de garantía próxima a vencer
        // del motor predictivo dispara sobre un dato imposible.
        _contexto.Con(Patentes.EquipoGestionar);
        var entrada = Valido();
        entrada.FechaAdquisicion = new DateOnly(2026, 5, 1);
        entrada.FechaFinGarantia = new DateOnly(2025, 5, 1);

        Negocio().Invoking(n => n.Registrar(entrada))
                 .Should().Throw<BusinessException>()
                 .Which.Campo.Should().Be("FechaFinGarantia");
    }

    [Fact]
    public void La_fecha_de_adquisicion_no_puede_estar_en_el_futuro()
    {
        _contexto.Con(Patentes.EquipoGestionar);
        var entrada = Valido();
        entrada.FechaAdquisicion = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        Negocio().Invoking(n => n.Registrar(entrada))
                 .Should().Throw<BusinessException>()
                 .Which.Campo.Should().Be("FechaAdquisicion");
    }

    [Fact]
    public void Una_ubicacion_que_no_es_de_la_organizacion_se_rechaza()
    {
        _contexto.Con(Patentes.EquipoGestionar);
        var entrada = Valido();
        entrada.IdUbicacion = Guid.NewGuid();

        Negocio().Invoking(n => n.Registrar(entrada))
                 .Should().Throw<BusinessException>()
                 .Which.Campo.Should().Be("IdUbicacion");
    }

    [Fact]
    public void Modificar_un_equipo_que_no_existe_no_dice_que_no_existe_sino_que_no_es_tuyo()
    {
        // El DAO filtra por organización, así que «no existe» y «es de otra
        // organización» llegan acá igual. El mensaje no distingue a propósito:
        // distinguirlos confirmaría la existencia de un equipo ajeno.
        _contexto.Con(Patentes.EquipoGestionar);

        Negocio().Invoking(n => n.Actualizar(Guid.NewGuid(), Valido()))
                 .Should().Throw<BusinessException>()
                 .Which.Message.Should().Contain("organización");
    }

    [Fact]
    public void Al_modificar_un_equipo_su_propio_codigo_no_cuenta_como_repetido()
    {
        _contexto.Con(Patentes.EquipoGestionar);
        var id = _equipos.Insert(new Equipo { Codigo = "NB-COM-100", NumeroSerie = "SN-NB-COM-100" });

        var entrada = Valido();
        entrada.Criticidad = 3;

        Negocio().Invoking(n => n.Actualizar(id, entrada)).Should().NotThrow();
        _equipos.GetById(id)!.Criticidad.Should().Be(3);
    }
}
