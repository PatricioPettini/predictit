using PredictIT.BLL;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.DAO.Helpers;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.IA;
using PredictIT.Service.Prediccion;
using PredictIT.Service.Reportes;
using PredictIT.Service.Respaldos;
using PredictIT.Service.Seguridad;

namespace PredictIT.Api.Infraestructura;

/// <summary>
/// Barrido predictivo programado (RF-11, CU-010).
///
/// Es el segundo disparador del motor. El primero es el hecho —una incidencia o
/// un mantenimiento reevalúan su equipo en el acto—, pero hay reglas que se
/// cumplen sin que pase nada: un mantenimiento vence, una garantía se acerca a
/// su fin, un equipo cumple años. Esas condiciones aparecen por el paso del
/// tiempo, y sin este barrido la alerta esperaría a que alguien abriera la
/// pantalla y apretara «Evaluar».
///
/// Corre sobre todas las organizaciones activas, cada una con su propio
/// contexto, porque las reglas son por organización.
/// </summary>
public class BarridoPredictivo(
    IServiceScopeFactory ambitos,
    ILogger<BarridoPredictivo> log,
    OpcionesBarrido opciones) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        if (opciones.Intervalo <= TimeSpan.Zero)
        {
            log.LogInformation("Barrido predictivo desactivado por configuración.");
            return;
        }

        // Se espera antes del primer barrido: al arrancar, la API todavía está
        // aplicando migraciones y atendiendo el primer login, y un barrido del
        // parque compite por las mismas conexiones.
        try
        {
            await Task.Delay(opciones.EsperaInicial, cancelacion);
        }
        catch (OperationCanceledException) { return; }

        while (!cancelacion.IsCancellationRequested)
        {
            try
            {
                Barrer();
            }
            catch (Exception ex)
            {
                // Un barrido que falla no puede matar el servicio en segundo
                // plano: si la excepción escapara de ExecuteAsync, el job no
                // volvería a correr hasta reiniciar la API.
                log.LogError(ex, "Falló el barrido predictivo.");
            }

            try
            {
                await Task.Delay(opciones.Intervalo, cancelacion);
            }
            catch (OperationCanceledException) { return; }
        }
    }

    private void Barrer()
    {
        using var ambito = ambitos.CreateScope();
        var sp = ambito.ServiceProvider;

        // Pool propio y acotado (bulkhead, ADR 0009): el barrido recorre el parque
        // completo de cada organización, y sin esto puede quedarse con las
        // conexiones que necesitan las peticiones de los usuarios.
        var conexiones = sp.GetRequiredService<ConexionesSql>().ParaSegundoPlano();
        var logger = sp.GetRequiredService<ILoggerService>();

        var daoSeguridad = new FactoryDaoSeguridad(conexiones);
        var bitacora = new BitacoraService(daoSeguridad, logger);

        // Las organizaciones se leen con un DAO sin contexto: es la única
        // lectura que cruza organizaciones y por eso no pasa por un business.
        var organizaciones = new FactoryDao(conexiones, ContextoSesion.Anonimo())
            .Organizaciones.Activas();

        foreach (var organizacion in organizaciones)
        {
            var contexto = ContextoSesion.Sistema(organizacion.Id);
            var dao = new FactoryDao(conexiones, contexto);

            var negocio = new FactoryBusiness(
                dao, daoSeguridad, contexto, bitacora,
                sp.GetRequiredService<ISeguridadService>(),
                sp.GetRequiredService<ITokenService>(),
                new ProveedorIASimulado(() => 0),
                sp.GetRequiredService<MotorPredictivo>(),
                sp.GetRequiredService<IRespaldoService>(),
                sp.GetRequiredService<ICifradoService>(),
                sp.GetRequiredService<IReporteService>());

            try
            {
                var resultado = negocio.Prediccion.EvaluarParque();

                log.LogInformation(
                    "Barrido predictivo de {Organizacion}: {Equipos} equipos, {Alertas} alertas nuevas.",
                    organizacion.NombreCorto, resultado.EquiposEvaluados, resultado.AlertasNuevas);
            }
            catch (Exception ex)
            {
                // Que una organización falle no puede dejar sin evaluar a las
                // demás: sin reglas configuradas, por ejemplo, EvaluarParque
                // lanza a propósito.
                log.LogWarning(ex, "No se pudo evaluar la organización {Organizacion}.",
                               organizacion.NombreCorto);

                bitacora.Registrar(TipoEvento.Codigos.ErrorSistema,
                    $"Barrido predictivo: no se pudo evaluar {organizacion.NombreCorto}. {ex.Message}",
                    null, organizacion.Id, traza: ex.ToString());
            }
        }
    }
}

/// <summary>Cada cuánto corre el barrido y cuánto espera antes del primero.</summary>
public class OpcionesBarrido
{
    public TimeSpan Intervalo { get; init; } = TimeSpan.FromHours(24);
    public TimeSpan EsperaInicial { get; init; } = TimeSpan.FromMinutes(2);
}
