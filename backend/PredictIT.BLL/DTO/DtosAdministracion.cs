namespace PredictIT.BLL.DTO;

/* ----------------------------------------------------------------------
   Mantenimientos (RF-10 · CU-008)
   ---------------------------------------------------------------------- */

public record MantenimientoListaDto(
    Guid Id,
    Guid IdEquipo,
    string CodigoEquipo,
    string Tipo,
    bool EsPreventivo,
    DateTime Fecha,
    string? Tecnico,
    string Descripcion,
    decimal? Costo,
    int? NumeroIncidencia);

public record MantenimientoEntradaDto
{
    public Guid IdEquipo { get; init; }
    public Guid IdTipo { get; init; }
    public DateTime? Fecha { get; init; }
    public string Descripcion { get; init; } = string.Empty;
    public string? Resultado { get; init; }
    public string? Repuestos { get; init; }
    public string? Observaciones { get; init; }
    public decimal? Costo { get; init; }

    /// <summary>
    /// El mantenimiento programado que este registro ejecuta, si vino de la
    /// agenda. Cuando no se indica y el tipo es preventivo, el sistema cierra
    /// igual el programado pendiente más viejo de ese equipo y ese tipo: el
    /// trabajo se hizo, y dejar la fecha abierta la mostraría vencida al día
    /// siguiente de haberla cumplido.
    /// </summary>
    public Guid? IdProgramado { get; init; }

    /// <summary>
    /// Incidencia que lo originó, si fue correctivo. Al cargarla, la incidencia
    /// queda vinculada al mantenimiento y su historial pasa a mostrarlo.
    /// </summary>
    public Guid? IdIncidencia { get; init; }
}

public record CatalogosMantenimientoDto(
    IReadOnlyList<TipoMantenimientoDto> Tipos,
    IReadOnlyList<TecnicoDto> Tecnicos);

public record TipoMantenimientoDto(Guid Id, string Nombre, bool EsPreventivo);

/* ----------------------------------------------------------------------
   Usuarios y permisos (RF-02, RF-03 · CU-002)
   ---------------------------------------------------------------------- */

public record UsuarioListaDto(
    Guid Id,
    string Username,
    string NombreCompleto,
    string Email,
    string? Telefono,
    bool Activo,
    bool Bloqueado,
    int IntentosFallidos,
    DateTime FechaAlta,
    DateTime? UltimoAcceso,
    IReadOnlyList<string> Roles,
    int CantidadPatentes,
    int IncidenciasAbiertas);

public record RolDto(Guid Id, string Nombre, string? Descripcion, bool EsSistema, int CantidadPatentes);

/// <summary>Una patente concedida a un usuario, y por qué camino la tiene.</summary>
/// <param name="TipoAcceso">
/// LECTURA, ESCRITURA o ADMINISTRACION. Va en el DTO porque es lo que permite
/// leer la lista de permisos de un usuario de un vistazo: veintiséis nombres
/// de patente no dicen si esa persona puede tocar la configuración, y el nivel
/// sí.
/// </param>
public record PatenteConcedidaDto(string DataKey, string Nombre, string Origen, string TipoAcceso);

public record UsuarioDetalleDto(
    UsuarioListaDto Datos,
    IReadOnlyList<PatenteConcedidaDto> Patentes);

/* ----------------------------------------------------------------------
   Bitácora (Req. Arq. 002)
   ---------------------------------------------------------------------- */

public record EntradaBitacoraDto(
    Guid Id,
    DateTime Fecha,
    string TipoEvento,
    string Criticidad,
    string? Usuario,
    string Descripcion,
    string? Entidad,
    string? Ip);

public record FiltroBitacoraDto
{
    public DateTime? Desde { get; init; }
    public DateTime? Hasta { get; init; }
    public string? CodigoTipoEvento { get; init; }

    /// <summary>
    /// Usuario cuya actividad se quiere ver (CU.Arq.002).
    ///
    /// Es la consulta que se hace de verdad cuando algo salió mal: no «qué pasó
    /// el martes» sino «qué hizo esta persona».
    /// </summary>
    public Guid? IdUsuario { get; init; }
}

/* ----------------------------------------------------------------------
   Reportes exportables (RF-14)
   ---------------------------------------------------------------------- */

public record FiltroReporteDto
{
    public DateTime? Desde { get; init; }
    public DateTime? Hasta { get; init; }
    public Guid? IdEstado { get; init; }
    public Guid? IdTecnico { get; init; }
}

/// <param name="Contenido">
/// El PDF ya armado. Va como bytes y no como ruta: el reporte no se guarda en
/// el servidor, se genera y se entrega. Un reporte guardado envejece y alguien
/// termina leyendo el de la semana pasada creyendo que es el de hoy.
/// </param>
public record ReporteGeneradoDto(
    string NombreArchivo,
    string TipoContenido,
    byte[] Contenido,
    int Filas);

/* ----------------------------------------------------------------------
   Historial de la organización
   ---------------------------------------------------------------------- */

/// <param name="Tipo">INCIDENCIA o MANTENIMIENTO.</param>
public record HechoHistorialDto(
    DateTime Fecha,
    string Tipo,
    string CodigoEquipo,
    string Titulo,
    string? Detalle,
    string? Estado,
    string? Persona);

/* ----------------------------------------------------------------------
   Errores del sistema (CU.Arq.007)
   ---------------------------------------------------------------------- */

/// <param name="Traza">
/// El detalle técnico. Es lo que distingue esta pantalla de la bitácora general
/// y la razón de que pida su propia patente: una traza nombra tablas, rutas y
/// versiones, que sirven tanto para diagnosticar como para atacar.
/// </param>
public record ErrorDto(
    Guid Id,
    DateTime Fecha,
    string TipoEvento,
    string Criticidad,
    string? Usuario,
    string Descripcion,
    string? Entidad,
    string? Ip,
    string? Traza);

public record FiltroErroresDto
{
    public DateTime? Desde { get; init; }
    public DateTime? Hasta { get; init; }

    /// <summary>
    /// Sumar las advertencias. Aparte de los errores porque son cosas
    /// distintas: un error es algo que falló, una advertencia es algo que
    /// funcionó de un modo que conviene mirar.
    /// </summary>
    public bool IncluirAdvertencias { get; init; }
}

/* ----------------------------------------------------------------------
   Respaldos (Req. Arq. 003)
   ---------------------------------------------------------------------- */

public record RespaldoDto(
    Guid Id,
    string Base,
    string NombreArchivo,
    string Ruta,
    DateTime Fecha,
    long? TamanoBytes,
    string Tipo,
    string? Usuario,
    bool Ok,
    string? Detalle);

/// <param name="UltimoOk">
/// Cuándo fue el último respaldo correcto de cada base. Es el dato que importa:
/// que existan cien respaldos viejos no dice nada si el último falló.
/// </param>
public record PanelRespaldosDto(
    IReadOnlyList<string> Bases,
    IReadOnlyDictionary<string, DateTime?> UltimoOk,
    IReadOnlyList<RespaldoDto> Historial);

/* ----------------------------------------------------------------------
   Configuración de asignación e IA (pantalla 10.5.1.7.7)
   ---------------------------------------------------------------------- */

public record ConfiguracionIaDto
{
    /// <summary>SIMULADO o CLAUDE.</summary>
    public string ProveedorIa { get; init; } = "SIMULADO";

    public string? Modelo { get; init; }

    public bool EnviarCarga { get; init; } = true;
    public bool EnviarEspecialidad { get; init; } = true;
    public bool EnviarHistorial { get; init; } = true;
    public bool EnviarDisponibilidad { get; init; }

    /// <summary>MENOR_CARGA o SIN_ASIGNAR.</summary>
    public string EstrategiaRespaldo { get; init; } = "MENOR_CARGA";

    public int TimeoutSegundos { get; init; } = 15;
}

/// <summary>
/// La configuración más el estado observado del proveedor.
/// </summary>
/// <param name="HayClaveCargada">
/// Si está disponible la clave de API. Se informa el hecho y nunca el valor: la
/// clave no vuelve a salir del servidor una vez configurada.
/// </param>
/// <param name="OrigenDeLaClave">
/// De dónde sale la clave: BASE si está guardada cifrada en la organización,
/// ENTORNO si viene de la variable del servidor, NINGUNA si no hay. Se informa
/// el origen porque cambia quién la controla: una clave en la base la administra
/// el cliente, una del entorno la administra quien opera el servidor.
/// </param>
public record PanelIaDto(
    ConfiguracionIaDto Configuracion,
    EstadoIaDto Estado,
    bool HayClaveCargada,
    string OrigenDeLaClave,
    int AsignacionesPorIa,
    int AsignacionesPorHeuristica,
    int AsignacionesPorRespaldo);
