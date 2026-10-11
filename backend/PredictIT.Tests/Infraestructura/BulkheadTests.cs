using FluentAssertions;
using Microsoft.Data.SqlClient;
using PredictIT.DAO.Helpers;

namespace PredictIT.Tests.Infraestructura;

/// <summary>
/// Bulkhead de los procesos en segundo plano (ADR 0009).
///
/// Lo que hay que probar no es que la cadena tenga un parámetro más: es que el
/// pool resultante sea **otro**. ADO.NET identifica el pool por la cadena de
/// conexión completa, así que dos cadenas distintas ya son dos pools; lo que
/// puede fallar es que las cadenas terminen siendo iguales, y entonces el
/// bulkhead no existe aunque el código dé la impresión de que sí.
/// </summary>
[Trait("Categoria", "Infraestructura")]
public class BulkheadTests
{
    private static ConexionesSql Base() =>
        new(EntornoPruebas.CadenaNegocio, EntornoPruebas.CadenaServicio);

    [Fact]
    public void Las_cadenas_de_segundo_plano_son_distintas_de_las_interactivas()
    {
        var interactivas = Base();
        var fondo = interactivas.ParaSegundoPlano();

        fondo.Negocio.Should().NotBe(interactivas.Negocio,
            "si la cadena fuera igual, compartirían pool y no habría bulkhead");
        fondo.Servicio.Should().NotBe(interactivas.Servicio);
    }

    [Fact]
    public void El_pool_de_segundo_plano_esta_acotado()
    {
        var fondo = Base().ParaSegundoPlano(maxConexiones: 5);

        new SqlConnectionStringBuilder(fondo.Negocio).MaxPoolSize.Should().Be(5);
        new SqlConnectionStringBuilder(fondo.Servicio).MaxPoolSize.Should().Be(5);
    }

    [Fact]
    public void Apuntan_a_las_mismas_bases()
    {
        // El bulkhead separa el pool, no los datos. Si cambiara la base, el
        // barrido evaluaría otra cosa.
        var interactivas = Base();
        var fondo = interactivas.ParaSegundoPlano();

        var original = new SqlConnectionStringBuilder(interactivas.Negocio);
        var aislada = new SqlConnectionStringBuilder(fondo.Negocio);

        aislada.InitialCatalog.Should().Be(original.InitialCatalog);
        aislada.DataSource.Should().Be(original.DataSource);
    }

    [Fact]
    public void El_proceso_de_fondo_se_identifica_en_la_base()
    {
        // Sin nombre de aplicación, sys.dm_exec_sessions muestra todas las
        // conexiones iguales y no se puede saber quién ocupa qué.
        var fondo = Base().ParaSegundoPlano();

        new SqlConnectionStringBuilder(fondo.Negocio).ApplicationName
            .Should().Be("PredictIT-Fondo");
    }
}
