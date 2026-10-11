import { Link, useLocation } from 'react-router-dom';
import { Encabezado, primeraPantalla } from '../componentes/Layout';
import { useT } from '../idioma/IdiomaContext';
import { useSesion } from '../sesion/SesionContext';
import { Patentes } from '../api/tipos';

/**
 * Dirección que no existe.
 *
 * Antes cualquier ruta desconocida redirigía en silencio a la primera pantalla
 * permitida. Para la raíz eso es lo correcto y se sigue haciendo; para
 * `/incidencias/INC-9999` no: quien llegó ahí desde un enlace viejo o de un
 * número mal tecleado terminaba en el tablero sin enterarse de que su enlace
 * estaba roto, y volvía a intentarlo.
 *
 * Conserva la barra lateral porque la sesión sigue abierta: sacársela lo haría
 * sentir expulsado del sistema por escribir mal una dirección.
 */
export function NoEncontrada() {
  const t = useT();
  const { puede } = useSesion();
  const { pathname } = useLocation();

  return (
    <>
      <Encabezado
        titulo={t('error.404Titulo', 'Esta página no existe')}
        contexto={t('error.404Contexto', 'La dirección que abriste no corresponde a ninguna pantalla del sistema')}
      />

      <div className="cuerpo">
        <div className="tarjeta" style={{ padding: 'var(--esp-6)', maxWidth: 620 }}>
          <p
            className="mono"
            style={{
              fontSize: 'var(--txt-cifra)',
              fontWeight: 'var(--peso-cifra)',
              color: 'var(--tx3)',
              lineHeight: 1,
              marginBottom: 'var(--esp-4)',
            }}
          >
            404
          </p>

          <p style={{ lineHeight: 'var(--interlineado-parrafo)', marginBottom: 'var(--esp-4)' }}>
            {t('error.404Detalle', 'La dirección')}{' '}
            <span className="mono" style={{ color: 'var(--tx)' }}>
              {pathname}
            </span>{' '}
            {t('error.404Detalle2',
               'no corresponde a nada del sistema. Puede que el registro se haya anulado, o que '
               + 'el número esté mal escrito.')}
          </p>

          <div style={{ display: 'flex', gap: 'var(--esp-2)', flexWrap: 'wrap' }}>
            <Link to={primeraPantalla(puede)} className="btn pri">
              {t('error.404Volver', 'Volver al inicio')}
            </Link>
            {puede(Patentes.incidenciaVerTodas) || puede(Patentes.incidenciaVerPropias) ? (
              <Link to="/incidencias" className="btn">
                {t('error.404Buscar', 'Buscar en incidencias')}
              </Link>
            ) : null}
          </div>

          <p className="sub" style={{ marginTop: 'var(--esp-4)', lineHeight: 'var(--interlineado-parrafo)' }}>
            {t('error.404Aviso',
               'Si llegaste desde un enlace del propio sistema, avisale al administrador: es un '
               + 'enlace roto y no un error tuyo.')}
          </p>
        </div>
      </div>
    </>
  );
}
