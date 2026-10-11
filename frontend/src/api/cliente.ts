import type {
  AlertaDto,
  CalibracionReglaDto,
  CoberturaIdiomaDto,
  DiccionarioDto,
  IdiomaDto,
  CatalogoItemDto,
  CatalogosEquipoDto,
  CatalogosIncidenciaDto,
  CatalogosMantenimientoDto,
  ConfiguracionIaDto,
  DashboardDto,
  EntradaBitacoraDto,
  EquipoEntradaDto,
  ErrorDto,
  HechoHistorialDto,
  TransicionDto,
  AtencionDto,
  ComprobanteDto,
  GeneracionComprobanteDto,
  PanelFacturacionDto,
  MantenimientoListaDto,
  MantenimientoProgramadoDto,
  GeneracionDto,
  PanelProgramadoDto,
  PlanMantenimientoDto,
  PlanMantenimientoEntradaDto,
  PanelIaDto,
  PanelRespaldosDto,
  ReglaDto,
  RespaldoDto,
  RolDto,
  UsuarioDetalleDto,
  UsuarioListaDto,
  EquipoDetalleDto,
  EquipoListaDto,
  FiltroEquipos,
  EstadoIaDto,
  FiltroIncidencias,
  ConsumoIaDto,
  GuiaReparacionDto,
  IncidenciaDetalleDto,
  IncidenciaListaDto,
  IncidenciaRegistradaDto,
  OrganizacionDto,
  PaginaDto,
  PanelPredictivoDto,
  ResultadoEvaluacionDto,
  ResumenNavegacionDto,
  SegmentosDto,
  RiesgoEquipoDto,
  SesionDto,
  SituacionPlanDto,
} from './tipos';

/**
 * Cliente HTTP de la API.
 *
 * El token se guarda en `sessionStorage` y no en `localStorage`: al cerrar la
 * pestaña la sesión se termina, que es lo esperable en un equipo compartido —
 * y en una PyME sin área de IT las máquinas se comparten.
 */

const CLAVE_TOKEN = 'predictit.token';

/** Error con el mensaje que la API escribió para mostrarle al usuario. */
export class ErrorApi extends Error {
  constructor(
    public readonly estado: number,
    mensaje: string,
    public readonly campo?: string,
    /**
     * Motivo en forma de codigo estable, cuando la API lo manda. Existe para
     * lo que hay que decidir y no mostrar: la pantalla de ingreso trata
     * distinto a `credenciales` que a `bloqueado`, y comparar el mensaje se
     * rompe con la interfaz en ingles.
     */
    public readonly codigo?: string,
  ) {
    super(mensaje);
    this.name = 'ErrorApi';
  }

  get esSesionInvalida() {
    return this.estado === 401;
  }

  get esSinPermiso() {
    return this.estado === 403;
  }

  /**
   * El recurso no existe: un identificador mal escrito, o un registro que se
   * anulo. No es una falla del sistema y no tiene que verse como tal.
   *
   * Son dos caminos. El 404 lo devuelve el enrutador cuando el identificador
   * ni siquiera es un GUID; el codigo `no-encontrado` lo pone la regla de
   * negocio cuando el GUID es valido pero no hay registro, o el registro es de
   * otra organizacion. El mensaje conflaciona esos dos ultimos a proposito:
   * distinguirlos filtraria si un identificador ajeno existe.
   */
  get esNoEncontrado() {
    return this.estado === 404 || this.codigo === 'no-encontrado';
  }
}

export const token = {
  leer: () => sessionStorage.getItem(CLAVE_TOKEN),
  guardar: (valor: string) => sessionStorage.setItem(CLAVE_TOKEN, valor),
  borrar: () => sessionStorage.removeItem(CLAVE_TOKEN),
};

async function pedir<T>(ruta: string, opciones: RequestInit = {}): Promise<T> {
  const jwt = token.leer();

  const respuesta = await fetch(`/api${ruta}`, {
    ...opciones,
    headers: {
      'Content-Type': 'application/json',
      ...(jwt ? { Authorization: `Bearer ${jwt}` } : {}),
      ...opciones.headers,
    },
  });

  if (respuesta.status === 204) return undefined as T;

  const texto = await respuesta.text();
  const cuerpo = texto ? JSON.parse(texto) : null;

  if (!respuesta.ok) {
    // La API devuelve un mensaje pensado para el usuario en los 400 y 403. En
    // los 500 devuelve uno genérico a propósito, así que no hay nada mejor que
    // mostrar que eso.
    throw new ErrorApi(
      respuesta.status,
      // El 404 no trae cuerpo: lo devuelve el enrutador de ASP.NET antes de
      // llegar al controlador cuando el identificador ni siquiera es un GUID.
      cuerpo?.mensaje
        ?? (respuesta.status === 404
              ? 'No encontramos ese registro. Puede que se haya anulado o que el enlace esté mal.'
              : 'No fue posible completar la operación.'),
      cuerpo?.campo,
      cuerpo?.codigo,
    );
  }

  return cuerpo as T;
}

function query(filtro: FiltroEquipos): string {
  const p = new URLSearchParams();
  // Sólo se mandan los filtros con valor: un parámetro vacío en la URL es ruido
  // y además la API lo trataría como filtro por cadena vacía.
  if (filtro.texto?.trim()) p.set('texto', filtro.texto.trim());
  if (filtro.tipo) p.set('tipo', filtro.tipo);
  if (filtro.estado) p.set('estado', filtro.estado);
  if (filtro.ubicacion) p.set('ubicacion', filtro.ubicacion);
  if (filtro.responsable) p.set('responsable', filtro.responsable);
  if (filtro.segmento) p.set('segmento', filtro.segmento);
  if (filtro.pagina) p.set('pagina', String(filtro.pagina));
  if (filtro.porPagina) p.set('porPagina', String(filtro.porPagina));
  const s = p.toString();
  return s ? `?${s}` : '';
}

export const api = {
  auth: {
    login: (username: string, contrasena: string) =>
      pedir<SesionDto>('/auth/login', {
        method: 'POST',
        body: JSON.stringify({ username, contrasena }),
      }),

    sesion: () => pedir<SesionDto>('/auth/sesion'),

    cambiarOrganizacion: (idOrganizacion: string) =>
      pedir<SesionDto>('/auth/organizacion', {
        method: 'POST',
        body: JSON.stringify({ idOrganizacion }),
      }),
  },

  equipos: {
    buscar: (filtro: FiltroEquipos = {}) =>
      pedir<PaginaDto<EquipoListaDto>>(`/equipos${query(filtro)}`),

    detalle: (id: string) => pedir<EquipoDetalleDto>(`/equipos/${id}`),

    /** Los equipos de los que el usuario es responsable (módulo del cliente). */
    mios: () => pedir<PaginaDto<EquipoListaDto>>('/equipos/mios'),

    catalogos: () => pedir<CatalogosEquipoDto>('/equipos/catalogos'),

    /** Los recuentos de las píldoras, con los mismos filtros que el listado. */
    segmentos: (filtro: FiltroEquipos = {}) =>
      pedir<SegmentosDto>(`/equipos/segmentos${query({ ...filtro, segmento: undefined,
                                                       pagina: undefined, porPagina: undefined })}`),

    registrar: (entrada: EquipoEntradaDto) =>
      pedir<string>('/equipos', { method: 'POST', body: JSON.stringify(entrada) }),

    actualizar: (id: string, entrada: EquipoEntradaDto) =>
      pedir<void>(`/equipos/${id}`, { method: 'PUT', body: JSON.stringify(entrada) }),

    darDeBaja: (id: string) => pedir<void>(`/equipos/${id}`, { method: 'DELETE' }),

    /**
     * Cambia el estado operativo. Es otra cosa que dar de baja: un equipo «en
     * reparación» sigue siendo del parque, y el motor predictivo no evalúa los
     * no operativos.
     */
    cambiarEstado: (id: string, idEstado: string, motivo?: string) =>
      pedir<void>(`/equipos/${id}/estado`, {
        method: 'PUT',
        body: JSON.stringify({ idEstado, motivo: motivo ?? null }),
      }),
  },

  organizacion: {
    actual: () => pedir<OrganizacionDto>('/organizacion'),
    plan: () => pedir<SituacionPlanDto>('/organizacion/plan'),
    consumoIa: () => pedir<ConsumoIaDto>('/organizacion/consumo-ia'),
    resumen: () => pedir<ResumenNavegacionDto>('/organizacion/resumen'),
  },

  incidencias: {
    buscar: (filtro: FiltroIncidencias = {}) =>
      pedir<PaginaDto<IncidenciaListaDto>>(`/incidencias${queryIncidencias(filtro)}`),

    detalle: (id: string) => pedir<IncidenciaDetalleDto>(`/incidencias/${id}`),

    catalogos: () => pedir<CatalogosIncidenciaDto>('/incidencias/catalogos'),

    registrar: (entrada: {
      titulo: string;
      descripcion: string;
      idEquipo: string;
      idCategoria?: string;
      idPrioridad?: string;
    }) =>
      pedir<IncidenciaRegistradaDto>('/incidencias', {
        method: 'POST',
        body: JSON.stringify(entrada),
      }),

    reasignar: (id: string, idTecnico: string) =>
      pedir<void>(`/incidencias/${id}/tecnico`, {
        method: 'PUT',
        body: JSON.stringify({ idTecnico }),
      }),

    /** Qué transiciones admite la incidencia ahora. Las decide el backend. */
    transiciones: (id: string) => pedir<TransicionDto[]>(`/incidencias/${id}/transiciones`),

    /**
     * Guía de reparación paso a paso (RF-15, CU-018).
     *
     * Se pide con un botón y no al abrir el detalle: cada consulta le cuesta al
     * proveedor, y un ticket se abre muchas más veces de las que se repara.
     */
    guiaReparacion: (id: string) =>
      pedir<GuiaReparacionDto>(`/incidencias/${id}/guia-reparacion`),

    atender: (id: string, entrada: AtencionDto) =>
      pedir<void>(`/incidencias/${id}/atencion`, {
        method: 'PUT',
        body: JSON.stringify(entrada),
      }),
  },

  /** Historial técnico de la organización. */
  historial: {
    consultar: (desde?: string, hasta?: string) => {
      const p = new URLSearchParams();
      if (desde) p.set('desde', desde);
      if (hasta) p.set('hasta', hasta);
      const s = p.toString();
      return pedir<HechoHistorialDto[]>(`/historial${s ? `?${s}` : ''}`);
    },
  },

  /**
   * Reportes en PDF. No pasa por `pedir`: la respuesta es un archivo y no
   * JSON, así que se descarga con un enlace temporal en lugar de parsearse.
   */
  reportes: {
    descargar: async (
      reporte: string,
      filtro: { desde?: string; hasta?: string; estado?: string; tecnico?: string } = {},
    ) => {
      const p = new URLSearchParams();
      Object.entries(filtro).forEach(([k, v]) => {
        if (v) p.set(k, v);
      });

      const jwt = token.leer();
      const respuesta = await fetch(`/api/reportes/${reporte}?${p.toString()}`, {
        headers: jwt ? { Authorization: `Bearer ${jwt}` } : {},
      });

      if (!respuesta.ok) {
        throw new ErrorApi(
          respuesta.status,
          respuesta.status === 403
            ? 'No tenés permiso para generar reportes.'
            : 'No se pudo generar el reporte.',
        );
      }

      const blob = await respuesta.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${reporte}.pdf`;
      a.click();
      // Sin revocar la URL el blob queda en memoria hasta recargar la página.
      URL.revokeObjectURL(url);
    },
  },

  /**
   * Idiomas y diccionario. Los dos primeros son anónimos a propósito: la
   * pantalla de inicio de sesión también está traducida y ahí no hay token.
   */
  idiomas: {
    listar: () => pedir<IdiomaDto[]>('/idiomas'),

    diccionario: (codigo?: string) =>
      pedir<DiccionarioDto>(`/idiomas/traducciones${codigo ? `?codigo=${codigo}` : ''}`),

    cambiar: (codigo: string) =>
      pedir<void>('/idiomas/preferencia', {
        method: 'PUT',
        body: JSON.stringify({ codigo }),
      }),

    cobertura: () => pedir<CoberturaIdiomaDto>('/idiomas/cobertura'),
  },

  prediccion: {
    panel: () => pedir<PanelPredictivoDto>('/prediccion/panel'),
    dashboard: () => pedir<DashboardDto>('/prediccion/dashboard'),
    alertas: () => pedir<AlertaDto[]>('/prediccion/alertas'),
    riesgo: (idEquipo: string) => pedir<RiesgoEquipoDto>(`/prediccion/equipos/${idEquipo}`),

    evaluar: () =>
      pedir<ResultadoEvaluacionDto>('/prediccion/evaluar', { method: 'POST' }),

    atender: (idAlerta: string, descartar: boolean) =>
      pedir<void>(`/prediccion/alertas/${idAlerta}?descartar=${descartar}`, { method: 'PUT' }),

    estadoIa: () => pedir<EstadoIaDto>('/prediccion/ia'),

    /** Cuántas alertas generó cada regla y en qué terminaron (H-48). */
    calibracion: () =>
      pedir<CalibracionReglaDto[]>('/prediccion/calibracion'),
  },

  mantenimientos: {
    listar: (idEquipo?: string) =>
      pedir<MantenimientoListaDto[]>(
        `/mantenimientos${idEquipo ? `?equipo=${idEquipo}` : ''}`,
      ),

    catalogos: () => pedir<CatalogosMantenimientoDto>('/mantenimientos/catalogos'),

    registrar: (entrada: {
      idEquipo: string;
      idTipo: string;
      fecha?: string;
      descripcion: string;
      resultado?: string;
      repuestos?: string;
      observaciones?: string;
      costo?: number;
      idIncidencia?: string;
      /** El trabajo agendado que este mantenimiento cierra, si viene de la agenda. */
      idProgramado?: string;
    }) => pedir<string>('/mantenimientos', { method: 'POST', body: JSON.stringify(entrada) }),
  },

  /*
    La agenda y los planes van aparte de `mantenimientos` porque son recursos
    distintos: uno es lo que se hizo y otro lo que está previsto.
  */
  programados: {
    listar: (estado?: string) =>
      pedir<MantenimientoProgramadoDto[]>(
        `/mantenimientos/programados${estado ? `?estado=${estado}` : ''}`,
      ),

    panel: () => pedir<PanelProgramadoDto>('/mantenimientos/programados/panel'),

    programar: (entrada: {
      idEquipo: string;
      idTipoMantenimiento: string;
      fechaProgramada: string;
      motivo?: string;
    }) =>
      pedir<string>('/mantenimientos/programados', {
        method: 'POST',
        body: JSON.stringify(entrada),
      }),

    reprogramar: (id: string, fechaProgramada: string, motivo: string) =>
      pedir<void>(`/mantenimientos/programados/${id}`, {
        method: 'PUT',
        body: JSON.stringify({ fechaProgramada, motivo }),
      }),

    anular: (id: string, motivo: string) =>
      pedir<void>(
        `/mantenimientos/programados/${id}?motivo=${encodeURIComponent(motivo)}`,
        { method: 'DELETE' },
      ),

    generar: () =>
      pedir<GeneracionDto>('/mantenimientos/programados/generar', { method: 'POST' }),
  },

  planes: {
    listar: () => pedir<PlanMantenimientoDto[]>('/mantenimientos/planes'),

    crear: (entrada: PlanMantenimientoEntradaDto) =>
      pedir<string>('/mantenimientos/planes', {
        method: 'POST',
        body: JSON.stringify(entrada),
      }),

    actualizar: (id: string, entrada: PlanMantenimientoEntradaDto) =>
      pedir<string>(`/mantenimientos/planes/${id}`, {
        method: 'PUT',
        body: JSON.stringify(entrada),
      }),
  },

  facturacion: {
    listar: (filtro: { estado?: string; periodo?: string } = {}) => {
      const p = new URLSearchParams();
      if (filtro.estado) p.set('estado', filtro.estado);
      if (filtro.periodo) p.set('periodo', filtro.periodo);
      const q = p.toString();
      return pedir<ComprobanteDto[]>(`/facturacion${q ? `?${q}` : ''}`);
    },

    panel: () => pedir<PanelFacturacionDto>('/facturacion/panel'),

    detalle: (id: string) => pedir<ComprobanteDto>(`/facturacion/${id}`),

    generar: (periodo?: string) =>
      pedir<GeneracionComprobanteDto>(
        `/facturacion${periodo ? `?periodo=${periodo}` : ''}`,
        { method: 'POST' },
      ),

    ajustar: (id: string, concepto: string, importe: number) =>
      pedir<ComprobanteDto>(`/facturacion/${id}/ajustes`, {
        method: 'POST',
        body: JSON.stringify({ concepto, importe }),
      }),

    emitir: (id: string) =>
      pedir<ComprobanteDto>(`/facturacion/${id}/emitir`, { method: 'POST' }),

    registrarPago: (id: string, fecha?: string) =>
      pedir<ComprobanteDto>(
        `/facturacion/${id}/pago${fecha ? `?fecha=${fecha}` : ''}`,
        { method: 'POST' },
      ),

    anular: (id: string, motivo: string) =>
      pedir<void>(`/facturacion/${id}?motivo=${encodeURIComponent(motivo)}`, {
        method: 'DELETE',
      }),
  },

  usuarios: {
    listar: () => pedir<UsuarioListaDto[]>('/usuarios'),
    detalle: (id: string) => pedir<UsuarioDetalleDto>(`/usuarios/${id}`),
    roles: () => pedir<RolDto[]>('/usuarios/roles'),

    cambiarEstado: (id: string, activo: boolean) =>
      pedir<void>(`/usuarios/${id}/estado?activo=${activo}`, { method: 'PUT' }),

    desbloquear: (id: string) => pedir<void>(`/usuarios/${id}/desbloquear`, { method: 'PUT' }),

    asignarRoles: (id: string, idsRol: string[]) =>
      pedir<void>(`/usuarios/${id}/roles`, { method: 'PUT', body: JSON.stringify({ idsRol }) }),
  },

  bitacora: {
    consultar: (
      filtro: { desde?: string; hasta?: string; tipo?: string; usuario?: string } = {},
    ) => {
      const p = new URLSearchParams();
      if (filtro.desde) p.set('desde', filtro.desde);
      if (filtro.hasta) p.set('hasta', filtro.hasta);
      if (filtro.tipo) p.set('tipo', filtro.tipo);
      if (filtro.usuario) p.set('usuario', filtro.usuario);
      const s = p.toString();
      return pedir<EntradaBitacoraDto[]>(`/bitacora${s ? `?${s}` : ''}`);
    },

    tipos: () => pedir<CatalogoItemDto[]>('/bitacora/tipos'),
  },

  /** Errores del sistema (CU.Arq.007). */
  errores: {
    consultar: (
      filtro: { desde?: string; hasta?: string; advertencias?: boolean } = {},
    ) => {
      const p = new URLSearchParams();
      if (filtro.desde) p.set('desde', filtro.desde);
      if (filtro.hasta) p.set('hasta', filtro.hasta);
      if (filtro.advertencias) p.set('advertencias', 'true');
      const s = p.toString();
      return pedir<ErrorDto[]>(`/configuracion/errores${s ? `?${s}` : ''}`);
    },
  },

  configuracionIa: {
    panel: () => pedir<PanelIaDto>('/configuracion/ia'),

    guardar: (entrada: ConfiguracionIaDto) =>
      pedir<void>('/configuracion/ia', { method: 'PUT', body: JSON.stringify(entrada) }),

    // No hay lectura de la clave, y es a propósito: una credencial que puede
    // salir del servidor es una credencial que se puede filtrar.
    guardarClave: (clave: string) =>
      pedir<void>('/configuracion/ia/clave', {
        method: 'PUT',
        body: JSON.stringify({ clave }),
      }),

    borrarClave: () => pedir<void>('/configuracion/ia/clave', { method: 'DELETE' }),
  },

  respaldos: {
    panel: () => pedir<PanelRespaldosDto>('/respaldos'),

    respaldar: (base: string) =>
      pedir<RespaldoDto>(`/respaldos?basedatos=${encodeURIComponent(base)}`, { method: 'POST' }),
  },

  reglas: {
    listar: () => pedir<ReglaDto[]>('/prediccion/reglas'),

    guardar: (regla: ReglaDto) =>
      pedir<string>('/prediccion/reglas', { method: 'PUT', body: JSON.stringify(regla) }),
  },
};

function queryIncidencias(filtro: FiltroIncidencias): string {
  const p = new URLSearchParams();
  if (filtro.texto?.trim()) p.set('texto', filtro.texto.trim());
  if (filtro.equipo) p.set('equipo', filtro.equipo);
  if (filtro.estado) p.set('estado', filtro.estado);
  if (filtro.prioridad) p.set('prioridad', filtro.prioridad);
  if (filtro.categoria) p.set('categoria', filtro.categoria);
  if (filtro.tecnico) p.set('tecnico', filtro.tecnico);
  // Los booleanos sólo viajan cuando están en true: `abiertas=false` y no
  // mandar nada significan lo mismo, y la URL sin ruido se lee mejor al depurar.
  if (filtro.abiertas) p.set('abiertas', 'true');
  if (filtro.revision) p.set('revision', 'true');
  if (filtro.desde) p.set('desde', filtro.desde);
  if (filtro.hasta) p.set('hasta', filtro.hasta);
  if (filtro.pagina) p.set('pagina', String(filtro.pagina));
  if (filtro.porPagina) p.set('porPagina', String(filtro.porPagina));
  const s = p.toString();
  return s ? `?${s}` : '';
}
