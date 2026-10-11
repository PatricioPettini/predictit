import { useCallback, useEffect, useState } from 'react';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type { ConfiguracionIaDto, PanelIaDto } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { ConfigNav } from './Organizacion';
import { useCerrarSiExpiro } from '../sesion/SesionContext';

/** Interruptor de un dato que se envía al proveedor. */
function Dato({
  titulo,
  detalle,
  valor,
  onCambiar,
}: {
  titulo: string;
  detalle: string;
  valor: boolean;
  onCambiar: (v: boolean) => void;
}) {
  return (
    <label
      style={{
        display: 'flex',
        gap: 10,
        alignItems: 'flex-start',
        padding: '10px 14px',
        borderBottom: '1px solid var(--linea-suave)',
        cursor: 'pointer',
      }}
    >
      <input
        type="checkbox"
        checked={valor}
        onChange={(e) => onCambiar(e.target.checked)}
        style={{ marginTop: 3 }}
      />
      <span>
        <span style={{ fontSize: 'var(--txt-tabla)' }}>{titulo}</span>
        <span className="sub" style={{ display: 'block', marginTop: 2 }}>
          {detalle}
        </span>
      </span>
    </label>
  );
}

/**
 * Carga de la clave de API del proveedor (CU.Arq.006).
 *
 * La clave entra y no sale: se guarda cifrada con AES-256-GCM y no hay endpoint
 * que la devuelva. Lo que se muestra es de dónde sale la que está en uso, que es
 * lo que hace falta saber para operar —una clave del entorno la controla quien
 * administra el servidor, una de la base la controla el cliente—.
 */
function ClaveDeApi({ origen, alCambiar }: { origen: string; alCambiar: () => void }) {
  const t = useT();
  const [valor, setValor] = useState('');
  const [guardando, setGuardando] = useState(false);
  const [aviso, setAviso] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  function guardar() {
    setGuardando(true);
    setError(null);
    setAviso(null);
    api.configuracionIa
      .guardarClave(valor)
      .then(() => {
        // Se limpia el campo apenas se guarda: dejar la clave escrita en
        // pantalla la deja a la vista de quien pase por el escritorio.
        setValor('');
        setAviso(t('aviso.claveGuardada', 'Clave guardada y cifrada.'));
        alCambiar();
      })
      .catch((ex) =>
        setError(ex instanceof ErrorApi ? ex.message : t('error.guardarClave', 'No se pudo guardar la clave.')),
      )
      .finally(() => setGuardando(false));
  }

  function borrar() {
    setGuardando(true);
    setError(null);
    setAviso(null);
    api.configuracionIa
      .borrarClave()
      .then(() => {
        setAviso(t('aviso.claveBorrada', 'Clave borrada.'));
        alCambiar();
      })
      .catch((ex) =>
        setError(ex instanceof ErrorApi ? ex.message : t('error.borrarClave', 'No se pudo borrar la clave.')),
      )
      .finally(() => setGuardando(false));
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      {origen === 'BASE' ? (
        <div className="aviso info">
          {t('ia.claveGuardadaNota', 'Hay una clave de API guardada y cifrada para esta organización. No se muestra ni puede recuperarse: para cambiarla hay que cargar una nueva.')}
        </div>
      ) : origen === 'ENTORNO' ? (
        <div className="aviso info">
          {t('ia.claveDelEntornoNota', 'Se está usando la clave del entorno del servidor. Si cargás una acá, esa pasa a tener prioridad y queda bajo el control de esta organización.')}
        </div>
      ) : (
        <div className="aviso atencion">
          {t('ia.sinClaveNota', 'No hay clave de API. El sistema va a seguir asignando con las reglas internas y lo va a dejar registrado en la bitácora.')}
        </div>
      )}

      <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
        <span className="sub">
          {origen === 'BASE'
            ? t('ia.reemplazarClave', 'Reemplazar la clave')
            : t('ia.cargarClave', 'Cargar una clave')}
        </span>
        <input
          className="campo"
          type="password"
          value={valor}
          autoComplete="off"
          placeholder="sk-ant-…"
          onChange={(e) => setValor(e.target.value)}
        />
      </label>

      <div style={{ display: 'flex', gap: 8 }}>
        <button
          type="button"
          className="btn pri"
          disabled={guardando || valor.trim().length < 20}
          onClick={guardar}
        >
          {t('ia.guardarCifrada', 'Guardar cifrada')}
        </button>

        {origen === 'BASE' ? (
          <button type="button" className="btn" disabled={guardando} onClick={borrar}>
            {t('ia.borrarClave', 'Borrar la clave guardada')}
          </button>
        ) : null}
      </div>

      {aviso ? <div className="aviso ok">{aviso}</div> : null}
      {error ? (
        <div className="aviso error" role="alert">
          {error}
        </div>
      ) : null}
    </div>
  );
}

export function IntegracionIa() {
  const { fechaHoraSegundos: fechaHoraDe } = useFormato();
  const fechaHora = (iso?: string | null) =>
    (iso ? fechaHoraDe(iso) : t('comun.nunca', 'nunca'));
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [panel, setPanel] = useState<PanelIaDto | null>(null);
  const [config, setConfig] = useState<ConfiguracionIaDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);

  const cargar = useCallback(() => {
    setError(null);
    api.configuracionIa
      .panel()
      .then((p) => {
        setPanel(p);
        setConfig(p.configuracion);
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarConfiguracion', 'No se pudo cargar la configuración.'));
      });
  }, [cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  function guardar() {
    if (!config) return;
    setGuardando(true);
    setError(null);
    setAviso(null);

    api.configuracionIa
      .guardar(config)
      .then(() => {
        setAviso(t('aviso.configuracionGuardada', 'Configuración guardada.'));
        cargar();
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.guardar', 'No se pudo guardar.'));
      })
      .finally(() => setGuardando(false));
  }

  const total = panel
    ? panel.asignacionesPorIa + panel.asignacionesPorHeuristica + panel.asignacionesPorRespaldo
    : 0;

  const abierto = panel?.estado.estadoCircuito !== 'CERRADO';

  return (
    <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
      <ConfigNav />
      <div className="principal">
        <Encabezado
          titulo={t('ia.titulo', 'Integración de IA')}
          contexto={panel
            ? t('ia.proveedorEs', 'Proveedor {proveedor}', { proveedor: panel.estado.proveedor })
            : t('comun.cargando', 'Cargando…')}
          acciones={
            config ? (
              <button type="button" className="btn pri" onClick={guardar} disabled={guardando}>
                {guardando ? t('comun.guardando', 'Guardando…') : t('comun.guardarCambios', 'Guardar cambios')}
              </button>
            ) : null
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

          {/* El estado degradado se muestra siempre que no sea normal (ADR 0009). */}
          {panel && abierto ? (
            <div className="aviso atencion" style={{ marginBottom: 12 }}>
              <b>{panel.estado.proveedor}:</b> {panel.estado.mensaje}
            </div>
          ) : null}

          {!config || !panel ? (
            <div className="tarjeta">
              <div className="vacio">{t('ia.cargando', 'Cargando la configuración…')}</div>
            </div>
          ) : (
            <div className="dos-columnas">
              <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <div className="tarjeta">
                  <header>
                    <h2>{t('ia.proveedor', 'Proveedor')}</h2>
                  </header>

                  <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 14 }}>
                    <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
                      <span className="sub">{t('ia.quienDecide', 'Quién decide la asignación')}</span>
                      <select
                        className="campo"
                        value={config.proveedorIa}
                        onChange={(e) => setConfig({ ...config, proveedorIa: e.target.value })}
                      >
                        <option value="SIMULADO">
                          {t('ia.reglasInternasOpcion', 'Reglas internas — determinístico, sin salir a internet')}
                        </option>
                        <option value="CLAUDE">{t('ia.claude', 'Claude — API de Anthropic')}</option>
                      </select>
                    </label>

                    {config.proveedorIa === 'CLAUDE' ? (
                      <>
                        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
                          <span className="sub">{t('comun.modelo', 'Modelo')}</span>
                          <input
                            className="campo"
                            value={config.modelo ?? ''}
                            placeholder="claude-haiku-4-5"
                            onChange={(e) => setConfig({ ...config, modelo: e.target.value })}
                          />
                        </label>

                        <ClaveDeApi
                          origen={panel.origenDeLaClave}
                          alCambiar={cargar}
                        />
                      </>
                    ) : null}

                    <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
                      <span className="sub">
                        {t('ia.tiempoDeEspera',
                          'Tiempo de espera: {segundos} s antes de darlo por no respondido',
                          { segundos: config.timeoutSegundos })}
                      </span>
                      <input
                        type="range"
                        min={1}
                        max={120}
                        value={config.timeoutSegundos}
                        onChange={(e) =>
                          setConfig({ ...config, timeoutSegundos: Number(e.target.value) })
                        }
                      />
                    </label>
                  </div>
                </div>

                <div className="tarjeta">
                  <header>
                    <h2>{t('ia.queDatos', 'Qué datos se le envían')}</h2>
                    <span className="nota">{t('ia.loDecideTuOrganizacion', 'Lo decide tu organización')}</span>
                  </header>

                  <Dato
                    titulo={t('ia.contextoCarga', 'Carga de trabajo de cada técnico')}
                    detalle={t('ia.contextoCargaAyuda', 'Cuántas incidencias abiertas tiene. Permite repartir el trabajo.')}
                    valor={config.enviarCarga}
                    onCambiar={(v) => setConfig({ ...config, enviarCarga: v })}
                  />
                  <Dato
                    titulo={t('ia.contextoEspecialidad', 'Especialidades y nivel')}
                    detalle={t('ia.contextoHistorialAyuda', 'Es el dato central: sin él la asignación sólo puede mirar la carga y la calidad cae mucho.')}
                    valor={config.enviarEspecialidad}
                    onCambiar={(v) => setConfig({ ...config, enviarEspecialidad: v })}
                  />
                  <Dato
                    titulo={t('ia.contextoHistorial', 'Historial sobre el equipo')}
                    detalle={t('ia.contextoEspecialidadAyuda', 'Cuántos casos de ese mismo equipo ya resolvió cada técnico.')}
                    valor={config.enviarHistorial}
                    onCambiar={(v) => setConfig({ ...config, enviarHistorial: v })}
                  />
                  <Dato
                    titulo={t('ia.contextoDisponibilidad', 'Disponibilidad horaria')}
                    detalle={t('ia.contextoDisponibilidadAyuda', 'Todavía no se registra en el sistema, así que activarlo no cambia nada por ahora.')}
                    valor={config.enviarDisponibilidad}
                    onCambiar={(v) => setConfig({ ...config, enviarDisponibilidad: v })}
                  />

                  <div style={{ padding: 14 }} className="sub">
                    {t('ia.datosNoViajanNota', 'Lo que se desactiva no viaja. Son datos de tu gente y la decisión es tuya.')}
                  </div>
                </div>

                <div className="tarjeta">
                  <header>
                    <h2>{t('ia.cuandoNoResponde', 'Cuando el proveedor no responde')}</h2>
                  </header>

                  <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 12 }}>
                    <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
                      <span className="sub">{t('ia.estrategiaRespaldo', 'Estrategia de respaldo')}</span>
                      <select
                        className="campo"
                        value={config.estrategiaRespaldo}
                        onChange={(e) =>
                          setConfig({ ...config, estrategiaRespaldo: e.target.value })
                        }
                      >
                        <option value="MENOR_CARGA">
                          {t('ia.respaldoMenosCarga', 'Asignar al técnico con menos incidencias abiertas')}
                        </option>
                        <option value="SIN_ASIGNAR">{t('ia.dejarSinAsignar', 'Dejar la incidencia sin asignar')}</option>
                      </select>
                    </label>

                    <div className="sub">
                      {t('ia.respaldoRevisionNota', 'En los dos casos la incidencia queda marcada para revisión: el respaldo asigna por carga, sin mirar la especialidad, así que alguien tiene que confirmar que el técnico sea el adecuado.')}
                    </div>
                  </div>
                </div>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <div className="tarjeta">
                  <header>
                    <h2>{t('ia.estadoProveedor', 'Estado del proveedor')}</h2>
                  </header>
                  <dl className="dl">
                    <dt>{t('ia.circuito', 'Circuito')}</dt>
                    <dd>
                      <span className={abierto ? 'chip medio' : 'chip bajo'}>
                        {panel.estado.estadoCircuito === 'CERRADO'
                          ? t('ia.respondiendo', 'Respondiendo')
                          : panel.estado.estadoCircuito === 'ABIERTO'
                            ? t('ia.sinRespuesta', 'Sin respuesta')
                            : t('comun.probando', 'Probando…')}
                      </span>
                    </dd>
                    <dt>{t('ia.fallosSeguidos', 'Fallos seguidos')}</dt>
                    <dd className="mono">{panel.estado.fallosConsecutivos}</dd>
                    <dt>{t('ia.ultimaConsultaOk', 'Última consulta correcta')}</dt>
                    <dd>{fechaHora(panel.estado.ultimaConsultaOk)}</dd>
                  </dl>

                  {panel.estado.ultimoError ? (
                    <div style={{ padding: '0 14px 14px' }}>
                      <div className="sub" style={{ marginBottom: 4 }}>
                        {t('ia.ultimoError', 'Último error')}
                      </div>
                      <div style={{ fontSize: 'var(--txt-tabla)' }}>{panel.estado.ultimoError}</div>
                    </div>
                  ) : null}

                  <div style={{ padding: '0 14px 14px' }} className="sub">
                    {t('ia.disyuntorNota', 'Tras cinco fallos seguidos el circuito se abre y el sistema deja de llamar al proveedor durante sesenta segundos, para no pagar la espera en cada incidencia.')}
                  </div>
                </div>

                <div className="tarjeta">
                  <header>
                    <h2>{t('ia.quienDecidio', 'Quién decidió las asignaciones')}</h2>
                    <span className="nota">{t('ia.enTotal', '{total} en total', { total })}</span>
                  </header>

                  {total === 0 ? (
                    <div className="vacio">{t('ia.sinAsignaciones', 'Todavía no hay asignaciones registradas.')}</div>
                  ) : (
                    <>
                      {[
                        [t('ia.porIa', 'Proveedor de IA'), panel.asignacionesPorIa, 'var(--ia-tx)'],
                        ['Reglas internas', panel.asignacionesPorHeuristica, 'var(--acento)'],
                        [t('ia.porRespaldo', 'Regla de respaldo'),
                     panel.asignacionesPorRespaldo, 'var(--medio-barra)'],
                      ].map(([etiqueta, cantidad, color]) => (
                        <div
                          key={etiqueta as string}
                          style={{
                            padding: '10px 14px',
                            borderBottom: '1px solid var(--linea-suave)',
                          }}
                        >
                          <div
                            style={{
                              display: 'flex',
                              justifyContent: 'space-between',
                              alignItems: 'baseline',
                            }}
                          >
                            <span style={{ fontSize: 'var(--txt-tabla)', color: 'var(--tx2)' }}>{etiqueta}</span>
                            <span className="mono" style={{ fontWeight: 600 }}>
                              {cantidad as number}
                            </span>
                          </div>
                          <div
                            style={{
                              height: 5,
                              marginTop: 6,
                              borderRadius: 3,
                              background: 'var(--linea-suave)',
                              overflow: 'hidden',
                            }}
                          >
                            <div
                              style={{
                                width: `${((cantidad as number) / total) * 100}%`,
                                height: '100%',
                                background: color as string,
                              }}
                            />
                          </div>
                        </div>
                      ))}

                      <div style={{ padding: 14 }} className="sub">
                        {t('ia.reglasNoEsIaNota', '«Reglas internas» no es lo mismo que «proveedor de IA»: el proveedor determinístico resuelve por coincidencia de términos y puntaje, no con un modelo. Registrarlos como lo mismo falsearía de dónde salió cada decisión.')}
                      </div>
                    </>
                  )}
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
