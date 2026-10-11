/**
 * Los siete estados internos, contados como se los cuenta a quien reportó.
 *
 * El solicitante no ve «En espera de repuesto» ni «Pendiente de asignación»:
 * son el vocabulario del taller, y lo que la persona quiere saber es si su
 * problema ya lo está mirando alguien. Cuatro pasos alcanzan para eso, y son
 * los mismos siete estados agrupados, no una máquina de estados paralela —el
 * sistema sigue teniendo una sola—.
 *
 * `Anulada` queda fuera del recorrido a propósito: no es un avance, es una
 * salida, y mostrarla como «paso 1 de 4» diría que todavía puede pasar algo.
 */

export const PASOS = 4;

export interface Avance {
  /** 1 a 4, o `null` cuando la incidencia salió del recorrido. */
  paso: number | null;
  clave: string;
  texto: string;
  /** El color del punto, de la misma paleta que usa el técnico. */
  color: string;
}

const PENDIENTE: Avance = {
  paso: 1,
  clave: 'pedido.recibido',
  texto: 'Lo recibimos',
  color: 'var(--est-pendiente)',
};

const POR_ESTADO: Record<string, Avance> = {
  'Pendiente de asignación': PENDIENTE,
  Asignada: {
    paso: 2,
    clave: 'pedido.asignado',
    texto: 'Se lo asignamos a un técnico',
    color: 'var(--est-asignada)',
  },
  'En curso': {
    paso: 3,
    clave: 'pedido.arreglando',
    texto: 'Lo están arreglando',
    color: 'var(--est-curso)',
  },
  // Esperar un repuesto es, para quien reportó, lo mismo que estar en curso:
  // alguien lo está atendiendo y todavía no terminó. Lo que cambia es el
  // motivo de la demora, y eso se cuenta en la novedad, no en el paso.
  'En espera de repuesto': {
    paso: 3,
    clave: 'pedido.esperandoRepuesto',
    texto: 'Esperando un repuesto',
    color: 'var(--est-repuesto)',
  },
  Resuelta: {
    paso: 4,
    clave: 'pedido.resuelto',
    texto: 'Resuelto',
    color: 'var(--est-resuelta)',
  },
  Cerrada: {
    paso: 4,
    clave: 'pedido.cerrado',
    texto: 'Resuelto y cerrado',
    color: 'var(--est-cerrada)',
  },
  Anulada: {
    paso: null,
    clave: 'pedido.anulado',
    texto: 'Cancelado',
    color: 'var(--est-anulada)',
  },
};

/**
 * Un estado que no esté en el catálogo conocido cae en el primer paso en vez
 * de romper la pantalla: es preferible mostrar «Lo recibimos» de menos que
 * dejar a alguien sin saber qué pasó con su reporte.
 */
export function avanceDe(estado: string): Avance {
  return POR_ESTADO[estado] ?? PENDIENTE;
}
