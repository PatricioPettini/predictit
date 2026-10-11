import { useState } from 'react';
import { api, ErrorApi } from '../api/cliente';
import type { GuiaReparacionDto, PasoReparacionDto } from '../api/tipos';
import { useT } from '../idioma/IdiomaContext';
import { useCerrarSiExpiro } from '../sesion/SesionContext';

/**
 * Guía de reparación paso a paso (RF-15, CU-018).
 *
 * Cuatro estados, y cada uno resuelve algo distinto:
 *
 *   apagado    El panel arranca con un botón y no con la guía puesta. Cada
 *              consulta es una llamada paga al proveedor, y un ticket se abre
 *              muchas más veces de las que se repara.
 *   pidiendo   El esqueleto tiene la forma de los pasos que van a llegar, y
 *              dice qué está leyendo el sistema mientras tanto.
 *   con guía   Origen y confianza declarados arriba de todo. El origen puede
 *              ser la IA o la heurística por reglas, que son dos cosas
 *              distintas y se ven distintas.
 *   sin guía   El proveedor no contestó y tampoco hay pasos. Se muestra el
 *              motivo, nunca una lista vacía.
 *
 * El riesgo de un paso no es un ícono al final de la línea: es el fondo de la
 * tarjeta. Así se ve antes de leer el texto, que es antes de empezar el paso.
 *
 * El violeta es de la IA y de nada más. Cuando la guía sale de reglas el panel
 * entero pasa a ámbar, y la distinción no se resuelve con una etiqueta chica:
 * cambia el color del panel completo.
 *
 * El porcentaje SÍ se muestra en los dos casos. La propuesta de diseño lo
 * reservaba para la IA, con el argumento de que la heurística no estima nada;
 * pero acá sí estima —el 55 % que devuelve es la certeza de la coincidencia de
 * términos con la que eligió la categoría, el mismo número que la pantalla
 * muestra en «Clasificación sugerida»—. Esconder un número que el sistema
 * calcula, justo en el panel que existe para hacer visible la trazabilidad,
 * seria decir menos de lo que se sabe.
 */
export function GuiaReparacion({ idIncidencia }: { idIncidencia: string }) {
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [guia, setGuia] = useState<GuiaReparacionDto | null>(null);
  const [pidiendo, setPidiendo] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function pedirGuia() {
    setPidiendo(true);
    setError(null);
    api.incidencias
      .guiaReparacion(idIncidencia)
      .then(setGuia)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(
          ex instanceof ErrorApi
            ? ex.message
            : t('guia.noSePudo', 'No se pudo pedir la guía.'),
        );
      })
      .finally(() => setPidiendo(false));
  }

  // Violeta sólo cuando la propuso el proveedor de IA. Con el proveedor
  // simulado el origen es HEURISTICA y el panel va en ámbar: decir «IA» ahí
  // sería falso, y la trazabilidad tiene que distinguirlos.
  const deIa = guia?.disponible === true && guia.origen === 'IA';

  return (
    <div
      className="tarjeta"
      style={{
        borderColor: guia ? (deIa ? 'var(--ia-borde)' : 'var(--resp-borde)') : undefined,
      }}
    >
      <header>
        <h2>{t('guia.titulo', 'Guía de reparación')}</h2>
        {guia ? (
          deIa ? (
            <span className="chip ia">
              IA
              {guia.confianza != null ? ` · ${Math.round(guia.confianza * 100)} %` : ''}
            </span>
          ) : (
            <span className="chip respaldo">
              {t('guia.heuristica', 'Heurística')}
              {guia.confianza != null ? ` · ${Math.round(guia.confianza * 100)} %` : ''}
            </span>
          )
        ) : null}
      </header>

      <div style={{ padding: 'var(--esp-4)' }}>
        {!guia && !pidiendo ? (
          <>
            <p
              className="sub"
              style={{ marginBottom: 'var(--esp-4)', lineHeight: 'var(--interlineado-parrafo)' }}
            >
              {t('guia.explicacion',
                 'El sistema puede armar una lista de pasos ordenados de menos a más invasivo, '
                 + 'con el riesgo marcado en los que lo tienen. Se pide cuando la necesitás: '
                 + 'cada consulta al proveedor tiene costo, así que no se genera sola al abrir '
                 + 'la incidencia.')}
            </p>
            <button type="button" className="btn pri" onClick={pedirGuia}>
              {t('guia.generar', 'Generar guía de reparación')}
            </button>
            {error ? (
              <div className="aviso error" style={{ marginTop: 'var(--esp-4)' }} role="alert">
                {error}
              </div>
            ) : null}
          </>
        ) : null}

        {pidiendo ? <EsqueletoGuia /> : null}

        {guia && !pidiendo ? <Pasos guia={guia} onReintentar={pedirGuia} /> : null}
      </div>
    </div>
  );
}

/**
 * El esqueleto tiene la forma de los pasos que van a llegar, y no un girador.
 * Tres bloques con el alto de un paso: cuando llega la guía la tarjeta no salta.
 */
function EsqueletoGuia() {
  const t = useT();
  return (
    <div role="status" aria-label={t('guia.armando', 'Armando la guía…')}>
      <p className="sub" style={{ marginBottom: 'var(--esp-3)' }}>
        {t('guia.armando', 'Armando la guía…')}
      </p>
      <div className="esqueleto">
        {[0, 1, 2].map((i) => (
          <div className="esqueleto-fila" key={i} style={{ height: 46 }}>
            <span className="esqueleto-celda" style={{ width: '70%' }} />
          </div>
        ))}
      </div>
      <p className="sub" style={{ marginTop: 'var(--esp-3)' }}>
        {t('guia.seguiTrabajando', 'Podés seguir trabajando en la incidencia mientras tanto.')}
      </p>
    </div>
  );
}

function Pasos({ guia, onReintentar }: { guia: GuiaReparacionDto; onReintentar: () => void }) {
  const t = useT();
  const deIa = guia.disponible && guia.origen === 'IA';

  return (
    <>
      {/*
        Cuando el proveedor no contestó, lo primero que se lee es por qué. El
        número de pasos también informa: cinco a medida contra tres genéricos le
        dice al técnico qué está mirando sin leer ninguno.
      */}
      {!guia.disponible ? (
        <div className="aviso atencion" style={{ marginBottom: 'var(--esp-4)' }}>
          <b>{t('guia.conReglas', 'Guía armada con reglas, no con IA.')}</b>{' '}
          {guia.motivo ?? t('guia.sinRespuesta', 'El proveedor no respondió.')}
        </div>
      ) : null}

      {guia.resumen ? (
        <p style={{ marginBottom: 'var(--esp-4)', lineHeight: 'var(--interlineado-parrafo)' }}>
          {guia.resumen}
        </p>
      ) : null}

      <ol className="pasos">
        {guia.pasos.map((paso) => (
          <Paso key={paso.orden} paso={paso} />
        ))}
      </ol>

      {guia.cuandoEscalar ? (
        <div className={deIa ? 'aviso ia' : 'aviso info'} style={{ marginTop: 'var(--esp-4)' }}>
          <b>{t('guia.cuandoEscalar', 'Cuándo escalar')}:</b> {guia.cuandoEscalar}
        </div>
      ) : null}

      <div style={{ display: 'flex', gap: 'var(--esp-2)', marginTop: 'var(--esp-4)' }}>
        <button type="button" className="btn" onClick={onReintentar}>
          {guia.disponible
            ? t('guia.volverAPedir', 'Volver a pedirla')
            : t('guia.reintentarIa', 'Reintentar con IA')}
        </button>
      </div>

      <p className="sub" style={{ marginTop: 'var(--esp-3)' }}>
        {t('guia.noCambiaEstado',
           'Es una sugerencia: marcar un paso no cambia el estado de la incidencia, y el '
           + 'sistema no ejecuta nada sobre el equipo.')}
      </p>
    </>
  );
}

function Paso({ paso }: { paso: PasoReparacionDto }) {
  const t = useT();
  return (
    <li className={paso.riesgo ? 'paso con-riesgo' : 'paso'}>
      <span className="paso-numero" aria-hidden="true">
        {paso.orden}
      </span>
      <span className="paso-cuerpo">
        <span className="paso-titulo">{paso.titulo}</span>
        {paso.detalle ? <span className="sub">{paso.detalle}</span> : null}
        {paso.riesgo ? (
          <span className="paso-riesgo">
            <b>{t('comun.riesgo', 'Riesgo')}:</b> {paso.riesgo}
          </span>
        ) : (
          <span className="sub">{t('guia.sinRiesgo', 'Sin riesgo')}</span>
        )}
      </span>
    </li>
  );
}
