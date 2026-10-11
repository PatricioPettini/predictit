using System.Text.RegularExpressions;
using FluentAssertions;
using PredictIT.Service.Seguridad;

namespace PredictIT.Tests.Seguridad;

[Trait("Categoria", "Unitaria")]
public class HashContrasenaTests
{
    [Fact]
    public void Una_contrasena_derivada_se_verifica_contra_su_propio_hash()
    {
        var hash = HashContrasena.Derivar("Admin.2026");

        HashContrasena.Verificar("Admin.2026", hash).Should().BeTrue();
    }

    [Fact]
    public void Una_contrasena_distinta_no_verifica()
    {
        var hash = HashContrasena.Derivar("Admin.2026");

        HashContrasena.Verificar("admin.2026", hash).Should().BeFalse("las contraseñas distinguen mayúsculas");
        HashContrasena.Verificar("Admin.2027", hash).Should().BeFalse();
        HashContrasena.Verificar("", hash).Should().BeFalse();
    }

    [Fact]
    public void Dos_derivaciones_de_la_misma_contrasena_dan_hashes_distintos()
    {
        // Cada derivación usa un salt nuevo. Si dos usuarios con la misma
        // contraseña tuvieran el mismo hash, una tabla precalculada rompería las
        // dos de una vez.
        var a = HashContrasena.Derivar("misma");
        var b = HashContrasena.Derivar("misma");

        a.Should().NotBe(b);
        HashContrasena.Verificar("misma", a).Should().BeTrue();
        HashContrasena.Verificar("misma", b).Should().BeTrue();
    }

    [Theory]
    [InlineData("admin", "Admin.2026")]
    [InlineData("tecnico1", "Tecnico.2026")]
    [InlineData("tecnico2", "Tecnico.2026")]
    [InlineData("tecnico3", "Tecnico.2026")]
    [InlineData("solicitante", "Usuario.2026")]
    [InlineData("partner", "Partner.2026")]
    public void Los_hashes_del_seed_verifican_con_su_contrasena(string usuario, string contrasena)
    {
        // El hash se LEE del script de datos iniciales en lugar de copiarse
        // acá. La copia envejece sin avisar: al subir las iteraciones de PBKDF2
        // el seed pasó a 600.000 y esta prueba se quedó verificando los hashes
        // de 120.000 que ya no están en ningún lado, sin ponerse en rojo.
        var hash = HashDelSeed(usuario);

        HashContrasena.Verificar(contrasena, hash)
            .Should().BeTrue($"el usuario {usuario} del seed tiene que poder iniciar sesión");
    }

    [Fact]
    public void Los_hashes_del_seed_usan_las_iteraciones_de_hoy()
    {
        // Si el seed queda con menos iteraciones, una instalación nueva arranca
        // con los hashes débiles y sólo se corrigen cuando cada persona entra.
        foreach (var usuario in new[] { "admin", "tecnico1", "tecnico2", "tecnico3",
                                        "solicitante", "partner" })
        {
            HashContrasena.NecesitaRehash(HashDelSeed(usuario))
                .Should().BeFalse($"el hash de {usuario} en el seed tiene que estar al día");
        }
    }

    /// <summary>El hash de un usuario, leído de <c>db/03-seed-servicio.sql</c>.</summary>
    private static string HashDelSeed(string usuario)
    {
        var seed = Path.Combine(RaizDelRepositorio(), "db", "03-seed-servicio.sql");
        File.Exists(seed).Should().BeTrue($"hace falta {seed} para leer los hashes");

        // La fila del MERGE tiene el username y, en el renglón siguiente, el hash.
        var patron = "'" + Regex.Escape(usuario) + @"',\s*\r?\n\s*'(PBKDF2\$[^']+)'";
        var m = Regex.Match(File.ReadAllText(seed), patron);
        m.Success.Should().BeTrue($"el seed tiene que traer el hash de {usuario}");
        return m.Groups[1].Value;
    }

    /// <summary>
    /// La raíz del repositorio, subiendo desde donde corre la prueba.
    ///
    /// No se usa una ruta relativa fija porque el directorio de trabajo cambia
    /// según se corra desde la solución, desde el proyecto o desde el CI.
    /// </summary>
    private static string RaizDelRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "db")))
        {
            dir = dir.Parent;
        }
        dir.Should().NotBeNull("la prueba tiene que poder encontrar la carpeta db/");
        return dir!.FullName;
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto plano")]
    [InlineData("PBKDF2$120000$solo-tres-partes")]
    [InlineData("BCRYPT$120000$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2$abc$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2$120000$no-es-base64!$aGFzaA==")]
    public void Un_hash_malformado_no_verifica_y_no_explota(string almacenado)
    {
        // Ante un hash corrupto hay que devolver "no verifica", no una excepción:
        // una excepción acá se traduciría en un 500 y le diría al atacante que
        // ese usuario existe y tiene el registro dañado.
        var accion = () => HashContrasena.Verificar("cualquiera", almacenado);

        accion.Should().NotThrow();
        HashContrasena.Verificar("cualquiera", almacenado).Should().BeFalse();
    }

    [Fact]
    public void Un_hash_con_menos_iteraciones_se_marca_para_rehash()
    {
        HashContrasena.NecesitaRehash(HashContrasena.Derivar("x", iteraciones: 1000)).Should().BeTrue();
        HashContrasena.NecesitaRehash(HashContrasena.Derivar("x")).Should().BeFalse();
    }
}
