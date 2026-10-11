import { useCallback, useEffect, useState } from 'react';
import { usePlural } from '../idioma/plural';
import type { Plural } from '../idioma/plural';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { Link, useParams } from 'react-router-dom';
import { api, ErrorApi } from '../api/cliente';
import type { CatalogosIncidenciaDto, IncidenciaDetalleDto } from '../api/tipos';
import { Patentes } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';
import { PanelAtencion } from '../componentes/PanelAtencion';
import { SinDatos } from '../componentes/Estados';
import { EstadoIncidencia, OrigenAsignacion, Prioridad } from '../componentes/Semantica';
import { GuiaReparacion } from '../componentes/GuiaReparacion';

/**
 * Cuánto tardó, en la unidad que se lee de un vistazo.
 *
 * «min» y «h» son abreviaturas de unidad y no palabras: se dejan como están y
 * no se traducen, igual que en el resto del sistema. El día sí es una palabra
 * y va por el diccionario.
 */
function horas(p: Plural, valor: number): string {
  if (valor < 1) return `${Math.round(valor * 60)} min`;
  if (valor < 48) return `${valor.toFixed(1)} h`;
  return p(Math.round(valor / 24), 'comun.dia', '1 día', '{n} días');
}

/**
 * Cómo se decidió la asignación.
 *
 * Es la parte de la pantalla que justifica el sistema: no alcanza con decir a
 * quién se asignó, hay que poder mostrar por qué y quién lo decidió.
 */
function BloqueAsignacion({ incidencia }: { incidencia: IncidenciaDetalleDto }) {
  const { fechaHoraSegundos: fechaHora } = useFormato();
  const t = useT();
  const a = incidencia.asignacion;

  if (!a) {
    return (
      <div className="tarjeta">
        <header>
          <h2>{t('incidencia.asignacion', 'Asignación')}</h2>
        </header>
        <div className="vacio">
          {t('incidencia.sinAsignacion', 'Esta incidencia todavía no pasó por el proceso de asignación.')}
        </div>
      </div>
    );
  }

  const porRespaldo = a.origen === 'RESPALDO';

  const etiquetaOrigen =
    a.origen === 'IA'
      ? t('ia.decididaPorIa', 'Decidida por el proveedor de IA')
      : a.origen === 'HEURISTICA'
        ? t('ia.decididaPorReglas', 'Decidida por reglas automáticas')
        : t('ia.porRespaldo', 'Regla de respaldo');

  return (
    <div className="tarjeta">
      <header>
        <h2>{t('incidencia.asignacion', 'Asignación')}</h2>
        <span className="nota">{fechaHora(a.fechaHora)}</span>
      </header>

      <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 10 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <OrigenAsignacion tipo={a.origen} revision={porRespaldo} />
          <span className="sub">{etiquetaOrigen}</span>
          {incidencia.pendienteRevision ? (
            <span className="chip medio">{t('incidencia.pendienteRevision', 'Pendiente de revisión')}</span>
          ) : null}
        </div>

        <div>
          <div className="sub">{t('incidencia.tecnico', 'Técnico')}</div>
          <div style={{ color: 'var(--tx)', fontWeight: 500 }}>
            {a.tecnico ?? t('incidencia.sinAsignar', 'Sin asignar')}
          </div>
        </div>

        {a.justificacion ? (
          <div>
            <div className="sub">{t('incidencia.justificacion', 'Justificación')}</div>
            <div>{a.justificacion}</div>
          </div>
        ) : null}

        {/*
          El motivo del respaldo se muestra tal cual: si el servicio no
          respondió, el administrador tiene que poder ver por qué.
        */}
        {a.motivoRespaldo ? (
          <div className="aviso atencion">
            <b>{t('incidencia.proveedorSinRespuesta', 'El proveedor no respondió.')}</b> {a.motivoRespaldo}
          </div>
        ) : null}
      </div>
    </div>
  );
}

/** Sugerencia del triage, con su confianza real. */
function BloqueClasificacion({ incidencia }: { incidencia: IncidenciaDetalleDto }) {
  const t = useT();
  const c = incidencia.clasificacion;
  if (!c) return null;

  const confianza = c.confianza ?? null;

  return (
    <div className="tarjeta">
      <header>
        <h2>{t('incidencia.clasificacion', 'Clasificación sugerida')}</h2>
        <span className="nota">
          {c.origen === 'IA'
            ? t('ia.porIa', 'Proveedor de IA')
            : t('ia.porHeuristicaLargo', 'Reglas automáticas')}
        </span>
      </header>

      <dl className="dl">
        <dt>{t('incidencia.categoria', 'Categoría')}</dt>
        <dd>{c.categoriaSugerida ?? t('comun.sinDeterminar', 'No se pudo determinar')}</dd>
        <dt>{t('incidencia.prioridad', 'Prioridad')}</dt>
        <dd>{c.prioridadSugerida ?? t('comun.sinDeterminar', 'No se pudo determinar')}</dd>
        {confianza !== null ? (
          <>
            <dt>{t('incidencia.confianza', 'Confianza')}</dt>
            <dd>{Math.round(confianza * 100)} %</dd>
          </>
        ) : null}
        {c.numeroIncidenciaDuplicada ? (
          <>
            <dt>{t('incidencia.duplicado', 'Posible duplicado')}</dt>
            <dd>#{String(c.numeroIncidenciaDuplicada).padStart(3, '0')}</dd>
          </>
        ) : null}
      </dl>

      {c.justificacion ? (
        <div style={{ padding: '10px 16px 14px' }} className="sub">
          {c.justificacion}
        </div>
      ) : null}
    </div>
  );
}

export function IncidenciaDetalle() {
  const { fechaHoraSegundos: fechaHora } = useFormato();
  const t = useT();
  const p = usePlural();
  const { id } = useParams<{ id: string }>();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [incidencia, setIncidencia] = useState<IncidenciaDetalleDto | null>(null);
  const [catalogos, setCatalogos] = useState<CatalogosIncidenciaDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [noExiste, setNoExiste] = useState(false);
  const [guardando, setGuardando] = useState(false);

  const cargar = useCallback(() => {
    if (!id) return;
    setError(null);
    api.incidencias
      .detalle(id)
      .then(setIncidencia)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setNoExiste(ex instanceof ErrorApi && ex.esNoEncontrado);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarIncidencia', 'No se pudo cargar la incidencia.'));
      });
  }, [id, cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  useEffect(() => {
    if (!puede(Patentes.incidenciaReasignarTodas) && !puede(Patentes.incidenciaReasignarPropias)) {
      return;
    }
    api.incidencias.catalogos().then(setCatalogos).catch(cerrarSiExpiro);
  }, [puede, cerrarSiExpiro]);

  function reasignar(idTecnico: string) {
    if (!id || !idTecnico) return;
    setGuardando(true);
    setError(null);

    api.incidencias
      .reasignar(id, idTecnico)
      .then(cargar)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.reasignar', 'No se pudo reasignar.'));
      })
      .finally(() => setGuardando(false));
  }

  if (error && !incidencia) {
    return (
      <>
        <Encabezado titulo={t('incidencia.titulo', 'Incidencias')} />
        <div className="cuerpo pagina-incidencia">
          <div className="tarjeta">
            {/*
              El registro que no existe NO va en rojo: no se rompio nada, el
              enlace esta mal o la incidencia se anulo. Un rojo que aparece
              cuando no hay problema entrena a la gente a ignorar los rojos.
            */}
            {noExiste ? (
              <SinDatos
                titulo={t('incidencia.noExiste', 'Esa incidencia no existe')}
                detalle={t('incidencia.noExisteDetalle',
                           'Puede que se haya anulado, o que el número del enlace esté mal escrito.')}
                accion={
                  <Link to="/incidencias" className="btn pri">
                    {t('incidencia.volverAlListado', 'Volver al listado')}
                  </Link>
                }
              />
            ) : (
              <div style={{ padding: 'var(--esp-4)' }}>
                <div className="aviso error" role="alert">
                  {error}
                </div>
                <div style={{ marginTop: 'var(--esp-3)' }}>
                  <Link to="/incidencias">{t('incidencia.volverAlListado', 'Volver al listado')}</Link>
                </div>
              </div>
            )}
          </div>
        </div>
      </>
    );
  }

  if (!incidencia) {
    return (
      <>
        <Encabezado titulo={t('comun.cargando', 'Cargando…')} />
        <div className="cuerpo pagina-incidencia">
          <div className="vacio">{t('incidencia.cargando', 'Cargando la incidencia…')}</div>
        </div>
      </>
    );
  }

  const puedeReasignar =
    puede(Patentes.incidenciaReasignarTodas) || puede(Patentes.incidenciaReasignarPropias);

  return (
    <>
      <Encabezado
        titulo={
          <span className="mono" style={{ fontSize: 'var(--txt-titulo)' }}>
            #{String(incidencia.numero).padStart(3, '0')}
          </span>
        }
        contexto={incidencia.titulo}
      />

      <div className="cuerpo pagina-incidencia">
        {error ? (
          <div className="aviso error" role="alert" style={{ marginBottom: 12 }}>
            {error}
          </div>
        ) : null}

        <div className="dos-columnas">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <div className="tarjeta">
              <header>
                <h2>{incidencia.titulo}</h2>
                <EstadoIncidencia estado={incidencia.estado} abierta={incidencia.abierta} />
              </header>

              <div style={{ padding: 14 }}>
                <div className="sub" style={{ marginBottom: 6 }}>
                  {t('incidencia.reportadaPor', 'Reportada por')} {incidencia.reportante} · {fechaHora(incidencia.fecha)}
                </div>
                <p style={{ margin: 0, whiteSpace: 'pre-wrap' }}>{incidencia.descripcion}</p>
              </div>
            </div>

            <BloqueAsignacion incidencia={incidencia} />

            {/* La guía queda en la columna de contexto junto a su procedencia. */}
            

            <PanelAtencion incidencia={incidencia} alCambiar={cargar} />

            {puedeReasignar && incidencia.abierta ? (
              <div className="tarjeta">
                <header>
                  <h2>{t('incidencia.reasignar', 'Reasignar')}</h2>
                </header>
                <div style={{ padding: 14, display: 'flex', gap: 10, alignItems: 'center' }}>
                  <select
                    className="campo"
                    defaultValue=""
                    disabled={guardando}
                    aria-label="Reasignar a"
                    onChange={(e) => reasignar(e.target.value)}
                  >
                    <option value="">{t('incidencia.elegirTecnico', 'Elegir técnico…')}</option>
                    {catalogos?.tecnicos.map((t) => (
                      <option key={t.id} value={t.id}>
                        {t.nombreCompleto}
                        {t.especialidades.length > 0
                          ? ` — ${t.especialidades.join(', ')}`
                          : ''}{' '}
                        ({t.incidenciasAbiertas} abiertas)
                      </option>
                    ))}
                  </select>
                  {guardando ? <span className="sub">{t('comun.guardando', 'Guardando…')}</span> : null}
                </div>
                <div className="sub" style={{ padding: '0 16px 14px' }}>
                  {t('incidencia.reasignarQuitaMarca', 'Reasignar a mano quita la marca de pendiente de revisión: ya la revisó una persona.')}
                </div>
              </div>
            ) : null}
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <div className="tarjeta">
              <header>
                <h2>{t('incidencia.datos', 'Datos')}</h2>
              </header>
              <dl className="dl">
                <dt>{t('incidencia.equipo', 'Equipo')}</dt>
                <dd>
                  <Link to={`/activos/${incidencia.idEquipo}`} className="mono">
                    {incidencia.codigoEquipo}
                  </Link>
                </dd>
                <dt>{t('incidencia.prioridad', 'Prioridad')}</dt>
                <dd>
                  <Prioridad nombre={incidencia.prioridad} nivel={incidencia.nivelPrioridad} />
                </dd>
                <dt>{t('incidencia.categoria', 'Categoría')}</dt>
                <dd>{incidencia.categoria ?? '—'}</dd>
                <dt>{t('comun.estado', 'Estado')}</dt>
                <dd>
                  <EstadoIncidencia estado={incidencia.estado} abierta={incidencia.abierta} />
                </dd>
                <dt>{t('incidencia.tecnico', 'Técnico')}</dt>
                <dd>{incidencia.tecnico ?? t('incidencia.sinAsignar', 'Sin asignar')}</dd>
                <dt>
                  {incidencia.abierta
                    ? t('incidencia.abiertaHace', 'Abierta hace')
                    : t('incidencia.resolucion', 'Resolución')}
                </dt>
                <dd>
                  {horas(p, incidencia.horasAbierta)}
                  {incidencia.fueraDeObjetivo ? (
                    <span className="chip alto" style={{ marginLeft: 6 }}>
                      {t('incidencia.fueraDeObjetivo', 'Fuera de objetivo')}
                    </span>
                  ) : null}
                </dd>
                {incidencia.fechaResolucion ? (
                  <>
                    <dt>{t('incidencia.cerradaEl', 'Cerrada')}</dt>
                    <dd>{fechaHora(incidencia.fechaResolucion)}</dd>
                  </>
                ) : null}
              </dl>
            </div>

            <BloqueClasificacion incidencia={incidencia} />
            {incidencia.abierta ? <GuiaReparacion idIncidencia={incidencia.id} /> : null}
          </div>
        </div>
      </div>
    </>
  );
}
