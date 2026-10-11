import { Fragment, useCallback, useEffect, useMemo, useState } from 'react';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type { ErrorDto } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { EsqueletoTabla, SinDatos } from '../componentes/Estados';
import { IconoBuscar } from '../componentes/Iconos';
import { ConfigNav } from './Organizacion';
import { useCerrarSiExpiro } from '../sesion/SesionContext';

function haceDias(dias: number): string {
  const d = new Date();
  d.setDate(d.getDate() - dias);
  return d.toISOString().slice(0, 10);
}

/**
 * La primera línea de la traza: el tipo de excepción y su mensaje. Alcanza para
 * saber si dos errores son el mismo sin desplegar el volcado completo.
 */
function resumenDeTraza(traza?: string): string | null {
  if (!traza) return null;
  const primera = traza.split('\n')[0]?.trim();
  return primera ? primera : null;
}

/**
 * Errores del sistema (CU.Arq.007).
 *
 * Sale de la misma bitácora que la pantalla de auditoría, filtrada por
 * criticidad. La diferencia es que acá se muestra la traza, y por eso pide su
 * propia patente: un volcado de pila nombra tablas, rutas y versiones, que
 * sirven tanto para diagnosticar como para atacar.
 *
 * No hay «resolver» ni «marcar como visto». La bitácora es inmutable, y un
 * estado sobre una fila inmutable habría que guardarlo en otra tabla, con lo
 * cual ya no sería el mismo registro. Lo que hay es la ventana de fechas: por
 * defecto siete días, porque cuando se abre esta pantalla lo que pasó es
 * reciente.
 */
export function Errores() {
  const { fechaHoraSegundos: fechaHora } = useFormato();
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [errores, setErrores] = useState<ErrorDto[] | null>(null);
  const [desde, setDesde] = useState(() => haceDias(7));
  const [hasta, setHasta] = useState(() => new Date().toISOString().slice(0, 10));
  const [advertencias, setAdvertencias] = useState(false);
  const [texto, setTexto] = useState('');
  const [abierto, setAbierto] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);

  const cargar = useCallback(() => {
    setCargando(true);
    setError(null);
    api.errores
      .consultar({ desde, hasta, advertencias })
      .then(setErrores)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarErrores', 'No se pudieron cargar los errores.'));
      })
      .finally(() => setCargando(false));
  }, [desde, hasta, advertencias, cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  // El texto filtra en memoria: el rango de fechas ya acotó el conjunto.
  const visibles = useMemo(() => {
    if (!errores) return [];
    const t = texto.trim().toLowerCase();
    if (!t) return errores;
    return errores.filter(
      (e) =>
        e.descripcion.toLowerCase().includes(t) ||
        (e.traza ?? '').toLowerCase().includes(t) ||
        (e.usuario ?? '').toLowerCase().includes(t),
    );
  }, [errores, texto]);

  const contexto = useMemo(() => {
    if (!errores) return t('comun.cargando', 'Cargando…');
    if (errores.length === 0) return t('error.sinErroresPeriodo', 'Sin errores en el período');
    const graves = errores.filter((e) => e.criticidad !== 'ADVERTENCIA').length;
    return `${errores.length} ${errores.length === 1 ? 'registro' : 'registros'} · ${graves} ${
      graves === 1 ? 'error' : 'errores'
    }`;
  }, [errores]);

  return (
    <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
      <ConfigNav />
      <div className="principal">
        <Encabezado titulo={t('config.errores', 'Errores del sistema')} contexto={contexto} />

        <div className="cuerpo">
          <div className="aviso info" style={{ marginBottom: 12 }}>
            {t('error.origenNota', 'Estas entradas salen de la misma bitácora inmutable que la pantalla de auditoría, filtradas por criticidad. Lo que se agrega acá es la traza técnica, que se muestra sólo a quien tiene la patente para verla.')}
          </div>

          <div className="filtros">
            <div className="campo buscador">
              <IconoBuscar />
              <input
                value={texto}
                onChange={(e) => setTexto(e.target.value)}
                placeholder="Mensaje, traza o usuario…"
                aria-label={t('error.buscar', 'Buscar en los errores')}
              />
            </div>

            <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span className="sub">{t('bitacora.desde', 'Desde')}</span>
              <input
                type="date"
                className="campo"
                value={desde}
                max={hasta}
                onChange={(e) => setDesde(e.target.value)}
              />
            </label>

            <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span className="sub">{t('bitacora.hasta', 'Hasta')}</span>
              <input
                type="date"
                className="campo"
                value={hasta}
                min={desde}
                onChange={(e) => setHasta(e.target.value)}
              />
            </label>

            <button
              type="button"
              className={advertencias ? 'btn pri' : 'btn'}
              onClick={() => setAdvertencias((v) => !v)}
            >
              {t('errores.incluirAdvertencias', 'Incluir advertencias')}
            </button>
          </div>

          {error ? (
            <div className="aviso error" role="alert">
              {error}
            </div>
          ) : null}

          {cargando && !errores ? (
            <EsqueletoTabla columnas={4} filas={5} />
          ) : visibles.length === 0 ? (
            texto.trim() ? (
              <SinDatos
                filtrado
                titulo={t('vacio.errorSinCoincidencia', 'Ningún error coincide con la búsqueda')}
                detalle={t('vacio.errorFiltroAyuda', 'Probá con otro texto.')}
              />
            ) : (
              <SinDatos
                titulo={t('vacio.sinErrores', 'No se registró ningún error')}
                detalle={t('vacio.sinErroresAyuda', 'Es el resultado esperado. Esta pantalla existe para el día en que no lo sea.')}
              />
            )
          ) : (
            <div className="tarjeta" style={{ overflow: 'hidden' }}>
              <table className="ot-table">
                <thead>
                  <tr>
                    <th style={{ width: 130 }}>{t('comun.fecha', 'Fecha')}</th>
                    <th style={{ width: 110 }}>{t('activo.criticidad', 'Criticidad')}</th>
                    <th>{t('comun.quePaso', 'Qué pasó')}</th>
                    <th style={{ width: 140 }}>{t('login.usuario', 'Usuario')}</th>
                    <th style={{ width: 100 }}>{t('comun.detalle', 'Detalle')}</th>
                  </tr>
                </thead>
                <tbody>
                  {visibles.map((e) => {
                    const resumen = resumenDeTraza(e.traza);
                    const desplegado = abierto === e.id;

                    return (
                      <Fragment key={e.id}>
                        <tr>
                          <td style={{ whiteSpace: 'nowrap' }}>{fechaHora(e.fecha)}</td>
                          <td>
                            <span
                              className={
                                e.criticidad === 'ADVERTENCIA' ? 'chip medio' : 'chip alto'
                              }
                            >
                              {e.criticidad}
                            </span>
                          </td>
                          <td>
                            {e.descripcion}
                            {resumen ? (
                              <div className="sub" style={{ marginTop: 2 }}>
                                {resumen}
                              </div>
                            ) : null}
                          </td>
                          <td>{e.usuario ?? '—'}</td>
                          <td>
                            {e.traza ? (
                              <button
                                type="button"
                                className="btn"
                                aria-expanded={desplegado}
                                onClick={() => setAbierto(desplegado ? null : e.id)}
                              >
                                {desplegado ? t('comun.ocultar', 'Ocultar') : t('errores.verTraza', 'Ver traza')}
                              </button>
                            ) : (
                              <span className="sub">{t('error.sinTraza', 'Sin traza')}</span>
                            )}
                          </td>
                        </tr>

                        {desplegado && e.traza ? (
                          <tr>
                            <td colSpan={5} style={{ background: 'var(--neutro-bg)' }}>
                              <pre
                                style={{
                                  margin: 0,
                                  padding: 12,
                                  fontSize: 'var(--txt-tabla)',
                                  lineHeight: 1.5,
                                  overflowX: 'auto',
                                  whiteSpace: 'pre',
                                  color: 'var(--tx2)',
                                }}
                              >
                                {e.traza}
                              </pre>
                              {e.ip || e.entidad ? (
                                <div className="sub" style={{ padding: '0 12px 10px' }}>
                                  {[e.entidad, e.ip].filter(Boolean).join(' · ')}
                                </div>
                              ) : null}
                            </td>
                          </tr>
                        ) : null}
                      </Fragment>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
