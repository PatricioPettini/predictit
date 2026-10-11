using FluentAssertions;
using PredictIT.DAO.Factory;
using PredictIT.DAO.Helpers;
using PredictIT.Domain.Seguridad;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Dao;

/// <summary>
/// Consultas de la bitácora: filtro por usuario (CU.Arq.002) y vista de errores
/// (CU.Arq.007).
///
/// Las pruebas leen lo que ya hay y no escriben nada. La bitácora tiene un
/// disparador que rechaza UPDATE y DELETE, así que una prueba que insertara
/// dejaría su fila para siempre.
/// </summary>
[Trait("Categoria", "Dao")]
public class BitacoraConsultaTests
{
    private static IFactoryDaoSeguridad Dao() =>
        new FactoryDaoSeguridad(new ConexionesSql(
            EntornoPruebas.CadenaNegocio, EntornoPruebas.CadenaServicio));

    private static readonly DateTime Desde = new(2020, 1, 1);
    private static readonly DateTime Hasta = DateTime.Today.AddDays(1);

    [Fact]
    public void El_filtro_por_usuario_devuelve_solo_ese_usuario()
    {
        var dao = Dao();

        var todas = dao.Bitacora.Consultar(Desde, Hasta, null, null);
        var conUsuario = todas.FirstOrDefault(b => b.IdUsuario is not null);

        // Sin una sola entrada con usuario no hay nada que filtrar, y eso sería
        // un problema del entorno y no del filtro.
        conUsuario.Should().NotBeNull("la bitácora de demostración tiene inicios de sesión");

        var filtradas = dao.Bitacora.Consultar(Desde, Hasta, null, null, conUsuario!.IdUsuario);

        filtradas.Should().NotBeEmpty();
        filtradas.Should().OnlyContain(b => b.IdUsuario == conUsuario.IdUsuario);
        filtradas.Count.Should().BeLessThan(todas.Count + 1);
    }

    [Fact]
    public void El_filtro_por_usuario_con_un_id_inexistente_no_devuelve_nada()
    {
        // Importa que devuelva vacío y no todo: un filtro que se ignora en
        // silencio muestra la actividad de todos cuando se pidió la de uno.
        var filtradas = Dao().Bitacora.Consultar(Desde, Hasta, null, null, Guid.NewGuid());

        filtradas.Should().BeEmpty();
    }

    [Fact]
    public void La_vista_de_errores_trae_solo_lo_que_salio_mal()
    {
        var dao = Dao();
        var criticidades = dao.Bitacora.TiposDeEvento()
            .ToDictionary(t => t.Codigo, t => t.Criticidad);

        var errores = dao.Bitacora.ConsultarErrores(Desde, Hasta, null, incluirAdvertencias: false);

        errores.Should().OnlyContain(
            b => criticidades[b.CodigoTipoEvento!] == CriticidadEvento.Error
                 || criticidades[b.CodigoTipoEvento!] == CriticidadEvento.Critico,
            "la pantalla de errores no puede mezclar eventos informativos");
    }

    [Fact]
    public void Las_advertencias_entran_solo_si_se_piden()
    {
        var dao = Dao();
        var criticidades = dao.Bitacora.TiposDeEvento()
            .ToDictionary(t => t.Codigo, t => t.Criticidad);

        var sinAdvertencias = dao.Bitacora.ConsultarErrores(Desde, Hasta, null, false);
        var conAdvertencias = dao.Bitacora.ConsultarErrores(Desde, Hasta, null, true);

        conAdvertencias.Count.Should().BeGreaterThanOrEqualTo(sinAdvertencias.Count);

        // La base de demostración tiene intentos de acceso fallidos, que son
        // advertencias: si no aparecieran al pedirlas, el flag no haría nada.
        conAdvertencias.Should().Contain(
            b => criticidades[b.CodigoTipoEvento!] == CriticidadEvento.Advertencia);

        sinAdvertencias.Should().NotContain(
            b => criticidades[b.CodigoTipoEvento!] == CriticidadEvento.Advertencia);
    }

    [Fact]
    public void La_vista_de_errores_devuelve_la_traza()
    {
        // Es la diferencia con la consulta general: sin la traza, esta pantalla
        // no agregaría nada a la bitácora que ya existe.
        var dao = Dao();

        var conTraza = dao.Bitacora.ConsultarErrores(Desde, Hasta, null, true)
            .FirstOrDefault(b => b.Traza is not null);

        var hayAlguna = dao.Bitacora.Consultar(Desde, Hasta, null, null)
            .Any(b => b.Traza is not null);

        if (hayAlguna)
        {
            conTraza.Should().NotBeNull("si hay entradas con traza, la vista de errores tiene que traerlas");
        }
    }
}
