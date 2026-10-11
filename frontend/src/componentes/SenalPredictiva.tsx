import type { CSSProperties } from 'react';
import { Link } from 'react-router-dom';
import type { EquipoEnRiesgoDto } from '../api/tipos';
import { useT } from '../idioma/IdiomaContext';
import { NivelRiesgo } from './NivelRiesgo';

/** A view of the returned ranking, never a new prediction or a park-wide distribution. */
export function SenalPredictiva({ equipos, puedeVerEquipo }: {
  equipos: EquipoEnRiesgoDto[];
  puedeVerEquipo: boolean;
}) {
  const t = useT();
  const ranking = equipos.filter(e => Number.isFinite(e.score) && e.score >= 0 && e.score <= 100)
    .slice().sort((a, b) => b.score - a.score);
  const primero = ranking[0];
  if (!primero) return null;
  const tono = (nivel: string) => nivel === 'ALTO' ? 'var(--alto-tx)' : nivel === 'MEDIO' ? 'var(--medio-tx)' : 'var(--bajo-tx)';
  return (
    <section className="senal-predictiva" aria-label={t('comun.riesgo', 'Riesgo')}>
      <div className="senal-destacada">
        <div className="senal-anillo" style={{ '--score': `${primero.score}%`, '--riesgo-color': tono(primero.nivel) } as CSSProperties} aria-hidden="true">
          <div><strong>{primero.score}</strong><small>0–100</small></div>
        </div>
        <div className="senal-contexto">
          <div className="sub">{t('dash.mayorRiesgo', 'Equipos con mayor riesgo')}</div>
          <h2>{puedeVerEquipo ? <Link to={`/activos/${primero.idEquipo}`}>{primero.codigo}</Link> : primero.codigo}</h2>
          <p>{primero.motivoPrincipal}</p>
          <NivelRiesgo nivel={primero.nivel} score={primero.score} />
          {puedeVerEquipo ? <Link className="senal-enlace" to={`/activos/${primero.idEquipo}`}>{t('comun.ver', 'Ver')} →</Link> : null}
        </div>
      </div>
      <div className="senal-ranking">
        <div className="senal-ranking-titulo"><h3>{t('dash.mayorRiesgo', 'Equipos con mayor riesgo')}</h3><span className="sub">0–100</span></div>
        <div className="senal-barras">
          {ranking.slice(0, 7).map(e => (
            <div key={e.idEquipo} className="senal-columna" style={{ '--riesgo-color': tono(e.nivel) } as CSSProperties}>
              <span className="mono">{e.score}</span>
              <div className="senal-columna-pista" aria-hidden="true"><i style={{ height: `${e.score}%` }} /></div>
              {puedeVerEquipo ? <Link to={`/activos/${e.idEquipo}`} title={e.codigo}>{e.codigo}</Link> : <span>{e.codigo}</span>}
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
