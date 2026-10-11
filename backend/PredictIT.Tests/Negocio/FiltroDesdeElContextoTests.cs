using FluentAssertions;
using PredictIT.BLL;
using PredictIT.BLL.Contracts;
using PredictIT.BLL.Implementations;
using PredictIT.DAO.Contracts;
using PredictIT.Domain.Negocio;
using PredictIT.Service.Seguridad;
using PredictIT.Tests.Dobles;
using Xunit;

namespace PredictIT.Tests.Negocio;

/// <summary>
/// Lo que la capa de negocio le impone al filtro antes de bajar al DAO.
///
/// `AislamientoOrganizacionTests` ya cubre que el DAO filtre por organización.
/// Lo que no cubría nada es lo de acá: que el negocio **pise** lo que mandó la
/// pantalla. La regla es «quien sólo puede ver las propias, ve las propias», y
/// si se confiara en el filtro que llega en la petición, alcanzaría con
/// cambiarlo desde el navegador para ver las de todos.
/// </summary>
public class FiltroDesdeElContextoTests
{
    private static readonly Guid IdUsuario = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid IdAjeno = Guid.Parse("33333333-0000-0000-0000-000000000002");

    private readonly IncidenciaDaoFalso _incidencias = new();
    private readonly ContextoFalso _contexto = new() { IdUsuario = IdUsuario };

    private IncidenciaBusiness Negocio() => new(
        new FabricaFalsa(_incidencias, new CatalogoDaoFalso()),
        new FabricaSeguridadFalsa(new UsuarioDaoFalso()), _contexto, new BitacoraFalsa(),
        proveedorIa: null!, prediccion: () => new PrediccionFalsa());

    [Fact]
    public void Quien_solo_ve_las_propias_no_puede_pedir_las_de_otro()
    {
        _contexto.Con(Patentes.IncidenciaVerPropias);

        // La pantalla manda el filtro de otro usuario. No alcanza.
        Negocio().Buscar(new FiltroIncidencias { IdInvolucrado = IdAjeno, IdTecnico = IdAjeno });

        _incidencias.UltimoFiltro!.IdInvolucrado.Should().Be(IdUsuario);
        _incidencias.UltimoFiltro.IdTecnico.Should().BeNull(
            "filtrar por técnico dejaría vacío el listado de quien reporta y no atiende");
    }

    [Fact]
    public void Quien_ve_todas_conserva_el_filtro_que_eligio()
    {
        _contexto.Con(Patentes.IncidenciaVerTodas);

        Negocio().Buscar(new FiltroIncidencias { IdTecnico = IdAjeno });

        _incidencias.UltimoFiltro!.IdTecnico.Should().Be(IdAjeno);
        _incidencias.UltimoFiltro.IdInvolucrado.Should().BeNull();
    }

    [Fact]
    public void Sin_ninguna_de_las_dos_patentes_no_se_lee_nada()
    {
        var accion = () => Negocio().Buscar(new FiltroIncidencias());

        accion.Should().Throw<PermisoDenegadoException>();
        _incidencias.UltimoFiltro.Should().BeNull("no debería haber llegado al DAO");
    }

}
