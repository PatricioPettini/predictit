using FluentAssertions;
using PredictIT.Service.Seguridad;

namespace PredictIT.Tests.Seguridad;

/// <summary>
/// Cifrado reversible (CU.Arq.006).
///
/// Lo que se prueba acá no es que «cifra»: eso lo garantiza AES. Lo que se
/// prueba es que el servicio no cometa los errores clásicos de esta clase de
/// código —reutilizar el nonce, aceptar un dato alterado, guardar en claro
/// cuando falta la clave— que son los que rompen el cifrado sin que se note.
/// </summary>
[Trait("Categoria", "Seguridad")]
public class CifradoTests
{
    private static CifradoService ConClave() => new(CifradoService.GenerarClave());

    [Fact]
    public void Lo_cifrado_se_recupera_igual()
    {
        var servicio = ConClave();
        const string clave = "sk-ant-api03-EJEMPLO-no-es-una-clave-real-0123456789";

        servicio.Descifrar(servicio.Cifrar(clave)).Should().Be(clave);
    }

    [Fact]
    public void El_mismo_texto_da_cifrados_distintos()
    {
        // Si dos cifrados del mismo texto fueran iguales, alguien con acceso a la
        // base sabría que dos organizaciones cargaron la misma clave sin poder
        // leerla. El nonce aleatorio es lo que lo evita.
        var servicio = ConClave();

        servicio.Cifrar("mismo texto").Should().NotBe(servicio.Cifrar("mismo texto"));
    }

    [Fact]
    public void Un_texto_alterado_no_se_descifra()
    {
        var servicio = ConClave();
        var cifrado = servicio.Cifrar("clave original");

        // Se altera un carácter del cuerpo cifrado, dejando intacto el formato.
        var partes = cifrado.Split('$');
        var cuerpo = partes[4].ToCharArray();
        cuerpo[0] = cuerpo[0] == 'A' ? 'B' : 'A';
        partes[4] = new string(cuerpo);

        var acto = () => servicio.Descifrar(string.Join('$', partes));

        acto.Should().Throw<InvalidOperationException>()
            .WithMessage("*alterado*");
    }

    [Fact]
    public void Otra_clave_no_descifra()
    {
        var cifrado = ConClave().Cifrar("secreto");

        var acto = () => ConClave().Descifrar(cifrado);

        acto.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Sin_clave_configurada_se_niega_a_cifrar()
    {
        // El punto de esta prueba es que la ausencia de clave sea un fallo
        // ruidoso. Un servicio que devolviera el texto tal cual «para no
        // romper» dejaría credenciales en claro en la base sin un solo aviso.
        var servicio = new CifradoService(null);

        servicio.Configurado.Should().BeFalse();
        var acto = () => servicio.Cifrar("algo");
        acto.Should().Throw<InvalidOperationException>().WithMessage("*clave de cifrado*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-base64!!")]
    [InlineData("Y29ydGE=")]
    public void Una_clave_invalida_se_rechaza_al_construir(string claveMala)
    {
        // Vacía se trata como «sin configurar»; las otras dos son errores de
        // configuración que conviene ver al arrancar y no en la primera
        // operación que necesite cifrar.
        if (claveMala.Length == 0)
        {
            new CifradoService(claveMala).Configurado.Should().BeFalse();
            return;
        }

        var acto = () => new CifradoService(claveMala);
        acto.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reconoce_lo_que_cifro_y_descarta_el_resto()
    {
        var servicio = ConClave();

        servicio.EsCifrado(servicio.Cifrar("x")).Should().BeTrue();
        servicio.EsCifrado(null).Should().BeFalse();
        servicio.EsCifrado("").Should().BeFalse();
        servicio.EsCifrado("sk-ant-api03-una-clave-en-claro").Should().BeFalse();

        // Un hash de contraseña tampoco es un texto cifrado, aunque comparta la
        // forma separada por «$». Confundirlos sería intentar descifrar algo que
        // por diseño no se puede.
        servicio.EsCifrado(HashContrasena.Derivar("hola", iteraciones: 1000)).Should().BeFalse();
    }

    [Fact]
    public void La_clave_generada_sirve_para_AES_256()
    {
        var clave = CifradoService.GenerarClave();

        Convert.FromBase64String(clave).Should().HaveCount(32);
        new CifradoService(clave).Configurado.Should().BeTrue();
    }

    [Fact]
    public void Soporta_texto_con_acentos_y_simbolos()
    {
        var servicio = ConClave();
        const string texto = "Ñandú — «clave» con acentos, emojis 🔐 y $ separadores$falsos";

        servicio.Descifrar(servicio.Cifrar(texto)).Should().Be(texto);
    }
}
