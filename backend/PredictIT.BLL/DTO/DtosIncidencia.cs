namespace PredictIT.BLL.DTO;

/// <summary>Lo que el solicitante carga al reportar una falla (CU-006).</summary>
public record IncidenciaEntradaDto
{
    public string Titulo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public Guid IdEquipo { get; init; }

    /// <summary>
    /// Opcional: si no viene, el triage la sugiere. Que el solicitante pueda
    /// dejarla vacía es el punto — no se le puede pedir a alguien de
    /// administración que sepa si su problema es «Red» o «Rendimiento».
    /// </summary>
    public Guid? IdCategoria { get; init; }
    public Guid? IdPrioridad { get; init; }
}

public record IncidenciaListaDto(
    Guid Id,
    int Numero,
    string Titulo,
    string CodigoEquipo,
    string Estado,
    bool Abierta,
    string Prioridad,
    int NivelPrioridad,
    string? Categoria,
    string? Tecnico,
    string TipoAsignacion,
    bool PendienteRevision,
    DateTime Fecha,
    DateTime? FechaResolucion,
    bool FueraDeObjetivo);

/// <summary>Cómo se decidió la asignación. Es la trazabilidad que se muestra en pantalla.</summary>
public record AsignacionDto(
    string Origen,
    Guid? IdTecnico,
    string? Tecnico,
    string? Justificacion,
    string? MotivoRespaldo,
    DateTime FechaHora);

/// <summary>Sugerencia del triage, con su origen y su confianza.</summary>
public record ClasificacionDto(
    string Origen,
    Guid? IdCategoriaSugerida,
    string? CategoriaSugerida,
    Guid? IdPrioridadSugerida,
    string? PrioridadSugerida,
    int? NumeroIncidenciaDuplicada,
    decimal? Confianza,
    string? Justificacion);

/// <summary>Un paso de la guía de reparación, tal como se le muestra al técnico.</summary>
/// <param name="Riesgo">Qué puede salir mal. Null si el paso no tiene riesgo.</param>
public sealed record PasoReparacionDto(int Orden, string Titulo, string? Detalle, string? Riesgo);

/// <summary>
/// Guía de reparación sugerida para una incidencia (RF-15, CU-018).
///
/// Es una sugerencia: el técnico decide qué hacer y el sistema no ejecuta nada
/// sobre el equipo. Por eso la respuesta trae el origen y la confianza, y por
/// eso no se guarda como parte de la incidencia: lo que queda registrado es que
/// se consultó, en la bitácora.
/// </summary>
/// <param name="Disponible">
/// False cuando el proveedor no respondió. La pantalla muestra el motivo en
/// lugar de una guía vacía, que es peor que no ofrecerla.
/// </param>
public sealed record GuiaReparacionDto(
    bool Disponible,
    string? Resumen,
    IReadOnlyList<PasoReparacionDto> Pasos,
    string? CuandoEscalar,
    decimal? Confianza,
    string Origen,
    string? Motivo);

/// <summary>Un avance en la atención de una incidencia (patente INCIDENCIA_ATENDER).</summary>
public record AtencionDto
{
    /// <summary>Nombre del estado destino, tal como está en el catálogo.</summary>
    public string Estado { get; init; } = string.Empty;

    /// <summary>
    /// Qué se encontró. Nulo deja el que ya estaba: quien pasa a «en espera de
    /// repuesto» no tiene por qué volver a escribirlo.
    /// </summary>
    public string? Diagnostico { get; init; }

    /// <summary>Qué se hizo. Obligatoria para resolver.</summary>
    public string? Solucion { get; init; }
}

/// <summary>
/// A qué estados se puede pasar desde el actual, y qué hace falta para cada uno.
/// Lo calcula el negocio y no la pantalla: la máquina de estados vive en un solo
/// lugar o deja de ser una máquina de estados.
/// </summary>
public record TransicionDto(string Estado, bool ExigeSolucion, string? Impedimento);

public record IncidenciaDetalleDto(
    Guid Id,
    int Numero,
    string Titulo,
    string Descripcion,
    DateTime Fecha,
    DateTime? FechaResolucion,
    Guid IdEquipo,
    string CodigoEquipo,
    Guid IdEstado,
    string Estado,
    bool Abierta,
    Guid IdPrioridad,
    string Prioridad,
    int NivelPrioridad,
    Guid? IdCategoria,
    string? Categoria,
    Guid? IdTecnico,
    string? Tecnico,
    string Reportante,
    string TipoAsignacion,
    bool PendienteRevision,
    string? Diagnostico,
    string? Solucion,
    bool FueraDeObjetivo,
    double HorasAbierta,
    AsignacionDto? Asignacion,
    ClasificacionDto? Clasificacion);

/// <summary>
/// Resultado de registrar una incidencia: lo que la pantalla necesita mostrar.
/// </summary>
/// <param name="Advertencia">
/// Aviso para el usuario cuando la asignación no la decidió la IA. Se devuelve
/// explícito en lugar de dejarlo inferir del origen: un sistema degradado tiene
/// que decir que está degradado (ADR 0009).
/// </param>
public record IncidenciaRegistradaDto(
    Guid Id,
    int Numero,
    AsignacionDto? Asignacion,
    ClasificacionDto? Clasificacion,
    string? Advertencia);

public record CatalogosIncidenciaDto(
    IReadOnlyList<EstadoIncidenciaDto> Estados,
    IReadOnlyList<CatalogoItemDto> Prioridades,
    IReadOnlyList<CatalogoItemDto> Categorias,
    IReadOnlyList<TecnicoDto> Tecnicos);

public record CatalogoItemDto(Guid Id, string Nombre);

/// <summary>
/// Un estado del ciclo de la incidencia, con si cierra el ciclo o no.
///
/// `EsFinal` viaja porque el tablero necesita saber dónde termina el trabajo, y
/// deducirlo del nombre seria adivinar: el dominio lo guarda como dato
/// justamente porque la organización puede renombrar sus estados.
/// </summary>
public record EstadoIncidenciaDto(Guid Id, string Nombre, bool EsFinal);

public record TecnicoDto(
    Guid Id,
    string NombreCompleto,
    IReadOnlyList<string> Especialidades,
    int IncidenciasAbiertas);

/* ----------------------------------------------------------------------
   Análisis predictivo
   ---------------------------------------------------------------------- */

public record AporteReglaDto(
    string Regla,
    int Peso,
    bool Aplicable,
    bool Cumple,
    double Intensidad,
    string Motivo);

public record RiesgoEquipoDto(
    Guid IdEquipo,
    string CodigoEquipo,
    int Score,
    string Nivel,
    DateTime Fecha,
    IReadOnlyList<AporteReglaDto> Aportes);

public record AlertaDto(
    Guid Id,
    Guid IdEquipo,
    string CodigoEquipo,
    string Regla,
    string Estado,
    string Motivo,
    string? Recomendacion,
    int NivelRiesgo,
    DateTime FechaGeneracion,
    DateTime? FechaAtencion);

public record ReglaDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    string Tipo,
    string Condicion,
    int Peso,
    bool Activa);

/// <summary>Resultado de evaluar el parque completo (CU-010).</summary>
public record ResultadoEvaluacionDto(
    int EquiposEvaluados,
    int AlertasNuevas,
    int EnRiesgoAlto,
    int EnRiesgoMedio);

/* ----------------------------------------------------------------------
   Dashboard
   ---------------------------------------------------------------------- */

/// <summary>Un indicador del encabezado del Dashboard (RF-13).</summary>
/// <remarks>
/// Viajan la <b>clave</b> del rótulo y la del detalle, no el texto: el RNF de
/// internacionalización dice que sumar un idioma se resuelve traduciendo el
/// diccionario y sin tocar la lógica, y una etiqueta armada acá en castellano
/// lo desmiente —el indicador quedaba en castellano con la interfaz en inglés—.
/// Los números que el detalle intercala van aparte, en <see cref="Datos"/>,
/// porque el orden de la frase cambia con el idioma.
/// </remarks>
public record IndicadorDto(
    string Clave,
    string Valor,
    string? DetalleClave,
    IReadOnlyDictionary<string, string>? Datos = null);

/// <summary>
/// Una fila de «Equipos con mayor riesgo».
/// </summary>
/// <param name="Responsable">
/// Responsable del **equipo**, no el técnico. En esta tabla cada fila es un
/// equipo, y un equipo no tiene técnico asignado: lo tienen sus incidencias.
/// El diseño original traía técnicos bajo este rótulo (D-15).
/// </param>
public record EquipoEnRiesgoDto(
    Guid IdEquipo,
    string Codigo,
    string? Responsable,
    string? Ubicacion,
    int Incidencias,
    int Score,
    string Nivel,
    string MotivoPrincipal);

public record DashboardDto(
    IReadOnlyList<IndicadorDto> Indicadores,
    IReadOnlyList<EquipoEnRiesgoDto> EquiposEnRiesgo,
    IReadOnlyList<AlertaDto> AlertasRecientes);

/// <summary>Estado del proveedor de IA. Es lo que muestra la pantalla de Integración.</summary>
public record EstadoIaDto(
    string Proveedor,
    string EstadoCircuito,
    int FallosConsecutivos,
    DateTime? SinRespuestaDesde,
    DateTime? UltimaConsultaOk,
    string? UltimoError,
    string Mensaje);

/// <summary>Una fila del ranking del parque por riesgo.</summary>
public record FilaRankingDto(
    Guid IdEquipo,
    string CodigoEquipo,
    int Score,
    string Nivel,
    DateTime Fecha,
    IReadOnlyList<string> ReglasDisparadas);

/// <summary>
/// El panel de análisis predictivo: distribución, ranking y alertas.
///
/// Se arma de lo ya calculado. Abrir la pantalla no reevalúa el parque: para
/// eso está el botón, que es una acción explícita del usuario.
/// </summary>
public record PanelPredictivoDto(
    int EquiposEvaluados,
    DateTime? UltimaEvaluacion,
    int ReglasActivas,
    int AlertasSinAtender,
    int EnRiesgoAlto,
    int EnRiesgoMedio,
    int EnRiesgoBajo,
    IReadOnlyList<FilaRankingDto> Ranking,
    IReadOnlyList<AlertaDto> Alertas);

/// <summary>
/// Cómo viene rindiendo una regla del motor (H-48).
///
/// <c>Descartadas</c> es el número que importa: una alerta descartada es una
/// que el técnico miró y decidió que no aplicaba. Muchas descartadas sobre
/// pocas atendidas quieren decir que el umbral está bajo y la regla está
/// molestando, no avisando.
/// </summary>
/// <param name="Precision">
/// Atendidas sobre el total ya resuelto. Es <c>null</c> mientras no haya
/// ninguna resuelta: un porcentaje sobre cero se lee como cero, y no es lo
/// mismo «esta regla falla siempre» que «esta regla todavía no se evaluó».
/// </param>
public record CalibracionReglaDto(
    Guid IdRegla,
    string Regla,
    bool Activa,
    int Peso,
    int Generadas,
    int Nuevas,
    int Atendidas,
    int Descartadas,
    double? Precision,
    DateTime? UltimaAlerta);
