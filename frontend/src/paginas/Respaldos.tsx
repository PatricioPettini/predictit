import { useCallback, useEffect, useState } from 'react';
import { usePlural } from '../idioma/plural';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type { PanelRespaldosDto } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { ConfigNav } from './Organizacion';
import { useCerrarSiExpiro } from '../sesion/SesionContext';

function tamano(bytes?: number): string {
  if (bytes == null) return '—';
  const mb = bytes / 1024 / 1024;
  return mb < 1024 ? `${mb.toFixed(1)} MB` : `${(mb / 1024).toFixed(2)} GB`;
}

/** Días desde el último respaldo correcto, o null si nunca hubo uno. */
function diasDesde(iso?: string | null): number | null {
  if (!iso) return null;
  // Con piso en cero: un respaldo de hace un minuto no puede mostrar «hace −1
  // días» por un desfasaje de reloj de unos segundos.
  return Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 86400000));
}

export function Respaldos() {
  const p = usePlural();
  const { fechaHoraSegundos: fechaHoraDe } = useFormato();
  const fechaHora = (iso?: string | null) =>
    (iso ? fechaHoraDe(iso) : t('comun.nunca', 'nunca'));
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [panel, setPanel] = useState<PanelRespaldosDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState<string | null>(null);

  const cargar = useCallback(() => {
    setError(null);
    api.respaldos
      .panel()
      .then(setPanel)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarPanel', 'No se pudo cargar el panel.'));
      });
  }, [cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  function respaldar(base: string) {
    setOcupado(base);
    setError(null);
    setAviso(null);

    api.respaldos
      .respaldar(base)
      .then((r) => {
        // El resultado dice si salió bien: el endpoint responde 200 aunque el
        // respaldo haya fallado, porque el intento sí se registró.
        setAviso(
          r.ok
            ? t('respaldo.hecho', 'Respaldo de {base} hecho: {archivo} ({tamano}).',
                { base: r.base, archivo: r.nombreArchivo, tamano: tamano(r.tamanoBytes) })
            : null,
        );
        if (!r.ok) {
          setError(t('respaldo.fallo', 'El respaldo de {base} falló: {detalle}',
                     { base: r.base, detalle: r.detalle ?? '' }));
        }
        cargar();
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.respaldar', 'No se pudo hacer el respaldo.'));
      })
      .finally(() => setOcupado(null));
  }

  return (
    <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
      <ConfigNav />
      <div className="principal">
        <Encabezado
          titulo={t('respaldo.titulo', 'Respaldos')}
          contexto={
            panel
              ? `${panel.historial.length} respaldos registrados · ${panel.bases.length} bases`
              : t('comun.cargando', 'Cargando…')
          }
        />

        <div className="cuerpo">
          {error ? (
            <div className="aviso error" role="alert" style={{ marginBottom: 12 }}>
              {error}
            </div>
          ) : null}

          {aviso ? (
            <div className="aviso info" style={{ marginBottom: 12 }}>
              {aviso}
            </div>
          ) : null}

          {!panel ? (
            <div className="tarjeta">
              <div className="vacio">{t('dash.cargando', 'Cargando el panel…')}</div>
            </div>
          ) : (
            <div className="dos-columnas">
              <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <div className="tarjeta">
                  <header>
                    <h2>{t('respaldo.estadoPorBase', 'Estado por base')}</h2>
                  </header>

                  {panel.bases.map((base) => {
                    const ultimo = panel.ultimoOk[base] ?? null;
                    const dias = diasDesde(ultimo);
                    // Siete días es el criterio: el respaldo diario es lo
                    // esperado, y una semana sin uno correcto es un problema.
                    const alarma = dias === null || dias > 7;

                    return (
                      <div
                        key={base}
                        style={{
                          padding: '12px 16px',
                          borderBottom: '1px solid var(--linea-suave)',
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          gap: 12,
                        }}
                      >
                        <div style={{ minWidth: 0 }}>
                          <div className="mono" style={{ fontSize: 'var(--txt-tabla)', color: 'var(--tx)' }}>
                            {base}
                          </div>
                          <div className="sub" style={{ marginTop: 3 }}>
                            {t('respaldo.ultimoCorrecto', 'Último respaldo correcto: {fecha}',
                               { fecha: fechaHora(ultimo) })}
                            {dias !== null
                              ? ' · ' + p(dias, 'respaldo.haceDia', 'hace 1 día', 'hace {n} días')
                              : ''}
                          </div>
                        </div>

                        <div
                          style={{ display: 'flex', alignItems: 'center', gap: 10, flexShrink: 0 }}
                        >
                          <span className={alarma ? 'chip alto' : 'chip bajo'}>
                            {dias === null
              ? t('respaldo.sinRespaldo', 'Sin respaldo')
              : alarma
                ? t('respaldo.atrasado', 'Atrasado')
                : t('respaldo.alDia', 'Al día')}
                          </span>
                          <button
                            type="button"
                            className="btn pri"
                            disabled={ocupado !== null}
                            onClick={() => respaldar(base)}
                          >
                            {ocupado === base
                              ? t('respaldo.respaldando', 'Respaldando…')
                              : t('respaldo.respaldarAhora', 'Respaldar ahora')}
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>

                <div className="tarjeta">
                  <header>
                    <h2>{t('respaldo.historial', 'Historial')}</h2>
                    <span className="nota">
                      {panel.historial.filter((r) => !r.ok).length > 0
                        ? `${panel.historial.filter((r) => !r.ok).length} con error`
                        : t('respaldo.sinErrores', 'sin errores')}
                    </span>
                  </header>

                  <table>
                    <thead>
                      <tr>
                        <th style={{ width: 125 }}>{t('comun.fecha', 'Fecha')}</th>
                        <th style={{ width: 155 }}>{t('respaldos.base', 'Base')}</th>
                        <th>{t('respaldo.archivo', 'Archivo')}</th>
                        <th style={{ width: 90 }}>{t('respaldo.tamano', 'Tamaño')}</th>
                        <th style={{ width: 95 }}>{t('comun.tipo', 'Tipo')}</th>
                        <th style={{ width: 130 }}>{t('respaldo.quien', 'Quién')}</th>
                        <th style={{ width: 90 }}>{t('mant.resultado', 'Resultado')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {panel.historial.map((r) => (
                        <tr key={r.id}>
                          <td className="sub" style={{ whiteSpace: 'nowrap' }}>
                            {fechaHora(r.fecha)}
                          </td>
                          <td className="mono" style={{ fontSize: 'var(--txt-tabla)' }}>
                            {r.base}
                          </td>
                          <td className="mono" style={{ fontSize: 'var(--txt-tabla)' }}>
                            {r.nombreArchivo}
                            {r.detalle && !r.ok ? (
                              <div className="sub" style={{ color: 'var(--alto-tx)' }}>
                                {r.detalle}
                              </div>
                            ) : null}
                          </td>
                          <td className="mono">{tamano(r.tamanoBytes)}</td>
                          <td>
                            <span className="chip">
                              {r.tipo === 'MANUAL'
                                ? t('comun.manual', 'Manual')
                                : t('comun.automatico', 'Automático')}
                            </span>
                          </td>
                          <td className="sub">{r.usuario ?? '—'}</td>
                          <td>
                            <span className={r.ok ? 'chip bajo' : 'chip alto'}>
                              {r.ok ? 'OK' : t('bitacora.error', 'Error')}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>

                  {panel.historial.length === 0 ? (
                    <div className="vacio">
                      {t('respaldo.sinRespaldos', 'Todavía no se hizo ningún respaldo. Usá «Respaldar ahora» en cada base.')}
                    </div>
                  ) : null}
                </div>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <div className="tarjeta">
                  <header>
                    <h2>{t('respaldo.porQueImporta', 'Por qué importa acá')}</h2>
                  </header>
                  <div style={{ padding: 14 }} className="sub">
                    {t('respaldo.historialNota', 'El valor del análisis predictivo depende del historial técnico acumulado por cada equipo. Perder ese historial no es perder registros administrativos: es perder la capacidad de anticipar fallas, y se recupera sólo con meses de operación.')}
                  </div>
                </div>

                <div className="tarjeta">
                  <header>
                    <h2>{t('respaldo.comoSeHace', 'Cómo se hace')}</h2>
                  </header>
                  <div style={{ padding: 14 }} className="sub">
                    {t('respaldo.seUsa', 'Se usa')} <span className="mono">BACKUP DATABASE</span> de SQL Server, no una
                    exportación propia: es el mecanismo que garantiza consistencia transaccional
                    del punto en el tiempo. Cada respaldo se escribe con verificación de páginas
                    y después se lee con{' '}
                    <span className="mono">RESTORE VERIFYONLY</span>{t('respaldo.confianzaNota', ', porque un respaldo que no se puede leer es peor que no tener respaldo: da confianza.')}
                  </div>
                </div>

                <div className="tarjeta">
                  <header>
                    <h2>{t('respaldos.restaurar', 'Restaurar')}</h2>
                  </header>
                  <div style={{ padding: 14 }} className="sub">
                    {t('respaldo.restauracionNota', 'La restauración no está en esta pantalla a propósito. Es una operación destructiva que reemplaza los datos actuales y corta todas las sesiones abiertas, y ponerla detrás de un botón web es pedir un accidente. El procedimiento está en el manual de administración.')}
                    <div style={{ marginTop: 10 }}>
                      {t('respaldo.probarNota', 'Un respaldo que no se probó restaurar no es un respaldo: conviene verificarlo cada tanto sobre una base de prueba, nunca sobre la de producción.')}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
