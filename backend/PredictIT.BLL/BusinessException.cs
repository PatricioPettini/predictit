namespace PredictIT.BLL;

/// <summary>
/// Regla de negocio incumplida.
///
/// Se distingue de una excepción cualquiera porque su mensaje **está pensado
/// para mostrarse al usuario**: la API la traduce a 400 con ese texto. Todo lo
/// demás que se escape se traduce a 500 con un mensaje genérico, porque no se
/// puede saber si expone detalles internos.
/// </summary>
public class BusinessException : Exception
{
    public BusinessException(string mensaje) : base(mensaje) { }

    public BusinessException(string mensaje, Exception interna) : base(mensaje, interna) { }

    /// <summary>Campo del formulario al que corresponde el problema, si aplica.</summary>
    public string? Campo { get; init; }

    /// <summary>
    /// Motivo en forma de codigo estable, para cuando el frontend tiene que
    /// hacer algo distinto segun cual sea y no alcanza con mostrar el texto.
    ///
    /// No reemplaza al mensaje: el mensaje se muestra, el codigo se compara. Se
    /// agrego porque la pantalla de ingreso trata distinto a las credenciales
    /// invalidas que a la cuenta bloqueada, y comparar el texto se rompe en
    /// cuanto la interfaz esta en ingles.
    /// </summary>
    public string? Codigo { get; init; }

    public static BusinessException De(string campo, string mensaje) =>
        new(mensaje) { Campo = campo };
}

/// <summary>
/// El usuario está autenticado pero no tiene la patente necesaria.
///
/// Va aparte de <see cref="BusinessException"/> porque la API la traduce a 403 y
/// no a 400, y porque se asienta en bitácora como acceso denegado.
/// </summary>
public class PermisoDenegadoException : Exception
{
    public PermisoDenegadoException(string dataKey)
        : base($"No tenés permiso para realizar esta acción ({dataKey}).")
    {
        DataKey = dataKey;
    }

    public string DataKey { get; }
}
