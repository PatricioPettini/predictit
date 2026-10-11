using System.Security.Cryptography;

namespace PredictIT.Service.Seguridad;

/// <summary>
/// Derivación y verificación de contraseñas con PBKDF2-HMAC-SHA256.
///
/// El formato almacenado es <c>PBKDF2$iteraciones$salt$hash</c>, con salt y hash
/// en Base64. Es el mismo del proyecto de referencia de 3.º año, y los usuarios
/// de demostración de la base ya están hasheados así.
///
/// La comparación es en tiempo constante: comparar con <c>==</c> corta en el
/// primer byte distinto y filtra información sobre el hash correcto a quien mida
/// los tiempos de respuesta.
///
/// Las iteraciones son las que recomienda OWASP para PBKDF2-HMAC-SHA256, y el
/// número viaja adentro del hash a propósito: subirlo no invalida lo ya
/// guardado, porque cada hash se verifica con las iteraciones con las que se
/// derivó. Lo que hace falta para que el cambio llegue a los usuarios viejos es
/// volver a derivar cuando entran, y de eso se ocupa el servicio de seguridad.
/// </summary>
public static class HashContrasena
{
    private const string Etiqueta = "PBKDF2";
    // 600.000 es la recomendación de la OWASP Password Storage Cheat Sheet para
    // PBKDF2-HMAC-SHA256. El proyecto venía con 120.000, que era el valor del
    // sistema de referencia de 3.º año y estaba por debajo: la revisión OWASP
    // lo anotaba como pendiente de A02.
    private const int IteracionesPorDefecto = 600_000;
    private const int BytesSalt = 16;
    private const int BytesHash = 32;

    public static string Derivar(string contrasena, int iteraciones = IteracionesPorDefecto)
    {
        if (string.IsNullOrEmpty(contrasena))
            throw new ArgumentException("La contraseña no puede estar vacía.", nameof(contrasena));

        var salt = RandomNumberGenerator.GetBytes(BytesSalt);
        var hash = Derivar(contrasena, salt, iteraciones);
        return string.Join('$', Etiqueta, iteraciones, Convert.ToBase64String(salt),
                           Convert.ToBase64String(hash));
    }

    public static bool Verificar(string contrasena, string almacenado)
    {
        if (string.IsNullOrWhiteSpace(contrasena) || string.IsNullOrWhiteSpace(almacenado))
            return false;

        var partes = almacenado.Split('$');
        if (partes.Length != 4 || partes[0] != Etiqueta) return false;
        if (!int.TryParse(partes[1], out var iteraciones) || iteraciones <= 0) return false;

        byte[] salt, esperado;
        try
        {
            salt = Convert.FromBase64String(partes[2]);
            esperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var calculado = Derivar(contrasena, salt, iteraciones, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    /// <summary>Indica si el hash conviene recalcularse porque quedó con menos iteraciones.</summary>
    public static bool NecesitaRehash(string almacenado)
    {
        var partes = almacenado.Split('$');
        return partes.Length != 4
               || partes[0] != Etiqueta
               || !int.TryParse(partes[1], out var it)
               || it < IteracionesPorDefecto;
    }

    private static byte[] Derivar(string contrasena, byte[] salt, int iteraciones, int bytes = BytesHash) =>
        Rfc2898DeriveBytes.Pbkdf2(contrasena, salt, iteraciones, HashAlgorithmName.SHA256, bytes);
}
