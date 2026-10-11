import { useEffect, useMemo, useState } from 'react';
import { usePlural } from '../idioma/plural';
import { useT } from '../idioma/IdiomaContext';
import { Link } from 'react-router-dom';
import { api, ErrorApi } from '../api/cliente';
import type { CatalogosEquipoDto, EquipoListaDto, FiltroEquipos, PaginaDto, SegmentosDto } from '../api/tipos';
import { Patentes } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { EsqueletoTabla, SinDatos } from '../componentes/Estados';
import { IconoBuscar, IconoImportar } from '../componentes/Iconos';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';
import { FormularioEquipo } from '../componentes/FormularioEquipo';
import { EstadoEquipo } from '../componentes/Semantica';

const POR_PAGINA = 20;

export function Activos() {
  const p = usePlural();
  const t = useT();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [filtro, setFiltro] = useState<FiltroEquipos>({ pagina: 1, porPagina: POR_PAGINA });
  const [texto, setTexto] = useState('');
  const [pagina, setPagina] = useState<PaginaDto<EquipoListaDto> | null>(null);
  const [catalogos, setCatalogos] = useState<CatalogosEquipoDto | null>(null);
  const [segmentos, setSegmentos] = useState<SegmentosDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);
  const [creando, setCreando] = useState(false);

  useEffect(() => {
    api.equipos.catalogos().then(setCatalogos).catch(cerrarSiExpiro);
  }, [cerrarSiExpiro]);

  // Los recuentos se piden aparte del listado y con los mismos filtros menos el
  // segmento: cada número dice cuántos vería quien pulse esa píldora ahora, y
  // no cambia al pasar de página, que es por lo que no viaja con el listado.
  useEffect(() => {
    let vigente = true;
    api.equipos
      .segmentos(filtro)
      .then((r) => { if (vigente) setSegmentos(r); })
      // Sin recuentos las píldoras se dibujan sin número y siguen filtrando:
      // es una guía, no el contenido de la pantalla.
      .catch(() => { if (vigente) setSegmentos(null); });
    return () => { vigente = false; };
  }, [filtro.texto, filtro.tipo, filtro.estado, filtro.ubicacion, filtro.responsable]);

  /**
   * Vuelve a pedir el listado. Se hace tocando el filtro y no llamando a la
   * búsqueda a mano: así el efecto que ya maneja el descarte de respuestas
   * viejas sigue siendo el único que consulta.
   */
  function recargar() {
    setCreando(false);
    setFiltro((f) => ({ ...f, pagina: 1 }));
  }

  // El texto se debouncea: sin esto, cada tecla dispara una consulta con LIKE
  // sobre la tabla entera. Si el texto no cambió respecto del filtro vigente se
  // devuelve el mismo objeto, y ahí React no vuelve a renderizar: sin eso el
  // montaje disparaba dos consultas idénticas, la del filtro inicial y la del
  // debounce asentándose con el buscador vacío.
  useEffect(() => {
    const t = setTimeout(() => {
      const limpio = texto.trim() || undefined;
      setFiltro((f) => (f.texto === limpio ? f : { ...f, texto: limpio, pagina: 1 }));
    }, 300);
    return () => clearTimeout(t);
  }, [texto]);

  useEffect(() => {
    let vigente = true;
    setCargando(true);
    setError(null);

    api.equipos
      .buscar(filtro)
      .then((r) => {
        // Una respuesta que llega tarde no debe pisar a una búsqueda posterior.
        if (vigente) setPagina(r);
      })
      .catch((ex) => {
        if (!vigente) return;
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarListado', 'No se pudo cargar el listado.'));
      })
      .finally(() => {
        if (vigente) setCargando(false);
      });

    return () => {
      vigente = false;
    };
  }, [filtro, cerrarSiExpiro]);

  const contexto = useMemo(() => {
    if (!pagina) return t('comun.cargando', 'Cargando…');

    const partes = [
      p(pagina.total, 'activo.registrado',
        '1 equipo registrado', '{n} equipos registrados'),
    ];
    if (pagina.totalPaginas > 1) {
      partes.push(t('comun.paginaDeTotal', 'página {pagina} de {total}',
                    { pagina: pagina.pagina, total: pagina.totalPaginas }));
    }
    const operativos = pagina.items.filter((e) => e.estadoOperativo).length;
    if (pagina.items.length > 0) {
      partes.push(p(operativos, 'activo.operativoEnPagina',
                    '1 operativo en esta página', '{n} operativos en esta página'));
    }
    return partes.join(' · ');
  }, [pagina, t, p]);

  function cambiar(campo: keyof FiltroEquipos, valor: string) {
    setFiltro((f) => ({ ...f, [campo]: valor || undefined, pagina: 1 }));
  }

  function limpiar() {
    setTexto('');
    setFiltro({ pagina: 1, porPagina: POR_PAGINA });
  }

  /** Pulsar la píldora activa la que no estaba, y vuelve a todo la que sí. */
  function alternarSegmento(valor: string) {
    setFiltro((f) => ({ ...f, segmento: f.segmento === valor ? undefined : valor, pagina: 1 }));
  }

  const hayFiltros =
    Boolean(texto) ||
    Boolean(filtro.tipo || filtro.estado || filtro.ubicacion || filtro.responsable
            || filtro.segmento);

  // Los tres son preguntas, no filtros por columna: qué está por romperse, a
  // qué le debo el preventivo, y qué ya no tiene garantía.
  const PILDORAS = [
    { valor: '', clave: 'activo.segTodos', texto: 'Todos', n: segmentos?.todos },
    { valor: 'riesgoAlto', clave: 'activo.segRiesgoAlto', texto: 'Riesgo alto',
      n: segmentos?.riesgoAlto, tono: 'alto' },
    { valor: 'preventivoVencido', clave: 'activo.segPreventivo', texto: 'Preventivo vencido',
      n: segmentos?.preventivoVencido, tono: 'medio' },
    { valor: 'garantiaVencida', clave: 'activo.segGarantia', texto: 'Garantía vencida',
      n: segmentos?.garantiaVencida },
  ] as const;

  return (
    <>
      <Encabezado
        titulo={t('activo.titulo', 'Activos')}
        contexto={contexto}
        acciones={
          puede(Patentes.equipoGestionar) ? (
            <>
              <button
                type="button"
                className="btn"
                disabled
                title={t('activo.cargaMasivaFuera', 'Fuera del alcance de esta versión: la carga masiva necesita una plantilla y una previsualización que no están definidas')}
              >
                <IconoImportar />
                {t('activo.importarPlanilla', 'Importar desde planilla')}
              </button>
              <button
                type="button"
                className="btn pri"
                disabled={!catalogos}
                onClick={() => setCreando((v) => !v)}
              >
                {creando ? t('comun.cancelar', 'Cancelar') : t('activo.nuevo', 'Nuevo equipo')}
              </button>
            </>
          ) : null
        }
      />

      <div className="cuerpo pagina-activos">
        {creando && catalogos ? (
          <FormularioEquipo
            catalogos={catalogos}
            alGuardar={recargar}
            alCancelar={() => setCreando(false)}
          />
        ) : null}

        <div className="filtros">
          <div className="campo buscador">
            <IconoBuscar />
            <input
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
              placeholder={t('activo.buscarPlaceholder', 'Código, serie, marca o responsable…')}
              aria-label={t('activo.buscar', 'Buscar equipos')}
            />
          </div>

          <select
            className="campo"
            value={filtro.tipo ?? ''}
            onChange={(e) => cambiar('tipo', e.target.value)}
            aria-label={t('activo.tipoEquipo', 'Tipo de equipo')}
          >
            <option value="">{t('comun.tipo', 'Tipo')}</option>
            {catalogos?.tipos.map((t) => (
              <option key={t.id} value={t.id}>
                {t.nombre}
              </option>
            ))}
          </select>

          <select
            className="campo"
            value={filtro.estado ?? ''}
            onChange={(e) => cambiar('estado', e.target.value)}
            aria-label={t('comun.estado', 'Estado')}
          >
            <option value="">{t('comun.estado', 'Estado')}</option>
            {catalogos?.estados.map((t) => (
              <option key={t.id} value={t.id}>
                {t.nombre}
              </option>
            ))}
          </select>

          <select
            className="campo"
            value={filtro.ubicacion ?? ''}
            onChange={(e) => cambiar('ubicacion', e.target.value)}
            aria-label={t('activo.ubicacion', 'Ubicación')}
          >
            <option value="">{t('activo.ubicacion', 'Ubicación')}</option>
            {catalogos?.ubicaciones.map((t) => (
              <option key={t.id} value={t.id}>
                {t.nombre}
              </option>
            ))}
          </select>

          <select
            className="campo"
            value={filtro.responsable ?? ''}
            onChange={(e) => cambiar('responsable', e.target.value)}
            aria-label={t('activo.responsable', 'Responsable')}
          >
            <option value="">{t('activo.responsable', 'Responsable')}</option>
            {catalogos?.responsables.map((t) => (
              <option key={t.id} value={t.id}>
                {t.nombreCompleto}
              </option>
            ))}
          </select>

          <button type="button" className="btn plano" onClick={limpiar} disabled={!hayFiltros}>
            {t('comun.limpiar', 'Limpiar')}
          </button>

          {/* Las píldoras van en su propio renglón y después de los
              desplegables: son atajos a lo que hay que mirar, no un filtro más
              de la fila. `aria-pressed` es lo que le dice a un lector de
              pantalla que quedó activa; el color solo no alcanza. */}
          <div className="pildoras" role="group"
               aria-label={t('activo.segmentos', 'Segmentos del inventario')}>
            {PILDORAS.map(({ valor, clave, texto: rotulo, n, ...resto }) => {
              const activa = (filtro.segmento ?? '') === valor;
              const tono = 'tono' in resto && n ? ` ${resto.tono}` : '';
              return (
                <button
                  key={valor || 'todos'}
                  type="button"
                  className={`pildora${activa ? ' activa' : ''}${tono}`}
                  aria-pressed={activa}
                  onClick={() => (valor ? alternarSegmento(valor)
                                        : setFiltro((f) => ({ ...f, segmento: undefined, pagina: 1 })))}
                >
                  {t(clave, rotulo)}
                  {n === undefined ? null : <span className="mono">{n}</span>}
                </button>
              );
            })}
          </div>
        </div>

        {error ? (
          <div className="aviso error" role="alert">
            {error}
          </div>
        ) : null}

        <div className="tarjeta">
          <table>
            <thead>
              <tr>
                <th style={{ width: 130 }}>{t('activo.codigo', 'Código')}</th>
                <th style={{ width: 140 }}>{t('comun.tipo', 'Tipo')}</th>
                <th>{t('activo.marcaModelo', 'Marca y modelo')}</th>
                <th style={{ width: 150 }}>{t('activo.ubicacion', 'Ubicación')}</th>
                <th style={{ width: 160 }}>{t('activo.responsable', 'Responsable')}</th>
                <th style={{ width: 150 }}>{t('comun.estado', 'Estado')}</th>
                <th style={{ width: 60 }} className="derecha">
                  {t('comun.ver', 'Ver')}
                </th>
              </tr>
            </thead>
            <tbody>
              {pagina?.items.map((e) => (
                <tr key={e.id}>
                  <td>
                    <Link to={`/activos/${e.id}`} className="mono">
                      {e.codigo}
                    </Link>
                  </td>
                  <td>{e.tipo}</td>
                  <td>
                    <span style={{ color: 'var(--tx)' }}>{e.marcaModelo}</span>
                    {e.numeroSerie ? <div className="sub">S/N {e.numeroSerie}</div> : null}
                  </td>
                  <td>{e.ubicacion ?? '—'}</td>
                  <td>{e.responsable ?? '—'}</td>
                  <td>
                    <EstadoEquipo estado={e.estado} operativo={e.estadoOperativo} />
                  </td>
                  <td className="derecha">
                    <Link to={`/activos/${e.id}`} style={{ fontSize: 'var(--txt-tabla)', fontWeight: 600 }}>
                      {t('comun.ver', 'Ver')}
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {cargando && !pagina ? <EsqueletoTabla columnas={7} /> : null}
          {!cargando && pagina?.items.length === 0 ? (
            hayFiltros ? (
              <SinDatos
                filtrado
                titulo={t('vacio.equipoSinCoincidencia', 'Ningún equipo coincide con la búsqueda')}
                detalle={t('vacio.equipoFiltroAyuda', 'Probá con menos filtros, o limpiálos para ver el parque completo.')}
                accion={
                  <button type="button" className="btn" onClick={limpiar}>
                    {t('comun.limpiarFiltros', 'Limpiar los filtros')}
                  </button>
                }
              />
            ) : (
              <SinDatos
                titulo={t('vacio.sinEquipos', 'Todavía no hay equipos cargados')}
                detalle={t('vacio.sinEquiposAyuda', 'El parque es la base de todo lo demás: sin equipos no hay incidencias que registrar ni riesgo que calcular.')}
              />
            )
          ) : null}

          {pagina && pagina.totalPaginas > 1 ? (
            <div className="pie-tabla">
              <span>
                {t('activo.mostrandoDeTotal', 'Mostrando {n} de {total} equipos', {
                  n: pagina.items.length,
                  total: pagina.total,
                })}
              </span>
              <Paginador
                pagina={pagina.pagina}
                total={pagina.totalPaginas}
                onIr={(p) => setFiltro((f) => ({ ...f, pagina: p }))}
              />
            </div>
          ) : null}
        </div>
      </div>
    </>
  );
}

/**
 * Paginador acotado a una ventana de cinco números.
 *
 * Con 52 equipos son 3 páginas, pero el parque proyectado llega a 1.000 (RNF-04)
 * y ahí serían 50 botones.
 */
function Paginador({
  pagina,
  total,
  onIr,
}: {
  pagina: number;
  total: number;
  onIr: (p: number) => void;
}) {
  const t = useT();
  const desde = Math.max(1, Math.min(pagina - 2, total - 4));
  const hasta = Math.min(total, desde + 4);
  const numeros = [];
  for (let i = desde; i <= hasta; i++) numeros.push(i);

  return (
    <div className="paginador">
      <button type="button" onClick={() => onIr(pagina - 1)} disabled={pagina <= 1} aria-label={t('comun.anterior', 'Anterior')}>
        ‹
      </button>
      {numeros.map((n) => (
        <button
          key={n}
          type="button"
          className={n === pagina ? 'actual' : undefined}
          onClick={() => onIr(n)}
          aria-current={n === pagina ? 'page' : undefined}
        >
          {n}
        </button>
      ))}
      <button
        type="button"
        onClick={() => onIr(pagina + 1)}
        disabled={pagina >= total}
        aria-label={t('comun.siguiente', 'Siguiente')}
      >
        ›
      </button>
    </div>
  );
}
