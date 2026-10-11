using FluentAssertions;
using PredictIT.Domain.Seguridad;

namespace PredictIT.Tests.Seguridad;

/// <summary>
/// El Composite de permisos: la pieza que resuelve el anidamiento de familias.
///
/// Se prueba sin base de datos a propósito. Es lógica de dominio pura y tiene
/// que poder verificarse sin infraestructura.
/// </summary>
[Trait("Categoria", "Unitaria")]
public class CompositePermisosTests
{
    private static Patente Patente(string clave) => new()
    {
        Id = Guid.NewGuid(),
        Nombre = clave,
        DataKey = clave
    };

    private static Familia Fam(string nombre) => new() { Id = Guid.NewGuid(), Nombre = nombre };

    [Fact]
    public void Una_patente_suelta_se_concede_a_si_misma()
    {
        var p = Patente("EQUIPO_VER");

        p.ObtenerPatentes().Should().ContainSingle().Which.DataKey.Should().Be("EQUIPO_VER");
        p.Concede("EQUIPO_VER").Should().BeTrue();
        p.Concede("EQUIPO_GESTIONAR").Should().BeFalse();
    }

    [Fact]
    public void Una_familia_concede_las_patentes_que_contiene()
    {
        var familia = Fam("CONSULTA_TECNICA");
        familia.Agregar(Patente("EQUIPO_VER"));
        familia.Agregar(Patente("ALERTA_VER"));

        familia.ObtenerPatentes().Should().HaveCount(2);
        familia.Concede("EQUIPO_VER").Should().BeTrue();
        familia.Concede("ALERTA_VER").Should().BeTrue();
    }

    [Fact]
    public void Una_familia_anidada_aporta_sus_patentes_a_la_familia_que_la_contiene()
    {
        // Es el caso real del sistema: quien atiende incidencias necesita todo lo
        // que necesita quien sólo consulta, más lo propio.
        var consulta = Fam("CONSULTA_TECNICA");
        consulta.Agregar(Patente("EQUIPO_VER"));
        consulta.Agregar(Patente("DASHBOARD_VER"));

        var operacion = Fam("OPERACION_TECNICA");
        operacion.Agregar(consulta);
        operacion.Agregar(Patente("INCIDENCIA_ATENDER"));

        operacion.ObtenerPatentes().Select(p => p.DataKey)
            .Should().BeEquivalentTo("EQUIPO_VER", "DASHBOARD_VER", "INCIDENCIA_ATENDER");
    }

    [Fact]
    public void El_anidamiento_funciona_a_mas_de_dos_niveles()
    {
        var nieta = Fam("NIETA");
        nieta.Agregar(Patente("HOJA_PROFUNDA"));

        var hija = Fam("HIJA");
        hija.Agregar(nieta);

        var abuela = Fam("ABUELA");
        abuela.Agregar(hija);

        abuela.Concede("HOJA_PROFUNDA").Should().BeTrue();
    }

    [Fact]
    public void Una_patente_alcanzada_por_dos_caminos_no_se_duplica()
    {
        // Diamante: dos familias distintas contienen la misma patente y las dos
        // cuelgan de una tercera. Sin deduplicar, el usuario tendría el permiso
        // dos veces y cualquier conteo saldría mal.
        var compartida = Patente("EQUIPO_VER");

        var a = Fam("A");
        a.Agregar(compartida);
        var b = Fam("B");
        b.Agregar(compartida);

        var raiz = Fam("RAIZ");
        raiz.Agregar(a);
        raiz.Agregar(b);

        raiz.ObtenerPatentes().Should().ContainSingle();
    }

    [Fact]
    public void Un_ciclo_entre_familias_no_cuelga_la_resolucion()
    {
        // Nada impide a nivel de base de datos armar A -> B -> A. Sin la guarda
        // de visitados, esto sería recursión infinita y tiraría abajo el proceso
        // en el primer login del usuario afectado.
        var a = Fam("A");
        var b = Fam("B");
        a.Agregar(Patente("DESDE_A"));
        b.Agregar(Patente("DESDE_B"));
        a.Agregar(b);
        b.Agregar(a);

        var resolver = () => a.ObtenerPatentes();

        resolver.Should().NotThrow();
        a.ObtenerPatentes().Select(p => p.DataKey).Should().BeEquivalentTo("DESDE_A", "DESDE_B");
    }

    [Fact]
    public void Una_familia_no_puede_contenerse_a_si_misma()
    {
        var familia = Fam("A");

        var accion = () => familia.Agregar(familia);

        accion.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Un_rol_suma_las_familias_y_las_patentes_sueltas()
    {
        // El Partner es el caso: tiene las familias de administración y operación
        // más ORGANIZACION_CAMBIAR, que no está en ninguna familia porque ningún
        // otro perfil la necesita.
        var administracion = Fam("ADMINISTRACION");
        administracion.Agregar(Patente("USUARIO_GESTIONAR"));

        var rol = new Rol { Id = Guid.NewGuid(), Nombre = "PARTNER" };
        rol.Familias.Add(administracion);
        rol.PatentesDirectas.Add(Patente("ORGANIZACION_CAMBIAR"));

        rol.ObtenerPatentes().Select(p => p.DataKey)
            .Should().BeEquivalentTo("USUARIO_GESTIONAR", "ORGANIZACION_CAMBIAR");
    }

    [Fact]
    public void Un_usuario_con_dos_roles_acumula_los_permisos_de_ambos()
    {
        var f1 = Fam("F1");
        f1.Agregar(Patente("A"));
        var f2 = Fam("F2");
        f2.Agregar(Patente("B"));

        var r1 = new Rol { Id = Guid.NewGuid(), Nombre = "R1" };
        r1.Familias.Add(f1);
        var r2 = new Rol { Id = Guid.NewGuid(), Nombre = "R2" };
        r2.Familias.Add(f2);

        var usuario = new Usuario { Id = Guid.NewGuid(), Username = "x" };
        usuario.Roles.Add(r1);
        usuario.Roles.Add(r2);

        usuario.Tiene("A").Should().BeTrue();
        usuario.Tiene("B").Should().BeTrue();
        usuario.Tiene("C").Should().BeFalse();
    }

    [Fact]
    public void Un_usuario_sin_roles_no_tiene_ningun_permiso()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Username = "x" };

        usuario.ObtenerPatentes().Should().BeEmpty();
        usuario.Tiene("EQUIPO_VER").Should().BeFalse();
    }

    [Theory]
    [InlineData("equipo_ver")]
    [InlineData("Equipo_Ver")]
    [InlineData("EQUIPO_VER")]
    public void La_comparacion_de_patentes_no_distingue_mayusculas(string consultada)
    {
        var familia = Fam("F");
        familia.Agregar(Patente("EQUIPO_VER"));

        familia.Concede(consultada).Should().BeTrue();
    }

    [Fact]
    public void El_partner_puede_operar_sobre_las_organizaciones_habilitadas()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var ajena = Guid.NewGuid();

        var partner = new Usuario { Id = Guid.NewGuid(), Username = "partner", IdOrganizacion = null };
        partner.OrganizacionesHabilitadas.Add(orgA);
        partner.OrganizacionesHabilitadas.Add(orgB);

        partner.PuedeOperarSobre(orgA).Should().BeTrue();
        partner.PuedeOperarSobre(orgB).Should().BeTrue();
        partner.PuedeOperarSobre(ajena).Should().BeFalse();
    }

    [Fact]
    public void Un_usuario_comun_solo_puede_operar_sobre_su_organizacion()
    {
        var propia = Guid.NewGuid();
        var usuario = new Usuario { Id = Guid.NewGuid(), Username = "admin", IdOrganizacion = propia };

        usuario.PuedeOperarSobre(propia).Should().BeTrue();
        usuario.PuedeOperarSobre(Guid.NewGuid()).Should().BeFalse();
    }
}
