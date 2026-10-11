import { Encabezado } from '../componentes/Layout';
import { ConfigNav } from './Organizacion';
import { DENSIDADES, NOMBRES_RETIRADOS, TEMAS, useTema } from '../tema/TemaContext';
import type { IdDensidad, IdTema } from '../tema/TemaContext';
import { useIdioma, useT } from '../idioma/IdiomaContext';

/**
 * Miniatura de la aplicación real en un tema.
 *
 * Un cuadradito de color no responde la pregunta que la persona se está
 * haciendo, que es «cómo se va a ver mi pantalla». La miniatura sí, y sale
 * gratis: los selectores de la capa de tokens son por atributo, así que
 * poniéndole `data-tema` a este `div` todo lo de adentro se pinta con esa
 * paleta aunque la página esté en la otra.
 */
function Miniatura({ tema }: { tema: 'oscuro' | 'claro' }) {
  return (
    <span
      data-tema={tema}
      aria-hidden="true"
      style={{
        display: 'flex',
        width: 96,
        height: 58,
        flexShrink: 0,
        overflow: 'hidden',
        borderRadius: 6,
        border: '1px solid var(--linea-fuerte)',
        background: 'var(--fondo)',
      }}
    >
      {/* barra lateral */}
      <span style={{ width: 22, background: 'var(--sup)', borderRight: '1px solid var(--linea)',
                     display: 'flex', flexDirection: 'column', gap: 3, padding: 4 }}>
        <span style={{ height: 4, borderRadius: 2, background: 'var(--acento)' }} />
        <span style={{ height: 3, borderRadius: 2, background: 'var(--linea-fuerte)' }} />
        <span style={{ height: 3, borderRadius: 2, background: 'var(--linea-fuerte)' }} />
      </span>
      {/* cuerpo: una cifra, una fila de riesgo alto y dos filas comunes */}
      <span style={{ flex: 1, padding: 4, display: 'flex', flexDirection: 'column', gap: 3 }}>
        <span style={{ height: 14, borderRadius: 3, background: 'var(--sup)',
                       border: '1px solid var(--linea)', display: 'flex', alignItems: 'center',
                       paddingLeft: 3, gap: 2 }}>
          <span style={{ width: 12, height: 5, borderRadius: 1, background: 'var(--tx)' }} />
          <span style={{ width: 6, height: 3, borderRadius: 1, background: 'var(--tx3)' }} />
        </span>
        <span style={{ height: 6, borderRadius: 2, background: 'var(--alto-bg)',
                       border: '1px solid var(--alto-borde)', display: 'flex',
                       alignItems: 'center', paddingLeft: 2 }}>
          <span style={{ width: 3, height: 3, borderRadius: '50%', background: 'var(--alto-barra)' }} />
        </span>
        <span style={{ height: 5, borderRadius: 2, background: 'var(--sup)' }} />
        <span style={{ height: 5, borderRadius: 2, background: 'var(--sup)' }} />
      </span>
    </span>
  );
}

/**
 * El alto de fila de cada densidad, para dibujar la muestra.
 *
 * Repite lo que dice `tokens.css` porque un `div` no puede leer el valor de
 * una variable CSS que todavia no se aplico a ningun elemento. Si cambia alla,
 * cambia aca: son cuatro numeros y la alternativa —medir el DOM para pintar
 * una miniatura— cuesta mas de lo que resuelve.
 */
const ALTO_FILA: Record<IdDensidad, number> = { compacta: 34, normal: 48, amplia: 60 };

/**
 * Muestra de densidad: tres líneas con el alto de fila real de cada opción.
 * Misma razón que la miniatura — la densidad se decide viéndola, no leyéndola.
 */
function MuestraDensidad({ alto }: { alto: number }) {
  return (
    <span aria-hidden="true"
          style={{ display: 'flex', flexDirection: 'column', gap: 2, width: 54, flexShrink: 0 }}>
      {[0, 1, 2].map((i) => (
        <span key={i} style={{ height: Math.round(alto / 6), borderRadius: 2,
                               background: 'var(--linea-fuerte)' }} />
      ))}
    </span>
  );
}

function Opcion({
  nombre,
  detalle,
  elegida,
  onElegir,
  extra,
}: {
  nombre: string;
  detalle: string;
  elegida: boolean;
  onElegir: () => void;
  extra?: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onElegir}
      aria-pressed={elegida}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 12,
        width: '100%',
        textAlign: 'left',
        padding: '11px 14px',
        border: 'none',
        borderBottom: '1px solid var(--linea-suave)',
        background: elegida ? 'var(--acento-suave)' : 'transparent',
        cursor: 'pointer',
        font: 'inherit',
        color: 'var(--tx)',
      }}
    >
      {/* Radio dibujado a mano: un input nativo no toma el color del tema. */}
      <span
        style={{
          width: 15,
          height: 15,
          borderRadius: '50%',
          flexShrink: 0,
          border: `2px solid ${elegida ? 'var(--acento)' : 'var(--linea-fuerte)'}`,
          background: elegida
            ? 'radial-gradient(circle, var(--acento) 0 45%, transparent 46%)'
            : 'transparent',
        }}
      />

      <span style={{ flex: 1, minWidth: 0 }}>
        <span style={{ fontSize: 'var(--txt-cuerpo)', fontWeight: elegida ? 600 : 500 }}>{nombre}</span>
        {/*
          Sobre la fila seleccionada el fondo se oscurece, y con el gris de
          `--tx3` el contraste cae por debajo de 4,5:1. Se sube a `--tx2` sólo
          en ese caso.
        */}
        <span
          className="sub"
          style={{ display: 'block', marginTop: 2, color: elegida ? 'var(--tx2)' : undefined }}
        >
          {detalle}
        </span>
      </span>

      {extra}
    </button>
  );
}

/**
 * Apariencia.
 *
 * Está en Configuración pero no configura la organización: es una preferencia de
 * quien mira. Se guarda en el navegador, no en la base, y por eso no requiere
 * ninguna patente — cambiar el color de la propia pantalla no es una operación
 * sobre los datos de nadie.
 */
export function Apariencia() {
  const t = useT();
  const { tema, pintado, densidad, movimientoReducido, elegirTema, elegirDensidad,
          elegirMovimiento, migradoDesde, descartarAviso } = useTema();
  const { idioma, idiomas, cambiar: cambiarIdioma, faltantes } = useIdioma();

  return (
    <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
      <ConfigNav />
      <div className="principal">
        <Encabezado
          titulo={t('config.apariencia', 'Apariencia')}
          contexto={t('apar.contexto', 'Preferencia de quien usa el sistema, guardada en este navegador')}
        />

        <div className="cuerpo">
          {/*
            Aviso de una sola vez. Quien tenia Carbon entra un dia y la pantalla
            cambio: se entera una vez de por que, y no vuelve a verlo. La
            alternativa —dejar el tema viejo en el selector sin mantenerlo— es
            peor, porque promete algo que ya no existe.
          */}
          {migradoDesde ? (
            <div className="aviso info" style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
              <span style={{ flex: 1 }}>
                {t('config.temaUnificadoA', 'El tema')}{' '}
                <b>{NOMBRES_RETIRADOS[migradoDesde] ?? migradoDesde}</b>{' '}
                {t('config.temaUnificadoB',
                   'se unifico con los dos que quedan. Tu configuracion ya quedo migrada.')}
              </span>
              <button type="button" className="btn plano" onClick={descartarAviso}>
                {t('config.entendido', 'Entendido')}
              </button>
            </div>
          ) : null}

          <div className="dos-columnas">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              <div className="tarjeta">
                <header>
                  <h2>{t('apar.tema', 'Tema')}</h2>
                  <span className="nota">{TEMAS.find((t) => t.id === tema)?.nombre}</span>
                </header>

                {/* `op` y no `t`: `t` es la función de traducción y `tema` es el
                    tema elegido. */}
                {TEMAS.map((op) => (
                  <Opcion
                    key={op.id}
                    nombre={t(op.clave, op.nombre)}
                    detalle={t(op.claveDetalle, op.detalle)}
                    elegida={op.id === tema}
                    onElegir={() => elegirTema(op.id as IdTema)}
                    extra={
                      // `sistema` no tiene paleta propia: se muestra la que
                      // esta pintando el navegador ahora mismo, que es
                      // exactamente lo que esa opcion significa.
                      <Miniatura tema={op.id === 'sistema' ? pintado : (op.id as 'oscuro' | 'claro')} />
                    }
                  />
                ))}
              </div>

              {/*
                El idioma va con el tema y la densidad porque es lo mismo: una
                preferencia de quien usa el sistema. La diferencia es que ésta
                además se guarda en el usuario, así que lo acompaña a otra
                máquina.
              */}
              <div className="tarjeta">
                <header>
                  <h2>{t('config.idioma', 'Idioma')}</h2>
                  <span className="nota">
                    {idiomas.find((i) => i.codigo === idioma)?.nombre ?? idioma}
                  </span>
                </header>

                {idiomas.length === 0 ? (
                  <div className="nota" style={{ padding: '4px 0 8px' }}>
                    {t('config.idiomasNoLeidos', 'No se pudo leer la lista de idiomas.')}
                  </div>
                ) : (
                  idiomas.map((i) => (
                    <Opcion
                      key={i.codigo}
                      nombre={i.nombre}
                      detalle={
                        i.esPorDefecto
                          ? t('config.idiomaDeFabrica', 'El idioma de fábrica del sistema.')
                          : t('config.idiomaCambia',
                              'Cambia la navegación y las pantallas traducidas.')
                      }
                      elegida={i.codigo === idioma}
                      onElegir={() => cambiarIdioma(i.codigo)}
                    />
                  ))
                )}

                {faltantes.length > 0 ? (
                  <div className="nota" style={{ marginTop: 10 }}>
                    {t('config.faltanEnEsteIdioma', 'En este idioma faltan')} <b>{faltantes.length}</b>{' '}
                    {faltantes.length === 1 ? 'clave' : 'claves'}. Donde falta, la pantalla
                    muestra un texto de reserva en lugar de quedar en blanco.
                  </div>
                ) : null}
              </div>

              <div className="tarjeta">
                <header>
                  <h2>{t('apar.densidad', 'Densidad')}</h2>
                  <span className="nota">{DENSIDADES.find((d) => d.id === densidad)?.nombre}</span>
                </header>

                {DENSIDADES.map((d) => (
                  <Opcion
                    key={d.id}
                    nombre={t(d.clave, d.nombre)}
                    detalle={t(d.claveDetalle, d.detalle)}
                    elegida={d.id === densidad}
                    onElegir={() => elegirDensidad(d.id as IdDensidad)}
                    extra={<MuestraDensidad alto={ALTO_FILA[d.id]} />}
                  />
                ))}

                <div className="nota" style={{ padding: '10px 14px' }}>
                  {t('config.densidadNota',
                     'La densidad cambia el alto de fila y el espaciado, nunca el tamano de la '
                     + 'letra: achicar el texto es la forma mas rapida de romper el contraste.')}
                </div>
              </div>

              <div className="tarjeta">
                <header>
                  <h2>{t('config.accesibilidad', 'Accesibilidad')}</h2>
                </header>

                <Opcion
                  nombre={t('config.reducirMovimiento', 'Reducir movimiento')}
                  detalle={t('config.reducirMovimientoDetalle',
                             'Saca el brillo que recorre los esqueletos de carga. Si ya lo pediste '
                             + 'en el sistema operativo, esto no hace falta: se respeta igual.')}
                  elegida={movimientoReducido}
                  onElegir={() => elegirMovimiento(!movimientoReducido)}
                />

                {/*
                  No es un interruptor y no tiene que parecerlo: el requisito de
                  no depender del color no es una preferencia que se apague.
                */}
                <div className="nota" style={{ padding: '10px 14px' }}>
                  {t('config.formaSiempre',
                     'El riesgo y la prioridad se leen ademas por forma y por texto, no solo por '
                     + 'color. Eso esta siempre activo y no se puede desactivar: es lo que hace '
                     + 'que la tabla sirva para alguien que no distingue rojo de verde.')}
                </div>
              </div>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              {/*
                Vista previa con los elementos que más color usan. Si un tema
                rompe la legibilidad de algo, se ve acá antes de tener que
                recorrer el sistema.
              */}
              <div className="tarjeta">
                <header>
                  <h2>{t('apar.vistaPrevia', 'Vista previa')}</h2>
                </header>

                <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 12 }}>
                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    <span className="chip alto">{t('analisis.riesgoAlto', 'Riesgo alto')}</span>
                    <span className="chip medio">{t('analisis.riesgoMedio', 'Riesgo medio')}</span>
                    <span className="chip bajo">{t('garantia.vigente', 'Vigente')}</span>
                    <span className="chip">{t('config.temaAutomatico', 'Automática')}</span>
                  </div>

                  <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap' }}>
                    {[
                      ['Operativo', 'var(--bajo-barra)'],
                      ['En observación', 'var(--medio-barra)'],
                      ['En reparación', 'var(--alto-barra)'],
                      ['Dado de baja', 'var(--tx3)'],
                    ].map(([texto, color]) => (
                      <span key={texto} className="estado">
                        <span className="punto" style={{ background: color }} />
                        {texto}
                      </span>
                    ))}
                  </div>

                  <div className="aviso atencion">
                    {t('config.ejemploNotificacion', '6 incidencias fueron asignadas por respaldo y están pendientes de revisión.')}
                  </div>

                  <div className="aviso info">
                    {t('apar.ejemploAviso', '47 equipos evaluados · 3 en riesgo alto y 5 en riesgo medio.')}
                  </div>

                  <div style={{ display: 'flex', gap: 8 }}>
                    <button type="button" className="btn pri">
                      {t('config.accionPrincipal', 'Acción principal')}
                    </button>
                    <button type="button" className="btn">
                      {t('apar.ejemploSecundaria', 'Secundaria')}
                    </button>
                    <button type="button" className="btn plano">
                      {t('apar.ejemploPlana', 'Plana')}
                    </button>
                  </div>

                  <table>
                    <thead>
                      <tr>
                        <th>{t('incidencia.equipo', 'Equipo')}</th>
                        <th style={{ width: 70 }}>{t('comun.riesgo', 'Riesgo')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr>
                        <td className="mono">PC-ADM-014</td>
                        <td>
                          <span className="estado">
                            <span className="punto" style={{ background: 'var(--alto-barra)' }} />
                            <span className="mono" style={{ fontWeight: 600 }}>
                              92
                            </span>
                          </span>
                        </td>
                      </tr>
                      <tr>
                        <td className="mono">NB-COM-007</td>
                        <td>
                          <span className="estado">
                            <span className="punto" style={{ background: 'var(--medio-barra)' }} />
                            <span className="mono" style={{ fontWeight: 600 }}>
                              51
                            </span>
                          </span>
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>

              <div className="tarjeta">
                <header>
                  <h2>{t('config.queNoCambia', 'Qué no cambia')}</h2>
                </header>
                <div style={{ padding: 14 }} className="sub">
                  {t('config.coloresNota', 'Los cuatro colores de estado —alto, medio, bajo e IA— se conservan como familias en todos los temas. El riesgo alto es rojizo siempre: cambiarle el color a un semáforo lo vuelve inútil. Un tema cambia las superficies, el texto y el color de marca, nunca el significado.')}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
