import { useCallback, useEffect, useMemo, useState } from 'react';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { Link, useSearchParams } from 'react-router-dom';
import { api, ErrorApi } from '../api/cliente';
import type {
  CatalogosMantenimientoDto,
  EquipoListaDto,
  MantenimientoListaDto,
} from '../api/tipos';
import { Patentes } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { EsqueletoTabla, SinDatos } from '../componentes/Estados';
import { IconoBuscar } from '../componentes/Iconos';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';

/** Alta de mantenimiento. Se muestra sobre el listado, no en otra pantalla. */
function Formulario({
  catalogos,
  equipos,
  desdeLaAgenda,
  onListo,
  onCancelar,
}: {
  catalogos: CatalogosMantenimientoDto | null;
  equipos: EquipoListaDto[];
  /*
    El trabajo agendado que se viene a registrar, si se llegó desde la agenda.
    El identificador viaja hasta el servidor para que cierre ese trabajo y no
    el que él adivine: dos preventivos del mismo tipo sobre el mismo equipo
    —uno atrasado y otro del mes que viene— sólo se distinguen acá.
  */
  desdeLaAgenda: { idProgramado: string; idEquipo: string; idTipo: string } | null;
  onListo: () => void;
  onCancelar: () => void;
}) {
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [idEquipo, setIdEquipo] = useState(desdeLaAgenda?.idEquipo ?? '');
  const [idTipo, setIdTipo] = useState(desdeLaAgenda?.idTipo ?? '');
  const [dia, setDia] = useState(() => new Date().toISOString().slice(0, 10));
  const [descripcion, setDescripcion] = useState('');
  const [resultado, setResultado] = useState('');
  const [repuestos, setRepuestos] = useState('');
  const [costo, setCosto] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  function enviar(e: React.FormEvent) {
    e.preventDefault();
    setEnviando(true);
    setError(null);

    api.mantenimientos
      .registrar({
        idEquipo,
        idTipo,
        // El input date da una fecha sin hora; se manda a mediodía para que el
        // corrimiento de zona no la pase al día anterior.
        fecha: `${dia}T12:00:00`,
        descripcion,
        resultado: resultado || undefined,
        repuestos: repuestos || undefined,
        costo: costo ? Number(costo) : undefined,
        idProgramado: desdeLaAgenda?.idProgramado,
      })
      .then(onListo)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.registrar', 'No se pudo registrar.'));
      })
      .finally(() => setEnviando(false));
  }

  return (
    <form onSubmit={enviar} className="tarjeta" style={{ marginBottom: 16 }}>
      <header>
        <h2>{t('mant.registrar', 'Registrar mantenimiento')}</h2>
      </header>

      <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 14 }}>
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <label style={{ flex: '2 1 260px', display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span className="sub">{t('incidencia.equipo', 'Equipo')}</span>
            <select
              className="campo"
              value={idEquipo}
              onChange={(e) => setIdEquipo(e.target.value)}
              required
            >
              <option value="">{t('comun.elegirEquipo', 'Elegir equipo…')}</option>
              {equipos.map((eq) => (
                <option key={eq.id} value={eq.id}>
                  {eq.codigo} — {eq.tipo}
                </option>
              ))}
            </select>
          </label>

          <label style={{ flex: '1 1 170px', display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span className="sub">{t('comun.tipo', 'Tipo')}</span>
            <select
              className="campo"
              value={idTipo}
              onChange={(e) => setIdTipo(e.target.value)}
              required
            >
              <option value="">{t('comun.elegir', 'Elegir…')}</option>
              {catalogos?.tipos.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nombre}
                </option>
              ))}
            </select>
          </label>

          <label style={{ flex: '0 1 150px', display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span className="sub">{t('comun.fecha', 'Fecha')}</span>
            <input
              type="date"
              className="campo"
              value={dia}
              max={new Date().toISOString().slice(0, 10)}
              onChange={(e) => setDia(e.target.value)}
              required
            />
          </label>

          <label style={{ flex: '0 1 140px', display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span className="sub">Costo (opcional)</span>
            <input
              type="number"
              min={0}
              step={100}
              className="campo"
              value={costo}
              onChange={(e) => setCosto(e.target.value)}
              placeholder="0"
            />
          </label>
        </div>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('mant.queSeHizo', 'Qué se hizo')}</span>
          <textarea
            className="campo"
            value={descripcion}
            onChange={(e) => setDescripcion(e.target.value)}
            rows={3}
            required
            placeholder={t('mant.descripcionEjemplo', 'Limpieza interna, cambio de pasta térmica y actualización de firmware.')}
            style={{ resize: 'vertical', minHeight: 68, paddingTop: 8 }}
          />
        </label>

        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <label style={{ flex: '1 1 240px', display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span className="sub">Resultado (opcional)</span>
            <input
              className="campo"
              value={resultado}
              onChange={(e) => setResultado(e.target.value)}
              placeholder={t('mant.ejemploResultado', 'Temperatura normalizada')}
            />
          </label>

          <label style={{ flex: '1 1 240px', display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span className="sub">Repuestos (opcional)</span>
            <input
              className="campo"
              value={repuestos}
              onChange={(e) => setRepuestos(e.target.value)}
              placeholder={t('mant.repuestoEjemplo', 'Pasta térmica')}
            />
          </label>
        </div>

        {error ? (
          <div className="aviso error" role="alert">
            {error}
          </div>
        ) : null}

        <div style={{ display: 'flex', gap: 8 }}>
          <button type="submit" className="btn pri" disabled={enviando}>
            {enviando ? 'Registrando…' : 'Registrar'}
          </button>
          <button type="button" className="btn" onClick={onCancelar} disabled={enviando}>
            {t('comun.cancelar', 'Cancelar')}
          </button>
        </div>
      </div>
    </form>
  );
}

export function Mantenimientos() {
  const { dinero: pesos } = useFormato();
  const { fecha } = useFormato();
  const t = useT();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [parametros, setParametros] = useSearchParams();

  // Se llega acá desde la agenda con el trabajo a registrar en la direccion.
  // Va por la direccion y no por el estado de la navegacion para que el enlace
  // se pueda compartir: «registrá vos este», pegado en un chat, tiene que
  // abrir el formulario con el equipo puesto.
  const desdeLaAgenda = useMemo(() => {
    const idProgramado = parametros.get('programado');
    const idEquipo = parametros.get('equipo');
    const idTipo = parametros.get('tipo');
    if (!idProgramado || !idEquipo || !idTipo) return null;
    return { idProgramado, idEquipo, idTipo };
  }, [parametros]);

  const [lista, setLista] = useState<MantenimientoListaDto[] | null>(null);
  const [catalogos, setCatalogos] = useState<CatalogosMantenimientoDto | null>(null);
  const [equipos, setEquipos] = useState<EquipoListaDto[]>([]);
  const [texto, setTexto] = useState('');
  const [soloPreventivos, setSoloPreventivos] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [alta, setAlta] = useState(desdeLaAgenda !== null);

  const cargar = useCallback(() => {
    setError(null);
    api.mantenimientos
      .listar()
      .then(setLista)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarListado', 'No se pudo cargar el listado.'));
      });
  }, [cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  // Cerrar el alta borra los parametros: si quedaran, recargar la pantalla
  // volveria a abrir el formulario del trabajo que se acaba de registrar.
  function cerrarAlta() {
    setAlta(false);
    if (desdeLaAgenda) setParametros({}, { replace: true });
  }

  useEffect(() => {
    api.mantenimientos.catalogos().then(setCatalogos).catch(cerrarSiExpiro);
    api.equipos
      .buscar({ porPagina: 200 })
      .then((p) => setEquipos(p.items))
      .catch(cerrarSiExpiro);
  }, [cerrarSiExpiro]);

  // El filtro es en memoria: el listado completo son cientos de filas, no
  // decenas de miles, y filtrar acá evita un viaje al servidor por cada tecla.
  const visibles = useMemo(() => {
    if (!lista) return [];
    const t = texto.trim().toLowerCase();
    return lista.filter((m) => {
      if (soloPreventivos && !m.esPreventivo) return false;
      if (!t) return true;
      return (
        m.codigoEquipo.toLowerCase().includes(t) ||
        m.descripcion.toLowerCase().includes(t) ||
        (m.tecnico ?? '').toLowerCase().includes(t)
      );
    });
  }, [lista, texto, soloPreventivos]);

  const contexto = useMemo(() => {
    if (!lista) return t('comun.cargando', 'Cargando…');
    const preventivos = lista.filter((m) => m.esPreventivo).length;
    const conCosto = lista.filter((m) => m.costo != null);
    const total = conCosto.reduce((s, m) => s + (m.costo ?? 0), 0);

    const partes = [
      lista.length === 1 ? '1 mantenimiento' : `${lista.length} mantenimientos`,
      `${preventivos} preventivos`,
    ];
    // El total sólo se muestra si hay costos cargados: un «$0» sobre registros
    // sin costo diría algo falso.
    if (conCosto.length > 0) partes.push(`${pesos(total)} en repuestos y servicio`);
    return partes.join(' · ');
  }, [lista]);

  return (
    <>
      <Encabezado
        titulo={t('mant.titulo', 'Mantenimientos')}
        contexto={contexto}
        acciones={
          puede(Patentes.mantenimientoRegistrar) && !alta ? (
            <button type="button" className="btn pri" onClick={() => setAlta(true)}>
              {t('mant.registrar', 'Registrar mantenimiento')}
            </button>
          ) : null
        }
      />

      <div className="cuerpo pagina-mantenimientos">
        {error ? (
          <div className="aviso error" role="alert" style={{ marginBottom: 12 }}>
            {error}
          </div>
        ) : null}

        {alta ? (
          <Formulario
            catalogos={catalogos}
            equipos={equipos}
            desdeLaAgenda={desdeLaAgenda}
            onListo={() => {
              cerrarAlta();
              cargar();
            }}
            onCancelar={cerrarAlta}
          />
        ) : null}

        <div className="filtros">
          <div className="campo buscador">
            <IconoBuscar />
            <input
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
              placeholder={t('mant.buscarPlaceholder', 'Equipo, técnico o descripción…')}
              aria-label={t('mant.buscar', 'Buscar mantenimientos')}
            />
          </div>

          <button
            type="button"
            className={soloPreventivos ? 'btn pri' : 'btn'}
            onClick={() => setSoloPreventivos((v) => !v)}
          >
            {t('mant.soloPreventivos', 'Sólo preventivos')}
          </button>
        </div>

        <div className="tarjeta">
          <table>
            <thead>
              <tr>
                <th style={{ width: 100 }}>{t('comun.fecha', 'Fecha')}</th>
                <th style={{ width: 125, whiteSpace: 'nowrap' }}>{t('incidencia.equipo', 'Equipo')}</th>
                <th style={{ width: 115 }}>{t('comun.tipo', 'Tipo')}</th>
                <th>{t('mant.queSeHizo', 'Qué se hizo')}</th>
                <th style={{ width: 145 }}>{t('incidencia.tecnico', 'Técnico')}</th>
                <th style={{ width: 95 }}>{t('comun.incidencia', 'Incidencia')}</th>
                <th style={{ width: 105 }} className="derecha">
                  {t('mant.costo', 'Costo')}
                </th>
              </tr>
            </thead>
            <tbody>
              {visibles.map((m) => (
                <tr key={m.id}>
                  <td>{fecha(m.fecha)}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    <Link to={`/activos/${m.idEquipo}`} className="mono">
                      {m.codigoEquipo}
                    </Link>
                  </td>
                  <td>
                    {/*
                      Que un mantenimiento sea preventivo no es decorativo: es lo
                      único que reinicia el contador de la regla de mantenimiento
                      vencido. Un correctivo no lo hace.
                    */}
                    <span className={m.esPreventivo ? 'chip bajo' : 'chip'}>{m.tipo}</span>
                  </td>
                  <td>{m.descripcion}</td>
                  <td>{m.tecnico ?? '—'}</td>
                  <td>
                    {m.numeroIncidencia ? (
                      <span className="mono">#{String(m.numeroIncidencia).padStart(3, '0')}</span>
                    ) : (
                      <span className="sub">—</span>
                    )}
                  </td>
                  <td className="derecha mono">
                    {m.costo != null ? pesos(m.costo) : <span className="sub">—</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {!lista ? <EsqueletoTabla columnas={6} /> : null}
          {lista && visibles.length === 0 ? (
            lista.length === 0 ? (
              <SinDatos
                titulo={t('vacio.sinMantenimientos', 'Todavía no hay mantenimientos registrados')}
                detalle={t('vacio.sinMantenimientosAyuda', 'Cada intervención alimenta el historial del equipo, y el historial es lo que el motor predictivo mira para calcular el riesgo.')}
              />
            ) : (
              <SinDatos
                filtrado
                titulo={t('vacio.mantenimientoSinCoincidencia', 'Ningún mantenimiento coincide con los criterios')}
                detalle={t('vacio.mantenimientoFiltroAyuda', 'Probá con menos filtros o con otro rango de fechas.')}
              />
            )
          ) : null}
        </div>
      </div>
    </>
  );
}
