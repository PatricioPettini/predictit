import { useT } from '../idioma/IdiomaContext';

/**
 * Las marcas que dicen «qué es esto»: estado, prioridad y origen de la decisión.
 *
 * Vivían repartidas. `colorEstado` estaba escrita tres veces —en Activos, en
 * Incidencias y en Mis equipos— con reglas distintas, y la prioridad se pintaba
 * con un chip que sólo distinguía tres de los cuatro niveles. Que dos pantallas
 * pinten el mismo dato de dos colores no es un detalle: es lo que hace que
 * alguien aprenda el código de una pantalla y se equivoque en la otra.
 *
 * Los tres respetan las reglas de la capa de tokens:
 *
 *   - Ningún estado usa el azul del acento. El azul es «hacé clic», no
 *     «está así», y usarlo para un estado le sacaría el único canal que la
 *     interfaz tiene reservado para lo accionable.
 *   - El violeta es sólo IA. El chip de origen es el único lugar donde aparece
 *     fuera del panel de sugerencias.
 *   - El color nunca va solo: el estado lleva su nombre al lado, la prioridad
 *     lleva barras y su nombre, y el riesgo —que vive en `NivelRiesgo`— lleva
 *     forma, puntaje y rótulo.
 */

/**
 * Los nombres de los catálogos, tal como están en la base.
 *
 * NO son texto de interfaz y no se traducen: son valores de `EstadoIncidencia`
 * y `EstadoEquipo`, con restricción de unicidad sobre la columna `nombre`. Si
 * algún día llevan un código propio, este es el único archivo que cambia.
 */
const ESTADO_INCIDENCIA: Record<string, string> = {
  'Pendiente de asignación': 'var(--est-pendiente)',
  Asignada: 'var(--est-asignada)',
  'En curso': 'var(--est-curso)',
  'En espera de repuesto': 'var(--est-repuesto)',
  Resuelta: 'var(--est-resuelta)',
  Cerrada: 'var(--est-cerrada)',
  Anulada: 'var(--est-anulada)',
};

const ESTADO_EQUIPO: Record<string, string> = {
  Operativo: 'var(--est-resuelta)',
  'En observación': 'var(--est-curso)',
  // Rosa y no rojo: está intervenido, que es lo mismo que le pasa a una
  // incidencia en espera de repuesto. El rojo queda para lo que no funciona.
  'En reparación': 'var(--est-repuesto)',
  'Fuera de servicio': 'var(--alto-barra)',
  'Dado de baja': 'var(--est-anulada)',
};

/**
 * Estado de una incidencia: punto de color y el nombre al lado.
 *
 * El nombre siempre está, y por eso el punto no necesita además una forma
 * propia por estado: el canal redundante que exige no depender del color ya lo
 * cubre el texto. En el nivel de riesgo es distinto —ahí el dato es un número y
 * el rótulo es chico—, y por eso ese sí lleva glifo.
 */
export function EstadoIncidencia({ estado, abierta }: { estado: string; abierta?: boolean }) {
  // `abierta` sólo se usa como respaldo: si llegara un estado que no está en el
  // catálogo, al menos se distingue lo abierto de lo cerrado.
  const color = ESTADO_INCIDENCIA[estado]
    ?? (abierta === false ? 'var(--est-cerrada)' : 'var(--est-pendiente)');
  return (
    <span className="estado">
      <span className="punto" style={{ background: color }} aria-hidden="true" />
      {estado}
    </span>
  );
}

/** Estado de un equipo. Mismo criterio que el de la incidencia. */
export function EstadoEquipo({ estado, operativo }: { estado: string; operativo?: boolean }) {
  const color = ESTADO_EQUIPO[estado] ?? (operativo === false ? 'var(--tx3)' : 'var(--est-resuelta)');
  return (
    <span className="estado">
      <span className="punto" style={{ background: color }} aria-hidden="true" />
      {estado}
    </span>
  );
}

const PRIORIDAD = [
  { desde: 4, color: 'var(--prio-critica)' },
  { desde: 3, color: 'var(--prio-alta)' },
  { desde: 2, color: 'var(--prio-media)' },
  { desde: 1, color: 'var(--prio-baja)' },
];

/**
 * Prioridad: barras y nombre.
 *
 * Las barras son el canal que no depende del color, y además comparan: cuatro
 * contra dos se lee en una columna sin leer ninguna palabra. La escala es la
 * de la base —`PrioridadIncidencia.nivel`, 1 baja a 4 crítica— y no la del
 * riesgo, que es otra cosa: un equipo de riesgo bajo puede tener una incidencia
 * crítica.
 */
export function Prioridad({ nombre, nivel }: { nombre: string; nivel: number }) {
  const color = PRIORIDAD.find((p) => nivel >= p.desde)?.color ?? 'var(--prio-baja)';
  return (
    <span className="prioridad" title={nombre}>
      <span className="prioridad-barras" aria-hidden="true">
        {[1, 2, 3, 4].map((i) => (
          <span
            key={i}
            style={{
              // La barra vacia es la pista, no el dato: va en --linea y no en
              // --linea-fuerte, porque con las cuatro del mismo peso "baja" y
              // "critica" se ven casi iguales y el conteo deja de servir.
              background: i <= nivel ? color : 'var(--linea)',
              // La barra crece con el nivel: la altura es el segundo canal, y
              // sirve incluso en una captura en blanco y negro.
              height: 4 + i * 2,
            }}
          />
        ))}
      </span>
      <span style={{ color }}>{nombre}</span>
    </span>
  );
}

/**
 * De dónde salió la asignación del técnico (RF-08, CU-007).
 *
 * Es la trazabilidad hecha visible, así que las cuatro procedencias tienen que
 * verse distintas de un vistazo:
 *
 *   IA          violeta, que es el único color reservado a lo que propuso el
 *               servicio externo.
 *   Automática  plano: la heurística por reglas es del propio sistema.
 *   Manual      plano y sin color: lo decidió una persona.
 *   Respaldo    ámbar, porque pide revisión. No es un error ni es IA: es lo
 *               que el sistema hizo cuando el proveedor no contestó.
 */
export function OrigenAsignacion({ tipo, revision }: { tipo: string; revision: boolean }) {
  const t = useT();

  if (tipo === 'PENDIENTE') {
    return <span className="sub">{t('incidencia.sinAsignar', 'Sin asignar')}</span>;
  }

  if (revision) {
    return <span className="chip respaldo">{t('incidencia.porRespaldo', 'Respaldo · revisar')}</span>;
  }

  if (tipo === 'IA') return <span className="chip ia">IA</span>;

  const texto =
    tipo === 'HEURISTICA'
      ? t('ia.automatica', 'Automática')
      : tipo === 'MANUAL'
        ? t('comun.manual', 'Manual')
        : tipo;

  return <span className="chip neutro">{texto}</span>;
}

/**
 * Estado de un trabajo agendado, con su distancia a hoy.
 *
 * El estado solo no alcanza: «Programado» describe igual al que vence mañana
 * que al que venció hace dos meses, y en una agenda lo único que se mira es
 * justamente eso. Por eso el rótulo lleva los días, que vienen calculados del
 * servidor —el navegador puede tener otra fecha, y entonces dos personas
 * verían distinto qué está vencido—.
 *
 * El rojo es el mismo que marca el riesgo alto del equipo, y no uno propio:
 * un preventivo atrasado es una de las cosas que lo elevan.
 */
export function EstadoProgramado({
  estado,
  vencido,
  diasDeAtraso,
}: {
  estado: string;
  vencido: boolean;
  diasDeAtraso: number;
}) {
  const t = useT();

  if (estado === 'EJECUTADO') {
    return (
      <span className="estado">
        <span className="punto" style={{ background: 'var(--est-resuelta)' }} aria-hidden="true" />
        {t('programado.ejecutado', 'Ejecutado')}
      </span>
    );
  }

  if (estado === 'ANULADO') {
    return (
      <span className="estado">
        <span className="punto" style={{ background: 'var(--est-anulada)' }} aria-hidden="true" />
        {t('programado.anulado', 'Anulado')}
      </span>
    );
  }

  if (vencido) {
    return (
      <span className="estado">
        <span className="punto" style={{ background: 'var(--alto-barra)' }} aria-hidden="true" />
        {t('programado.vencidoDias', 'Vencido · {dias} d', { dias: String(diasDeAtraso) })}
      </span>
    );
  }

  // `diasDeAtraso` es negativo mientras falta, así que acá se da vuelta para
  // que el rótulo diga cuántos faltan y no «menos siete».
  const faltan = -diasDeAtraso;

  return (
    <span className="estado">
      <span className="punto" style={{ background: 'var(--est-pendiente)' }} aria-hidden="true" />
      {faltan === 0
        ? t('programado.hoy', 'Hoy')
        : t('programado.enDias', 'En {dias} d', { dias: String(faltan) })}
    </span>
  );
}

/**
 * Estado de un comprobante del servicio.
 *
 * Mismo criterio que el de un trabajo agendado: «Emitido» describe igual al que
 * vence la semana que viene que al que venció hace dos meses, y en una
 * facturación lo que se mira es justamente eso. Por eso el rótulo del vencido
 * lleva los días de atraso, calculados en el servidor.
 *
 * El rojo del vencido es el mismo que el del preventivo atrasado y el del
 * riesgo alto: en todo el sistema significa «esto ya debería haber pasado».
 */
export function EstadoComprobante({
  estado,
  vencido,
  diasDeAtraso,
}: {
  estado: string;
  vencido: boolean;
  diasDeAtraso: number;
}) {
  const t = useT();

  const punto = (color: string, texto: string) => (
    <span className="estado">
      <span className="punto" style={{ background: color }} aria-hidden="true" />
      {texto}
    </span>
  );

  if (estado === 'PAGADO') return punto('var(--est-resuelta)', t('factura.pagado', 'Cobrado'));
  if (estado === 'ANULADO') return punto('var(--est-anulada)', t('factura.anulado', 'Anulado'));

  if (estado === 'BORRADOR') {
    // El borrador no es un estado de cobro: es un cálculo que todavía se puede
    // rehacer, y por eso no lleva ni verde ni rojo.
    return punto('var(--est-cerrada)', t('factura.borrador', 'Borrador'));
  }

  if (vencido) {
    return punto(
      'var(--alto-barra)',
      t('factura.vencidoDias', 'Vencido · {dias} d', { dias: String(diasDeAtraso) }),
    );
  }

  return punto('var(--est-pendiente)', t('factura.emitidoEstado', 'Emitido'));
}
