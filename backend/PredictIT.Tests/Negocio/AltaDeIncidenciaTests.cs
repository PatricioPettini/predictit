using FluentAssertions;
using PredictIT.BLL;
using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.BLL.Implementations;
using PredictIT.Tests.Dobles;
using Xunit;

namespace PredictIT.Tests.Negocio;

/// <summary>
/// Lo que el alta de una incidencia rechaza antes de escribir nada (RF-05,
/// CU-006).
///
/// Son las validaciones que el formulario también hace, y por eso mismo hay que
/// probarlas acá: la validación del formulario es una comodidad para el
/// usuario, no un control. Un POST hecho a mano no la ejecuta.
///
/// El otro motivo para probarlas es que cada límite tiene una razón que no se
/// ve en el número: 2000 caracteres es lo que entra en la columna, y un título
/// de tres letras no le dice nada al técnico que lo va a leer.
/// </summary>
public class AltaDeIncidenciaTests
{
    private readonly IncidenciaDaoFalso _incidencias = new();
    private readonly ContextoFalso _contexto = new();

    private IncidenciaBusiness Negocio() => new(
        new FabricaFalsa(_incidencias, new CatalogoDaoFalso()),
        new FabricaSeguridadFalsa(new UsuarioDaoFalso()), _contexto, new BitacoraFalsa(),
        proveedorIa: null!, prediccion: () => new PrediccionFalsa());

    [Theory]
    [InlineData("", "El equipo no arranca desde ayer", "titulo")]
    [InlineData("no", "El equipo no arranca desde ayer", "titulo")]
    [InlineData("La notebook no enciende", "", "descripcion")]
    [InlineData("La notebook no enciende", "no anda", "descripcion")]
    public void Un_reporte_que_no_dice_nada_se_rechaza(
        string titulo, string descripcion, string campoEsperado)
    {
        _contexto.Con(Patentes.IncidenciaRegistrar);

        var accion = () => Negocio().Registrar(new IncidenciaEntradaDto
        {
            Titulo = titulo, Descripcion = descripcion, IdEquipo = Guid.NewGuid()
        });

        accion.Should().Throw<BusinessException>()
              .Which.Campo.Should().Be(campoEsperado);
    }

    [Theory]
    [InlineData(151, 20, "titulo")]
    [InlineData(20, 2001, "descripcion")]
    public void Un_texto_mas_largo_que_la_columna_se_rechaza_y_no_se_recorta(
        int largoTitulo, int largoDescripcion, string campoEsperado)
    {
        // Recortarlo en silencio perdería justo el final, que es donde suele
        // estar lo que pasó.
        _contexto.Con(Patentes.IncidenciaRegistrar);

        var accion = () => Negocio().Registrar(new IncidenciaEntradaDto
        {
            Titulo = new string('a', largoTitulo),
            Descripcion = new string('b', largoDescripcion),
            IdEquipo = Guid.NewGuid()
        });

        accion.Should().Throw<BusinessException>()
              .Which.Campo.Should().Be(campoEsperado);
    }

    [Fact]
    public void Los_espacios_no_cuentan_como_contenido()
    {
        _contexto.Con(Patentes.IncidenciaRegistrar);

        var accion = () => Negocio().Registrar(new IncidenciaEntradaDto
        {
            Titulo = "          ", Descripcion = "                    ",
            IdEquipo = Guid.NewGuid()
        });

        accion.Should().Throw<BusinessException>().Which.Campo.Should().Be("titulo");
    }

    [Fact]
    public void Sin_la_patente_de_registrar_no_se_registra()
    {
        var accion = () => Negocio().Registrar(new IncidenciaEntradaDto
        {
            Titulo = "La notebook no enciende",
            Descripcion = "Desde ayer a la mañana no da señal de vida.",
            IdEquipo = Guid.NewGuid()
        });

        accion.Should().Throw<PermisoDenegadoException>();
    }
}
