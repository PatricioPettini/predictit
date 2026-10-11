using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace PredictIT.Service.Seguridad;

public class OpcionesToken
{
    public string Emisor { get; set; } = "PredictIT";
    public string Audiencia { get; set; } = "PredictIT.Web";
    public string Clave { get; set; } = string.Empty;
    public int MinutosVigencia { get; set; } = 480;
}

public interface ITokenService
{
    (string Token, DateTime Expira) Emitir(Guid idUsuario, string username, Guid idOrganizacion);
}

/// <summary>
/// Emisión del token de sesión.
///
/// El token lleva el usuario y la organización activa, y **no** lleva los
/// permisos: si los llevara, revocar una patente no tendría efecto hasta que el
/// token expirara. Los permisos se resuelven de la base en cada petición.
/// </summary>
public class TokenService : ITokenService
{
    public const string ClaimOrganizacion = "predictit:org";

    private readonly OpcionesToken _opciones;

    public TokenService(OpcionesToken opciones)
    {
        if (string.IsNullOrWhiteSpace(opciones.Clave) || opciones.Clave.Length < 32)
            throw new InvalidOperationException(
                "La clave de firma del token tiene que tener al menos 32 caracteres. " +
                "Se configura en Jwt:Clave o en la variable PREDICTIT_JWT_CLAVE.");

        _opciones = opciones;
    }

    public (string Token, DateTime Expira) Emitir(Guid idUsuario, string username, Guid idOrganizacion)
    {
        var expira = DateTime.UtcNow.AddMinutes(_opciones.MinutosVigencia);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, idUsuario.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(ClaimOrganizacion, idOrganizacion.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Clave)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opciones.Emisor,
            audience: _opciones.Audiencia,
            claims: claims,
            expires: expira,
            signingCredentials: credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}

/// <summary>Nombres de los claims estándar, para no repetir literales.</summary>
internal static class JwtRegisteredClaimNames
{
    public const string Sub = "sub";
    public const string UniqueName = "unique_name";
    public const string Jti = "jti";
}
