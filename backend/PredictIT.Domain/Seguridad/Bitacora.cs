namespace PredictIT.Domain.Seguridad;

public enum CriticidadEvento
{
    Info,
    Advertencia,
    Error,
    Critico
}

/// <summary>Catálogo de tipos de evento de la bitácora.</summary>
public class TipoEvento
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public CriticidadEvento Criticidad { get; set; } = CriticidadEvento.Info;

    public override string ToString() => Codigo;

    /// <summary>Códigos de los tipos de evento que el sistema asienta.</summary>
    public static class Codigos
    {
        public const string LoginOk = "LOGIN_OK";
        public const string LoginFallido = "LOGIN_FALLIDO";
        public const string Logout = "LOGOUT";
        public const string AccesoDenegado = "ACCESO_DENEGADO";
        public const string Alta = "ALTA";
        public const string Modificacion = "MODIFICACION";
        public const string Baja = "BAJA";
        public const string ClasificacionIa = "CLASIFICACION_IA";
        public const string AsignacionIa = "ASIGNACION_IA";
        public const string AsignacionHeuristica = "ASIGNACION_HEURISTICA";
        public const string ClasificacionHeuristica = "CLASIFICACION_HEURISTICA";
        public const string AsignacionRespaldo = "ASIGNACION_RESPALDO";
        public const string GuiaReparacionIa = "GUIA_REPARACION_IA";
        public const string GuiaReparacionHeuristica = "GUIA_REPARACION_HEURISTICA";
        public const string ReasignacionManual = "REASIGNACION_MANUAL";
        public const string IncidenciaAlta = "INCIDENCIA_ALTA";
        public const string IncidenciaSinAsignar = "INCIDENCIA_SIN_ASIGNAR";
        public const string IncidenciaAtendida = "INCIDENCIA_ATENDIDA";
        public const string IncidenciaResuelta = "INCIDENCIA_RESUELTA";
        public const string IncidenciaCerrada = "INCIDENCIA_CERRADA";
        public const string IncidenciaAnulada = "INCIDENCIA_ANULADA";
        public const string EquipoCambioEstado = "EQUIPO_CAMBIO_ESTADO";
        public const string ReporteGenerado = "REPORTE_GENERADO";
        public const string MantenimientoAlta = "MANTENIMIENTO_ALTA";

        // La agenda del preventivo deja rastro igual que la ejecución: que
        // un trabajo se corra o se anule es tan auditable como que se haga,
        // y es lo que permite contestar después por qué el equipo llegó sin
        // mantenimiento a la falla.
        public const string MantenimientoProgramado = "MANTENIMIENTO_PROGRAMADO";
        public const string MantenimientoReprogramado = "MANTENIMIENTO_REPROGRAMADO";
        public const string MantenimientoAnulado = "MANTENIMIENTO_ANULADO";
        // La facturacion del servicio. Emitir y cobrar son hechos economicos:
        // tienen que poder reconstruirse despues sin depender de que alguien
        // se acuerde de quien apreto el boton.
        public const string ComprobanteGenerado = "COMPROBANTE_GENERADO";
        public const string ComprobanteAjustado = "COMPROBANTE_AJUSTADO";
        public const string ComprobanteEmitido = "COMPROBANTE_EMITIDO";
        public const string ComprobantePagado = "COMPROBANTE_PAGADO";
        public const string ComprobanteAnulado = "COMPROBANTE_ANULADO";
        public const string ReglaModificada = "REGLA_MODIFICADA";
        public const string AlertaGenerada = "ALERTA_GENERADA";
        public const string AnalisisPredictivo = "ANALISIS_PREDICTIVO";
        public const string AlertaAtendida = "ALERTA_ATENDIDA";
        public const string Backup = "BACKUP";
        public const string Restore = "RESTORE";
        public const string ErrorSistema = "ERROR_SISTEMA";
    }
}

/// <summary>
/// Entrada de bitácora. Es inmutable por diseño: la base tiene un trigger que
/// rechaza cualquier UPDATE o DELETE, así que no alcanza con no exponer la
/// operación en la aplicación (Req. Arq. 002).
/// </summary>
public class Bitacora
{
    public Guid Id { get; set; }
    public Guid? IdUsuario { get; set; }

    /// <summary>Lo que se tecleó en un login fallido, cuando no hay usuario que referenciar.</summary>
    public string? UsuarioTexto { get; set; }

    public Guid IdTipoEvento { get; set; }
    public string? CodigoTipoEvento { get; set; }
    public DateTime Fecha { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? Entidad { get; set; }
    public Guid? IdEntidad { get; set; }
    public Guid? IdOrganizacion { get; set; }
    public string? Ip { get; set; }

    /// <summary>Traza completa, sólo en excepciones no controladas.</summary>
    public string? Traza { get; set; }
}
