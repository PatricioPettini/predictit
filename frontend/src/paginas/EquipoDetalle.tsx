import { useCallback, useEffect, useState } from 'react';
import { usePlural } from '../idioma/plural';
import type { Plural } from '../idioma/plural';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import type { Traducir } from '../idioma/IdiomaContext';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api, ErrorApi } from '../api/cliente';
import type {
  CatalogosEquipoDto,
  EquipoDetalleDto,
  IncidenciaListaDto,
  MantenimientoListaDto,
  RiesgoEquipoDto,
} from '../api/tipos';
import { Patentes } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';
import { FormularioEquipo } from '../componentes/FormularioEquipo';
import { GlifoRiesgo } from '../componentes/NivelRiesgo';
import { EstadoEquipo, EstadoIncidencia } from '../componentes/Semantica';
import { SinDatos } from '../componentes/Estados';

const CRITICIDAD = [
  ['', ''],
  ['activo.criticidadBaja', 'Baja'],
  ['activo.criticidadMedia', 'Media'],
  ['activo.criticidadAlta', 'Alta'],
  ['activo.criticidadCritica', 'Crítica'],
] as const;

/**
 * La criticidad se guarda como número y se muestra como palabra. El índice
 * cero está vacío a propósito: la criticidad empieza en 1, y un equipo con un
 * valor fuera de rango muestra el número en lugar de nada.
 */
function nombreCriticidad(
  t: (clave: string, porDefecto: string) => string,
  valor: number,
): string {
  const fila = CRITICIDAD[valor];
  return fila ? t(fila[0], fila[1]) : String(valor);
}

function marcaModelo(e: EquipoDetalleDto): string {
  return [e.marca, e.modelo].filter(Boolean).join(' ');
}

/**
 * La antigüedad en años y meses.
 *
 * Recibe el plural en lugar de armar la frase con un ternario por cada
 * unidad: «1 año y 1 mes» tiene cuatro combinaciones y cada idioma las
 * resuelve distinto. Con las tres formas como frases completas, quien traduce
 * decide dónde va cada número.
 */
function antiguedad(t: Traducir, p: Plural, meses?: number): string {
  if (meses === undefined || meses === null) return '—';

  const anios = Math.floor(meses / 12);
  const resto = meses % 12;

  if (anios === 0) return p(resto, 'activo.antiguedadMeses', '1 mes', '{n} meses');
  if (resto === 0) return p(anios, 'activo.antiguedadAnios', '1 año', '{n} años');

  // El «y» que une las dos partes también se traduce: concatenado a mano
  // quedaba «1 year y 8 months» con la interfaz en inglés.
  return t('activo.antiguedadAniosMeses', '{anios} y {meses}', {
    anios: p(anios, 'activo.antiguedadAnios', '1 año', '{n} años'),
    meses: p(resto, 'activo.antiguedadMeses', '1 mes', '{n} meses'),
  });
}

/** Estado de la garantía, con el color que corresponde a su urgencia. */
function Garantia({ equipo }: { equipo: EquipoDetalleDto }) {
  const { fecha } = useFormato();
  const t = useT();
  if (!equipo.fechaFinGarantia) return <>—</>;

  const dias = equipo.diasParaVencimientoGarantia ?? 0;

  if (equipo.garantiaVencida) {
    return (
      <>
        {fecha(equipo.fechaFinGarantia)} <span className="chip alto">{t('garantia.vencida', 'Vencida')}</span>
      </>
    );
  }
  // 60 días es el umbral de la regla "Garantía próxima a vencer" del motor
  // predictivo: acá se muestra el mismo criterio para que no digan cosas
  // distintas.
  if (dias <= 60) {
    return (
      <>
        {fecha(equipo.fechaFinGarantia)} <span className="chip medio">{t('garantia.venceEn', 'Vence en {dias} días', { dias })}</span>
      </>
    );
  }
  return (
    <>
      {fecha(equipo.fechaFinGarantia)} <span className="chip bajo">{t('garantia.vigente', 'Vigente')}</span>
    </>
  );
}

/**
 * Cambio de estado operativo del equipo.
 *
 * Va aparte de la edición de la ficha porque es otra operación, con otra
 * patente: marcar un equipo «en reparación» es del día a día del técnico y
 * editar su ficha no. Pide el motivo porque el estado dice que un equipo está
 * fuera de servicio, y el motivo es lo que permite entender por qué.
 */
function CambioDeEstado({
  equipo,
  catalogos,
  alCambiar,
}: {
  equipo: EquipoDetalleDto;
  catalogos: CatalogosEquipoDto;
  alCambiar: () => void;
}) {
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();
  const [idEstado, setIdEstado] = useState('');
  const [motivo, setMotivo] = useState('');
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // «Dado de baja» no se ofrece: la baja es otra operación, que además valida
  // que el equipo no tenga incidencias abiertas.
  const posibles = catalogos.estados.filter(
    (e) => e.id !== equipo.idEstadoEquipo && e.nombre !== 'Dado de baja',
  );

  function guardar() {
    if (!idEstado) return;
    setGuardando(true);
    setError(null);

    api.equipos
      .cambiarEstado(equipo.id, idEstado, motivo.trim() || undefined)
      .then(() => {
        setIdEstado('');
        setMotivo('');
        alCambiar();
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cambiarEstado', 'No se pudo cambiar el estado.'));
      })
      .finally(() => setGuardando(false));
  }

  return (
    <div className="tarjeta">
      <header>
        <h2>{t('activo.estadoOperativo', 'Estado operativo')}</h2>
      </header>

      <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 10 }}>
        <div className="sub">
          {t('activo.ahoraEsta', 'Ahora está')} <strong>{equipo.estado}</strong>{t('activo.noOperativoNota', '. El motor predictivo no evalúa los equipos que no están operativos.')}
        </div>

        <select
          className="campo"
          value={idEstado}
          disabled={guardando}
          aria-label={t('activo.nuevoEstado', 'Nuevo estado')}
          onChange={(e) => setIdEstado(e.target.value)}
        >
          <option value="">{t('activo.elegirEstado', 'Elegir el nuevo estado…')}</option>
          {posibles.map((e) => (
            <option key={e.id} value={e.id}>
              {e.nombre}
            </option>
          ))}
        </select>

        <input
          className="campo"
          value={motivo}
          maxLength={200}
          disabled={guardando || !idEstado}
          placeholder={t('activo.motivoBitacora', 'Motivo — queda en la bitácora')}
          onChange={(e) => setMotivo(e.target.value)}
        />

        <button
          type="button"
          className="btn"
          disabled={guardando || !idEstado}
          onClick={guardar}
        >
          {guardando
            ? t('comun.guardando', 'Guardando…')
            : t('activo.cambiarEstado', 'Cambiar el estado')}
        </button>

        {error ? (
          <div className="aviso error" role="alert">
            {error}
          </div>
        ) : null}
      </div>
    </div>
  );
}

interface Hecho {
  clave: string;
  fecha: string;
  que: string;
  titulo: string;
  detalle: string;
  estado: string | null;
  abierta: boolean;
}

/**
 * El historial del equipo: incidencias y mantenimientos en una sola lÃ­nea de
 * tiempo. Se mezclan a propÃ³sito. Separados en dos tarjetas obligan al tÃ©cnico
 * a cruzar fechas de dos listas para contestar la pregunta que de verdad se
 * hace, que es quÃ© le fue pasando a este equipo.
 */
function HistorialTecnico({ idEquipo }: { idEquipo: string }) {
  const { fecha } = useFormato();
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();
  const { puede } = useSesion();
  const [hechos, setHechos] = useState<Hecho[] | null>(null);

  const veIncidencias =
    puede(Patentes.incidenciaVerTodas) || puede(Patentes.incidenciaVerPropias);
  const veMantenimientos = puede(Patentes.mantenimientoVer);

  useEffect(() => {
    if (!veIncidencias && !veMantenimientos) {
      setHechos([]);
      return;
    }

    Promise.all([
      veIncidencias
        ? api.incidencias.buscar({ equipo: idEquipo, porPagina: 50 }).then((p) => p.items)
        : Promise.resolve([] as IncidenciaListaDto[]),
      veMantenimientos
        ? api.mantenimientos.listar(idEquipo)
        : Promise.resolve([] as MantenimientoListaDto[]),
    ])
      .then(([incidencias, mantenimientos]) => {
        const juntos: Hecho[] = [
          ...incidencias.map((i) => ({
            clave: 'i' + i.id,
            fecha: i.fecha,
            que: 'Incidencia',
            titulo: '#' + i.numero + ' · ' + i.titulo,
            detalle: [i.categoria, i.tecnico].filter(Boolean).join(' · '),
            estado: i.estado,
            abierta: i.abierta,
          })),
          ...mantenimientos.map((m) => ({
            clave: 'm' + m.id,
            fecha: m.fecha,
            que: m.esPreventivo ? 'Mantenimiento preventivo' : 'Mantenimiento',
            titulo: m.descripcion,
            detalle: [m.tipo, m.tecnico].filter(Boolean).join(' · '),
            estado: null,
            abierta: false,
          })),
        ];
        juntos.sort((a, b) => b.fecha.localeCompare(a.fecha));
        setHechos(juntos);
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setHechos([]);
      });
  }, [idEquipo, veIncidencias, veMantenimientos, cerrarSiExpiro]);

  return (
    <div className="tarjeta">
      <header>
        <h2>{t('activo.historialTecnico', 'Historial técnico')}</h2>
        {hechos && hechos.length > 0 ? (
          <span className="nota">
            {hechos.length} {hechos.length === 1 ? 'registro' : 'registros'}
          </span>
        ) : null}
      </header>

      {!hechos ? (
        <div className="vacio">{t('activo.cargandoHistorial', 'Cargando el historial…')}</div>
      ) : hechos.length === 0 ? (
        <div className="vacio">
          {veIncidencias || veMantenimientos
            ? t('activo.sinHistorial',
                'Este equipo no tiene incidencias ni mantenimientos registrados.')
            : t('activo.sinPermisoHistorial',
                'No tenés permiso para ver el historial de este equipo.')}
        </div>
      ) : (
        <table className="tabla">
          <thead>
            <tr>
              <th>{t('comun.fecha', 'Fecha')}</th>
              <th>{t('comun.quePaso', 'Qué pasó')}</th>
              <th>{t('comun.detalle', 'Detalle')}</th>
            </tr>
          </thead>
          <tbody>
            {hechos.map((h) => (
              <tr key={h.clave}>
                <td className="mono" style={{ whiteSpace: 'nowrap' }}>
                  {fecha(h.fecha)}
                </td>
                <td>
                  <div>{h.titulo}</div>
                  <div className="nota">{h.que}</div>
                </td>
                <td>
                  {h.detalle || '—'}
                  {h.estado ? (
                    <>
                      {' '}
                      <EstadoIncidencia estado={h.estado} abierta={h.abierta} />
                    </>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

/**
 * El riesgo del equipo con el aporte de cada regla.
 *
 * El desglose es lo que importa: un score de 62 no dice nada, y «62 porque
 * acumula cuatro fallas en noventa días y la garantía vence en tres semanas»
 * dice todo. Por eso ocupa el ancho de la pantalla y no la columna angosta de
 * la derecha, donde las cinco reglas entraban como una tabla de dos columnas
 * apretada y debajo de todo lo demás: el motor predictivo es el centro del
 * sistema, y estaba dibujado como un dato al margen.
 *
 * Cada regla muestra cuánto aportó sobre cuánto podía aportar. El porcentaje
 * sobre el total que había antes se movía solo: si una regla dejaba de
 * aplicar, las otras «subían» sin haber cambiado nada.
 */
function Riesgo({ idEquipo }: { idEquipo: string }) {
  const { fecha } = useFormato();
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();
  const { puede } = useSesion();
  const [riesgo, setRiesgo] = useState<RiesgoEquipoDto | null>(null);
  const [sinDatos, setSinDatos] = useState(false);

  const puedeVer = puede(Patentes.alertaVer);

  useEffect(() => {
    if (!puedeVer) return;
    api.prediccion
      .riesgo(idEquipo)
      .then(setRiesgo)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setSinDatos(true);
      });
  }, [idEquipo, puedeVer, cerrarSiExpiro]);

  if (!puedeVer) return null;

  const nivel = riesgo?.nivel ?? '';
  const clase = nivel === 'ALTO' ? 'chip alto' : nivel === 'MEDIO' ? 'chip medio' : 'chip bajo';

  // Las reglas que no son aplicables al equipo se esconden: una regla de
  // garantía sobre un equipo sin fecha de garantía no dice nada.
  const aplicables = (riesgo?.aportes ?? []).filter((a) => a.aplicable);

  return (
    <div className="tarjeta">
      <header>
        <h2>{t('activo.desgloseRiesgo', 'Desglose del riesgo')}</h2>
        {riesgo ? <span className="nota">Evaluado el {fecha(riesgo.fecha)}</span> : null}
      </header>

      {sinDatos ? (
        <div className="vacio">
          {t('activo.sinEvaluar', 'Este equipo todavía no fue evaluado por el motor predictivo.')}
        </div>
      ) : !riesgo ? (
        <div className="vacio">{t('analisis.calculando', 'Calculando el riesgo…')}</div>
      ) : (
        <div style={{ padding: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 14 }}>
            <GlifoRiesgo nivel={nivel} />
            <span className="mono" style={{ fontSize: 'var(--txt-cifra)', fontWeight: 'var(--peso-cifra)' }}>
              {riesgo.score}
            </span>
            <span className="nota">/ 100</span>
            <span className={clase}>
              {nivel === 'ALTO' ? 'Alto' : nivel === 'MEDIO' ? 'Medio' : 'Bajo'}
            </span>
          </div>

          {aplicables.length === 0 ? (
            <div className="nota">{t('analisis.sinReglasAplicables', 'Ninguna regla es aplicable a este equipo.')}</div>
          ) : (
            <div className="desglose">
              {aplicables.map((a) => {
                const aporta = a.peso * Math.min(a.intensidad, 1);
                // El tono dice cuánto de su techo alcanzó esta regla, no cuánto
                // pesa: una regla chica al tope es una señal, y una grande
                // apenas empezada no lo es todavía.
                const tono = a.cumple ? 'alto' : a.intensidad >= 0.5 ? 'medio' : 'neutro';
                return (
                  <div key={a.regla} className={`aporte aporte-${tono}`}>
                    <div className="aporte-regla">{a.regla}</div>
                    <div className="aporte-puntos mono">
                      {Math.round(aporta)}
                      <span> / {a.peso} pts</span>
                    </div>
                    {a.motivo ? <div className="aporte-motivo">{a.motivo}</div> : null}
                  </div>
                );
              })}
            </div>
          )}

          <div className="nota" style={{ marginTop: 12 }}>
            {t('analisis.scoreNota', 'El score es el promedio de las reglas ponderado por su peso, ajustado por la criticidad del equipo. Una regla aporta desde antes de alcanzar su umbral.')}
          </div>
        </div>
      )}
    </div>
  );
}

export function EquipoDetalle() {
  const { fecha } = useFormato();
  const t = useT();
  const p = usePlural();
  const { id } = useParams<{ id: string }>();
  const cerrarSiExpiro = useCerrarSiExpiro();
  const { puede } = useSesion();
  const [equipo, setEquipo] = useState<EquipoDetalleDto | null>(null);
  const [catalogos, setCatalogos] = useState<CatalogosEquipoDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [noExiste, setNoExiste] = useState(false);
  const [editando, setEditando] = useState(false);
  const [bajaError, setBajaError] = useState<string | null>(null);
  const navegar = useNavigate();

  const cargar = useCallback(() => {
    if (!id) return;
    setError(null);
    api.equipos
      .detalle(id)
      .then((e) => {
        setEquipo(e);
        setEditando(false);
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setNoExiste(ex instanceof ErrorApi && ex.esNoEncontrado);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarEquipo', 'No se pudo cargar el equipo.'));
      });
  }, [id, cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  useEffect(() => {
    if (!puede(Patentes.equipoGestionar) && !puede(Patentes.equipoCambiarEstado)) return;
    api.equipos.catalogos().then(setCatalogos).catch(() => setCatalogos(null));
  }, [puede]);

  function darDeBaja() {
    if (!id) return;
    setBajaError(null);

    api.equipos
      .darDeBaja(id)
      .then(() => navegar('/activos'))
      .catch((ex) => {
        cerrarSiExpiro(ex);
        // El backend rechaza la baja de un equipo con incidencias abiertas y
        // explica por qué: se muestra su mensaje y no uno genérico.
        setBajaError(ex instanceof ErrorApi ? ex.message : t('error.darDeBaja', 'No se pudo dar de baja el equipo.'));
      });
  }

  if (error) {
    return (
      <>
        <Encabezado titulo={t('activo.titulo', 'Activos')} />
        <div className="cuerpo pagina-equipo">
          <div className="tarjeta">
            {noExiste ? (
              <SinDatos
                titulo={t('activo.noExiste', 'Ese equipo no existe')}
                detalle={t('activo.noExisteDetalle',
                           'Puede que se haya dado de baja, o que el enlace esté mal.')}
                accion={
                  <Link to="/activos" className="btn pri">
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
                  <Link to="/activos">{t('incidencia.volverAlListado', 'Volver al listado')}</Link>
                </div>
              </div>
            )}
          </div>
        </div>
      </>
    );
  }

  if (!equipo) {
    return (
      <>
        <Encabezado titulo={t('comun.cargando', 'Cargando…')} />
        <div className="cuerpo pagina-equipo">
          <div className="vacio">{t('activo.cargando', 'Cargando el equipo…')}</div>
        </div>
      </>
    );
  }

  return (
    <>
      <Encabezado
        titulo={<span className="mono" style={{ fontSize: 'var(--txt-titulo)' }}>{equipo.codigo}</span>}
        contexto={
          <>
            {equipo.tipo}
            {marcaModelo(equipo) ? ` · ${marcaModelo(equipo)}` : ''}
            {equipo.ubicacion ? ` · ${equipo.ubicacion}` : ''}
          </>
        }
        acciones={
          puede(Patentes.equipoGestionar) ? (
            <>
              <button
                type="button"
                className="btn"
                disabled={!catalogos}
                onClick={() => setEditando((v) => !v)}
              >
                {editando ? t('comun.cancelar', 'Cancelar') : t('activo.editarFicha', 'Editar ficha')}
              </button>
              <button type="button" className="btn" onClick={darDeBaja}>
                {t('activo.darDeBaja', 'Dar de baja')}
              </button>
            </>
          ) : null
        }
      />

      <div className="cuerpo pagina-equipo">
        {bajaError ? (
          <div className="aviso error" role="alert" style={{ marginBottom: 12 }}>
            {bajaError}
          </div>
        ) : null}

        {editando && catalogos ? (
          <FormularioEquipo
            catalogos={catalogos}
            equipo={equipo}
            alGuardar={cargar}
            alCancelar={() => setEditando(false)}
          />
        ) : null}

        {id ? <Riesgo idEquipo={id} /> : null}

        <div className="dos-columnas">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <div className="tarjeta">
              <header>
                <h2>{t('activo.fichaTecnica', 'Ficha técnica')}</h2>
              </header>
              <dl className="dl">
                <dt>{t('activo.codigo', 'Código')}</dt>
                <dd className="mono">{equipo.codigo}</dd>
                <dt>{t('comun.tipo', 'Tipo')}</dt>
                <dd>{equipo.tipo}</dd>
                <dt>{t('activo.marcaModelo', 'Marca y modelo')}</dt>
                <dd>{marcaModelo(equipo) || '—'}</dd>
                <dt>{t('activo.serie', 'N.º de serie')}</dt>
                <dd className="mono">{equipo.numeroSerie ?? '—'}</dd>
                <dt>{t('activo.ubicacion', 'Ubicación')}</dt>
                <dd>{equipo.ubicacion ?? '—'}</dd>
                <dt>{t('activo.responsable', 'Responsable')}</dt>
                <dd>{equipo.responsable ?? '—'}</dd>
                <dt>{t('comun.estado', 'Estado')}</dt>
                <dd>
                  <EstadoEquipo estado={equipo.estado} />
                </dd>
                <dt>{t('activo.criticidad', 'Criticidad')}</dt>
                <dd>{nombreCriticidad(t, equipo.criticidad)}</dd>
                {equipo.descripcionTecnica ? (
                  <>
                    <dt>{t('incidencia.descripcion', 'Descripción')}</dt>
                    <dd>{equipo.descripcionTecnica}</dd>
                  </>
                ) : null}
              </dl>
            </div>

            {id ? <HistorialTecnico idEquipo={id} /> : null}
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            {puede(Patentes.equipoCambiarEstado) && catalogos ? (
              <CambioDeEstado equipo={equipo} catalogos={catalogos} alCambiar={cargar} />
            ) : null}

            <div className="tarjeta">
              <header>
                <h2>{t('activo.datosAdministrativos', 'Datos administrativos')}</h2>
              </header>
              <dl className="dl">
                <dt>{t('activo.altaSistema', 'Alta en el sistema')}</dt>
                <dd>{fecha(equipo.fechaAlta)}</dd>
                <dt>{t('activo.adquisicion', 'Adquisición')}</dt>
                <dd>{fecha(equipo.fechaAdquisicion)}</dd>
                <dt>{t('activo.antiguedad', 'Antigüedad')}</dt>
                <dd>{antiguedad(t, p, equipo.antiguedadEnMeses)}</dd>
                <dt>{t('ia.proveedor', 'Proveedor')}</dt>
                <dd>{equipo.proveedor ?? '—'}</dd>
                <dt>{t('activo.garantia', 'Fin de garantía')}</dt>
                <dd>
                  <Garantia equipo={equipo} />
                </dd>
              </dl>
            </div>

          </div>
        </div>
      </div>
    </>
  );
}
