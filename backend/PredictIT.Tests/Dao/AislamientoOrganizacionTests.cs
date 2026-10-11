using FluentAssertions;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Dao;

/// <summary>
/// El aislamiento entre organizaciones.
///
/// Es el riesgo de seguridad más serio del sistema (ADR 0005): una consulta que
/// se olvide el filtro filtra datos de un cliente a otro. Ningún analizador
/// estático lo detecta, así que la única red que queda es esta.
///
/// Hay una prueba por operación y no una sola de muestra, porque el defecto se
/// introduce de a una consulta.
/// </summary>
[Trait("Categoria", "Integracion")]
public class AislamientoOrganizacionTests
{
    private static IFactoryDao Como(Guid organizacion) =>
        new FactoryDao(Datos.Conexiones(), ContextoDePrueba.TodoPermitido(organizacion));

    [Fact]
    public void El_listado_solo_devuelve_equipos_de_la_organizacion_de_la_sesion()
    {
        var delEstudio = Como(Datos.Estudio).Equipos.GetAll();
        var deLaClinica = Como(Datos.Clinica).Equipos.GetAll();

        delEstudio.Should().NotBeEmpty();
        deLaClinica.Should().NotBeEmpty();

        delEstudio.Should().OnlyContain(e => e.IdOrganizacion == Datos.Estudio);
        deLaClinica.Should().OnlyContain(e => e.IdOrganizacion == Datos.Clinica);

        var codigosEstudio = delEstudio.Select(e => e.Codigo).ToHashSet();
        var codigosClinica = deLaClinica.Select(e => e.Codigo).ToHashSet();
        codigosEstudio.Overlaps(codigosClinica).Should().BeFalse(
            "ninguna organización debería ver los equipos de la otra");
    }

    [Fact]
    public void No_se_puede_leer_por_id_un_equipo_de_otra_organizacion()
    {
        // PC-ADM-014 existe y pertenece al Estudio. Pedirlo desde la sesión de la
        // Clínica tiene que dar nada, no el equipo.
        Como(Datos.Estudio).Equipos.GetById(Datos.PcAdm014).Should().NotBeNull();
        Como(Datos.Clinica).Equipos.GetById(Datos.PcAdm014).Should().BeNull();
    }

    [Fact]
    public void La_busqueda_con_filtros_tambien_aisla()
    {
        var resultado = Como(Datos.Clinica).Equipos.Buscar(new FiltroEquipos
        {
            Texto = "PC-ADM",   // un código que sólo existe en el Estudio
            PorPagina = 100
        });

        resultado.Items.Should().BeEmpty();
        resultado.Total.Should().Be(0);
    }

    [Fact]
    public void El_conteo_de_equipos_es_por_organizacion()
    {
        var estudio = Como(Datos.Estudio).Equipos.ContarPorOrganizacion();
        var clinica = Como(Datos.Clinica).Equipos.ContarPorOrganizacion();

        estudio.Should().Be(52, "es el parque que describe el documento");
        clinica.Should().Be(2);
    }

    [Fact]
    public void La_unicidad_del_codigo_se_evalua_dentro_de_la_organizacion()
    {
        // El mismo código puede existir en dos clientes distintos. Si la
        // comprobación no filtrara por organización, el alta de un equipo
        // fallaría por un código que usa otro cliente.
        Como(Datos.Estudio).Equipos.ExisteCodigo("PC-ADM-014").Should().BeTrue();
        Como(Datos.Clinica).Equipos.ExisteCodigo("PC-ADM-014").Should().BeFalse();
    }

    [Fact]
    public void Las_ubicaciones_son_de_la_organizacion()
    {
        var estudio = Como(Datos.Estudio).Catalogos.Ubicaciones();
        var clinica = Como(Datos.Clinica).Catalogos.Ubicaciones();

        estudio.Should().OnlyContain(u => u.IdOrganizacion == Datos.Estudio);
        clinica.Should().OnlyContain(u => u.IdOrganizacion == Datos.Clinica);
        estudio.Select(u => u.Nombre).Should().NotIntersectWith(clinica.Select(u => u.Nombre));
    }

    [Fact]
    public void Los_catalogos_globales_se_comparten_entre_organizaciones()
    {
        // Los tipos y estados de equipo no son de nadie: son el mismo catálogo
        // para todos. Filtrarlos por organización sería un error en el otro
        // sentido.
        var estudio = Como(Datos.Estudio).Catalogos.TiposDeEquipo().Select(t => t.Id);
        var clinica = Como(Datos.Clinica).Catalogos.TiposDeEquipo().Select(t => t.Id);

        estudio.Should().BeEquivalentTo(clinica);
    }
}
