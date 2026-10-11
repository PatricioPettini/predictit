namespace PredictIT.BLL.DTO;

/* ----------------------------------------------------------------------
   Multiidioma (Req. Arq. 001 · CU.Arq.004 Traducir)
   ---------------------------------------------------------------------- */

public record IdiomaDto(string Codigo, string Nombre, bool EsPorDefecto);

/// <summary>
/// El diccionario completo de un idioma.
/// </summary>
/// <param name="Textos">
/// Clave a texto. Viaja entero y no de a una clave: la interfaz necesita todas
/// para dibujar la primera pantalla, y pedirlas de a una serían doscientos
/// viajes al servidor por carga.
/// </param>
public record DiccionarioDto(
    string Codigo,
    string Nombre,
    IDictionary<string, string> Textos);

public record CoberturaPorIdiomaDto(
    string Codigo,
    string Nombre,
    int ClavesTraducidas,
    IReadOnlyList<string> ClavesFaltantes);

/// <summary>
/// Cobertura de traducción por idioma.
///
/// Existe porque una clave sin traducir no se nota: el diccionario cae al
/// idioma por defecto y el texto aparece, sólo que en el idioma equivocado.
/// Esto lo hace medible.
/// </summary>
public record CoberturaIdiomaDto(
    int ClavesTotales,
    IReadOnlyList<CoberturaPorIdiomaDto> PorIdioma);
