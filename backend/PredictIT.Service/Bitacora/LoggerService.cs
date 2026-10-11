namespace PredictIT.Service.Seguridad;

/// <summary>
/// Registro técnico de la aplicación, separado de la bitácora.
///
/// La bitácora es un registro de negocio y auditoría: quién hizo qué. El logger
/// es para lo que le pasa al sistema, incluido el caso en que la bitácora misma
/// falla —que por definición no se puede asentar en la bitácora—.
/// </summary>
public interface ILoggerService
{
    void Informacion(string mensaje);
    void Advertencia(string mensaje);
    void Error(string mensaje, Exception? ex = null);
}

/// <summary>Implementación sobre el logger de la plataforma.</summary>
public class LoggerService : ILoggerService
{
    private readonly Microsoft.Extensions.Logging.ILogger<LoggerService> _log;

    public LoggerService(Microsoft.Extensions.Logging.ILogger<LoggerService> log) => _log = log;

    public void Informacion(string mensaje) =>
        Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(_log, "{Mensaje}", mensaje);

    public void Advertencia(string mensaje) =>
        Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(_log, "{Mensaje}", mensaje);

    public void Error(string mensaje, Exception? ex = null) =>
        Microsoft.Extensions.Logging.LoggerExtensions.LogError(_log, ex, "{Mensaje}", mensaje);
}
