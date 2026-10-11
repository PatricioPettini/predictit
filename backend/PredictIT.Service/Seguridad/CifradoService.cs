using System.Security.Cryptography;
using System.Text;

namespace PredictIT.Service.Seguridad;

/// <summary>
/// Cifrado y descifrado reversible (CU.Arq.006).
///
/// Es lo opuesto a <see cref="HashContrasena"/>, y la distinción importa: una
/// contraseña se deriva en un sentido y no se recupera nunca, porque el sistema
/// no necesita conocerla —sólo verificarla—. En cambio la clave de API de un
/// proveedor externo hay que **enviarla**, así que hay que poder recuperarla.
/// Usar hash ahí sería inútil; usar cifrado en las contraseñas sería peor que
/// inútil, porque volvería recuperable algo que no tiene por qué serlo.
///
/// Algoritmo: AES-256 en modo GCM, que además de cifrar autentica. Un texto
/// alterado no se descifra a basura silenciosamente: falla, porque la etiqueta
/// de autenticación no coincide. Con AES-CBC habría que sumar un HMAC aparte
/// para conseguir lo mismo, y ese es el error clásico de esta clase de código.
///
/// Formato almacenado: <c>AESGCM$v1$nonce$etiqueta$cifrado</c>, las tres partes
/// en Base64. Lleva versión adelante para poder rotar el algoritmo sin tener que
/// adivinar con qué se cifró cada fila.
/// </summary>
public interface ICifradoService
{
    /// <summary>Cifra un texto. Dos llamadas con el mismo texto dan resultados distintos.</summary>
    string Cifrar(string texto);

    /// <summary>
    /// Descifra. Lanza si el texto fue alterado o si está cifrado con otra
    /// clave: es preferible a devolver un valor que parezca válido.
    /// </summary>
    string Descifrar(string cifrado);

    /// <summary>Si el valor tiene la forma de algo cifrado por este servicio.</summary>
    bool EsCifrado(string? valor);

    /// <summary>Si hay clave de cifrado configurada. Sin clave, cifrar falla.</summary>
    bool Configurado { get; }
}

public class CifradoService : ICifradoService
{
    private const string Etiqueta = "AESGCM";
    private const string Version = "v1";
    private const int BytesClave = 32;   // AES-256
    private const int BytesNonce = 12;   // el tamaño que recomienda GCM
    private const int BytesTag = 16;

    private readonly byte[]? _clave;

    /// <param name="claveBase64">
    /// Clave de 32 bytes en Base64. Viene de configuración y nunca del código:
    /// una clave en el repositorio es una clave pública.
    /// </param>
    public CifradoService(string? claveBase64)
    {
        if (string.IsNullOrWhiteSpace(claveBase64)) return;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(claveBase64);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "La clave de cifrado no está en Base64. Generá una con " +
                "[Convert]::ToBase64String((1..32 | %{ Get-Random -Max 256 })).");
        }

        if (bytes.Length != BytesClave)
        {
            throw new InvalidOperationException(
                $"La clave de cifrado tiene {bytes.Length} bytes y AES-256 necesita {BytesClave}.");
        }

        _clave = bytes;
    }

    public bool Configurado => _clave is not null;

    public string Cifrar(string texto)
    {
        if (texto is null) throw new ArgumentNullException(nameof(texto));

        var clave = _clave ?? throw new InvalidOperationException(
            "No hay clave de cifrado configurada: falta Cifrado:Clave o PREDICTIT_CLAVE_CIFRADO. " +
            "Guardar el dato en claro no es una alternativa aceptable, así que la operación se rechaza.");

        // Nonce nuevo en cada cifrado. Repetir un nonce con la misma clave es la
        // única forma de romper GCM sin romper AES, así que no se deriva de nada
        // ni se reutiliza: se sortea.
        var nonce = RandomNumberGenerator.GetBytes(BytesNonce);
        var plano = Encoding.UTF8.GetBytes(texto);
        var cifrado = new byte[plano.Length];
        var tag = new byte[BytesTag];

        using var aes = new AesGcm(clave, BytesTag);
        aes.Encrypt(nonce, plano, cifrado, tag);

        return string.Join('$', Etiqueta, Version,
                           Convert.ToBase64String(nonce),
                           Convert.ToBase64String(tag),
                           Convert.ToBase64String(cifrado));
    }

    public string Descifrar(string cifrado)
    {
        var clave = _clave ?? throw new InvalidOperationException(
            "No hay clave de cifrado configurada, así que no se puede descifrar lo guardado.");

        if (!EsCifrado(cifrado))
            throw new InvalidOperationException("El valor no tiene el formato de un texto cifrado.");

        var partes = cifrado.Split('$');

        byte[] nonce, tag, datos;
        try
        {
            nonce = Convert.FromBase64String(partes[2]);
            tag = Convert.FromBase64String(partes[3]);
            datos = Convert.FromBase64String(partes[4]);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("El texto cifrado está corrupto.");
        }

        if (nonce.Length != BytesNonce || tag.Length != BytesTag)
            throw new InvalidOperationException("El texto cifrado está corrupto.");

        var plano = new byte[datos.Length];

        using var aes = new AesGcm(clave, BytesTag);
        try
        {
            aes.Decrypt(nonce, datos, tag, plano);
        }
        catch (CryptographicException)
        {
            // Pasa por dos motivos distintos que no conviene distinguir hacia
            // afuera: la clave no es la misma, o alguien tocó el dato guardado.
            throw new InvalidOperationException(
                "No se pudo descifrar: el dato fue alterado o está cifrado con otra clave.");
        }

        return Encoding.UTF8.GetString(plano);
    }

    public bool EsCifrado(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return false;

        var partes = valor.Split('$');
        return partes.Length == 5 && partes[0] == Etiqueta && partes[1] == Version;
    }

    /// <summary>
    /// Genera una clave nueva. Está acá y no en un script para que el modo de
    /// generarla sea el mismo que el de usarla.
    /// </summary>
    public static string GenerarClave() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(BytesClave));
}
