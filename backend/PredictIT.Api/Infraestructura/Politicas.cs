namespace PredictIT.Api.Infraestructura;

/// <summary>
/// Nombres de las políticas que configura <c>Program.cs</c> y que los
/// controladores referencian por atributo.
///
/// Están acá y no como cadenas sueltas porque un atributo necesita una
/// constante de compilación, y porque el día que el nombre cambie en un lado y
/// no en el otro la política simplemente deja de aplicarse: no falla el
/// arranque, no falla ninguna prueba, y el endpoint queda sin límite.
/// </summary>
public static class Politicas
{
    /// <summary>Límite de intentos de autenticación por dirección de origen.</summary>
    public const string IntentosDeLogin = "intentos-de-login";
}
