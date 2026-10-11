import { useEffect, useMemo, useState } from 'react';
import { useHace } from '../idioma/hace';
import { usePlural } from '../idioma/plural';
import { useT } from '../idioma/IdiomaContext';
import { Link } from 'react-router-dom';
import { api, ErrorApi } from '../api/cliente';
import type {
  CatalogosIncidenciaDto,
  FiltroIncidencias,
  IncidenciaListaDto,
  PaginaDto,
} from '../api/tipos';
import { Patentes } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { EsqueletoTabla, SinDatos } from '../componentes/Estados';
import { IconoBuscar } from '../componentes/Iconos';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';
import { EstadoIncidencia, OrigenAsignacion, Prioridad } from '../componentes/Semantica';

const POR_PAGINA = 20;

/**
 * Cuántas trae el tablero.
 *
 * Un tablero paginado no es un tablero: la gracia es ver de una dónde está
 * parado el trabajo, y «página 1 de 3» esconde justo lo que se vino a mirar.
 * El tope existe igual, porque una organización con mil incidencias cerradas
 * no tiene que traerlas todas para dibujar cuatro columnas; cuando se corta,
 * el tablero lo dice en vez de mostrar un recorte silencioso.
 */
const POR_TABLERO = 200;

/**
 * Cuántas terminadas muestra su columna.
 *
 * Las columnas abiertas se muestran enteras: esconder trabajo en curso es
 * justo lo contrario de para qué sirve un tablero. La de terminadas no: crece
 * sin techo y nadie actúa sobre ella desde acá. Con veinte tarjetas ocupaba
 * cuatro pantallas de alto y aplastaba a las columnas que importan.
 */
const TOPE_TERMINADAS = 6;

/**
 * El tablero: una columna por estado, una tarjeta por incidencia.
 *
 * Es de sólo lectura y no se arrastra, a propósito. El ciclo de vida de la
 * incidencia lo valida la capa de negocio (ADR 0012) y no toda transición está
 * permitida desde todo estado; arrastrar sugiere que cualquier movimiento vale
 * y descubrir que no recién al soltar. El estado se cambia en el detalle, que
 * es donde el sistema muestra qué transiciones admite.
 *
 * Y arrastrar traería su propio problema: la WCAG pide una alternativa por
 * teclado para toda acción de arrastre. Acá cada tarjeta ya es un enlace, que
 * es accesible sin agregar nada.
 */
function Tablero({ columnas, cargando, recortado, mostradas, total }: {
  columnas: {
    clave: string; rotulo: string; tarjetas: IncidenciaListaDto[];
    ocultas?: number; total?: number;
  }[];
  cargando: boolean;
  recortado: boolean;
  mostradas: number;
  total: number;
}) {
  const t = useT();

  if (cargando) {
    return <div className="vacio">{t('comun.cargando', 'Cargando…')}</div>;
  }

  return (
    <>
      <div className="tablero">
        {columnas.map((c) => (
          <section className="tablero-columna" key={c.clave}>
            <header>
              <span>{c.rotulo}</span>
              {/* El recuento es el de la columna entera y no el de lo que se
                  dibuja: si dijera seis, la columna estaría mintiendo sobre
                  cuántas hay. */}
              <span className="mono">{c.total ?? c.tarjetas.length}</span>
            </header>

            {c.tarjetas.length === 0 ? (
              <p className="tablero-vacia">{t('incidencia.columnaVacia', 'Nada acá')}</p>
            ) : (
              c.tarjetas.map((i) => (
                <Link className="tablero-tarjeta" key={i.id} to={`/incidencias/${i.id}`}>
                  <div className="tablero-tarjeta-cabeza">
                    <span className="mono">#{String(i.numero).padStart(3, '0')}</span>
                    <Prioridad nombre={i.prioridad} nivel={i.nivelPrioridad} />
                  </div>
                  <div className="tablero-titulo">{i.titulo}</div>
                  <div className="sub">
                    <span className="mono">{i.codigoEquipo}</span>
                    {i.tecnico ? ` · ${i.tecnico}` : ''}
                  </div>
                  {i.fueraDeObjetivo && i.abierta ? (
                    <div className="sub" style={{ color: 'var(--alto-tx)' }}>
                      {t('incidencia.fueraObjetivo', 'Fuera del objetivo de resolución')}
                    </div>
                  ) : null}
                </Link>
              ))
            )}

            {c.ocultas ? (
              <p className="tablero-vacia">
                {t('incidencia.yMasTerminadas', 'y {n} más', { n: c.ocultas })}
              </p>
            ) : null}
          </section>
        ))}
      </div>

      {/* Un tablero recortado que no lo dice miente sobre el estado del
          trabajo: parece que hay menos de lo que hay. */}
      {recortado ? (
        <p className="sub" style={{ marginTop: 10 }}>
          {/* El número sale de la respuesta y no de la constante: si el
              tamaño de página fuera otro, la constante estaría declarando una
              cantidad distinta de la que hay en pantalla. */}
          {t('incidencia.tableroRecortado',
             'El tablero muestra las {n} más recientes de {total}. Filtrá para ver el resto.',
             { n: mostradas, total })}
        </p>
      ) : null}
    </>
  );
}

export function Incidencias() {
  const p = usePlural();
  const t = useT();
  const hace = useHace();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [filtro, setFiltro] = useState<FiltroIncidencias>({ pagina: 1, porPagina: POR_PAGINA });
  const [texto, setTexto] = useState('');
  const [pagina, setPagina] = useState<PaginaDto<IncidenciaListaDto> | null>(null);
  const [catalogos, setCatalogos] = useState<CatalogosIncidenciaDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);
  const [vista, setVista] = useState<'lista' | 'tablero'>('lista');

  /** Cambiar de vista cambia cuántas se piden: el tablero las quiere todas. */
  function cambiarVista(cual: 'lista' | 'tablero') {
    setVista(cual);
    setFiltro((f) => ({
      ...f, pagina: 1, porPagina: cual === 'tablero' ? POR_TABLERO : POR_PAGINA,
    }));
  }

  /**
   * Las columnas del tablero: una por estado no terminal, y una última con
   * todo lo que ya cerró.
   *
   * Salen del catálogo y no de una lista escrita acá: los estados los define
   * la base, vienen en el orden del ciclo, y `esFinal` es un dato del dominio
   * justamente porque una organización puede renombrarlos. Las tres terminales
   * —resuelta, cerrada, anulada— se juntan en una: un tablero muestra dónde
   * está el trabajo, y lo terminado no tiene tres lugares distintos donde
   * estar.
   */
  const columnas = useMemo(() => {
    const estados = catalogos?.estados ?? [];
    const items = pagina?.items ?? [];

    const abiertas = estados
      .filter((e) => !e.esFinal)
      .map((e) => ({
        clave: e.id,
        rotulo: e.nombre,
        tarjetas: items.filter((i) => i.estado === e.nombre),
      }));

    const nombresFinales = new Set(estados.filter((e) => e.esFinal).map((e) => e.nombre));
    const cerradas = items.filter((i) => nombresFinales.has(i.estado));

    return [
      ...abiertas,
      {
        clave: 'terminadas',
        rotulo: t('incidencia.terminadas', 'Terminadas'),
        tarjetas: cerradas.slice(0, TOPE_TERMINADAS),
        ocultas: Math.max(0, cerradas.length - TOPE_TERMINADAS),
        total: cerradas.length,
      },
    ];
  }, [catalogos, pagina, t]);

  useEffect(() => {
    api.incidencias.catalogos().then(setCatalogos).catch(cerrarSiExpiro);
  }, [cerrarSiExpiro]);

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

    api.incidencias
      .buscar(filtro)
      .then((r) => {
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

    const abiertas = pagina.items.filter((i) => i.abierta).length;
    const sinAsignar = pagina.items.filter((i) => i.tipoAsignacion === 'PENDIENTE').length;
    const porRespaldo = pagina.items.filter((i) => i.pendienteRevision).length;

    const partes = [
      p(pagina.total, 'incidencia.contadas', '1 incidencia', '{n} incidencias'),
      p(abiertas, 'incidencia.abiertaEnPagina',
        '1 abierta en esta página', '{n} abiertas en esta página'),
    ];
    if (sinAsignar > 0) {
      partes.push(p(sinAsignar, 'incidencia.contadasSinAsignar',
                    '1 sin asignar', '{n} sin asignar'));
    }
    if (porRespaldo > 0) {
      partes.push(p(porRespaldo, 'incidencia.contadasPorRespaldo',
                    '1 asignada por respaldo', '{n} asignadas por respaldo'));
    }
    return partes.join(' · ');
  }, [pagina, t, p]);

  /** Incidencias que la asignación de respaldo dejó marcadas (CP-14). */
  const pendientesDeRevision = pagina?.items.filter((i) => i.pendienteRevision).length ?? 0;

  function cambiar(campo: keyof FiltroIncidencias, valor: string) {
    setFiltro((f) => ({ ...f, [campo]: valor || undefined, pagina: 1 }));
  }

  function limpiar() {
    setTexto('');
    // El tamaño de página lo decide la vista y no los filtros: reescribir el
    // filtro entero con el de la lista dejaba al tablero pidiendo veinte
    // mientras seguía dibujándose como tablero, y las incidencias que no
    // entraban desaparecían de las columnas sin que nada lo explicara.
    setFiltro({ pagina: 1, porPagina: vista === 'tablero' ? POR_TABLERO : POR_PAGINA });
  }

  const hayFiltros =
    Boolean(texto) ||
    Boolean(
      filtro.estado || filtro.prioridad || filtro.categoria || filtro.tecnico || filtro.revision,
    );

  return (
    <>
      <Encabezado
        titulo={t('incidencia.titulo', 'Incidencias')}
        contexto={contexto}
        acciones={
          puede(Patentes.incidenciaRegistrar) ? (
            <Link to="/incidencias/nueva" className="btn pri">
              {t('incidencia.registrar', 'Registrar incidencia')}
            </Link>
          ) : null
        }
      />

      <div className="cuerpo pagina-incidencias">
        {/*
          El aviso sólo aparece cuando hay algo que revisar. Un sistema degradado
          que no lo dice es peor que uno caído (ADR 0009).
        */}
        {pendientesDeRevision > 0 && !filtro.revision ? (
          <div className="aviso atencion" style={{ marginBottom: 12 }}>
            <span>
              {pendientesDeRevision === 1
                ? t('incidencia.unaPorRespaldo',
                    '1 incidencia fue asignada por respaldo y está pendiente de revisión.')
                : t('incidencia.variasPorRespaldo',
                    '{n} incidencias fueron asignadas por respaldo y están pendientes de revisión.',
                    { n: pendientesDeRevision })}
            </span>
            <button
              type="button"
              className="btn plano"
              style={{ marginLeft: 10 }}
              onClick={() => setFiltro((f) => ({ ...f, revision: true, pagina: 1 }))}
            >
              {t('incidencia.revisarlas', 'Revisarlas')}
            </button>
          </div>
        ) : null}

        {filtro.revision ? (
          <div className="aviso info" style={{ marginBottom: 12 }}>
            {t('incidencia.soloRespaldo', 'Mostrando sólo las asignadas por respaldo.')}
            <button
              type="button"
              className="btn plano"
              style={{ marginLeft: 10 }}
              onClick={() => setFiltro((f) => ({ ...f, revision: undefined, pagina: 1 }))}
            >
              {t('comun.verTodas', 'Ver todas')}
            </button>
          </div>
        ) : null}

        <div className="filtros">
          <div className="campo buscador">
            <IconoBuscar />
            <input
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
              placeholder={t('incidencia.buscarPlaceholder', 'Número, título o equipo…')}
              aria-label={t('incidencia.buscar', 'Buscar incidencias')}
            />
          </div>

          <select
            className="campo"
            value={filtro.estado ?? ''}
            onChange={(e) => cambiar('estado', e.target.value)}
            aria-label={t('comun.estado', 'Estado')}
          >
            <option value="">{t('comun.estado', 'Estado')}</option>
            {catalogos?.estados.map((e) => (
              <option key={e.id} value={e.id}>
                {e.nombre}
              </option>
            ))}
          </select>

          <select
            className="campo"
            value={filtro.prioridad ?? ''}
            onChange={(e) => cambiar('prioridad', e.target.value)}
            aria-label={t('incidencia.prioridad', 'Prioridad')}
          >
            <option value="">{t('incidencia.prioridad', 'Prioridad')}</option>
            {catalogos?.prioridades.map((p) => (
              <option key={p.id} value={p.id}>
                {p.nombre}
              </option>
            ))}
          </select>

          <select
            className="campo"
            value={filtro.categoria ?? ''}
            onChange={(e) => cambiar('categoria', e.target.value)}
            aria-label={t('incidencia.categoria', 'Categoría')}
          >
            <option value="">{t('incidencia.categoria', 'Categoría')}</option>
            {catalogos?.categorias.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nombre}
              </option>
            ))}
          </select>

          {/* El selector de técnico sólo tiene sentido para quien ve las de todos. */}
          {puede(Patentes.incidenciaVerTodas) ? (
            <select
              className="campo"
              value={filtro.tecnico ?? ''}
              onChange={(e) => cambiar('tecnico', e.target.value)}
              aria-label={t('incidencia.tecnico', 'Técnico')}
            >
              <option value="">{t('incidencia.tecnico', 'Técnico')}</option>
              {catalogos?.tecnicos.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nombreCompleto}
                </option>
              ))}
            </select>
          ) : null}

          <button type="button" className="btn plano" onClick={limpiar} disabled={!hayFiltros}>
            {t('comun.limpiar', 'Limpiar')}
          </button>

          {/* Las dos vistas de lo mismo. Los filtros valen para las dos: lo que
              cambia es cómo se muestra, no qué se muestra. */}
          <div className="pildoras" role="group"
               aria-label={t('incidencia.vista', 'Cómo ver las incidencias')}>
            <button type="button" className={`pildora${vista === 'lista' ? ' activa' : ''}`}
                    aria-pressed={vista === 'lista'} onClick={() => cambiarVista('lista')}>
              {t('incidencia.vistaLista', 'Lista')}
            </button>
            <button type="button" className={`pildora${vista === 'tablero' ? ' activa' : ''}`}
                    aria-pressed={vista === 'tablero'} onClick={() => cambiarVista('tablero')}>
              {t('incidencia.vistaTablero', 'Tablero')}
            </button>
          </div>
        </div>

        {error ? (
          <div className="aviso error" role="alert">
            {error}
          </div>
        ) : null}

        {vista === 'tablero' ? (
          <Tablero columnas={columnas} cargando={cargando && !pagina}
                   recortado={Boolean(pagina && pagina.total > pagina.items.length)}
                   mostradas={pagina?.items.length ?? 0} total={pagina?.total ?? 0} />
        ) : (
        <div className="tarjeta">
          <table>
            <thead>
              <tr>
                <th style={{ width: 70 }}>{t('incidencia.numero', 'N.º')}</th>
                <th>{t('incidencia.asunto', 'Título')}</th>
                <th style={{ width: 130 }}>{t('incidencia.equipo', 'Equipo')}</th>
                <th style={{ width: 120 }}>{t('incidencia.categoria', 'Categoría')}</th>
                <th style={{ width: 100 }}>{t('incidencia.prioridad', 'Prioridad')}</th>
                <th style={{ width: 175 }}>{t('comun.estado', 'Estado')}</th>
                <th style={{ width: 150 }}>{t('incidencia.tecnico', 'Técnico')}</th>
                <th style={{ width: 140 }}>{t('incidencia.asignacion', 'Asignación')}</th>
                <th style={{ width: 105 }}>{t('activo.antiguedad', 'Antigüedad')}</th>
              </tr>
            </thead>
            <tbody>
              {pagina?.items.map((i) => (
                <tr
                  key={i.id}
                  // La franja ámbar marca las asignaciones de respaldo pendientes
                  // de revisión: es la señal de que el sistema estuvo degradado.
                  style={
                    i.pendienteRevision
                      ? { boxShadow: 'inset 3px 0 0 var(--medio-barra)' }
                      : undefined
                  }
                >
                  <td>
                    <Link to={`/incidencias/${i.id}`} className="mono">
                      #{String(i.numero).padStart(3, '0')}
                    </Link>
                  </td>
                  <td>
                    <span style={{ color: 'var(--tx)' }}>{i.titulo}</span>
                    {i.fueraDeObjetivo && i.abierta ? (
                      <div className="sub" style={{ color: 'var(--alto-tx)' }}>
                        {t('incidencia.fueraObjetivo', 'Fuera del objetivo de resolución')}
                      </div>
                    ) : null}
                  </td>
                  <td className="mono">{i.codigoEquipo}</td>
                  <td>{i.categoria ?? '—'}</td>
                  <td>
                    <Prioridad nombre={i.prioridad} nivel={i.nivelPrioridad} />
                  </td>
                  <td>
                    <EstadoIncidencia estado={i.estado} abierta={i.abierta} />
                  </td>
                  <td>{i.tecnico ?? <span className="sub">{t('incidencia.sinAsignar', 'Sin asignar')}</span>}</td>
                  <td>
                    <OrigenAsignacion tipo={i.tipoAsignacion} revision={i.pendienteRevision} />
                  </td>
                  <td className="sub">{hace(i.fecha)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {cargando && !pagina ? <EsqueletoTabla columnas={6} /> : null}
          {!cargando && pagina?.items.length === 0 ? (
            <SinDatos
              filtrado
              titulo={t('vacio.incidenciaSinCoincidencia', 'Ninguna incidencia coincide con la búsqueda')}
              detalle={t('vacio.incidenciaFiltroAyuda', 'Probá con menos filtros. Si buscabas las abiertas y no hay, es una buena noticia.')}
            />
          ) : null}

          {pagina && pagina.totalPaginas > 1 ? (
            <div className="pie-tabla">
              <span>
                {t('incidencia.mostrandoDeTotal', 'Mostrando {n} de {total} incidencias', {
                  n: pagina.items.length,
                  total: pagina.total,
                })}
              </span>
              <div className="paginador">
                <button
                  type="button"
                  onClick={() => setFiltro((f) => ({ ...f, pagina: pagina.pagina - 1 }))}
                  disabled={pagina.pagina <= 1}
                  aria-label={t('comun.anterior', 'Anterior')}
                >
                  ‹
                </button>
                <span style={{ padding: '0 8px', fontSize: 'var(--txt-tabla)', color: 'var(--tx2)' }}>
                  {pagina.pagina} de {pagina.totalPaginas}
                </span>
                <button
                  type="button"
                  onClick={() => setFiltro((f) => ({ ...f, pagina: pagina.pagina + 1 }))}
                  disabled={pagina.pagina >= pagina.totalPaginas}
                  aria-label={t('comun.siguiente', 'Siguiente')}
                >
                  ›
                </button>
              </div>
            </div>
          ) : null}
        </div>
        )}

        {pagina && pagina.items.some((i) => i.pendienteRevision) ? (
          <p className="sub" style={{ marginTop: 10 }}>
            {t('incidencia.franjaAmbarNota', 'La franja ámbar marca las asignaciones de respaldo pendientes de revisión.')}
          </p>
        ) : null}
      </div>
    </>
  );
}
