import { useT } from '../idioma/IdiomaContext';
import type { Traducir } from '../idioma/IdiomaContext';

/**
 * El nivel de riesgo de un equipo.
 *
 * Lo codifica cuatro veces y el color es la cuarta, que es lo que pide la capa
 * de tokens del rediseño: glifo de tres escalones con uno, dos o tres llenos;
 * el puntaje en monoespaciada, que se compara en columna; el rótulo ALTO,
 * MEDIO o BAJO; y recién ahí el color.
 *
 * Antes de esto la tabla mostraba un punto de color y el número. Para alguien
 * que no distingue el rojo del ámbar, un 68 y un 92 se veían igual de graves,
 * y son cosas distintas.
 *
 * Vive en un componente y no en cada pantalla porque estaba duplicado en el
 * tablero y en el análisis predictivo, con su propia función de color en cada
 * archivo.
 */

type Nivel = 'ALTO' | 'MEDIO' | 'BAJO' | string;

const ESCALONES = [
  { alto: 4, y: 7 },
  { alto: 7, y: 4 },
  { alto: 11, y: 0 },
];

function llenos(nivel: Nivel): number {
  if (nivel === 'ALTO') return 3;
  if (nivel === 'MEDIO') return 2;
  return 1;
}

function color(nivel: Nivel): string {
  if (nivel === 'ALTO') return 'var(--alto-barra)';
  if (nivel === 'MEDIO') return 'var(--medio-barra)';
  return 'var(--bajo-barra)';
}

/**
 * El rotulo del nivel.
 *
 * `nivel` llega en mayusculas desde la API y es un valor, no un texto: se
 * compara. Lo que se muestra es otra cosa y sale del diccionario.
 */
function rotulo(t: Traducir, nivel: Nivel): string {
  if (nivel === 'ALTO') return t('riesgo.alto', 'Alto');
  if (nivel === 'MEDIO') return t('riesgo.medio', 'Medio');
  return t('riesgo.bajo', 'Bajo');
}

/** El glifo solo, para donde no entra el rótulo. */
export function GlifoRiesgo({ nivel }: { nivel: Nivel }) {
  const activos = llenos(nivel);
  return (
    <svg
      width="13"
      height="11"
      viewBox="0 0 13 11"
      aria-hidden="true"
      style={{ flex: 'none', display: 'block' }}
    >
      {ESCALONES.map((e, i) => (
        <rect
          key={i}
          x={i * 4.5}
          y={e.y}
          width="3"
          height={e.alto}
          rx="1"
          fill={i < activos ? color(nivel) : 'var(--linea-fuerte)'}
        />
      ))}
    </svg>
  );
}

export function NivelRiesgo({
  nivel,
  score,
  conRotulo = true,
}: {
  nivel: Nivel;
  score: number;
  conRotulo?: boolean;
}) {
  const t = useT();

  return (
    <span
      className="estado nivel-riesgo"
      style={{ gap: 7 }}
      // El lector de pantalla lee una sola frase en lugar de deletrear el
      // glifo, el número y el rótulo por separado.
      role="img"
      aria-label={t('riesgo.lectura', 'Riesgo {nivel}, puntaje {score} de 100', {
        nivel: rotulo(t, nivel).toLowerCase(),
        score,
      })}
    >
      <GlifoRiesgo nivel={nivel} />
      <span className="mono" style={{ fontWeight: 600 }}>
        {score}
      </span>
      {conRotulo ? (
        <span
          className="sub"
          style={{ fontSize: 'var(--txt-micro)', textTransform: 'uppercase', letterSpacing: '.12em' }}
        >
          {rotulo(t, nivel)}
        </span>
      ) : null}
    </span>
  );
}
