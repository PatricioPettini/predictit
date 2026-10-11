import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, ErrorApi } from '../../api/cliente';
import type { IncidenciaDetalleDto } from '../../api/tipos';
import { Encabezado } from '../../componentes/Layout';
import { SinDatos } from '../../componentes/Estados';
import { useFormato } from '../../idioma/formato';
import { useT } from '../../idioma/IdiomaContext';
import { useCerrarSiExpiro } from '../../sesion/SesionContext';
import { avanceDe, PASOS } from './avance';

/**
 * El detalle de un pedido, contado como se lo cuenta a quien lo hizo.
 *
 * La misma incidencia que el técnico ve con su diagnóstico, su justificación
 * de asignación y su clasificación. Acá no aparece nada de eso: ni score, ni
 * nivel de riesgo, ni prioridad, ni categoría, ni el panel violeta de la IA.
 * Mostrarle «prioridad Alta, confianza 86 %» a quien reportó le pide que opine
 * sobre algo que no puede juzgar.
 *
 * Las novedades salen de fechas que el sistema realmente guarda —cuándo se
 * recibió, cuándo se asignó, cuándo se resolvió— y no de un historial de
 * transiciones, que no existe. Por eso no dice «empezaron a revisarla»: ese
 * momento no está registrado en ningún lado y ponerlo sería inventarlo.
 */
export function PedidoDetalle() {
  const t = useT();
  const { fechaHora } = useFormato();
  const { id } = useParams<{ id: string }>();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [pedido, setPedido] = useState<IncidenciaDetalleDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [noExiste, setNoExiste] = useState(false);

  useEffect(() => {
    if (!id) return;
    api.incidencias
      .detalle(id)
      .then(setPedido)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setNoExiste(ex instanceof ErrorApi && ex.esNoEncontrado);
        setError(
          ex instanceof ErrorApi ? ex.message : t('pedido.noSePudoUno', 'No se pudo cargar el pedido.'),
        );
      });
  }, [id, cerrarSiExpiro]);

  if (error) {
    return (
      <>
        <Encabezado titulo={t('nav.misPedidos', 'Mis pedidos')} />
        <div className="cuerpo">
          <div className="tarjeta">
            {noExiste ? (
              <SinDatos
                titulo={t('pedido.noExiste', 'Ese pedido no existe')}
                detalle={t('pedido.noExisteDetalle', 'Puede que el enlace esté mal escrito.')}
                accion={
                  <Link to="/mis-pedidos" className="btn pri">
                    {t('pedido.volver', 'Volver a mis pedidos')}
                  </Link>
                }
              />
            ) : (
              <div style={{ padding: 'var(--esp-4)' }}>
                <div className="aviso error" role="alert">
                  {error}
                </div>
                <div style={{ marginTop: 'var(--esp-3)' }}>
                  <Link to="/mis-pedidos">{t('pedido.volver', 'Volver a mis pedidos')}</Link>
                </div>
              </div>
            )}
          </div>
        </div>
      </>
    );
  }

  if (!pedido) {
    return (
      <>
        <Encabezado titulo={t('comun.cargando', 'Cargando…')} />
        <div className="cuerpo">
          <div className="vacio">{t('pedido.cargando', 'Cargando tu pedido…')}</div>
        </div>
      </>
    );
  }

  const avance = avanceDe(pedido.estado);

  // Sólo momentos con fecha guardada. Se arma de más viejo a más nuevo y se da
  // vuelta: lo último que pasó es lo primero que la persona quiere leer.
  const novedades: { cuando: string; texto: string }[] = [
    { cuando: pedido.fecha, texto: t('pedido.novedadRecibido', 'Recibimos tu pedido') },
  ];
  if (pedido.asignacion?.fechaHora) {
    novedades.push({
      cuando: pedido.asignacion.fechaHora,
      texto: t('pedido.novedadAsignado', 'Se lo asignamos a un técnico'),
    });
  }
  if (pedido.fechaResolucion) {
    novedades.push({
      cuando: pedido.fechaResolucion,
      texto: t('pedido.novedadResuelto', 'Nos avisaron que quedó resuelto'),
    });
  }
  novedades.reverse();

  return (
    <>
      <Encabezado
        titulo={pedido.titulo}
        contexto={
          <>
            <Link to="/mis-pedidos">{t('nav.misPedidos', 'Mis pedidos')}</Link> ›{' '}
            <span className="mono">#{String(pedido.numero).padStart(3, '0')}</span>
          </>
        }
      />

      <div className="cuerpo">
        <div style={{ maxWidth: 620, display: 'flex', flexDirection: 'column', gap: 'var(--esp-4)' }}>
          <div className="tarjeta">
            <div style={{ padding: 'var(--esp-4)' }}>
              <span className="estado" style={{ fontSize: 'var(--txt-seccion)', fontWeight: 600 }}>
                <span className="punto" style={{ background: avance.color }} aria-hidden="true" />
                {t(avance.clave, avance.texto)}
              </span>

              {avance.paso !== null ? (
                <div
                  className="avance"
                  style={{ marginTop: 'var(--esp-3)' }}
                  aria-label={`${t('pedido.paso', 'Paso')} ${avance.paso} / ${PASOS}`}
                >
                  {Array.from({ length: PASOS }).map((_, i) => (
                    <span
                      key={i}
                      className={i < avance.paso! ? 'avance-tramo hecho' : 'avance-tramo'}
                      style={i < avance.paso! ? { background: avance.color } : undefined}
                    />
                  ))}
                </div>
              ) : null}

              {/*
                El nombre del tecnico si se muestra: saber que hay una persona
                concreta ocupandose es lo que baja la ansiedad de quien reporto.
                Lo que no se muestra es POR QUE se lo asignaron a esa persona.
              */}
              {pedido.tecnico ? (
                <p className="sub" style={{ marginTop: 'var(--esp-3)', lineHeight: 'var(--interlineado-parrafo)' }}>
                  {t('pedido.loEstaViendo', 'Lo está viendo')} <b style={{ color: 'var(--tx2)' }}>{pedido.tecnico}</b>
                  {t('pedido.delEquipoTecnico', ', del equipo técnico.')}
                </p>
              ) : null}
            </div>
          </div>

          <div className="tarjeta">
            <header>
              <h2>{t('pedido.novedades', 'Novedades')}</h2>
            </header>
            <div style={{ padding: 'var(--esp-4)' }}>
              <ol className="novedades">
                {novedades.map((n) => (
                  <li key={n.cuando + n.texto}>
                    <span className="novedad-texto">{n.texto}</span>
                    <span className="sub">{fechaHora(n.cuando)}</span>
                  </li>
                ))}
              </ol>
            </div>
          </div>

          <div className="tarjeta">
            <header>
              <h2>{t('pedido.loQueEscribiste', 'Lo que escribiste')}</h2>
              <span className="nota mono">{pedido.codigoEquipo}</span>
            </header>
            <div style={{ padding: 'var(--esp-4)' }}>
              <p style={{ whiteSpace: 'pre-wrap', lineHeight: 'var(--interlineado-parrafo)' }}>
                {pedido.descripcion}
              </p>
            </div>
          </div>

          {/*
            La solucion se muestra cuando existe, y en el lugar donde la persona
            la va a buscar: que le digan que esta resuelto sin decirle que se
            hizo obliga a llamar para preguntar.
          */}
          {pedido.solucion ? (
            <div className="tarjeta">
              <header>
                <h2>{t('pedido.queSeHizo', 'Qué se hizo')}</h2>
              </header>
              <div style={{ padding: 'var(--esp-4)' }}>
                <p style={{ whiteSpace: 'pre-wrap', lineHeight: 'var(--interlineado-parrafo)' }}>
                  {pedido.solucion}
                </p>
              </div>
            </div>
          ) : null}

          <Link to="/mis-pedidos" className="btn" style={{ alignSelf: 'flex-start' }}>
            {t('pedido.volver', 'Volver a mis pedidos')}
          </Link>
        </div>
      </div>
    </>
  );
}
