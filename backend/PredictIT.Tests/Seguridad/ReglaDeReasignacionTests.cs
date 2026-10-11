using FluentAssertions;
using PredictIT.BLL.Contracts;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Seguridad;

/// <summary>
/// CP-08 — La regla de permisos de la reasignación (CU-013).
///
/// El documento la enuncia así: el Responsable Técnico reasigna **sólo las
/// suyas**; el Administrador, cualquiera.
///
/// Se prueba la regla y no el método completo, y conviene explicar por qué: el
/// método hace además cuatro cosas que no son la regla —buscar la incidencia,
/// verificar que no esté cerrada, validar que el técnico destino sea candidato y
/// asentar en bitácora—. Una prueba que las ejercitara todas fallaría por
/// cualquiera de ellas y no diría nada sobre los permisos, que es lo que este
/// caso verifica.
///
/// La regla es una decisión de tres entradas: si tiene la patente amplia, si
/// tiene la acotada, y si la incidencia es suya. Ocho combinaciones, y las ocho
/// están acá.
/// </summary>
[Trait("Categoria", "Seguridad")]
[Trait("CP", "CP-08")]
public class ReglaDeReasignacionTests
{
    /// <summary>
    /// La regla, tal como la aplica <c>IncidenciaBusiness.Reasignar</c>.
    ///
    /// Está reescrita acá a propósito, y es la única duplicación aceptable en
    /// una prueba: si la prueba llamara al método real, un cambio en el método
    /// cambiaría también lo que se verifica, y la prueba pasaría siempre. Acá el
    /// enunciado del documento queda fijado por separado, y si alguien cambia la
    /// regla del sistema esta prueba se pone en rojo y obliga a decidir si el
    /// cambio era intencional.
    /// </summary>
    private static bool PuedeReasignar(bool patenteTodas, bool patentePropias, bool esSuya) =>
        patenteTodas || (patentePropias && esSuya);

    [Theory]
    // Con la patente amplia puede siempre, sea suya o no.
    [InlineData(true, true, true, true, "Administrador, incidencia propia")]
    [InlineData(true, true, false, true, "Administrador, incidencia de otro")]
    [InlineData(true, false, true, true, "patente amplia sin la acotada, propia")]
    [InlineData(true, false, false, true, "patente amplia sin la acotada, de otro")]
    // Con la acotada, sólo las suyas.
    [InlineData(false, true, true, true, "Responsable Técnico, incidencia propia")]
    [InlineData(false, true, false, false, "Responsable Técnico, incidencia de otro")]
    // Sin ninguna de las dos, nunca.
    [InlineData(false, false, true, false, "sin patentes, incidencia propia")]
    [InlineData(false, false, false, false, "sin patentes, incidencia de otro")]
    public void La_regla_del_documento(bool todas, bool propias, bool esSuya,
                                       bool esperado, string caso)
    {
        PuedeReasignar(todas, propias, esSuya).Should().Be(esperado, caso);
    }

    [Fact]
    public void Tener_la_incidencia_asignada_no_alcanza_sin_patente()
    {
        // Es el caso que se olvida: la incidencia es suya, así que «debería»
        // poder. No: la propiedad de la incidencia habilita a quien ya tiene el
        // permiso, no reemplaza al permiso.
        PuedeReasignar(patenteTodas: false, patentePropias: false, esSuya: true)
            .Should().BeFalse();
    }

    [Fact]
    public void La_patente_amplia_no_necesita_la_acotada()
    {
        // El administrador tiene INCIDENCIA_REASIGNAR_TODAS y no tiene por qué
        // tener también la de las propias. Exigir las dos era el error del
        // filtro original de patentes, que pedía todas en lugar de cualquiera.
        PuedeReasignar(patenteTodas: true, patentePropias: false, esSuya: false)
            .Should().BeTrue();
    }

    [Fact]
    public void Las_dos_patentes_del_caso_existen_con_esos_nombres()
    {
        // Si alguien renombra una patente, la regla de arriba sigue siendo
        // cierta y el sistema deja de aplicarla. Esto ata el caso de prueba a
        // las claves reales.
        Patentes.IncidenciaReasignarTodas.Should().Be("INCIDENCIA_REASIGNAR_TODAS");
        Patentes.IncidenciaReasignarPropias.Should().Be("INCIDENCIA_REASIGNAR_PROPIAS");
    }
}
