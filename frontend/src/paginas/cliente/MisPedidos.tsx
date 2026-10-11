import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, ErrorApi } from '../../api/cliente';
import type { IncidenciaListaDto } from '../../api/tipos';
import { Patentes } from '../../api/tipos';
import { Encabezado } from '../../componentes/Layout';
import { SinDatos } from '../../componentes/Estados';
import { useHace } from '../../idioma/hace';
import { useT } from '../../idioma/IdiomaContext';
import { useCerrarSiExpiro, useSesion } from '../../sesion/SesionContext';
import { avanceDe, PASOS } from './avance';

/**
 * Mis pedidos — módulo del solicitante.
 *
 * Es la misma consulta que hace el listado del técnico: el servidor filtra por
 * la patente, así que a quien sólo tiene «ver las propias» le llegan las
 * propias y nada más. Lo que cambia es la lectura.
 *
 * No es una tabla. Una tabla sirve para comparar veinte filas y decidir cuál
 * atender primero, que es el trabajo del técnico; acá hay dos o tres pedidos
 * propios y la pregunta es «¿en qué anda el mío?». Y no aparecen prioridad,
 * categoría ni técnico asignado: son las palabras del taller.
 */
export function MisPedidos() {
  const t = useT();
  const hace = useHace();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [pedidos, setPedidos] = useState<IncidenciaListaDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.incidencias
      .buscar({ porPagina: 100 })
      .then((p) => setPedidos(p.items))
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(
          ex instanceof ErrorApi ? ex.message : t('pedido.noSePudo', 'No se pudieron cargar tus pedidos.'),
        );
      });
  }, [cerrarSiExpiro]);

  const abiertos = pedidos?.filter((p) => p.abierta) ?? [];
  const cerrados = pedidos?.filter((p) => !p.abierta) ?? [];

  return (
    <>
      <Encabezado
        titulo={t('nav.misPedidos', 'Mis pedidos')}
        contexto={
          pedidos === null
            ? t('comun.cargando', 'Cargando…')
            : `${abiertos.length} ${t('pedido.abiertos', 'abiertos')} · ${cerrados.length} ${t('pedido.resueltos', 'resueltos')}`
        }
        acciones={
          puede(Patentes.incidenciaRegistrar) ? (
            <Link to="/reportar" className="btn pri">
              {t('activo.reportarProblema', 'Reportar un problema')}
            </Link>
          ) : null
        }
      />

      <div className="cuerpo">
        <div style={{ maxWidth: 720, display: 'flex', flexDirection: 'column', gap: 'var(--esp-4)' }}>
          {error ? (
            <div className="aviso error" role="alert">
              {error}
            </div>
          ) : null}

          {pedidos !== null && pedidos.length === 0 ? (
            <div className="tarjeta">
              <SinDatos
                titulo={t('pedido.sinPedidos', 'Todavía no reportaste nada')}
                detalle={t('pedido.sinPedidosDetalle',
                           'Cuando algo no ande, reportalo desde acá y vas a poder seguirlo en esta pantalla.')}
                accion={
                  puede(Patentes.incidenciaRegistrar) ? (
                    <Link to="/reportar" className="btn pri">
                      {t('activo.reportarProblema', 'Reportar un problema')}
                    </Link>
                  ) : undefined
                }
              />
            </div>
          ) : null}

          {abiertos.map((p) => (
            <Tarjeta key={p.id} pedido={p} hace={hace} />
          ))}

          {cerrados.length > 0 ? (
            <p className="etiqueta" style={{ marginTop: 'var(--esp-2)' }}>
              {t('pedido.yaResueltos', 'Ya resueltos')}
            </p>
          ) : null}

          {cerrados.map((p) => (
            <Tarjeta key={p.id} pedido={p} hace={hace} />
          ))}
        </div>
      </div>
    </>
  );
}

function Tarjeta({ pedido, hace }: { pedido: IncidenciaListaDto; hace: (f: string) => string }) {
  const t = useT();
  const avance = avanceDe(pedido.estado);

  return (
    <Link to={`/mis-pedidos/${pedido.id}`} className="tarjeta pedido">
      <div className="pedido-cabeza">
        <span className="pedido-titulo">{pedido.titulo}</span>
        <span className="estado">
          <span className="punto" style={{ background: avance.color }} aria-hidden="true" />
          {t(avance.clave, avance.texto)}
        </span>
      </div>

      <div className="sub">
        <span className="mono">#{String(pedido.numero).padStart(3, '0')}</span>
        {' · '}
        <span className="mono">{pedido.codigoEquipo}</span>
        {' · '}
        {hace(pedido.fecha)}
      </div>

      {/*
        La barra de avance sólo aparece mientras hay avance que mostrar. En un
        pedido cancelado seria mentir: no va a seguir.
      */}
      {avance.paso !== null ? (
        <div className="avance" aria-label={`${t('pedido.paso', 'Paso')} ${avance.paso} / ${PASOS}`}>
          {Array.from({ length: PASOS }).map((_, i) => (
            <span
              key={i}
              className={i < avance.paso! ? 'avance-tramo hecho' : 'avance-tramo'}
              style={i < avance.paso! ? { background: avance.color } : undefined}
            />
          ))}
        </div>
      ) : null}
    </Link>
  );
}
