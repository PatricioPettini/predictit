import { useCallback, useEffect, useState } from 'react';
import { useHace } from '../idioma/hace';
import { usePlural } from '../idioma/plural';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { Link } from 'react-router-dom';
import { api, ErrorApi } from '../api/cliente';
import type { AlertaDto, EstadoIaDto, PanelPredictivoDto } from '../api/tipos';
import { Patentes } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { NivelRiesgo } from '../componentes/NivelRiesgo';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';

/** Los cortes son los mismos que define el motor. Si cambian allá, cambian acá. */
const CORTE_MEDIO = 40;
const CORTE_ALTO = 70;

/** Una fila de la distribución del parque por nivel de riesgo. */
function FranjaRiesgo({
  etiqueta,
  rango,
  cantidad,
  total,
  color,
}: {
  etiqueta: string;
  rango: string;
  cantidad: number;
  total: number;
  color: string;
}) {
  const t = useT();

  // Con el parque vacío no se puede calcular un porcentaje. Mostrar 0 % sería
  // afirmar algo sobre datos que no existen.
  const porcentaje = total === 0 ? null : (cantidad / total) * 100;

  return (
    <div style={{ padding: '10px 16px', borderBottom: '1px solid var(--linea-suave)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
        <span style={{ fontSize: 'var(--txt-tabla)', color: 'var(--tx2)' }}>
          <span
            className="punto"
            style={{ background: color, display: 'inline-block', marginRight: 7 }}
          />
          {etiqueta} <span className="sub">· {rango}</span>
        </span>
        <span>
          <span className="mono" style={{ fontSize: 'var(--txt-seccion)', fontWeight: 600, color: 'var(--tx)' }}>
            {cantidad}
          </span>{' '}
          <span className="sub">
            {cantidad === 1 ? t('comun.equipo', 'equipo') : t('comun.equipos', 'equipos')}
            {porcentaje === null ? '' : ` · ${porcentaje.toFixed(1).replace('.', ',')} %`}
          </span>
        </span>
      </div>

      <div
        style={{
          height: 5,
          marginTop: 7,
          borderRadius: 3,
          background: 'var(--linea-suave)',
          overflow: 'hidden',
        }}
      >
        <div
          style={{
            width: `${porcentaje ?? 0}%`,
            height: '100%',
            background: color,
          }}
        />
      </div>
    </div>
  );
}

function TarjetaAlerta({
  alerta,
  onAtender,
  ocupado,
  puedeAtender,
}: {
  alerta: AlertaDto;
  onAtender: (id: string, descartar: boolean) => void;
  ocupado: boolean;
  puedeAtender: boolean;
}) {
  const t = useT();
  const hace = useHace();
  const nivel = alerta.nivelRiesgo >= CORTE_ALTO ? 'ALTO' : alerta.nivelRiesgo >= CORTE_MEDIO ? 'MEDIO' : 'BAJO';

  return (
    <div style={{ padding: 14, borderBottom: '1px solid var(--linea-suave)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 10 }}>
        <div style={{ minWidth: 0 }}>
          <div>
            <Link to={`/activos/${alerta.idEquipo}`} className="mono">
              {alerta.codigoEquipo}
            </Link>{' '}
            <span className="sub">· {alerta.regla}</span>
          </div>
          <div style={{ marginTop: 4, fontSize: 'var(--txt-tabla)' }}>{alerta.motivo}</div>
          {alerta.recomendacion ? (
            <div className="sub" style={{ marginTop: 4 }}>
              -&gt; {alerta.recomendacion}
            </div>
          ) : null}
        </div>
        <span
          className={nivel === 'ALTO' ? 'chip alto' : nivel === 'MEDIO' ? 'chip medio' : 'chip'}
          style={{ alignSelf: 'flex-start' }}
        >
          {nivel === 'ALTO' ? 'Alta' : nivel === 'MEDIO' ? 'Media' : 'Baja'}
        </span>
      </div>

      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginTop: 9,
        }}
      >
        <span className="sub">
          {t('analisis.generadaHace', 'Generada {cuando}',
            { cuando: hace(alerta.fechaGeneracion, { conHoraDeHoy: true }) })}
        </span>
        {puedeAtender ? (
          <span style={{ display: 'flex', gap: 6 }}>
            <button
              type="button"
              className="btn"
              disabled={ocupado}
              onClick={() => onAtender(alerta.id, false)}
            >
              {t('analisis.atender', 'Atender')}
            </button>
            {/*
              Descartar y atender no son lo mismo: una dice «lo resolví», la otra
              «esta alerta no aplica». Distinguirlas es lo que después permite
              saber si las reglas están bien calibradas.
            */}
            <button
              type="button"
              className="btn plano"
              disabled={ocupado}
              onClick={() => onAtender(alerta.id, true)}
            >
              {t('analisis.descartar', 'Descartar')}
            </button>
          </span>
        ) : null}
      </div>
    </div>
  );
}

export function Predictivo() {
  const p = usePlural();
  const { fechaHoraSegundos: fechaHora } = useFormato();
  const t = useT();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [panel, setPanel] = useState<PanelPredictivoDto | null>(null);
  const [ia, setIa] = useState<EstadoIaDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  const cargar = useCallback(() => {
    setError(null);
    api.prediccion
      .panel()
      .then(setPanel)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarAnalisis', 'No se pudo cargar el análisis.'));
      });

    api.prediccion.estadoIa().then(setIa).catch(cerrarSiExpiro);
  }, [cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  function reevaluar() {
    setOcupado(true);
    setError(null);
    setAviso(null);

    api.prediccion
      .evaluar()
      .then((r) => {
        // Las tres partes se arman con el plural del diccionario y se unen
        // con una frase que tambien se traduce: pegadas con `+` la coma y el
        // «y» quedaban en castellano con la interfaz en ingles.
        setAviso(
          t('analisis.resultadoEvaluacion',
            '{equipos}: {alertas}, {altos} en riesgo alto y {medios} en riesgo medio.', {
              equipos: p(r.equiposEvaluados, 'analisis.equipoEvaluado',
                '1 equipo evaluado', '{n} equipos evaluados'),
              alertas: r.alertasNuevas === 0
                ? t('analisis.ningunaAlertaNueva', 'ninguna alerta nueva')
                : p(r.alertasNuevas, 'analisis.alertaNueva', '1 alerta nueva', '{n} alertas nuevas'),
              altos: r.enRiesgoAlto,
              medios: r.enRiesgoMedio,
            }),
        );
        cargar();
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.reevaluar', 'No se pudo reevaluar el parque.'));
      })
      .finally(() => setOcupado(false));
  }

  function atender(id: string, descartar: boolean) {
    setOcupado(true);
    api.prediccion
      .atender(id, descartar)
      .then(cargar)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.actualizarAlerta', 'No se pudo actualizar la alerta.'));
      })
      .finally(() => setOcupado(false));
  }

  const contexto = !panel
    ? t('comun.cargando', 'Cargando…')
    : panel.equiposEvaluados === 0
      ? t('analisis.parqueSinEvaluar', 'Todavía no se evaluó el parque')
      : [
          p(panel.equiposEvaluados, 'analisis.equipoEvaluado',
            '1 equipo evaluado', '{n} equipos evaluados'),
          t('analisis.ultimaEvaluacion', 'última evaluación {fecha}',
            { fecha: fechaHora(panel.ultimaEvaluacion) }),
          p(panel.reglasActivas, 'analisis.reglaActiva',
            '1 regla activa', '{n} reglas activas'),
          p(panel.alertasSinAtender, 'analisis.alertaSinAtender',
            '1 alerta sin atender', '{n} alertas sin atender'),
        ].join(' · ');

  return (
    <>
      <Encabezado
        titulo={t('analisis.titulo', 'Análisis predictivo')}
        contexto={contexto}
        acciones={
          <>
            {puede(Patentes.reglaGestionar) ? (
              <Link to="/configuracion/reglas" className="btn">
                {t('predictivo.configurarReglas', 'Configurar reglas')}
              </Link>
            ) : null}
            <button type="button" className="btn pri" onClick={reevaluar} disabled={ocupado}>
              {ocupado
                ? t('analisis.evaluando', 'Evaluando…')
                : t('analisis.reevaluarAhora', 'Reevaluar ahora')}
            </button>
          </>
        }
      />

      <div className="cuerpo pagina-analisis">
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

        {/* El estado degradado del proveedor se muestra siempre que no sea normal. */}
        {ia && ia.estadoCircuito !== 'CERRADO' ? (
          <div className="aviso atencion" style={{ marginBottom: 12 }}>
            <b>Proveedor de IA «{ia.proveedor}»:</b> {ia.mensaje}
          </div>
        ) : null}

        <div className="dos-columnas">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <div className="tarjeta">
              <header>
                <h2>{t('analisis.distribucion', 'Distribución del parque por nivel de riesgo')}</h2>
                {panel ? (
                  <span className="nota">
                    {p(panel.equiposEvaluados, 'analisis.equipoEvaluado',
                      '1 equipo evaluado', '{n} equipos evaluados')}
                  </span>
                ) : null}
              </header>

              {panel && panel.equiposEvaluados > 0 ? (
                <>
                  <FranjaRiesgo
                    etiqueta={t('analisis.riesgoAlto', 'Riesgo alto')}
                    rango={`${CORTE_ALTO}–100`}
                    cantidad={panel.enRiesgoAlto}
                    total={panel.equiposEvaluados}
                    color="var(--alto-barra)"
                  />
                  <FranjaRiesgo
                    etiqueta={t('analisis.riesgoMedio', 'Riesgo medio')}
                    rango={`${CORTE_MEDIO}–${CORTE_ALTO - 1}`}
                    cantidad={panel.enRiesgoMedio}
                    total={panel.equiposEvaluados}
                    color="var(--medio-barra)"
                  />
                  <FranjaRiesgo
                    etiqueta={t('analisis.riesgoBajo', 'Riesgo bajo')}
                    rango={`0–${CORTE_MEDIO - 1}`}
                    cantidad={panel.enRiesgoBajo}
                    total={panel.equiposEvaluados}
                    color="var(--bajo-barra)"
                  />
                </>
              ) : (
                <div className="vacio">
                  {t('analisis.sinEvaluarParque', 'El parque todavía no se evaluó. Usá «Reevaluar ahora» para calcular el riesgo de cada equipo.')}
                </div>
              )}
            </div>

            <div className="tarjeta">
              <header>
                <h2>{t('analisis.ranking', 'Ranking de equipos por score')}</h2>
              </header>

              <table>
                <thead>
                  <tr>
                    <th style={{ width: 140 }}>{t('incidencia.equipo', 'Equipo')}</th>
                    <th style={{ width: 70 }}>{t('analisis.score', 'Score')}</th>
                    <th>{t('analisis.reglasDisparadas', 'Reglas que se dispararon')}</th>
                  </tr>
                </thead>
                <tbody>
                  {panel?.ranking.slice(0, 12).map((f) => (
                    <tr key={f.idEquipo}>
                      <td>
                        <Link to={`/activos/${f.idEquipo}`} className="mono">
                          {f.codigoEquipo}
                        </Link>
                      </td>
                      <td>
                        <NivelRiesgo nivel={f.nivel} score={f.score} />
                      </td>
                      <td>
                        {f.reglasDisparadas.length === 0 ? (
                          // El score es continuo: una regla al 80 % de su umbral
                          // aporta sin disparar. Decir sólo «ninguna» al lado de
                          // un score de 51 deja al usuario preguntándose de
                          // dónde salió ese número.
                          <span className="sub">{t('analisis.sinUmbral', 'Ninguna alcanzó su umbral')}</span>
                        ) : (
                          <span style={{ display: 'flex', gap: 5, flexWrap: 'wrap' }}>
                            {f.reglasDisparadas.map((r) => (
                              <span key={r} className="chip">
                                {r}
                              </span>
                            ))}
                          </span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              {panel && panel.ranking.length === 0 ? (
                <div className="vacio">{t('analisis.sinEvaluaciones', 'No hay evaluaciones todavía.')}</div>
              ) : null}

              {panel && panel.ranking.length > 12 ? (
                <div className="pie-tabla">
                  <span>
                    {t('analisis.mostrandoDoce',
                      'Mostrando los 12 de mayor riesgo, de {total} equipos evaluados',
                      { total: panel.ranking.length })}
                  </span>
                </div>
              ) : null}
            </div>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <div className="tarjeta">
              <header>
                <h2>{t('analisis.alertas', 'Alertas activas')}</h2>
                {panel ? <span className="nota">{panel.alertas.length}</span> : null}
              </header>

              {panel?.alertas.slice(0, 10).map((a) => (
                <TarjetaAlerta
                  key={a.id}
                  alerta={a}
                  onAtender={atender}
                  ocupado={ocupado}
                  puedeAtender={puede(Patentes.alertaVer)}
                />
              ))}

              {panel && panel.alertas.length === 0 ? (
                <div className="vacio">{t('dash.sinAlertas', 'No hay alertas activas.')}</div>
              ) : null}

              {panel && panel.alertas.length > 10 ? (
                <div className="pie-tabla">
                  <span>{t('analisis.mostrandoDiez', 'Mostrando 10 de {total}', { total: panel.alertas.length })}</span>
                </div>
              ) : null}
            </div>

            {ia ? (
              <div className="tarjeta">
                <header>
                  <h2>{t('analisis.proveedorAsignacion', 'Proveedor de asignación')}</h2>
                  <span className="nota">{ia.proveedor}</span>
                </header>
                <dl className="dl">
                  <dt>{t('comun.estado', 'Estado')}</dt>
                  <dd>
                    <span
                      className={ia.estadoCircuito === 'CERRADO' ? 'chip bajo' : 'chip medio'}
                    >
                      {ia.estadoCircuito === 'CERRADO'
                        ? t('ia.respondiendo', 'Respondiendo')
                        : ia.estadoCircuito === 'ABIERTO'
                          ? t('ia.sinRespuesta', 'Sin respuesta')
                          : t('comun.probando', 'Probando…')}
                    </span>
                  </dd>
                  <dt>{t('ia.fallosSeguidos', 'Fallos seguidos')}</dt>
                  <dd>{ia.fallosConsecutivos}</dd>
                  <dt>{t('analisis.ultimaConsultaOk', 'Última consulta OK')}</dt>
                  <dd>{fechaHora(ia.ultimaConsultaOk)}</dd>
                </dl>
                <div className="sub" style={{ padding: '0 16px 14px' }}>
                  {ia.mensaje}
                </div>
              </div>
            ) : null}
          </div>
        </div>
      </div>
    </>
  );
}
