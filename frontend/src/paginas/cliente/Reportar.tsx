import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, ErrorApi } from '../../api/cliente';
import type { EquipoListaDto, IncidenciaRegistradaDto } from '../../api/tipos';
import { Encabezado } from '../../componentes/Layout';
import { useT } from '../../idioma/IdiomaContext';
import { useCerrarSiExpiro } from '../../sesion/SesionContext';

/**
 * Reportar un problema — módulo del solicitante (CU-006).
 *
 * Es el mismo alta de incidencia que usa el técnico, con otra pantalla y por
 * una razón concreta: quien reporta no es técnico. El formulario del técnico
 * pide categoría y prioridad, y a alguien de administración no se le puede
 * preguntar si su problema es «Red» o «Rendimiento». Eso lo pone el triage.
 *
 * Tres pasos y cada uno hace una sola pregunta, porque se abre desde el
 * teléfono, parado al lado de una máquina que no arranca. El paso 3 muestra
 * exactamente lo que se va a mandar: nada viaja sin que la persona lo haya
 * leído.
 *
 * En todo el recorrido no aparecen las palabras score, riesgo, prioridad ni
 * categoría. El mismo dato que al técnico le sirve para decidir, al
 * solicitante sólo le da miedo o lo invita a discutir la prioridad.
 */
export function Reportar() {
  const t = useT();
  const navegar = useNavigate();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [equipos, setEquipos] = useState<EquipoListaDto[] | null>(null);
  const [paso, setPaso] = useState(1);

  const [idEquipo, setIdEquipo] = useState('');
  const [titulo, setTitulo] = useState('');
  const [descripcion, setDescripcion] = useState('');
  const [puedeTrabajar, setPuedeTrabajar] = useState<'si' | 'aMedias' | 'no' | ''>('');

  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [resultado, setResultado] = useState<IncidenciaRegistradaDto | null>(null);

  useEffect(() => {
    api.equipos
      .mios()
      .then((p) => setEquipos(p.items))
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(
          ex instanceof ErrorApi
            ? ex.message
            : t('error.cargarMisEquipos', 'No se pudieron cargar tus equipos.'),
        );
      });
  }, [cerrarSiExpiro]);

  const equipo = equipos?.find((e) => e.id === idEquipo);

  /**
   * Cómo llega al técnico lo que la persona contestó sobre si puede seguir
   * trabajando.
   *
   * Va al final de la descripción y no a la prioridad. La prioridad la decide
   * el triage con el historial del equipo; esto es un dato del que reporta, y
   * mezclarlos haría que una persona apurada pudiera subirse la prioridad
   * sola. Como frase suelta el técnico la lee igual, y el paso 3 la muestra
   * aparte para que nadie mande algo que no leyó.
   *
   * En primera persona porque termina dentro de «lo que escribiste», que es
   * como la propia persona vuelve a leer su pedido: en tercera sonaría a que
   * alguien más habló por ella.
   */
  const FRASE: Record<string, string> = {
    si: t('reportar.fraseSi', 'Puedo seguir usando el equipo.'),
    aMedias: t('reportar.fraseAMedias', 'Puedo seguir usándolo, pero con dificultad.'),
    no: t('reportar.fraseNo', 'No puedo seguir usando el equipo.'),
  };

  function enviar() {
    setEnviando(true);
    setError(null);

    const cierre = puedeTrabajar ? `\n\n${FRASE[puedeTrabajar]}` : '';

    api.incidencias
      .registrar({ titulo: titulo.trim(), descripcion: descripcion.trim() + cierre, idEquipo })
      .then(setResultado)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(
          ex instanceof ErrorApi
            ? ex.message
            : t('error.registrarIncidencia', 'No se pudo registrar el pedido.'),
        );
      })
      .finally(() => setEnviando(false));
  }

  if (resultado) return <Confirmacion resultado={resultado} />;

  return (
    <>
      <Encabezado
        titulo={t('reportar.titulo', 'Reportar un problema')}
        contexto={t('reportar.pasoDe', 'Paso {n} de 3').replace('{n}', String(paso))}
      />

      <div className="cuerpo">
        <div style={{ maxWidth: 620, display: 'flex', flexDirection: 'column', gap: 'var(--esp-4)' }}>
          {error ? (
            <div className="aviso error" role="alert">
              {error}
            </div>
          ) : null}

          {paso === 1 ? (
            <PasoEquipo
              equipos={equipos}
              elegido={idEquipo}
              onElegir={(id) => {
                setIdEquipo(id);
                setPaso(2);
              }}
            />
          ) : null}

          {paso === 2 ? (
            <PasoQuePasa
              titulo={titulo}
              descripcion={descripcion}
              puedeTrabajar={puedeTrabajar}
              setTitulo={setTitulo}
              setDescripcion={setDescripcion}
              setPuedeTrabajar={setPuedeTrabajar}
              onVolver={() => setPaso(1)}
              onSeguir={() => setPaso(3)}
            />
          ) : null}

          {paso === 3 ? (
            <PasoRevisar
              equipo={equipo}
              titulo={titulo}
              descripcion={descripcion}
              frase={puedeTrabajar ? FRASE[puedeTrabajar] ?? null : null}
              enviando={enviando}
              onCorregir={() => setPaso(2)}
              onEnviar={enviar}
            />
          ) : null}

          <button
            type="button"
            className="btn plano"
            onClick={() => navegar('/mis-equipos')}
            style={{ alignSelf: 'flex-start' }}
          >
            {t('reportar.cancelar', 'Cancelar y volver a mis equipos')}
          </button>
        </div>
      </div>
    </>
  );
}

/* ------------------------------------------------------------------ paso 1 */

/**
 * Elegir equipo.
 *
 * Botones grandes y no un desplegable: en un teléfono un `select` abre una
 * rueda nativa donde «PC-ADM-014 — PC de escritorio (Administración)» se lee
 * cortado, y acá la persona elige por dónde está el aparato, no por su código.
 */
function PasoEquipo({
  equipos,
  elegido,
  onElegir,
}: {
  equipos: EquipoListaDto[] | null;
  elegido: string;
  onElegir: (id: string) => void;
}) {
  const t = useT();

  return (
    <div className="tarjeta">
      <header>
        <h2>{t('reportar.queEquipo', '¿Qué equipo te está dando problemas?')}</h2>
      </header>

      <div style={{ padding: 'var(--esp-4)', display: 'flex', flexDirection: 'column', gap: 'var(--esp-2)' }}>
        {equipos === null ? (
          <p className="sub">{t('comun.cargando', 'Cargando…')}</p>
        ) : equipos.length === 0 ? (
          <p className="sub" style={{ lineHeight: 'var(--interlineado-parrafo)' }}>
            {t('reportar.sinEquipos',
               'No figurás como responsable de ningún equipo, así que no hay de cuál reportar. '
               + 'Avisale al responsable técnico para que te asigne el tuyo.')}
          </p>
        ) : (
          equipos.map((e) => (
            <button
              key={e.id}
              type="button"
              className={e.id === elegido ? 'opcion-grande elegida' : 'opcion-grande'}
              onClick={() => onElegir(e.id)}
            >
              <span className="opcion-titulo">{e.marcaModelo || e.tipo}</span>
              <span className="sub">
                <span className="mono">{e.codigo}</span>
                {e.ubicacion ? ` · ${e.ubicacion}` : ''}
              </span>
            </button>
          ))
        )}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ paso 2 */

function PasoQuePasa({
  titulo,
  descripcion,
  puedeTrabajar,
  setTitulo,
  setDescripcion,
  setPuedeTrabajar,
  onVolver,
  onSeguir,
}: {
  titulo: string;
  descripcion: string;
  puedeTrabajar: string;
  setTitulo: (v: string) => void;
  setDescripcion: (v: string) => void;
  setPuedeTrabajar: (v: 'si' | 'aMedias' | 'no') => void;
  onVolver: () => void;
  onSeguir: () => void;
}) {
  const t = useT();

  // Los mismos mínimos que valida el backend, comprobados acá para no mandar
  // un pedido que va a volver con error: cinco caracteres el título y diez la
  // descripción.
  const listo = titulo.trim().length >= 5 && descripcion.trim().length >= 10;

  return (
    <div className="tarjeta">
      <header>
        <h2>{t('reportar.contame', 'Contame qué pasa')}</h2>
      </header>

      <div style={{ padding: 'var(--esp-4)', display: 'flex', flexDirection: 'column', gap: 'var(--esp-4)' }}>
        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="etiqueta">{t('reportar.enUnaLinea', 'En una línea, ¿qué notás?')}</span>
          <input
            className="campo"
            value={titulo}
            maxLength={150}
            autoFocus
            placeholder={t('reportar.tituloEjemplo', 'Se apaga sola al abrir el sistema de gestión')}
            onChange={(e) => setTitulo(e.target.value)}
          />
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="etiqueta">{t('reportar.contalo', 'Contámelo con tus palabras')}</span>
          <textarea
            className="campo"
            rows={4}
            value={descripcion}
            maxLength={2000}
            placeholder={t('reportar.descripcionEjemplo',
                           'Va lentísima y se apaga sola cuando abro facturación. Hoy me pasó tres veces.')}
            onChange={(e) => setDescripcion(e.target.value)}
          />
          <span className="sub">
            {t('reportar.sinTecnicismos',
               'No hace falta que sepas de computadoras ni que uses palabras técnicas.')}
          </span>
        </label>

        <div>
          <span className="etiqueta" style={{ display: 'block', marginBottom: 'var(--esp-2)' }}>
            {t('reportar.podesTrabajar', '¿Podés seguir trabajando?')}
          </span>
          <div style={{ display: 'flex', gap: 'var(--esp-2)', flexWrap: 'wrap' }}>
            {([
              ['si', t('reportar.si', 'Sí')],
              ['aMedias', t('reportar.aMedias', 'Sí, a medias')],
              ['no', t('reportar.no', 'No, estoy frenada')],
            ] as const).map(([valor, texto]) => (
              <button
                key={valor}
                type="button"
                className={puedeTrabajar === valor ? 'btn pri' : 'btn'}
                onClick={() => setPuedeTrabajar(valor)}
                aria-pressed={puedeTrabajar === valor}
              >
                {texto}
              </button>
            ))}
          </div>
        </div>

        <div style={{ display: 'flex', gap: 'var(--esp-2)' }}>
          <button type="button" className="btn" onClick={onVolver}>
            {t('comun.volver', 'Volver')}
          </button>
          <button type="button" className="btn pri" disabled={!listo} onClick={onSeguir}>
            {t('comun.continuar', 'Continuar')}
          </button>
        </div>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ paso 3 */

function PasoRevisar({
  equipo,
  titulo,
  descripcion,
  frase,
  enviando,
  onCorregir,
  onEnviar,
}: {
  equipo?: EquipoListaDto;
  titulo: string;
  descripcion: string;
  frase: string | null;
  enviando: boolean;
  onCorregir: () => void;
  onEnviar: () => void;
}) {
  const t = useT();

  return (
    <div className="tarjeta">
      <header>
        <h2>{t('reportar.revisar', 'Revisá antes de enviar')}</h2>
      </header>

      <dl className="dl">
        <dt>{t('incidencia.equipo', 'Equipo')}</dt>
        <dd>
          {equipo ? (
            <>
              {equipo.marcaModelo || equipo.tipo} <span className="mono sub">{equipo.codigo}</span>
            </>
          ) : (
            '—'
          )}
        </dd>
        <dt>{t('reportar.quePasa', 'Qué pasa')}</dt>
        <dd>{titulo}</dd>
        <dt>{t('reportar.contaste', 'Lo que contaste')}</dt>
        <dd style={{ whiteSpace: 'pre-wrap' }}>{descripcion}</dd>
        {/*
          La frase sobre si puede trabajar se muestra por separado aunque viaje
          dentro de la descripcion: se manda lo que la persona leyo, sin
          agregados que no vio.
        */}
        {frase ? (
          <>
            <dt>{t('reportar.podesTrabajar', '¿Podés seguir trabajando?')}</dt>
            <dd>{frase}</dd>
          </>
        ) : null}
      </dl>

      <div style={{ padding: 'var(--esp-4)' }}>
        <p className="sub" style={{ marginBottom: 'var(--esp-4)', lineHeight: 'var(--interlineado-parrafo)' }}>
          {t('reportar.queVaAPasar',
             'Cuando lo mandes, un técnico lo va a ver enseguida. Vas a poder seguirlo desde '
             + '«Mis pedidos» con el número que te damos al enviar.')}
        </p>

        <div style={{ display: 'flex', gap: 'var(--esp-2)' }}>
          <button type="button" className="btn" onClick={onCorregir} disabled={enviando}>
            {t('reportar.corregir', 'Corregir algo')}
          </button>
          <button type="button" className="btn pri" onClick={onEnviar} disabled={enviando}>
            {enviando ? t('comun.enviando', 'Enviando…') : t('reportar.enviar', 'Enviar el reporte')}
          </button>
        </div>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------- confirmación */

/**
 * No dice «éxito».
 *
 * Da el número del pedido y los tres pasos que siguen, que es lo que la
 * persona necesita para no volver a reportar lo mismo en dos horas.
 */
function Confirmacion({ resultado }: { resultado: IncidenciaRegistradaDto }) {
  const t = useT();
  const numero = `#${String(resultado.numero).padStart(3, '0')}`;

  return (
    <>
      <Encabezado titulo={t('reportar.listo', 'Listo, ya lo mandamos')} />

      <div className="cuerpo">
        <div style={{ maxWidth: 620, display: 'flex', flexDirection: 'column', gap: 'var(--esp-4)' }}>
          {/*
            Si hubo que aplicar el respaldo se lo dice con todas las letras. Un
            sistema degradado que no lo dice es peor que uno caído (ADR 0009).
            Lo que NO se muestra acá es a quién se lo asignaron ni con qué
            justificación: eso es del taller.
          */}
          {resultado.advertencia ? <div className="aviso atencion">{resultado.advertencia}</div> : null}

          <div className="tarjeta">
            <div style={{ padding: 'var(--esp-6)' }}>
              <p style={{ lineHeight: 'var(--interlineado-parrafo)', marginBottom: 'var(--esp-4)' }}>
                {t('reportar.quedoRegistrado', 'Tu pedido quedó registrado como')}{' '}
                <b className="mono" style={{ fontSize: 'var(--txt-titulo)' }}>{numero}</b>.{' '}
                {t('reportar.anotaNumero', 'Anotá ese número si tenés que llamar.')}
              </p>

              <p className="etiqueta" style={{ marginBottom: 'var(--esp-2)' }}>
                {t('reportar.queSigue', 'Qué pasa ahora')}
              </p>
              <ol className="pasos">
                {[
                  t('reportar.sigue1', 'Un técnico lo revisa y lo toma.'),
                  t('reportar.sigue2', 'Te avisamos cuando empiece a arreglarlo.'),
                  t('reportar.sigue3', 'Cuando esté resuelto te lo confirmamos acá.'),
                ].map((texto, i) => (
                  <li className="paso" key={texto}>
                    <span className="paso-numero" aria-hidden="true">
                      {i + 1}
                    </span>
                    <span className="paso-cuerpo">
                      <span className="paso-titulo">{texto}</span>
                    </span>
                  </li>
                ))}
              </ol>
            </div>
          </div>

          <div style={{ display: 'flex', gap: 'var(--esp-2)', flexWrap: 'wrap' }}>
            <Link to={`/mis-pedidos/${resultado.id}`} className="btn pri">
              {t('reportar.verMiPedido', 'Ver mi pedido')}
            </Link>
            <Link to="/mis-equipos" className="btn">
              {t('reportar.volverAEquipos', 'Volver a mis equipos')}
            </Link>
          </div>
        </div>
      </div>
    </>
  );
}
