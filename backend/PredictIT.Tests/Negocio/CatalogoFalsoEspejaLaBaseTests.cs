using FluentAssertions;
using PredictIT.DAO.Factory;
using PredictIT.Tests.Dobles;
using PredictIT.Tests.Infraestructura;
using Xunit;

namespace PredictIT.Tests.Negocio;

/// <summary>
/// El catálogo de estados que usan los dobles tiene que ser el de la base.
///
/// Las pruebas de la máquina de estados corren contra un catálogo escrito a
/// mano, y `IncidenciaBusiness` compara los estados **por nombre**. Si mañana
/// alguien renombra «En espera de repuesto» en la base, la máquina deja de
/// reconocerlo y todas esas pruebas siguen en verde contra un catálogo que ya
/// no existe: verdes y sin valor, que es peor que rojas.
///
/// Esta prueba es la que impide que eso pase. Es de integración —toca la base—
/// a propósito: es el único lugar donde las dos listas se pueden comparar.
/// </summary>
public class CatalogoFalsoEspejaLaBaseTests
{
    [Fact]
    public void Los_estados_del_doble_son_los_de_la_base()
    {
        var reales = new FactoryDao(Datos.Conexiones(),
                                    ContextoDePrueba.TodoPermitido(Datos.Estudio))
            .Catalogos.EstadosDeIncidencia();

        var falsos = new CatalogoDaoFalso().EstadosDeIncidencia();

        falsos.Select(e => new { e.Nombre, e.EsFinal })
              .Should().BeEquivalentTo(reales.Select(e => new { e.Nombre, e.EsFinal }),
                  "los dobles de las pruebas de negocio comparan por nombre");
    }
}
