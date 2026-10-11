/**
 * Contrato con la API.
 *
 * Es el espejo de los DTO de `PredictIT.BLL.DTO`: si cambia uno, tiene que
 * cambiar el otro. Se escriben a mano y no se generan del OpenAPI a propósito —
 * son pocos y explícitos, y así el desajuste aparece al compilar el frontend
 * en lugar de en tiempo de ejecución.
 */

export interface PlanDto {
  codigo: string;
  nombre: string;
  abonoMensual: number;
  equiposIncluidos: number;
  precioEquipoAdicional: number;
  soporte?: string;
}

export interface OrganizacionDto {
  id: string;
  razonSocial: string;
  nombreCorto: string;
  cuit?: string;
  plan?: PlanDto;
}

/**
 * Los contadores que la navegación muestra al lado de cada sección.
 *
 * Cada campo es opcional porque la API omite el que el usuario no puede ver.
 * No llega en cero: cero significa «no hay», y para un contador que no
 * corresponde eso sería una afirmación sobre datos ajenos.
 */
export interface ResumenNavegacionDto {
  equipos?: number;
  incidenciasAbiertas?: number;
  mantenimientosVencidos?: number;
}

export interface PlanAlternativoDto {
  nombre: string;
  totalMensual: number;
  diferencia: number;
  conviene: boolean;
  equiposDesdeLosQueConviene: number;
}

export interface SituacionPlanDto {
  plan: PlanDto;
  equiposAdministrados: number;
  equiposIncluidos: number;
  equiposAdicionales: number;
  abonoBase: number;
  costoAdicionales: number;
  totalMensual: number;
  alternativa?: PlanAlternativoDto;
}

/**
 * Cuanto se uso el servicio de IA en el mes, por clase de consulta.
 *
 * Sale de la bitacora y no de un contador propio: cada consulta ya queda
 * asentada ahi, y un segundo contador seria un numero que puede contradecir al
 * registro de auditoria.
 */
export interface ConsumoIaDto {
  desde: string;
  hasta: string;
  clasificacionesIa: number;
  clasificacionesHeuristica: number;
  asignacionesIa: number;
  asignacionesHeuristica: number;
  asignacionesRespaldo: number;
  guiasIa: number;
  guiasHeuristica: number;
  /** Las que le costaron algo a la organizacion. */
  totalIa: number;
  /** Las que resolvio el propio sistema, sin salir a la red. */
  totalPropio: number;
}

export interface SesionDto {
  idUsuario: string;
  username: string;
  nombreCompleto: string;
  email: string;
  roles: string[];
  patentes: string[];
  organizacion: OrganizacionDto;
  organizacionesDisponibles: OrganizacionDto[];
  token: string;
  expira: string;
}

export interface EquipoListaDto {
  id: string;
  codigo: string;
  tipo: string;
  marcaModelo: string;
  numeroSerie?: string;
  ubicacion?: string;
  responsable?: string;
  estado: string;
  estadoOperativo: boolean;
}

export interface EquipoDetalleDto {
  id: string;
  codigo: string;
  idTipoEquipo: string;
  tipo: string;
  marca?: string;
  modelo?: string;
  numeroSerie?: string;
  descripcionTecnica?: string;
  fechaAlta: string;
  fechaAdquisicion?: string;
  fechaFinGarantia?: string;
  garantiaVencida: boolean;
  diasParaVencimientoGarantia?: number;
  antiguedadEnMeses?: number;
  proveedor?: string;
  criticidad: number;
  idEstadoEquipo: string;
  estado: string;
  idUbicacion?: string;
  ubicacion?: string;
  idResponsable?: string;
  responsable?: string;
}

/** Un paso de la guia de reparacion. `riesgo` es null si el paso no lo tiene. */
export interface PasoReparacionDto {
  orden: number;
  titulo: string;
  detalle?: string;
  riesgo?: string;
}

/**
 * Guia de reparacion sugerida (RF-15, CU-018).
 *
 * `disponible` en false significa que el proveedor no respondio. En ese caso
 * `pasos` trae la guia heuristica y `motivo` dice por que: una lista vacia
 * seria peor que no ofrecer nada.
 */
export interface GuiaReparacionDto {
  disponible: boolean;
  resumen?: string;
  pasos: PasoReparacionDto[];
  cuandoEscalar?: string;
  confianza?: number;
  origen: string;
  motivo?: string;
}

/** Lo que el formulario manda al crear o editar un equipo. */
export interface EquipoEntradaDto {
  codigo: string;
  idTipoEquipo: string;
  marca?: string | null;
  modelo?: string | null;
  numeroSerie?: string | null;
  descripcionTecnica?: string | null;
  fechaAdquisicion?: string | null;
  fechaFinGarantia?: string | null;
  proveedor?: string | null;
  criticidad: number;
  idEstadoEquipo: string;
  idUbicacion?: string | null;
  idResponsable?: string | null;
}

export interface CatalogosEquipoDto {
  tipos: { id: string; nombre: string }[];
  estados: { id: string; nombre: string; operativo: boolean }[];
  ubicaciones: { id: string; nombre: string; descripcion?: string }[];
  responsables: { id: string; nombreCompleto: string }[];
}

export interface PaginaDto<T> {
  items: T[];
  total: number;
  pagina: number;
  porPagina: number;
  totalPaginas: number;
}

export interface FiltroEquipos {
  texto?: string;
  tipo?: string;
  estado?: string;
  ubicacion?: string;
  responsable?: string;
  /** Uno de los tres segmentos, o vacío para todo el parque. Ver {@link SegmentosDto}. */
  segmento?: string;
  pagina?: number;
  porPagina?: number;
}

/**
 * Cuántos equipos hay en cada segmento del inventario.
 *
 * Los tres no son filtros por columna sino las preguntas que alguien se hace
 * parado frente al parque: qué está por romperse, a qué le debo el preventivo
 * y qué ya no tiene garantía.
 */
export interface SegmentosDto {
  todos: number;
  riesgoAlto: number;
  preventivoVencido: number;
  garantiaVencida: number;
}

/** Claves de patente que el frontend consulta para mostrar u ocultar cosas. */
export const Patentes = {
  equipoVer: 'EQUIPO_VER',
  equipoGestionar: 'EQUIPO_GESTIONAR',
  incidenciaVerTodas: 'INCIDENCIA_VER_TODAS',
  incidenciaVerPropias: 'INCIDENCIA_VER_PROPIAS',
  incidenciaRegistrar: 'INCIDENCIA_REGISTRAR',
  incidenciaReasignarTodas: 'INCIDENCIA_REASIGNAR_TODAS',
  incidenciaReasignarPropias: 'INCIDENCIA_REASIGNAR_PROPIAS',
  mantenimientoVer: 'MANTENIMIENTO_VER',
  mantenimientoRegistrar: 'MANTENIMIENTO_REGISTRAR',
  mantenimientoPlanificar: 'MANTENIMIENTO_PLANIFICAR',
  facturacionVer: 'FACTURACION_VER',
  facturacionGestionar: 'FACTURACION_GESTIONAR',
  rolGestionar: 'ROL_GESTIONAR',
  historialVerPropio: 'HISTORIAL_VER_PROPIO',
  alertaVer: 'ALERTA_VER',
  dashboardVer: 'DASHBOARD_VER',
  usuarioGestionar: 'USUARIO_GESTIONAR',
  organizacionGestionar: 'ORGANIZACION_GESTIONAR',
  organizacionCambiar: 'ORGANIZACION_CAMBIAR',
  bitacoraVer: 'BITACORA_VER',
  backupGestionar: 'BACKUP_GESTIONAR',
  reglaGestionar: 'REGLA_GESTIONAR',
  iaConfigurar: 'IA_CONFIGURAR',
  idiomaGestionar: 'IDIOMA_GESTIONAR',
  errorVer: 'ERROR_VER',
  incidenciaAtender: 'INCIDENCIA_ATENDER',
  equipoCambiarEstado: 'EQUIPO_CAMBIAR_ESTADO',
  reporteGenerar: 'REPORTE_GENERAR',
  historialVerOrganizacion: 'HISTORIAL_VER_ORGANIZACION',
} as const;

/* ----------------------------------------------------------------------
   Incidencias
   ---------------------------------------------------------------------- */

export interface IncidenciaListaDto {
  id: string;
  numero: number;
  titulo: string;
  codigoEquipo: string;
  estado: string;
  abierta: boolean;
  prioridad: string;
  nivelPrioridad: number;
  categoria?: string;
  tecnico?: string;
  tipoAsignacion: string;
  pendienteRevision: boolean;
  fecha: string;
  fechaResolucion?: string;
  fueraDeObjetivo: boolean;
}

export interface AsignacionDto {
  origen: string;
  idTecnico?: string;
  tecnico?: string;
  justificacion?: string;
  motivoRespaldo?: string;
  fechaHora: string;
}

export interface ClasificacionDto {
  origen: string;
  idCategoriaSugerida?: string;
  categoriaSugerida?: string;
  idPrioridadSugerida?: string;
  prioridadSugerida?: string;
  numeroIncidenciaDuplicada?: number;
  confianza?: number;
  justificacion?: string;
}

export interface IncidenciaDetalleDto {
  id: string;
  numero: number;
  titulo: string;
  descripcion: string;
  fecha: string;
  fechaResolucion?: string;
  idEquipo: string;
  codigoEquipo: string;
  idEstado: string;
  estado: string;
  abierta: boolean;
  idPrioridad: string;
  prioridad: string;
  nivelPrioridad: number;
  idCategoria?: string;
  categoria?: string;
  idTecnico?: string;
  tecnico?: string;
  reportante: string;
  tipoAsignacion: string;
  pendienteRevision: boolean;
  diagnostico?: string;
  solucion?: string;
  fueraDeObjetivo: boolean;
  horasAbierta: number;
  asignacion?: AsignacionDto;
  clasificacion?: ClasificacionDto;
}

export interface IncidenciaRegistradaDto {
  id: string;
  numero: number;
  asignacion?: AsignacionDto;
  clasificacion?: ClasificacionDto;
  advertencia?: string;
}

export interface CatalogoItemDto {
  id: string;
  nombre: string;
}

/**
 * Un estado del ciclo de la incidencia, con si cierra el ciclo o no.
 *
 * `esFinal` viaja porque el tablero necesita saber dónde termina el trabajo, y
 * deducirlo del nombre sería adivinar: el dominio lo guarda como dato
 * justamente porque la organización puede renombrar sus estados.
 */
export interface EstadoIncidenciaDto extends CatalogoItemDto {
  esFinal: boolean;
}

export interface TecnicoDto {
  id: string;
  nombreCompleto: string;
  especialidades: string[];
  incidenciasAbiertas: number;
}

export interface CatalogosIncidenciaDto {
  estados: EstadoIncidenciaDto[];
  prioridades: CatalogoItemDto[];
  categorias: CatalogoItemDto[];
  tecnicos: TecnicoDto[];
}

export interface FiltroIncidencias {
  texto?: string;
  equipo?: string;
  estado?: string;
  prioridad?: string;
  categoria?: string;
  tecnico?: string;
  abiertas?: boolean;
  revision?: boolean;
  desde?: string;
  hasta?: string;
  pagina?: number;
  porPagina?: number;
}

/* ----------------------------------------------------------------------
   Análisis predictivo
   ---------------------------------------------------------------------- */

export interface AlertaDto {
  id: string;
  idEquipo: string;
  codigoEquipo: string;
  regla: string;
  estado: string;
  motivo: string;
  recomendacion?: string;
  nivelRiesgo: number;
  fechaGeneracion: string;
  fechaAtencion?: string;
}

export interface FilaRankingDto {
  idEquipo: string;
  codigoEquipo: string;
  score: number;
  nivel: string;
  fecha: string;
  reglasDisparadas: string[];
}

export interface PanelPredictivoDto {
  equiposEvaluados: number;
  ultimaEvaluacion?: string;
  reglasActivas: number;
  alertasSinAtender: number;
  enRiesgoAlto: number;
  enRiesgoMedio: number;
  enRiesgoBajo: number;
  ranking: FilaRankingDto[];
  alertas: AlertaDto[];
}

export interface ResultadoEvaluacionDto {
  equiposEvaluados: number;
  alertasNuevas: number;
  enRiesgoAlto: number;
  enRiesgoMedio: number;
}

export interface AporteReglaDto {
  regla: string;
  peso: number;
  aplicable: boolean;
  cumple: boolean;
  intensidad: number;
  motivo: string;
}

export interface RiesgoEquipoDto {
  idEquipo: string;
  codigoEquipo: string;
  score: number;
  nivel: string;
  fecha: string;
  aportes: AporteReglaDto[];
}

export interface IndicadorDto {
  /** Clave del rotulo en el diccionario, no el texto. */
  clave: string;
  valor: string;
  /** Clave del detalle. El backend elige entre singular y plural. */
  detalleClave?: string;
  /** Los numeros que el detalle intercala en sus {marcadores}. */
  datos?: Record<string, string>;
}

/**
 * Una fila de «Equipos con mayor riesgo».
 *
 * `responsable` es el responsable del EQUIPO, no el técnico: cada fila es un
 * equipo, y un equipo no tiene técnico asignado — lo tienen sus incidencias.
 */
export interface EquipoEnRiesgoDto {
  idEquipo: string;
  codigo: string;
  responsable?: string;
  ubicacion?: string;
  incidencias: number;
  score: number;
  nivel: string;
  motivoPrincipal: string;
}

export interface DashboardDto {
  indicadores: IndicadorDto[];
  equiposEnRiesgo: EquipoEnRiesgoDto[];
  alertasRecientes: AlertaDto[];
}

/* ----------------------------------------------------------------------
   Administración
   ---------------------------------------------------------------------- */

export interface MantenimientoListaDto {
  id: string;
  idEquipo: string;
  codigoEquipo: string;
  tipo: string;
  esPreventivo: boolean;
  fecha: string;
  tecnico?: string;
  descripcion: string;
  costo?: number;
  numeroIncidencia?: number;
}

export interface TipoMantenimientoDto {
  id: string;
  nombre: string;
  esPreventivo: boolean;
}

export interface CatalogosMantenimientoDto {
  tipos: TipoMantenimientoDto[];
  tecnicos: TecnicoDto[];
}

/** Un plan: cada cuánto le toca el preventivo a un equipo o a un tipo de equipo. */
export interface PlanMantenimientoDto {
  id: string;
  idEquipo?: string;
  idTipoEquipo?: string;
  alcance: string;
  idTipoMantenimiento: string;
  tipoMantenimiento: string;
  cadaDias: number;
  activo: boolean;
  descripcion?: string;
  /** Cuántos equipos operativos alcanza hoy. Un plan que no alcanza a ninguno
      está bien formado y no sirve para nada, y eso hay que verlo. */
  equiposAlcanzados: number;
}

export interface PlanMantenimientoEntradaDto {
  idEquipo?: string;
  idTipoEquipo?: string;
  idTipoMantenimiento: string;
  cadaDias: number;
  activo: boolean;
  descripcion?: string;
}

/** Una fecha concreta en la que hay que hacerle el preventivo a un equipo. */
export interface MantenimientoProgramadoDto {
  id: string;
  idEquipo: string;
  codigoEquipo: string;
  ubicacion?: string;
  idTipoMantenimiento: string;
  tipoMantenimiento: string;
  fechaProgramada: string;
  estado: string;
  vencido: boolean;
  /** Positivo si ya venció, negativo si falta. Lo calcula el servidor: el
      navegador puede tener otra fecha, y entonces dos personas verían distinto
      qué está vencido. */
  diasDeAtraso: number;
  idPlan?: string;
  idMantenimiento?: string;
  motivo?: string;
}

export interface GeneracionDto {
  planesActivos: number;
  generados: number;
  yaEstaban: number;
}

export interface PanelProgramadoDto {
  vencidos: number;
  proximosSieteDias: number;
  programadosTotal: number;
  planesActivos: number;
  proximos: MantenimientoProgramadoDto[];
}

export interface LineaComprobanteDto {
  id: string;
  orden: number;
  concepto: string;
  cantidad: number;
  precioUnitario: number;
  importe: number;
  esAjuste: boolean;
}

/** Un comprobante del servicio: lo que la organización paga por un período. */
export interface ComprobanteDto {
  id: string;
  periodo: string;
  /** Ausente mientras es borrador: numerar algo que se puede borrar deja huecos. */
  numero?: number;
  plan: string;
  equiposAdministrados: number;
  equiposIncluidos: number;
  equiposAdicionales: number;
  subtotal: number;
  alicuotaIva: number;
  iva: number;
  total: number;
  estado: string;
  /** Emitido, con el vencimiento pasado y sin pago. Lo calcula el servidor. */
  vencido: boolean;
  diasDeAtraso: number;
  fechaEmision?: string;
  fechaVencimiento?: string;
  fechaPago?: string;
  motivo?: string;
  lineas: LineaComprobanteDto[];
}

export interface GeneracionComprobanteDto {
  id: string;
  periodo: string;
  /** El período ya estaba facturado y no se tocó. */
  yaExistia: boolean;
  total: number;
}

export interface PanelFacturacionDto {
  periodoSugerido: string;
  periodoSugeridoFacturado: boolean;
  emitidos: number;
  pagados: number;
  vencidos: number;
  totalPendiente: number;
  totalVencido: number;
  facturadoUltimos12Meses: number;
  ultimos: ComprobanteDto[];
}

export interface UsuarioListaDto {
  id: string;
  username: string;
  nombreCompleto: string;
  email: string;
  telefono?: string;
  activo: boolean;
  bloqueado: boolean;
  intentosFallidos: number;
  fechaAlta: string;
  ultimoAcceso?: string;
  roles: string[];
  cantidadPatentes: number;
  incidenciasAbiertas: number;
}

export interface RolDto {
  id: string;
  nombre: string;
  descripcion?: string;
  esSistema: boolean;
  cantidadPatentes: number;
}

/** Una patente del usuario, y por qué rol la tiene. */
export interface PatenteConcedidaDto {
  dataKey: string;
  nombre: string;
  origen: string;
  /** LECTURA, ESCRITURA o ADMINISTRACION. */
  tipoAcceso: string;
}

export interface UsuarioDetalleDto {
  datos: UsuarioListaDto;
  patentes: PatenteConcedidaDto[];
}

/** Un avance en la atención de una incidencia. */
export interface AtencionDto {
  estado: string;
  diagnostico?: string | null;
  solucion?: string | null;
}

/**
 * A qué estados se puede pasar desde el actual. Lo calcula el backend: la
 * máquina de estados vive en un solo lugar.
 */
export interface TransicionDto {
  estado: string;
  exigeSolucion: boolean;
  impedimento?: string | null;
}

/** Un hecho del historial técnico: INCIDENCIA o MANTENIMIENTO. */
export interface HechoHistorialDto {
  fecha: string;
  tipo: string;
  codigoEquipo: string;
  titulo: string;
  detalle?: string | null;
  estado?: string | null;
  persona?: string | null;
}

export interface EntradaBitacoraDto {
  id: string;
  fecha: string;
  tipoEvento: string;
  criticidad: string;
  usuario?: string;
  descripcion: string;
  entidad?: string;
  ip?: string;
}

export interface ConfiguracionIaDto {
  proveedorIa: string;
  modelo?: string;
  enviarCarga: boolean;
  enviarEspecialidad: boolean;
  enviarHistorial: boolean;
  enviarDisponibilidad: boolean;
  estrategiaRespaldo: string;
  timeoutSegundos: number;
}

/** Errores del sistema con su traza (CU.Arq.007). */
export interface ErrorDto {
  id: string;
  fecha: string;
  tipoEvento: string;
  criticidad: string;
  usuario?: string;
  descripcion: string;
  entidad?: string;
  ip?: string;
  traza?: string;
}

export interface PanelIaDto {
  configuracion: ConfiguracionIaDto;
  estado: EstadoIaDto;
  hayClaveCargada: boolean;
  /** BASE, ENTORNO o NINGUNA. */
  origenDeLaClave: string;
  asignacionesPorIa: number;
  asignacionesPorHeuristica: number;
  asignacionesPorRespaldo: number;
}

export interface ReglaDto {
  id: string;
  nombre: string;
  descripcion?: string;
  tipo: string;
  condicion: string;
  peso: number;
  activa: boolean;
}

/** Los cinco tipos de regla que el motor sabe evaluar. */
export const TIPOS_DE_REGLA = [
  { id: 'RECURRENCIA_FALLAS', nombre: 'Fallas recurrentes', claves: 'minIncidencias, ventanaDias' },
  { id: 'ACUMULACION_INCIDENCIAS', nombre: 'Misma falla repetida', claves: 'minIncidencias, ventanaDias, mismaCategoria' },
  { id: 'MANTENIMIENTO_VENCIDO', nombre: 'Mantenimiento vencido', claves: 'diasSinMantenimiento' },
  { id: 'ANTIGUEDAD_EQUIPO', nombre: 'Antigüedad del equipo', claves: 'aniosUmbral' },
  { id: 'GARANTIA_POR_VENCER', nombre: 'Garantía por vencer', claves: 'diasAviso' },
] as const;

export interface RespaldoDto {
  id: string;
  base: string;
  nombreArchivo: string;
  ruta: string;
  fecha: string;
  tamanoBytes?: number;
  tipo: string;
  usuario?: string;
  ok: boolean;
  detalle?: string;
}

export interface PanelRespaldosDto {
  bases: string[];
  /** Último respaldo correcto por base. Null si nunca hubo uno. */
  ultimoOk: Record<string, string | null>;
  historial: RespaldoDto[];
}

export interface EstadoIaDto {
  proveedor: string;
  estadoCircuito: string;
  fallosConsecutivos: number;
  sinRespuestaDesde?: string;
  ultimaConsultaOk?: string;
  ultimoError?: string;
  mensaje: string;
}

/** Rendimiento de una regla del motor, para calibrar su umbral (H-48). */
export interface CalibracionReglaDto {
  idRegla: string;
  regla: string;
  activa: boolean;
  peso: number;
  generadas: number;
  nuevas: number;
  atendidas: number;
  descartadas: number;
  /** Atendidas sobre resueltas. Null mientras no haya ninguna resuelta. */
  precision: number | null;
  ultimaAlerta?: string | null;
}

/** Un idioma disponible en el sistema. */
export interface IdiomaDto {
  codigo: string;
  nombre: string;
  esPorDefecto: boolean;
}

/**
 * El diccionario completo de un idioma. Viaja entero y no de a una clave: la
 * interfaz necesita todas las de la pantalla antes de dibujarla.
 */
export interface DiccionarioDto {
  codigo: string;
  nombre: string;
  textos: Record<string, string>;
}

export interface CoberturaPorIdiomaDto {
  codigo: string;
  nombre: string;
  traducidas: number;
  faltantes: number;
  porcentaje: number;
}

export interface CoberturaIdiomaDto {
  clavesTotales: number;
  porIdioma: CoberturaPorIdiomaDto[];
}
