namespace PredictIT.Domain.Seguridad;

/// <summary>
/// Idioma disponible para la interfaz (Req. Arq. 001).
///
/// El código sigue la convención BCP 47 —`es-AR`, `en-US`— y no un identificador
/// propio: es lo que entiende el navegador, así que se puede negociar el idioma
/// inicial con el encabezado `Accept-Language` sin traducir nada.
/// </summary>
public class Idioma
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    /// <summary>
    /// El idioma al que se cae cuando el pedido no indica ninguno o indica uno
    /// que no está activo. Hay exactamente uno.
    /// </summary>
    public bool EsPorDefecto { get; set; }

    public override string ToString() => Nombre;
}

/// <summary>Una traducción: la clave y su texto en un idioma.</summary>
public class Traduccion
{
    public Guid Id { get; set; }
    public Guid IdIdioma { get; set; }

    /// <summary>
    /// Clave con ámbito por puntos —`nav.activos`, `incidencia.titulo`— para que
    /// se pueda ver de un vistazo a qué pantalla pertenece el texto.
    /// </summary>
    public string Clave { get; set; } = string.Empty;

    public string Texto { get; set; } = string.Empty;
}
