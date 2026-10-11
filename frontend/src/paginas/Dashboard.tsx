import { useCallback, useEffect, useState } from 'react';
import { useHace } from '../idioma/hace';
import { useT } from '../idioma/IdiomaContext';
import { usePlural } from '../idioma/plural';
import { Link } from 'react-router-dom';
import { api, ErrorApi } from '../api/cliente';
import type { DashboardDto, IndicadorDto } from '../api/tipos';
import { Patentes } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { Indicador as TarjetaIndicador } from '../componentes/Indicador';
import { SenalPredictiva } from '../componentes/SenalPredictiva';
import { IconoActivos, IconoIncidencias, IconoAnalisis } from '../componentes/Iconos';
import { EsqueletoTabla } from '../componentes/Estados';
import { NivelRiesgo } from '../componentes/NivelRiesgo';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';

const CORTE_MEDIO = 40;
const CORTE_ALTO = 70;

/**
 * El castellano de cada clave que manda el backend.
 *
 * El backend manda la clave y no la frase, para que sumar un idioma sea cargar
 * filas en el diccionario. El texto de reserva vive aca por la misma razon que
 * el segundo argumento de `t()` en todo el resto del frontend: si el
 * diccionario no cargo, la pantalla tiene que poder leerse igual.
 */
const RESERVA: Record<string, string> = {
  'dash.equiposActivos': 'Equipos activos',
  'dash.incidenciasAbiertas': 'Incidencias abiertas',
  'dash.alertas': 'Alertas predictivas',
  'dash.enRiesgoAlto': 'Equipos en riesgo alto',
  'dash.porRespaldo': 'Asignadas por respaldo',
  'dash.deRegistrados': 'de {total} registrados',
  'dash.todasAsignadas': 'todas asignadas',
  'dash.unaSinAsignar': '1 sin asignar',
  'dash.variasSinAsignar': '{cantidad} sin asignar',
  'dash.unaEstaSemana': '1 generada esta semana',
  'dash.variasEstaSemana': '{cantidad} generadas esta semana',
  'dash.sinIntervenciones': 'sin intervenciones pendientes',
  'dash.requierenIntervencion': 'requieren intervención',
  'dash.pendientesDeRevision': 'pendientes de revisión',
};

/** Tarjeta de indicador del encabezado. */
function Indicador({
  indicador,
}: {
  indicador: IndicadorDto;
}) {
  const t = useT();
  const { clave, valor, detalleClave, datos } = indicador;
  const detalle = detalleClave
    ? t(detalleClave, RESERVA[detalleClave] ?? detalleClave, datos)
    : undefined;

  return <TarjetaIndicador titulo={t(clave, RESERVA[clave] ?? clave)} valor={valor} detalle={detalle}
    icono={clave === 'dash.equiposActivos' ? <IconoActivos /> : clave === 'dash.incidenciasAbiertas' ? <IconoIncidencias /> : <IconoAnalisis />}
    tono={clave === 'dash.enRiesgoAlto' && Number(valor) > 0 ? 'alto' : undefined} />;
}

export function Dashboard() {
  const t = useT();
  const p = usePlural();
  const hace = useHace();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [datos, setDatos] = useState<DashboardDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const [aviso, setAviso] = useState<string | null>(null);

  const cargar = useCallback(() => {
    setError(null);
    api.prediccion
      .dashboard()
      .then(setDatos)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarPanel', 'No se pudo cargar el panel.'));
      });
  }, [cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  function reevaluar() {
    setOcupado(true);
    setAviso(null);
    api.prediccion
      .evaluar()
      .then((r) => {
        setAviso(
          t('dash.resultadoEvaluacion', '{equipos} · {alertas}.', {
            equipos: p(r.equiposEvaluados, 'analisis.equipoEvaluado',
              '1 equipo evaluado', '{n} equipos evaluados'),
            alertas: r.alertasNuevas === 0
              ? t('analisis.ningunaAlertaNueva', 'ninguna alerta nueva')
              : p(r.alertasNuevas, 'analisis.alertaNueva', '1 alerta nueva', '{n} alertas nuevas'),
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

  return (
    <>
      <Encabezado
        titulo={t('dash.titulo', 'Dashboard')}
        acciones={
          <>
            {puede(Patentes.alertaVer) ? (
              <button type="button" className="btn" onClick={reevaluar} disabled={ocupado}>
                {ocupado ? t('analisis.evaluando', 'Evaluando…') : t('dash.reevaluarRiesgo', 'Reevaluar riesgo')}
              </button>
            ) : null}
            {puede(Patentes.incidenciaRegistrar) ? (
              <Link to="/incidencias/nueva" className="btn pri">
                {t('incidencia.registrar', 'Registrar incidencia')}
              </Link>
            ) : null}
          </>
        }
      />

      <div className="cuerpo pagina-dashboard">
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

        {!datos ? (
          <div className="tarjeta">
            <EsqueletoTabla columnas={4} filas={6} />
          </div>
        ) : (
          <>
            {/*
              El maximo de columnas viaja como variable CSS para que la media
              query de pantallas chicas pueda pasar a `auto-fit` sin pelearle a
              un estilo en linea, que siempre gana.
            */}
            <div
              className="indicadores"
              style={
                {
                  '--columnas': Math.min(datos.indicadores.length, 5),
                } as React.CSSProperties
              }
            >
              {datos.indicadores.map((i) => (
                <Indicador key={i.clave} indicador={i} />
              ))}
            </div>

            {/*
              `alignItems: start` para que cada tarjeta termine donde termina su
              contenido. Sin eso la de la izquierda se estiraba hasta el alto de
              la columna de alertas y quedaba media pantalla de vacio debajo de
              una tabla de siete filas.
            */}
            <SenalPredictiva equipos={datos.equiposEnRiesgo} puedeVerEquipo={puede(Patentes.equipoVer)} />
            <div className="dos-columnas dashboard-detalles">
              <div className="tarjeta">
                <header>
                  <h2>{t('dash.mayorRiesgo', 'Equipos con mayor riesgo')}</h2>
                  <Link to="/analisis" style={{ fontSize: 'var(--txt-tabla)', fontWeight: 600 }}>
                    {t('dash.verAnalisis', 'Ver análisis completo')}
                  </Link>
                </header>

                <table>
                  <thead>
                    <tr>
                      <th style={{ width: 118, whiteSpace: 'nowrap' }}>{t('incidencia.equipo', 'Equipo')}</th>
                      {/*
                        Responsable del EQUIPO, no el técnico. Cada fila es un
                        equipo, y un equipo no tiene técnico: lo tienen sus
                        incidencias. El diseño traía técnicos acá (D-15).
                      */}
                      <th>{t('activo.responsable', 'Responsable')}</th>
                      <th>{t('activo.ubicacion', 'Ubicación')}</th>
                      <th style={{ width: 55 }}>{t('dash.incAbreviado', 'Inc.')}</th>
                      <th style={{ width: 70 }}>{t('comun.riesgo', 'Riesgo')}</th>
                      <th>{t('dash.motivoPrincipal', 'Motivo principal')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {datos.equiposEnRiesgo.map((e) => (
                      <tr key={e.idEquipo}>
                        {/* El codigo no se parte: es un identificador. */}
                        <td style={{ whiteSpace: 'nowrap' }}>
                          <Link to={`/activos/${e.idEquipo}`} className="mono">
                            {e.codigo}
                          </Link>
                        </td>
                        <td className="no-parte">
                          {e.responsable ?? <span className="sub">—</span>}
                        </td>
                        <td className="no-parte">{e.ubicacion ?? '—'}</td>
                        <td className="mono">{e.incidencias}</td>
                        <td>
                          <NivelRiesgo nivel={e.nivel} score={e.score} />
                        </td>
                        <td className="sub">{e.motivoPrincipal}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                {datos.equiposEnRiesgo.length === 0 ? (
                  <div className="vacio">
                    {t('dash.sinEvaluar', 'Todavía no se evaluó el parque. Usá «Reevaluar riesgo» para calcular el score de cada equipo.')}
                  </div>
                ) : null}
              </div>

              <div className="tarjeta">
                <header>
                  <h2>{t('dash.alertasRecientes', 'Alertas recientes')}</h2>
                  <span className="nota">
                    {p(datos.alertasRecientes.length, 'dash.alertaActiva',
                      '1 activa', '{n} activas')}
                  </span>
                </header>

                {datos.alertasRecientes.map((a) => {
                  const nivel =
                    a.nivelRiesgo >= CORTE_ALTO
                      ? 'alto'
                      : a.nivelRiesgo >= CORTE_MEDIO
                        ? 'medio'
                        : 'bajo';

                  return (
                    <div
                      key={a.id}
                      className="alerta-reciente"
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                        <div style={{ minWidth: 0 }}>
                          <div style={{ fontSize: 'var(--txt-tabla)' }}>
                            {a.regla} —{' '}
                            <Link to={`/activos/${a.idEquipo}`} className="mono">
                              {a.codigoEquipo}
                            </Link>
                          </div>
                          {a.recomendacion ? (
                            <div className="sub" style={{ marginTop: 3 }}>
                              {a.recomendacion}
                            </div>
                          ) : null}
                        </div>
                        <span
                          className={nivel === 'alto' ? 'chip alto' : nivel === 'medio' ? 'chip medio' : 'chip'}
                          style={{ alignSelf: 'flex-start' }}
                        >
                          {t('comun.riesgo', 'Riesgo')} {nivel === 'alto' ? t('riesgo.alto', 'Alto') : nivel === 'medio' ? t('riesgo.medio', 'Medio') : t('riesgo.bajo', 'Bajo')}
                        </span>
                      </div>
                      <div className="sub" style={{ marginTop: 6 }}>
                        {hace(a.fechaGeneracion)}
                      </div>
                    </div>
                  );
                })}

                {datos.alertasRecientes.length === 0 ? (
                  <div className="vacio">{t('dash.sinAlertas', 'No hay alertas activas.')}</div>
                ) : null}
              </div>
            </div>
          </>
        )}
      </div>
    </>
  );
}
