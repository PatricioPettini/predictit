import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';

/**
 * Selección de tema visual.
 *
 * El tema es una preferencia de la persona, no de la organización: se guarda en
 * `localStorage` y no en la base. Que sobreviva al cierre de sesión es
 * deliberado — quien elige el modo oscuro lo elige para su vista, no para su
 * cuenta— y por eso es lo único que se guarda en el navegador: el token de
 * sesión sigue en `sessionStorage`.
 *
 * No hay ningún componente que sepa qué tema está activo. El provider escribe
 * dos atributos en el elemento raíz y las variables CSS hacen el resto. Eso es
 * lo que permite sumar un tema sin tocar una pantalla.
 */

// `clave` y `claveDetalle` acompañan al texto porque la lista se declara acá,
// fuera de todo componente, y `t` sólo existe adentro de uno. El castellano
// queda igual al lado de su clave: es lo que se muestra si el diccionario no
// tiene la entrada.
export const TEMAS = [
  {
    id: 'oscuro',
    clave: 'tema.oscuro',
    nombre: 'Oscuro',
    claveDetalle: 'tema.oscuroDetalle',
    detalle: 'Recomendado para salas con poca luz. Es el tema de fábrica.',
  },
  {
    id: 'claro',
    clave: 'tema.claro',
    nombre: 'Claro',
    claveDetalle: 'tema.claroDetalle',
    detalle: 'Mejor para imprimir y para proyectar.',
  },
  {
    id: 'sistema',
    clave: 'tema.sistema',
    nombre: 'Seguir el sistema',
    claveDetalle: 'tema.sistemaDetalle',
    detalle: 'Cambia solo al anochecer si tu equipo lo hace.',
  },
] as const;

/**
 * Temas que existieron y ya no. A quien tenga uno guardado en el navegador se
 * lo lleva al que lo reemplaza: sin esto, la pantalla de Apariencia le mostraría
 * una selección que no está en la lista.
 *
 * Los tres primeros cayeron en el rediseño 02: con el acento restringido a lo
 * accionable, los claros dejaron de distinguirse del clásico.
 *
 * Los tres últimos caen en el rediseño 03. Con seis temas hay treinta y seis
 * pares de color que verificar cada vez que se toca un token, y con dos hay
 * doce; lo que la gente personaliza de verdad es «me molesta el brillo», y eso
 * lo resuelve claro contra oscuro. El mapeo respeta la claridad de cada uno:
 * clásico era el claro de fábrica y va a `claro`, no a `oscuro`.
 */
const REEMPLAZADOS: Record<string, string> = {
  indigo: 'claro',
  bosque: 'claro',
  pizarra: 'oscuro',
  clasico: 'claro',
  grafito: 'oscuro',
  carbon: 'oscuro',
};

/** Nombre visible de cada tema retirado, para el aviso de migración. */
export const NOMBRES_RETIRADOS: Record<string, string> = {
  indigo: 'Índigo',
  bosque: 'Bosque',
  pizarra: 'Pizarra',
  clasico: 'Clásico',
  grafito: 'Grafito',
  carbon: 'Carbón',
};

export const DENSIDADES = [
  { id: 'compacta', clave: 'tema.compacta', nombre: 'Compacta',
    claveDetalle: 'tema.compactaDetalle', detalle: 'Fila de 34 px, para carga y auditoría.' },
  { id: 'normal', clave: 'tema.normal', nombre: 'Normal',
    claveDetalle: 'tema.normalDetalle', detalle: 'Fila de 48 px, la del diseño.' },
  { id: 'amplia', clave: 'tema.amplia', nombre: 'Amplia',
    claveDetalle: 'tema.ampliaDetalle', detalle: 'Fila de 60 px, para pantallas grandes.' },
] as const;

export type IdTema = (typeof TEMAS)[number]['id'];
export type IdDensidad = (typeof DENSIDADES)[number]['id'];
/** Lo que termina escrito en `data-tema`: `sistema` no es una paleta. */
export type TemaPintado = 'oscuro' | 'claro';

const CLAVE_TEMA = 'predictit.tema';
const CLAVE_DENSIDAD = 'predictit.densidad';
const CLAVE_MIGRADO = 'predictit.tema.migrado';
const CLAVE_MOVIMIENTO = 'predictit.movimiento';

const CONSULTA_OSCURO = '(prefers-color-scheme: dark)';

interface EstadoTema {
  tema: IdTema;
  /** El que se está pintando. Con `sistema` elegido depende del navegador. */
  pintado: TemaPintado;
  densidad: IdDensidad;
  /**
   * Reduccion de movimiento pedida en Apariencia. Se SUMA a la del sistema
   * operativo, no la reemplaza: quien ya la tiene puesta en Windows la
   * conserva con esto en `false`.
   */
  movimientoReducido: boolean;
  elegirTema: (id: IdTema) => void;
  elegirDensidad: (id: IdDensidad) => void;
  elegirMovimiento: (reducido: boolean) => void;
  /**
   * Nombre del tema retirado que traía esta persona, o `null`. Lo consume el
   * aviso de una sola vez de la pantalla de Apariencia.
   */
  migradoDesde: string | null;
  descartarAviso: () => void;
}

const Contexto = createContext<EstadoTema | null>(null);

/**
 * Lee una preferencia guardada.
 *
 * Va con try/catch porque en una ventana privada, o con las cookies de sitio
 * bloqueadas, el solo hecho de acceder a `localStorage` lanza. Sin la guarda, la
 * aplicación no arrancaría en esos navegadores por una preferencia de color.
 */
function leer<T extends string>(clave: string, validos: readonly T[], porDefecto: T): T {
  try {
    const valor = localStorage.getItem(clave) as T | null;
    return valor && validos.includes(valor) ? valor : porDefecto;
  } catch {
    return porDefecto;
  }
}

function guardar(clave: string, valor: string) {
  try {
    localStorage.setItem(clave, valor);
  } catch {
    // Que no se pueda recordar la preferencia no es motivo para interrumpir nada.
  }
}

function olvidar(clave: string) {
  try {
    localStorage.removeItem(clave);
  } catch {
    // Idem: no poder limpiar una marca no justifica romper la pantalla.
  }
}

function prefiereOscuro(): boolean {
  try {
    return window.matchMedia(CONSULTA_OSCURO).matches;
  } catch {
    // Un entorno sin matchMedia —jsdom sin polyfill, por ejemplo— no tiene
    // preferencia que consultar, y el de fábrica es el oscuro.
    return true;
  }
}

/**
 * Resuelve la preferencia guardada, migrando la de un tema retirado.
 *
 * Devuelve también de cuál venía, que es lo que necesita el aviso: quien tenía
 * Carbón tiene que enterarse una vez de que ahora está en Oscuro, y no
 * encontrarse con que la pantalla cambió sin explicación.
 */
function preferenciaInicial(): { tema: IdTema; migradoDesde: string | null } {
  const ids = TEMAS.map((t) => t.id);
  const guardado = leer(CLAVE_TEMA, [...ids, ...Object.keys(REEMPLAZADOS)], 'oscuro');
  const reemplazo = REEMPLAZADOS[guardado];
  if (!reemplazo) {
    // El aviso puede haber quedado pendiente de una visita anterior: se migró
    // el tema, se guardó el nuevo, y la persona cerró antes de leerlo.
    let pendiente: string | null = null;
    try {
      pendiente = localStorage.getItem(CLAVE_MIGRADO);
    } catch {
      pendiente = null;
    }
    return { tema: guardado as IdTema, migradoDesde: pendiente };
  }
  guardar(CLAVE_TEMA, reemplazo);
  guardar(CLAVE_MIGRADO, guardado);
  return { tema: reemplazo as IdTema, migradoDesde: guardado };
}

export function ProveedorTema({ children }: { children: ReactNode }) {
  const inicial = useMemo(preferenciaInicial, []);
  const [tema, setTema] = useState<IdTema>(inicial.tema);
  const [migradoDesde, setMigradoDesde] = useState<string | null>(inicial.migradoDesde);

  const [densidad, setDensidad] = useState<IdDensidad>(() =>
    leer(
      CLAVE_DENSIDAD,
      DENSIDADES.map((d) => d.id),
      'normal',
    ),
  );

  const [movimientoReducido, setMovimiento] = useState(
    () => leer(CLAVE_MOVIMIENTO, ['si', 'no'] as const, 'no') === 'si',
  );

  // Sólo se consulta al navegador cuando la persona eligió seguirlo. Guardar el
  // resultado en estado y no leerlo al pintar es lo que hace que el cambio de
  // modo del sistema operativo se refleje sin recargar.
  const [sistemaOscuro, setSistemaOscuro] = useState(prefiereOscuro);

  useEffect(() => {
    if (tema !== 'sistema') return;
    let medio: MediaQueryList;
    try {
      medio = window.matchMedia(CONSULTA_OSCURO);
    } catch {
      return;
    }
    const alCambiar = (e: MediaQueryListEvent) => setSistemaOscuro(e.matches);
    setSistemaOscuro(medio.matches);
    medio.addEventListener('change', alCambiar);
    return () => medio.removeEventListener('change', alCambiar);
  }, [tema]);

  const pintado: TemaPintado =
    tema === 'sistema' ? (sistemaOscuro ? 'oscuro' : 'claro') : tema;

  useEffect(() => {
    const raiz = document.documentElement;
    // En `data-tema` va el tema pintado y nunca `sistema`: la capa de tokens
    // define dos paletas, y `sistema` es una forma de elegir entre las dos.
    raiz.setAttribute('data-tema', pintado);
    raiz.setAttribute('data-densidad', densidad);
    // El atributo se pone y se saca: dejarlo en `normal` obligaria a escribir
    // cada regla dos veces, una por valor.
    if (movimientoReducido) raiz.setAttribute('data-movimiento', 'reducido');
    else raiz.removeAttribute('data-movimiento');
  }, [pintado, densidad, movimientoReducido]);

  const elegirTema = useCallback((id: IdTema) => {
    setTema(id);
    guardar(CLAVE_TEMA, id);
  }, []);

  const elegirDensidad = useCallback((id: IdDensidad) => {
    setDensidad(id);
    guardar(CLAVE_DENSIDAD, id);
  }, []);

  const elegirMovimiento = useCallback((reducido: boolean) => {
    setMovimiento(reducido);
    guardar(CLAVE_MOVIMIENTO, reducido ? 'si' : 'no');
  }, []);

  const descartarAviso = useCallback(() => {
    setMigradoDesde(null);
    olvidar(CLAVE_MIGRADO);
  }, []);

  const valor = useMemo<EstadoTema>(
    () => ({
      tema, pintado, densidad, movimientoReducido,
      elegirTema, elegirDensidad, elegirMovimiento,
      migradoDesde, descartarAviso,
    }),
    [tema, pintado, densidad, movimientoReducido, elegirTema, elegirDensidad,
     elegirMovimiento, migradoDesde, descartarAviso],
  );

  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}

export function useTema() {
  const ctx = useContext(Contexto);
  if (!ctx) throw new Error('useTema tiene que usarse dentro de ProveedorTema.');
  return ctx;
}
