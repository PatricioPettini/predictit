import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type {
  CatalogosMantenimientoDto,
  EquipoListaDto,
  MantenimientoProgramadoDto,
  PanelProgramadoDto,
} from '../api/tipos';
import { Patentes } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { EsqueletoTabla, SinDatos } from '../componentes/Estados';
import { IconoBuscar } from '../componentes/Iconos';
import { EstadoProgramado } from '../componentes/Semantica';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';

/** El día de hoy con el formato del input date, que es el que espera el servidor. */
function hoy() {
  return new Date().toISOString().slice(0, 10);
}

/** Alta manual de una fecha. Se muestra sobre el listado, no en otra pantalla. */
function Formulario({
  catalogos,
  equipos,
  onListo,
  onCancelar,
}: {
  catalogos: CatalogosMantenimientoDto | null;
  equipos: EquipoListaDto[];
  onListo: () => void;
  onCancelar: () => void;
}) {
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [idEquipo, setIdEquipo] = useState('');
  const [idTipo, setIdTipo] = useState('');
  const [dia, setDia] = useState(hoy);
  const [motivo, setMotivo] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  // Sólo los preventivos: agendar un correctivo no tiene sentido —el correctivo
  // es la respuesta a algo que ya pasó— y el servidor lo rechaza.
  const preventivos = (catalogos?.tipos ?? []).filter((x) => x.esPreventivo);

  function enviar(e: React.FormEvent) {
    e.preventDefault();
    setEnviando(true);
    setError(null);

    api.programados
      .programar({
        idEquipo,
        idTipoMantenimiento: idTipo,
        fechaProgramada: dia,
        motivo: motivo || undefined,
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
        <h2>{t('agenda.programar', 'Programar mantenimiento')}</h2>
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

          <label style={{ flex: '1 1 190px', display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span className="sub">{t('comun.tipo', 'Tipo')}</span>
            <select
              className="campo"
              value={idTipo}
              onChange={(e) => setIdTipo(e.target.value)}
              required
            >
              <option value="">{t('comun.elegir', 'Elegir…')}</option>
              {preventivos.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.nombre}
                </option>
              ))}
            </select>
          </label>

          <label style={{ flex: '0 1 160px', display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span className="sub">{t('agenda.fechaPrevista', 'Fecha prevista')}</span>
            <input
              type="date"
              className="campo"
              value={dia}
              min={hoy()}
              onChange={(e) => setDia(e.target.value)}
              required
            />
          </label>
        </div>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('agenda.motivoOpcional', 'Motivo (opcional)')}</span>
          <input
            className="campo"
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            placeholder={t('agenda.motivoEjemplo', 'Pedido del responsable del área')}
          />
        </label>

        {error ? (
          <div className="aviso error" role="alert">
            {error}
          </div>
        ) : null}

        <div style={{ display: 'flex', gap: 8 }}>
          <button type="submit" className="btn pri" disabled={enviando}>
            {enviando ? t('comun.guardando', 'Guardando…') : t('agenda.agendar', 'Agendar')}
          </button>
          <button type="button" className="btn" onClick={onCancelar} disabled={enviando}>
            {t('comun.cancelar', 'Cancelar')}
          </button>
        </div>
      </div>
    </form>
  );
}

/**
 * Reprogramar o anular, dentro de la propia fila.
 *
 * Las dos piden motivo y el servidor lo exige: una fecha que se corre sin
 * explicación deja una agenda que nadie puede auditar, que es justo lo que el
 * plan viene a resolver.
 */
function Accion({
  fila,
  modo,
  onListo,
  onCancelar,
}: {
  fila: MantenimientoProgramadoDto;
  modo: 'reprogramar' | 'anular';
  onListo: () => void;
  onCancelar: () => void;
}) {
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [dia, setDia] = useState(fila.fechaProgramada.slice(0, 10));
  const [motivo, setMotivo] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  function enviar(e: React.FormEvent) {
    e.preventDefault();
    setEnviando(true);
    setError(null);

    const operacion =
      modo === 'reprogramar'
        ? api.programados.reprogramar(fila.id, dia, motivo)
        : api.programados.anular(fila.id, motivo);

    operacion
      .then(onListo)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.guardar', 'No se pudo guardar.'));
      })
      .finally(() => setEnviando(false));
  }

  return (
    <tr>
      <td colSpan={6} style={{ background: 'var(--sup-2)' }}>
        <form
          onSubmit={enviar}
          style={{
            display: 'flex',
            gap: 10,
            alignItems: 'flex-end',
            flexWrap: 'wrap',
            padding: '4px 0',
          }}
        >
          {modo === 'reprogramar' ? (
            <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
              <span className="sub">{t('agenda.nuevaFecha', 'Nueva fecha')}</span>
              <input
                type="date"
                className="campo"
                value={dia}
                onChange={(e) => setDia(e.target.value)}
                required
                autoFocus
              />
            </label>
          ) : null}

          <label style={{ flex: '1 1 280px', display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span className="sub">{t('agenda.motivo', 'Motivo')}</span>
            <input
              className="campo"
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              required
              minLength={5}
              autoFocus={modo === 'anular'}
              placeholder={
                modo === 'reprogramar'
                  ? t('agenda.motivoReprogramar', 'El equipo está en uso hasta fin de mes')
                  : t('agenda.motivoAnular', 'El equipo se dio de baja')
              }
            />
          </label>

          <button type="submit" className="btn pri" disabled={enviando}>
            {modo === 'reprogramar'
              ? t('agenda.reprogramar', 'Reprogramar')
              : t('agenda.anular', 'Anular')}
          </button>
          <button type="button" className="btn" onClick={onCancelar} disabled={enviando}>
            {t('comun.cancelar', 'Cancelar')}
          </button>

          {error ? (
            <div className="aviso error" role="alert" style={{ flexBasis: '100%' }}>
              {error}
            </div>
          ) : null}
        </form>
      </td>
    </tr>
  );
}

export function Agenda() {
  const { fecha } = useFormato();
  const t = useT();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [lista, setLista] = useState<MantenimientoProgramadoDto[] | null>(null);
  const [panel, setPanel] = useState<PanelProgramadoDto | null>(null);
  const [catalogos, setCatalogos] = useState<CatalogosMantenimientoDto | null>(null);
  const [equipos, setEquipos] = useState<EquipoListaDto[]>([]);
  const [estado, setEstado] = useState('PROGRAMADO');
  const [texto, setTexto] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [alta, setAlta] = useState(false);
  const [editando, setEditando] = useState<{ id: string; modo: 'reprogramar' | 'anular' } | null>(
    null,
  );
  const [generando, setGenerando] = useState(false);

  const planificar = puede(Patentes.mantenimientoPlanificar);

  const cargar = useCallback(() => {
    setError(null);
    setEditando(null);

    api.programados
      .listar(estado)
      .then(setLista)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(
          ex instanceof ErrorApi ? ex.message : t('error.cargarListado', 'No se pudo cargar el listado.'),
        );
      });

    api.programados.panel().then(setPanel).catch(cerrarSiExpiro);
  }, [cerrarSiExpiro, estado]);

  useEffect(cargar, [cargar]);

  useEffect(() => {
    api.mantenimientos.catalogos().then(setCatalogos).catch(cerrarSiExpiro);
    api.equipos
      .buscar({ porPagina: 200 })
      .then((p) => setEquipos(p.items))
      .catch(cerrarSiExpiro);
  }, [cerrarSiExpiro]);

  function generar() {
    setGenerando(true);
    setAviso(null);
    setError(null);

    api.programados
      .generar()
      .then((r) => {
        // Se dice también cuántas ya estaban: sin eso, apretar el botón dos
        // veces seguidas parece no haber hecho nada y da la impresión de que
        // falló, cuando en realidad la agenda ya estaba completa.
        setAviso(
          t(
            'agenda.generado',
            'Se agendaron {generados} trabajo(s) a partir de {planes} plan(es). {yaEstaban} ya estaban.',
            {
              generados: String(r.generados),
              planes: String(r.planesActivos),
              yaEstaban: String(r.yaEstaban),
            },
          ),
        );
        cargar();
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.guardar', 'No se pudo guardar.'));
      })
      .finally(() => setGenerando(false));
  }

  // El filtro por texto es en memoria: la agenda de una organización son
  // decenas de filas, y filtrar acá evita un viaje al servidor por cada tecla.
  const visibles = useMemo(() => {
    if (!lista) return [];
    const q = texto.trim().toLowerCase();
    if (!q) return lista;
    return lista.filter(
      (m) =>
        m.codigoEquipo.toLowerCase().includes(q) ||
        m.tipoMantenimiento.toLowerCase().includes(q) ||
        (m.ubicacion ?? '').toLowerCase().includes(q),
    );
  }, [lista, texto]);

  const contexto = useMemo(() => {
    if (!panel) return t('comun.cargando', 'Cargando…');
    return t('agenda.contexto', '{vencidos} vencido(s) · {proximos} en los próximos 7 días', {
      vencidos: String(panel.vencidos),
      proximos: String(panel.proximosSieteDias),
    });
  }, [panel, t]);

  const indicadores = panel
    ? [
        { clave: 'agenda.vencidos', texto: 'Vencidos', valor: panel.vencidos },
        { clave: 'agenda.proximos7', texto: 'Próximos 7 días', valor: panel.proximosSieteDias },
        { clave: 'agenda.agendados', texto: 'Agendados', valor: panel.programadosTotal },
        { clave: 'agenda.planesActivos', texto: 'Planes activos', valor: panel.planesActivos },
      ]
    : [];

  return (
    <>
      <Encabezado
        titulo={t('agenda.titulo', 'Agenda de mantenimiento')}
        contexto={contexto}
        acciones={
          planificar ? (
            <div style={{ display: 'flex', gap: 8 }}>
              <button type="button" className="btn" onClick={generar} disabled={generando}>
                {generando
                  ? t('comun.guardando', 'Guardando…')
                  : t('agenda.generar', 'Generar desde los planes')}
              </button>
              {!alta ? (
                <button type="button" className="btn pri" onClick={() => setAlta(true)}>
                  {t('agenda.programar', 'Programar mantenimiento')}
                </button>
              ) : null}
            </div>
          ) : null
        }
      />

      <div className="cuerpo pagina-agenda">
        {error ? (
          <div className="aviso error" role="alert" style={{ marginBottom: 12 }}>
            {error}
          </div>
        ) : null}

        {aviso ? (
          <div className="aviso" role="status" style={{ marginBottom: 12 }}>
            {aviso}
          </div>
        ) : null}

        {panel ? (
          <div
            className="indicadores"
            style={{ '--columnas': indicadores.length } as React.CSSProperties}
          >
            {indicadores.map((i) => (
              <div key={i.clave} className="tarjeta" style={{ padding: '14px 16px' }}>
                <div className="sub" style={{ marginBottom: 6 }}>
                  {t(i.clave, i.texto)}
                </div>
                <div
                  className="mono"
                  style={{
                    fontSize: 'var(--txt-cifra)',
                    fontWeight: 600,
                    // El rojo sólo cuando hay algo vencido: un cero en rojo
                    // llama la atención sobre la única cifra que está bien.
                    color:
                      i.clave === 'agenda.vencidos' && i.valor > 0 ? 'var(--alto-barra)' : 'var(--tx)',
                    lineHeight: 1,
                  }}
                >
                  {i.valor}
                </div>
              </div>
            ))}
          </div>
        ) : null}

        {alta ? (
          <Formulario
            catalogos={catalogos}
            equipos={equipos}
            onListo={() => {
              setAlta(false);
              cargar();
            }}
            onCancelar={() => setAlta(false)}
          />
        ) : null}

        <div className="filtros">
          <div className="campo buscador">
            <IconoBuscar />
            <input
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
              placeholder={t('agenda.buscarPlaceholder', 'Equipo, tipo o ubicación…')}
              aria-label={t('agenda.buscar', 'Buscar en la agenda')}
            />
          </div>

          <select
            className="campo"
            value={estado}
            onChange={(e) => setEstado(e.target.value)}
            aria-label={t('comun.estado', 'Estado')}
            style={{ maxWidth: 190 }}
          >
            <option value="PROGRAMADO">{t('programado.programados', 'Programados')}</option>
            <option value="EJECUTADO">{t('programado.ejecutados', 'Ejecutados')}</option>
            <option value="ANULADO">{t('programado.anulados', 'Anulados')}</option>
          </select>
        </div>

        <div className="tarjeta">
          <table>
            <thead>
              <tr>
                <th style={{ width: 110 }}>{t('agenda.fechaPrevista', 'Fecha prevista')}</th>
                <th style={{ width: 130, whiteSpace: 'nowrap' }}>{t('incidencia.equipo', 'Equipo')}</th>
                <th style={{ width: 160 }}>{t('equipo.ubicacion', 'Ubicación')}</th>
                <th>{t('comun.tipo', 'Tipo')}</th>
                <th style={{ width: 150 }}>{t('comun.estado', 'Estado')}</th>
                <th style={{ width: 200 }} className="derecha">
                  {t('comun.acciones', 'Acciones')}
                </th>
              </tr>
            </thead>
            <tbody>
              {visibles.map((m) => [
                <tr key={m.id}>
                  <td>{fecha(m.fechaProgramada)}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    <Link to={`/activos/${m.idEquipo}`} className="mono">
                      {m.codigoEquipo}
                    </Link>
                  </td>
                  <td className="sub">{m.ubicacion ?? '—'}</td>
                  <td>
                    {m.tipoMantenimiento}
                    {m.motivo ? <div className="sub">{m.motivo}</div> : null}
                  </td>
                  <td>
                    <EstadoProgramado
                      estado={m.estado}
                      vencido={m.vencido}
                      diasDeAtraso={m.diasDeAtraso}
                    />
                  </td>
                  <td className="derecha">
                    {planificar && m.estado === 'PROGRAMADO' ? (
                      <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
                        {/*
                          «Registrar» lleva al alta de mantenimiento y no cierra
                          nada acá: lo que cierra un trabajo agendado es haberlo
                          hecho, y eso son los datos del mantenimiento —qué se
                          hizo, repuestos, costo—, que se cargan allá.
                        */}
                        <Link
                          className="btn"
                          to={
                            `/mantenimientos?programado=${m.id}` +
                            `&equipo=${m.idEquipo}&tipo=${m.idTipoMantenimiento}`
                          }
                        >
                          {t('agenda.registrarTrabajo', 'Registrar')}
                        </Link>
                        <button
                          type="button"
                          className="btn"
                          onClick={() => setEditando({ id: m.id, modo: 'reprogramar' })}
                        >
                          {t('agenda.reprogramar', 'Reprogramar')}
                        </button>
                        <button
                          type="button"
                          className="btn"
                          onClick={() => setEditando({ id: m.id, modo: 'anular' })}
                        >
                          {t('agenda.anular', 'Anular')}
                        </button>
                      </div>
                    ) : null}
                  </td>
                </tr>,

                editando?.id === m.id ? (
                  <Accion
                    key={`${m.id}-accion`}
                    fila={m}
                    modo={editando.modo}
                    onListo={cargar}
                    onCancelar={() => setEditando(null)}
                  />
                ) : null,
              ])}
            </tbody>
          </table>

          {lista === null ? <EsqueletoTabla columnas={6} /> : null}

          {lista !== null && visibles.length === 0 ? (
            <SinDatos
              titulo={t('agenda.sinNada', 'No hay trabajos agendados')}
              detalle={t(
                'agenda.sinNadaDetalle',
                'Cargá un plan en Configuración › Planes de mantenimiento y generá la agenda, o programá una fecha suelta.',
              )}
            />
          ) : null}
        </div>
      </div>
    </>
  );
}
