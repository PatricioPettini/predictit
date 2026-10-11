import { useCallback, useEffect, useState } from 'react';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type { IncidenciaDetalleDto, TransicionDto } from '../api/tipos';
import { Patentes } from '../api/tipos';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';

/**
 * Atención de una incidencia: avanzar el estado y cargar diagnóstico y solución.
 *
 * Las transiciones las decide el backend, no esta pantalla: se consultan y se
 * ofrecen las que devuelve. Si la máquina de estados viviera acá también,
 * habría dos y se contradirían la primera vez que cambie una regla.
 *
 * Una transición que el backend marca con impedimento se muestra
 * **deshabilitada con el motivo**, en lugar de esconderse. Un botón que
 * desaparece se lee como un bug; uno deshabilitado que dice por qué se lee
 * como una regla.
 */
export function PanelAtencion({
  incidencia,
  alCambiar,
}: {
  incidencia: IncidenciaDetalleDto;
  alCambiar: () => void;
}) {
  const t = useT();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [transiciones, setTransiciones] = useState<TransicionDto[] | null>(null);
  const [diagnostico, setDiagnostico] = useState(incidencia.diagnostico ?? '');
  const [solucion, setSolucion] = useState(incidencia.solucion ?? '');
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const puedeAtender = puede(Patentes.incidenciaAtender);

  const cargarTransiciones = useCallback(() => {
    api.incidencias
      .transiciones(incidencia.id)
      .then(setTransiciones)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setTransiciones([]);
      });
  }, [incidencia.id, cerrarSiExpiro]);

  useEffect(cargarTransiciones, [cargarTransiciones]);

  // Los textos se sincronizan cuando la incidencia se recarga: si no, después
  // de guardar quedaría lo que el usuario tenía escrito y no lo que se grabó.
  useEffect(() => {
    setDiagnostico(incidencia.diagnostico ?? '');
    setSolucion(incidencia.solucion ?? '');
  }, [incidencia.diagnostico, incidencia.solucion]);

  function avanzar(estado: string) {
    setGuardando(true);
    setError(null);

    api.incidencias
      .atender(incidencia.id, {
        estado,
        // Se manda sólo lo que tiene contenido: el backend interpreta el nulo
        // como «dejá el texto que ya estaba».
        diagnostico: diagnostico.trim() || null,
        solucion: solucion.trim() || null,
      })
      .then(() => {
        alCambiar();
        cargarTransiciones();
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.avanzarIncidencia', 'No se pudo avanzar la incidencia.'));
      })
      .finally(() => setGuardando(false));
  }

  if (!puedeAtender) return null;

  const finalizada = transiciones !== null && transiciones.length === 0;

  return (
    <div className="tarjeta">
      <header>
        <h2>{t('incidencia.atencion', 'Atención')}</h2>
      </header>

      <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 14 }}>
        {finalizada ? (
          <div className="aviso info">
            {t('incidencia.laIncidenciaEsta', 'La incidencia está')} <strong>{incidencia.estado.toLowerCase()}</strong> {t('incidencia.estadoFinalNota', 'y no admite más cambios de estado. Es un estado final.')}
          </div>
        ) : null}

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('incidencia.diagnosticoLabel', 'Diagnóstico — qué se encontró')}</span>
          <textarea
            className="campo"
            rows={3}
            value={diagnostico}
            disabled={guardando || finalizada}
            placeholder={t('incidencia.diagnosticoEjemplo', 'Ventilador del disipador trabado por acumulación de polvo.')}
            onChange={(e) => setDiagnostico(e.target.value)}
          />
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('incidencia.solucionLabel', 'Solución — qué se hizo')}</span>
          <textarea
            className="campo"
            rows={3}
            value={solucion}
            disabled={guardando || finalizada}
            placeholder={t('incidencia.solucionEjemplo', 'Se reemplazó el ventilador y se limpió el disipador.')}
            onChange={(e) => setSolucion(e.target.value)}
          />
        </label>

        {transiciones === null ? (
          <div className="sub">{t('incidencia.cargandoTransiciones', 'Cargando lo que se puede hacer…')}</div>
        ) : transiciones.length > 0 ? (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
            {/* `tr` y no `t`: dentro del map, `t` taparía la función de
                traducción y el texto del botón quedaría sin traducir sin que
                falle nada. */}
            {transiciones.map((tr) => {
              const resolver = tr.estado === 'Resuelta';

              // Resolver exige los dos textos, y eso lo sabe el formulario:
              // el backend mira lo guardado y acá se mira lo escrito, que es
              // lo que va a viajar en la misma llamada.
              const faltaTexto = resolver && (!diagnostico.trim() || !solucion.trim());
              const bloqueada = Boolean(tr.impedimento) || faltaTexto;

              return (
                <button
                  key={tr.estado}
                  type="button"
                  className={resolver ? 'btn pri' : 'btn'}
                  disabled={guardando || bloqueada}
                  // El motivo va en el title además del texto de abajo: quien
                  // llega con el teclado al botón deshabilitado también tiene
                  // que poder saber por qué.
                  title={
                    tr.impedimento ??
                    (faltaTexto
                      ? t('incidencia.resolverExigeTextos',
                          'Resolver exige el diagnóstico y la solución.')
                      : t('incidencia.pasarA', 'Pasar a {estado}', { estado: tr.estado }))
                  }
                  onClick={() => avanzar(tr.estado)}
                >
                  {tr.estado}
                </button>
              );
            })}
          </div>
        ) : null}

        {transiciones?.some((tr) => tr.impedimento) ? (
          <ul className="sub" style={{ margin: 0, paddingLeft: 18 }}>
            {transiciones
              .filter((tr) => tr.impedimento)
              .map((tr) => (
                <li key={tr.estado}>
                  <strong>{tr.estado}:</strong> {tr.impedimento}
                </li>
              ))}
          </ul>
        ) : null}

        {transiciones?.some((tr) => tr.estado === 'Resuelta') ? (
          <div className="sub">
            {!diagnostico.trim() || !solucion.trim()
              ? t('incidencia.faltanTextos',
                  'Para resolverla hay que completar el diagnóstico y la solución.')
              : t('incidencia.sellaFecha',
                  'Al resolverla se sella la fecha de resolución: es el dato del que sale el tiempo de reparación.')}
          </div>
        ) : null}

        {error ? (
          <div className="aviso error" role="alert">
            {error}
          </div>
        ) : null}
      </div>
    </div>
  );
}
