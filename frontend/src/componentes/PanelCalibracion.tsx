import { useEffect, useState } from 'react';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { api } from '../api/cliente';
import type { CalibracionReglaDto } from '../api/tipos';
import { useCerrarSiExpiro } from '../sesion/SesionContext';
import { EsqueletoTabla, SinDatos } from './Estados';

/**
 * Cómo viene rindiendo cada regla del motor.
 *
 * El dato que importa es el de las **descartadas**: una alerta descartada es
 * una que alguien miró y decidió que no aplicaba. Muchas descartadas sobre
 * pocas atendidas quieren decir que el umbral está bajo y que la regla está
 * molestando en lugar de avisar.
 *
 * Hasta ahora la diferencia entre atendida y descartada se guardaba y no se
 * mostraba en ninguna parte, así que los umbrales se ajustaban a ojo.
 */

function porcentaje(v: number | null | undefined): string {
  return v === null || v === undefined ? '—' : `${Math.round(v * 100)} %`;
}

type Traducir = (clave: string, porDefecto: string) => string;

/**
 * Lectura de la precisión, en palabras. Un número solo no dice qué hacer.
 *
 * Recibe `t` en lugar de mudarse adentro del componente: la lectura no depende
 * del estado de la pantalla, y acá afuera se puede probar sola.
 */
function veredicto(
  t: Traducir,
  c: CalibracionReglaDto,
): { texto: string; clase: string } {
  const resueltas = c.atendidas + c.descartadas;

  if (c.generadas === 0)
    return { texto: t('analisis.nuncaDisparo', 'Nunca disparó'), clase: 'chip' };
  if (resueltas < 3)
    return { texto: t('analisis.pocosDatos', 'Pocos datos'), clase: 'chip' };
  if (c.precision !== null && c.precision < 0.4)
    return { texto: t('analisis.umbralBajo', 'Umbral bajo'), clase: 'chip medio' };
  if (c.precision !== null && c.precision > 0.85)
    return { texto: t('analisis.bienCalibrada', 'Bien calibrada'), clase: 'chip bajo' };
  return { texto: t('analisis.razonable', 'Razonable'), clase: 'chip' };
}

export function PanelCalibracion() {
  const t = useT();

  // Sin fecha el campo dice «nunca» y no una raya: acá la ausencia significa
  // que la regla no disparó todavía, que es distinto de «no hay dato».
  const { fecha: fechaDe } = useFormato();
  const fecha = (iso?: string | null) =>
    (iso ? fechaDe(iso) : t('comun.nunca', 'nunca'));
  const cerrarSiExpiro = useCerrarSiExpiro();
  const [filas, setFilas] = useState<CalibracionReglaDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.prediccion
      .calibracion()
      .then(setFilas)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(t('error.cargarRendimiento', 'No se pudo cargar el rendimiento de las reglas.'));
      });
  }, [cerrarSiExpiro]);

  const conDatos = (filas ?? []).filter((f) => f.generadas > 0);

  return (
    <div className="tarjeta">
      <header>
        <h2>{t('analisis.rendimientoReglas', 'Rendimiento de las reglas')}</h2>
        <span className="nota">
          {t('analisis.ajustarUmbrales', 'Para ajustar los umbrales con el histórico y no a ojo')}
        </span>
      </header>

      {error ? (
        <div className="aviso error" role="alert">
          {error}
        </div>
      ) : !filas ? (
        <EsqueletoTabla columnas={6} filas={5} />
      ) : conDatos.length === 0 ? (
        <SinDatos
          titulo={t('vacio.sinAlertasRegla', 'Ninguna regla generó alertas todavía')}
          detalle={t('vacio.calibracionAyuda', 'Cuando el motor empiece a generar alertas y alguien las atienda o las descarte, acá va a aparecer si cada umbral está bien puesto.')}
        />
      ) : (
        <>
          <table className="ot-table">
            <thead>
              <tr>
                <th>{t('comun.regla', 'Regla')}</th>
                <th style={{ width: 70, textAlign: 'right' }}>{t('analisis.genero', 'Generó')}</th>
                <th style={{ width: 80, textAlign: 'right' }}>{t('calib.atendidas', 'Atendidas')}</th>
                <th style={{ width: 90, textAlign: 'right' }}>{t('calib.descartadas', 'Descartadas')}</th>
                <th style={{ width: 80, textAlign: 'right' }}>{t('calib.aciertos', 'Aciertos')}</th>
                <th style={{ width: 130 }}>{t('calib.lectura', 'Lectura')}</th>
              </tr>
            </thead>
            <tbody>
              {filas.map((c) => {
                const v = veredicto(t, c);
                return (
                  <tr key={c.idRegla}>
                    <td>
                      <div>{c.regla}</div>
                      <div className="nota">
                        peso {c.peso}
                        {c.activa ? '' : ` ${t('calib.inactiva', '· inactiva')}`}
                        {c.generadas > 0
                      ? ' · ' + t('analisis.ultimaAlerta', 'última {fecha}',
                                  { fecha: fecha(c.ultimaAlerta) })
                      : ''}
                      </div>
                    </td>
                    <td className="mono" style={{ textAlign: 'right' }}>{c.generadas}</td>
                    <td className="mono" style={{ textAlign: 'right' }}>{c.atendidas}</td>
                    <td className="mono" style={{ textAlign: 'right' }}>{c.descartadas}</td>
                    <td className="mono" style={{ textAlign: 'right' }}>
                      {porcentaje(c.precision)}
                    </td>
                    <td>
                      <span className={v.clase}>{v.texto}</span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          <div className="nota" style={{ marginTop: 10 }}>
            {t('analisis.aciertosNota', '«Aciertos» son las atendidas sobre las que ya se resolvieron. Queda vacío mientras no haya ninguna resuelta: un porcentaje sobre cero se lee como cero, y no es lo mismo que una regla falle siempre a que todavía no se haya evaluado.')}
          </div>
        </>
      )}
    </div>
  );
}
