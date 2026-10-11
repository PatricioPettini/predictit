using System.Data;

namespace PredictIT.DAO.Helpers;

/// <summary>
/// Lectura tipada de un <see cref="IDataRecord"/>.
///
/// Existe para que los mappers no repitan el mismo <c>IsDBNull</c> en cada
/// columna: sin esto, cada mapper son treinta líneas de ceremonia y el error de
/// olvidarse una comprobación aparece recién en tiempo de ejecución y sólo
/// cuando el dato viene nulo.
/// </summary>
public static class LectorExtensiones
{
    public static Guid Guid(this IDataRecord r, string columna) => r.GetGuid(r.GetOrdinal(columna));

    public static Guid? GuidNulo(this IDataRecord r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? null : r.GetGuid(i);
    }

    public static string Texto(this IDataRecord r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? string.Empty : r.GetString(i);
    }

    public static string? TextoNulo(this IDataRecord r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    public static int Entero(this IDataRecord r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
    }

    public static decimal Decimal(this IDataRecord r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i));
    }

    /// <summary>
    /// Distingue el nulo del cero. Importa donde el cero es un valor legítimo:
    /// «sin objetivo de resolución» no es «objetivo de cero horas».
    /// </summary>
    public static int? EnteroNulo(this IDataRecord r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
    }

    /// <summary>
    /// Para columnas BIGINT. `EnteroNulo` las truncaría: el tamaño de un
    /// respaldo pasa los 2 GB sin esfuerzo.
    /// </summary>
    public static long? LargoNulo(this IDataRecord r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? null : Convert.ToInt64(r.GetValue(i));
    }

    public static decimal? DecimalNulo(this IDataRecord r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? null : Convert.ToDecimal(r.GetValue(i));
    }

    public static bool Booleano(this IDataRecord r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return !r.IsDBNull(i) && r.GetBoolean(i);
    }

    public static DateTime Fecha(this IDataRecord r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? default : r.GetDateTime(i);
    }

    public static DateTime? FechaNula(this IDataRecord r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? null : r.GetDateTime(i);
    }

    public static DateOnly? SoloFechaNula(this IDataRecord r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? null : DateOnly.FromDateTime(r.GetDateTime(i));
    }

    /// <summary>Indica si el conjunto de resultados trae esa columna.</summary>
    public static bool Tiene(this IDataRecord r, string columna)
    {
        for (var i = 0; i < r.FieldCount; i++)
        {
            if (string.Equals(r.GetName(i), columna, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
