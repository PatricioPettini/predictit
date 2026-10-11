using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.IA;
using PredictIT.Service.Respaldos;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// Configuración del proveedor de inteligencia artificial (CU.Arq.006).
///
/// La clave de API entra y no vuelve a salir: se guarda cifrada y lo único que
/// se puede consultar es si hay una y de dónde viene. Ningún método devuelve su
/// valor, ni truncado.
/// </summary>
public class IntegracionIaBusiness(
    IFactoryDaoSeguridad seguridad,
    IContextoSesion contexto,
    IFactoryDao dao,
    IBitacoraService bitacora,
    IProveedorIA proveedorIa,
    ICifradoService cifrado)
    : BaseAdministracion(seguridad, contexto), IIntegracionIaBusiness
{
    public PanelIaDto PanelIa()
    {
        Exigir(Patentes.IaConfigurar);

        var c = dao.ConfiguracionAsignacion.DeLaOrganizacion() ?? new ConfiguracionAsignacion();

        var recomendaciones = dao.Incidencias.GetAll()
            .Select(i => dao.Incidencias.RecomendacionDe(i.Id))
            .Where(r => r is not null)
            .ToList();

        return new PanelIaDto(
            new ConfiguracionIaDto
            {
                ProveedorIa = c.ProveedorIa,
                Modelo = c.Modelo,
                EnviarCarga = c.EnviarCarga,
                EnviarEspecialidad = c.EnviarEspecialidad,
                EnviarHistorial = c.EnviarHistorial,
                EnviarDisponibilidad = c.EnviarDisponibilidad,
                EstrategiaRespaldo = c.EstrategiaRespaldo,
                TimeoutSegundos = c.TimeoutSegundos
            },
            EstadoDeLaIa(),
            OrigenDeLaClave(c) != "NINGUNA",
            OrigenDeLaClave(c),
            recomendaciones.Count(r => r!.Origen == "IA"),
            recomendaciones.Count(r => r!.Origen == "HEURISTICA"),
            recomendaciones.Count(r => r!.Origen == "RESPALDO"));
    }

    /// <summary>
    /// De dónde sale la clave de API, con la base primero.
    ///
    /// El orden importa: si la organización cargó su propia clave, esa manda
    /// sobre la del servidor. La variable de entorno queda como respaldo para el
    /// entorno de desarrollo, donde no se quiere configurar nada para arrancar.
    /// </summary>
    private string OrigenDeLaClave(ConfiguracionAsignacion c)
    {
        if (cifrado.EsCifrado(c.ApiKeyCifrada)) return "BASE";

        return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"))
            ? "NINGUNA"
            : "ENTORNO";
    }

    public void GuardarClaveIa(string clave)
    {
        Exigir(Patentes.IaConfigurar);

        clave = (clave ?? string.Empty).Trim();

        if (clave.Length < 20)
        {
            throw BusinessException.De("clave",
                "Esa no parece una clave de API: son cadenas largas. Revisá que la hayas copiado entera.");
        }

        if (!cifrado.Configurado)
        {
            // No se guarda en claro como alternativa. Que el sistema quede sin
            // clave es recuperable; una credencial de un tercero en claro en la
            // base, no.
            throw new BusinessException(
                "No hay clave de cifrado configurada en el servidor, así que no se puede " +
                "guardar la clave de API de forma segura. Configurá Cifrado:Clave primero.");
        }

        var actual = dao.ConfiguracionAsignacion.DeLaOrganizacion() ?? new ConfiguracionAsignacion();
        actual.ApiKeyCifrada = cifrado.Cifrar(clave);
        dao.ConfiguracionAsignacion.Guardar(actual);

        // Se asienta el hecho y jamás el valor, ni recortado: los primeros
        // caracteres de una credencial ya son información sobre ella.
        bitacora.Registrar(TipoEvento.Codigos.Modificacion,
            "Se guardó una clave de API cifrada para el proveedor de asignación.",
            Contexto.IdUsuario, Contexto.IdOrganizacion);
    }

    public void BorrarClaveIa()
    {
        Exigir(Patentes.IaConfigurar);

        var actual = dao.ConfiguracionAsignacion.DeLaOrganizacion();
        if (actual is null || actual.ApiKeyCifrada is null) return;

        actual.ApiKeyCifrada = null;
        dao.ConfiguracionAsignacion.Guardar(actual);

        bitacora.Registrar(TipoEvento.Codigos.Modificacion,
            "Se borró la clave de API del proveedor de asignación.",
            Contexto.IdUsuario, Contexto.IdOrganizacion);
    }

    public void GuardarConfiguracionIa(ConfiguracionIaDto entrada)
    {
        Exigir(Patentes.IaConfigurar);

        if (entrada.ProveedorIa is not ("SIMULADO" or "CLAUDE"))
            throw BusinessException.De("proveedorIa", "El proveedor tiene que ser SIMULADO o CLAUDE.");

        if (entrada.EstrategiaRespaldo is not ("MENOR_CARGA" or "SIN_ASIGNAR"))
        {
            throw BusinessException.De("estrategiaRespaldo",
                "La estrategia de respaldo tiene que ser MENOR_CARGA o SIN_ASIGNAR.");
        }

        if (entrada.TimeoutSegundos is < 1 or > 120)
            throw BusinessException.De("timeoutSegundos", "El timeout va de 1 a 120 segundos.");

        // Desactivar la especialidad degrada mucho la asignación: es el dato
        // central del Contexto. Se avisa en bitácora en lugar de impedirlo — la
        // decisión sobre los datos de su gente es de la organización.
        if (!entrada.EnviarEspecialidad)
        {
            bitacora.Registrar(TipoEvento.Codigos.Modificacion,
                "Se desactivó el envío de la especialidad de los técnicos al proveedor de " +
                "asignación. La asignación va a decidirse sólo por carga de trabajo.");
        }

        var actual = dao.ConfiguracionAsignacion.DeLaOrganizacion() ?? new ConfiguracionAsignacion();

        dao.ConfiguracionAsignacion.Guardar(new ConfiguracionAsignacion
        {
            ProveedorIa = entrada.ProveedorIa,
            // La clave no llega por este camino y no se toca: se toma de la
            // variable de entorno. Reescribirla acá con un nulo la borraría.
            ApiKeyCifrada = actual.ApiKeyCifrada,
            Modelo = string.IsNullOrWhiteSpace(entrada.Modelo) ? null : entrada.Modelo.Trim(),
            EnviarCarga = entrada.EnviarCarga,
            EnviarEspecialidad = entrada.EnviarEspecialidad,
            EnviarHistorial = entrada.EnviarHistorial,
            EnviarDisponibilidad = entrada.EnviarDisponibilidad,
            EstrategiaRespaldo = entrada.EstrategiaRespaldo,
            TimeoutSegundos = entrada.TimeoutSegundos
        });

        bitacora.Registrar(TipoEvento.Codigos.Modificacion,
            $"Configuración de asignación: proveedor {entrada.ProveedorIa}, respaldo " +
            $"{entrada.EstrategiaRespaldo}, timeout {entrada.TimeoutSegundos} s.");
    }

    /// <summary>
    /// Estado del proveedor, con la última consulta registrada. El texto lo
    /// arma `EstadoProveedor` para que esta pantalla y la de análisis no digan
    /// cosas distintas del mismo estado.
    /// </summary>
    private EstadoIaDto EstadoDeLaIa()
    {
        var o = EstadoProveedor.Observar(proveedorIa);
        var c = dao.ConfiguracionAsignacion.DeLaOrganizacion();

        return new EstadoIaDto(
            o.Proveedor,
            o.Estado.ToString().ToUpperInvariant(),
            o.FallosConsecutivos,
            o.SinRespuestaDesde,
            c?.UltimaConsultaOk,
            c?.UltimoError,
            o.Mensaje);
    }
}
