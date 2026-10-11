import { useEffect, useState } from 'react';
import { useT } from '../idioma/IdiomaContext';
import { Link, useNavigate } from 'react-router-dom';
import { api, ErrorApi } from '../api/cliente';
import type {
  CatalogosIncidenciaDto,
  EquipoListaDto,
  IncidenciaRegistradaDto,
} from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { ErrorDeFormulario, marcaDeError } from '../componentes/Estados';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';
import { Patentes } from '../api/tipos';

/** Id del aviso de error: lo referencia el campo que lo causo. */
const ID_ERROR = 'error-incidencia-nueva';

/**
 * Alta de incidencia (CU-006).
 *
 * La categoría y la prioridad son opcionales a propósito: quien reporta es
 * alguien de administración, no un técnico. Si las deja vacías, el triage las
 * sugiere; si las carga, lo que cargó manda.
 */
export function IncidenciaNueva() {
  const t = useT();
  const navegar = useNavigate();
  const cerrarSiExpiro = useCerrarSiExpiro();
  const { puede } = useSesion();

  const [catalogos, setCatalogos] = useState<CatalogosIncidenciaDto | null>(null);
  const [equipos, setEquipos] = useState<EquipoListaDto[]>([]);

  const [titulo, setTitulo] = useState('');
  const [descripcion, setDescripcion] = useState('');
  const [idEquipo, setIdEquipo] = useState('');
  const [idCategoria, setIdCategoria] = useState('');
  const [idPrioridad, setIdPrioridad] = useState('');

  const [error, setError] = useState<string | null>(null);
  const [campoConError, setCampoConError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [resultado, setResultado] = useState<IncidenciaRegistradaDto | null>(null);

  useEffect(() => {
    api.incidencias.catalogos().then(setCatalogos).catch(cerrarSiExpiro);

    // Quién ve el inventario completo elige de ahí; quién no, elige entre los
    // equipos de los que es responsable.
    //
    // Esto no era un detalle: el formulario pedía el inventario completo
    // siempre, el solicitante recibía 403, el desplegable quedaba vacío y el
    // error se tragaba. La única acción de ese perfil era imposible y la
    // pantalla no decía por qué.
    //
    // Se traen los primeros 200 equipos. Con el parque proyectado de 1.000
    // (RNF-04) esto pide un buscador, no una lista; queda anotado y no
    // disimulado con una lista truncada en silencio.
    const pedido = puede(Patentes.equipoVer)
      ? api.equipos.buscar({ porPagina: 200 })
      : api.equipos.mios();

    pedido
      .then((p) => setEquipos(p.items))
      .catch((ex) => {
        cerrarSiExpiro(ex);
        // Sin equipos no se puede registrar nada: se dice, en lugar de
        // ofrecer un desplegable vacío.
        setError(ex instanceof ErrorApi
          ? ex.message
          : t('error.cargarMisEquipos', 'No se pudieron cargar tus equipos.'));
      });
  }, [cerrarSiExpiro]);

  function enviar(e: React.FormEvent) {
    e.preventDefault();
    setEnviando(true);
    setError(null);
    setCampoConError(null);

    api.incidencias
      .registrar({
        titulo,
        descripcion,
        idEquipo,
        idCategoria: idCategoria || undefined,
        idPrioridad: idPrioridad || undefined,
      })
      .then(setResultado)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        if (ex instanceof ErrorApi) {
          setError(ex.message);
          setCampoConError(ex.campo ?? null);
        } else {
          setError(t('error.registrarIncidencia', 'No se pudo registrar la incidencia.'));
        }
      })
      .finally(() => setEnviando(false));
  }

  if (resultado) {
    return (
      <>
        <Encabezado
          titulo={`Incidencia #${String(resultado.numero).padStart(3, '0')} registrada`}
          contexto={t('incidencia.quedoCargado', 'El reporte quedó cargado')}
        />

        <div className="cuerpo pagina-alta">
          <div style={{ maxWidth: 640, display: 'flex', flexDirection: 'column', gap: 16 }}>
            {/*
              Si hubo que aplicar el respaldo, se lo dice al usuario con todas
              las letras. Un sistema degradado que no lo dice es peor que uno
              caído (ADR 0009).
            */}
            {resultado.advertencia ? (
              <div className="aviso atencion">{resultado.advertencia}</div>
            ) : null}

            <div className="tarjeta">
              <header>
                <h2>{t('incidencia.queHizoElSistema', 'Qué hizo el sistema')}</h2>
              </header>
              <dl className="dl">
                <dt>{t('incidencia.clasificacionSeccion', 'Clasificación')}</dt>
                <dd>
                  {resultado.clasificacion?.categoriaSugerida
                  ?? t('comun.sinDeterminar', 'No se pudo determinar')}
                  {resultado.clasificacion?.prioridadSugerida
                    ? ` · prioridad ${resultado.clasificacion.prioridadSugerida}`
                    : ''}
                </dd>
                <dt>Asignada a</dt>
                <dd>
                {resultado.asignacion?.tecnico ?? t('incidencia.sinAsignar', 'Sin asignar')}
              </dd>
                {resultado.asignacion?.justificacion ? (
                  <>
                    <dt>{t('comun.porQue', 'Por qué')}</dt>
                    <dd>{resultado.asignacion.justificacion}</dd>
                  </>
                ) : null}
                {resultado.clasificacion?.numeroIncidenciaDuplicada ? (
                  <>
                    <dt>{t('incidencia.atencion', 'Atención')}</dt>
                    <dd>
                      Parece repetir la incidencia #
                      {String(resultado.clasificacion.numeroIncidenciaDuplicada).padStart(3, '0')}.
                    </dd>
                  </>
                ) : null}
              </dl>
            </div>

            <div style={{ display: 'flex', gap: 8 }}>
              <Link to={`/incidencias/${resultado.id}`} className="btn pri">
                {t('incidencia.verLaIncidencia', 'Ver la incidencia')}
              </Link>
              <Link to="/incidencias" className="btn">
                {t('incidencia.volverAlListado', 'Volver al listado')}
              </Link>
            </div>
          </div>
        </div>
      </>
    );
  }

  return (
    <>
      <Encabezado
        titulo={t('incidencia.registrar', 'Registrar incidencia')}
        contexto={t('incidencia.altaContexto',
                    'El sistema propone categoría, prioridad y técnico; el técnico puede corregirlos.')}
      />

      <div className="cuerpo pagina-alta">
        <form onSubmit={enviar} style={{ maxWidth: 640 }}>
          <ErrorDeFormulario id={ID_ERROR} mensaje={error} />

          <div className="tarjeta">
            <header>
              <h2>{t('comun.quePaso', 'Qué pasó')}</h2>
            </header>

            <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 14 }}>
              <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
                <span className="sub">{t('incidencia.equipo', 'Equipo')}</span>
                <select
                  className="campo"
                  value={idEquipo}
                  onChange={(e) => setIdEquipo(e.target.value)}
                  required
                  {...marcaDeError('idEquipo', campoConError, ID_ERROR)}
                >
                  <option value="">{t('comun.elegirEquipo', 'Elegir equipo…')}</option>
                  {equipos.map((eq) => (
                    <option key={eq.id} value={eq.id}>
                      {eq.codigo} — {eq.tipo}
                      {eq.ubicacion ? ` (${eq.ubicacion})` : ''}
                    </option>
                  ))}
                </select>
              </label>

              <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
                <span className="sub">{t('incidencia.asunto', 'Título')}</span>
                <input
                  className="campo"
                  value={titulo}
                  onChange={(e) => setTitulo(e.target.value)}
                  placeholder={t('incidencia.tituloEjemplo', 'No imprime en red')}
                  maxLength={150}
                  required
                  {...marcaDeError('titulo', campoConError, ID_ERROR)}
                />
              </label>

              <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
                <span className="sub">{t('incidencia.descripcion', 'Descripción')}</span>
                <textarea
                  className="campo"
                  value={descripcion}
                  onChange={(e) => setDescripcion(e.target.value)}
                  placeholder={t('incidencia.descripcionEjemplo', 'Contá qué pasa, desde cuándo y qué probaste.')}
                  rows={5}
                  maxLength={2000}
                  required
                  {...marcaDeError('descripcion', campoConError, ID_ERROR)}
                  style={{ resize: 'vertical', minHeight: 96, paddingTop: 8 }}
                />
                <span className="sub">{descripcion.length} / 2000</span>
              </label>
            </div>
          </div>

          <div className="tarjeta" style={{ marginTop: 16 }}>
            <header>
              <h2>{t('incidencia.clasificacionSeccion', 'Clasificación')}</h2>
              <span className="nota">{t('comun.opcional', 'Opcional')}</span>
            </header>

            <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 14 }}>
              <p className="sub" style={{ margin: 0 }}>
                {t('incidencia.dejarVacioNota', 'Si no sabés qué elegir, dejalo vacío: el sistema lo sugiere a partir de lo que escribiste y el técnico lo puede corregir.')}
              </p>

              <div style={{ display: 'flex', gap: 12 }}>
                <label style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 5 }}>
                  <span className="sub">{t('incidencia.categoria', 'Categoría')}</span>
                  <select
                    className="campo"
                    value={idCategoria}
                    onChange={(e) => setIdCategoria(e.target.value)}
                  >
                    <option value="">{t('incidencia.queLoSugieraElSistema', 'Que lo sugiera el sistema')}</option>
                    {catalogos?.categorias.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.nombre}
                      </option>
                    ))}
                  </select>
                </label>

                <label style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 5 }}>
                  <span className="sub">{t('incidencia.prioridad', 'Prioridad')}</span>
                  <select
                    className="campo"
                    value={idPrioridad}
                    onChange={(e) => setIdPrioridad(e.target.value)}
                  >
                    <option value="">{t('incidencia.queLoSugieraElSistema', 'Que lo sugiera el sistema')}</option>
                    {catalogos?.prioridades.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.nombre}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
            </div>
          </div>

          <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
            <button type="submit" className="btn pri" disabled={enviando}>
              {enviando ? t('incidencia.registrando', 'Registrando…') : t('incidencia.registrar', 'Registrar incidencia')}
            </button>
            <button
              type="button"
              className="btn"
              onClick={() => navegar('/incidencias')}
              disabled={enviando}
            >
              {t('comun.cancelar', 'Cancelar')}
            </button>
          </div>
        </form>
      </div>
    </>
  );
}
