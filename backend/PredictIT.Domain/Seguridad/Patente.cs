namespace PredictIT.Domain.Seguridad;

/// <summary>
/// Permiso atómico. Es la hoja del Composite: no contiene a nadie.
///
/// <see cref="DataKey"/> es la clave estable que se usa en el código
/// (<c>[RequierePatente("EQUIPO_VER")]</c>). El <see cref="ComponenteSeguridad.Nombre"/>
/// es lo que se muestra en la pantalla de permisos y puede cambiar sin romper nada.
/// </summary>
/// <summary>
/// Nivel de acceso que otorga una patente.
///
/// El modelo de referencia declara el atributo y lo deja en 1 para todas sus
/// patentes, con lo cual no distingue nada. Acá se puebla con el nivel real,
/// porque es lo que permite responder «qué puede llegar a hacer este rol» sin
/// tener que leer las veintiséis patentes una por una.
///
/// Los valores son los del modelo de referencia —enteros— para no romper la
/// correspondencia con él.
/// </summary>
public enum TipoAcceso
{
    /// <summary>Ve datos y no cambia ninguno.</summary>
    Lectura = 1,

    /// <summary>Cambia datos de negocio: equipos, incidencias, mantenimientos.</summary>
    Escritura = 2,

    /// <summary>
    /// Cambia cómo se comporta el sistema o quién puede hacer qué. Es el nivel
    /// que hay que mirar dos veces antes de otorgar.
    /// </summary>
    Administracion = 3
}

public class Patente : ComponenteSeguridad
{
    public string DataKey { get; set; } = string.Empty;
    public string? Modulo { get; set; }
    public TipoAcceso TipoAcceso { get; set; } = TipoAcceso.Lectura;

    internal override void Recolectar(IDictionary<Guid, Patente> acumulado, ISet<Guid> visitados)
    {
        acumulado[Id] = this;
    }

    public override string ToString() => DataKey;
}
