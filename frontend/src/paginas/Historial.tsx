import { useCallback, useEffect, useMemo, useState } from 'react';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type { HechoHistorialDto } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { EsqueletoTabla, SinDatos } from '../componentes/Estados';
import { IconoBuscar } from '../componentes/Iconos';
import { useCerrarSiExpiro } from '../sesion/SesionContext';

function haceDias(dias: number): string {
  const d = new Date();
  d.setDate(d.getDate() - dias);
  return d.toISOString().slice(0, 10);
}

/**
 * Historial técnico de la organización.
 *
 * No es la bitácora: son dos registros distintos. La bitácora audita quién hizo
 * qué en el sistema; esto cuenta qué le viene pasando al parque. Por eso las
 * incidencias y los mantenimientos van en una sola línea de tiempo y no en dos
 * listados: la pregunta que responde es «qué pasó con los equipos», y esa
 * pregunta no distingue de qué tabla salió el hecho.
 */
export function Historial() {
  const { fecha } = useFormato();
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [hechos, setHechos] = useState<HechoHistorialDto[] | null>(null);
  const [desde, setDesde] = useState(() => haceDias(90));
  const [hasta, setHasta] = useState(() => new Date().toISOString().slice(0, 10));
  const [tipo, setTipo] = useState('');
  const [texto, setTexto] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);

  const cargar = useCallback(() => {
    setCargando(true);
    setError(null);
    api.historial
      .consultar(desde, hasta)
      .then(setHechos)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarHistorial', 'No se pudo cargar el historial.'));
      })
      .finally(() => setCargando(false));
  }, [desde, hasta, cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  const visibles = useMemo(() => {
    if (!hechos) return [];
    const t = texto.trim().toLowerCase();

    return hechos.filter((h) => {
      if (tipo && h.tipo !== tipo) return false;
      if (!t) return true;
      return (
        h.codigoEquipo.toLowerCase().includes(t) ||
        h.titulo.toLowerCase().includes(t) ||
        (h.detalle ?? '').toLowerCase().includes(t) ||
        (h.persona ?? '').toLowerCase().includes(t)
      );
    });
  }, [hechos, tipo, texto]);

  const contexto = useMemo(() => {
    if (!hechos) return t('comun.cargando', 'Cargando…');
    const inc = hechos.filter((h) => h.tipo === 'INCIDENCIA').length;
    const man = hechos.length - inc;
    return `${hechos.length} hechos · ${inc} incidencias · ${man} mantenimientos`;
  }, [hechos]);

  return (
    <>
      <Encabezado titulo={t('nav.historial', 'Historial del parque')} contexto={contexto} />

      <div className="cuerpo pagina-historial">
        <div className="filtros">
          <div className="campo buscador">
            <IconoBuscar />
            <input
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
              placeholder={t('historial.buscarPlaceholder', 'Equipo, descripción o persona…')}
              aria-label={t('historial.buscar', 'Buscar en el historial')}
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

          <select
            className="campo"
            value={tipo}
            aria-label={t('historial.tipoHecho', 'Tipo de hecho')}
            onChange={(e) => setTipo(e.target.value)}
          >
            <option value="">{t('historial.ambos', 'Incidencias y mantenimientos')}</option>
            <option value="INCIDENCIA">{t('historial.soloIncidencias', 'Sólo incidencias')}</option>
            <option value="MANTENIMIENTO">{t('historial.soloMantenimientos', 'Sólo mantenimientos')}</option>
          </select>
        </div>

        {error ? (
          <div className="aviso error" role="alert">
            {error}
          </div>
        ) : null}

        {cargando && !hechos ? (
          <EsqueletoTabla columnas={5} />
        ) : visibles.length === 0 ? (
          texto.trim() || tipo ? (
            <SinDatos
              filtrado
              titulo={t('vacio.hechoSinCoincidencia', 'Ningún hecho coincide con los filtros')}
              detalle={t('vacio.hechoFiltroAyuda', 'Probá con otro texto o quitando el filtro por tipo.')}
            />
          ) : (
            <SinDatos
              titulo={t('vacio.sinHechos', 'No pasó nada en el período')}
              detalle={t('vacio.sinHechosAyuda', 'Ni incidencias ni mantenimientos. Si esperabas ver algo, probá ampliando las fechas.')}
            />
          )
        ) : (
          <div className="tarjeta tabla-desplazable">
            <table className="ot-table">
              <thead>
                <tr>
                  <th style={{ width: 80 }}>{t('comun.fecha', 'Fecha')}</th>
                  <th style={{ width: 130 }}>{t('comun.tipo', 'Tipo')}</th>
                  <th style={{ width: 130 }}>{t('incidencia.equipo', 'Equipo')}</th>
                  <th>{t('comun.quePaso', 'Qué pasó')}</th>
                  <th style={{ width: 150 }}>{t('comun.estado', 'Estado')}</th>
                  <th style={{ width: 140 }}>{t('activo.responsable', 'Responsable')}</th>
                </tr>
              </thead>
              <tbody>
                {visibles.map((h, i) => (
                  <tr key={`${h.fecha}-${h.codigoEquipo}-${i}`}>
                    <td style={{ whiteSpace: 'nowrap' }}>{fecha(h.fecha)}</td>
                    <td>
                      <span className="chip">
                        {h.tipo === 'INCIDENCIA' ? 'Incidencia' : 'Mantenimiento'}
                      </span>
                    </td>
                    <td className="mono">{h.codigoEquipo}</td>
                    <td>
                      {h.titulo}
                      {h.detalle ? (
                        <div className="sub" style={{ marginTop: 2 }}>
                          {h.detalle}
                        </div>
                      ) : null}
                    </td>
                    <td>{h.estado ?? '—'}</td>
                    <td>{h.persona ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </>
  );
}
