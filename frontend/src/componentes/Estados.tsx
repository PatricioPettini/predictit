import { useEffect, useRef, type ReactNode } from 'react';
import { useT } from '../idioma/IdiomaContext';

/**
 * Los dos estados que una tabla tiene antes de tener datos.
 *
 * Hasta ahora los dos eran un renglón de texto gris centrado, y la diferencia
 * entre «todavía no cargó» y «no hay nada» era leer la frase. Son cosas
 * distintas: en la primera hay que esperar y en la segunda hay que hacer algo,
 * y la persona decide eso en el primer vistazo, sin leer.
 */

/**
 * Esqueleto de carga: la forma de la tabla, sin los datos.
 *
 * Se dibuja la grilla y no un cartel de «cargando» por dos razones. La página
 * no salta cuando llegan los datos, porque el alto ya está ocupado; y se ve de
 * entrada cuántas columnas tiene la tabla, así que la espera se siente más
 * corta aunque dure lo mismo.
 */
export function EsqueletoTabla({
  columnas = 4,
  filas = 6,
}: {
  columnas?: number;
  filas?: number;
}) {
  const t = useT();
  return (
    <div className="esqueleto" role="status" aria-label={t('comun.cargando', 'Cargando…')}>
      {Array.from({ length: filas }).map((_, f) => (
        <div className="esqueleto-fila" key={f} aria-hidden="true">
          {Array.from({ length: columnas }).map((_, c) => (
            <span
              className="esqueleto-celda"
              key={c}
              // Anchos distintos por columna: una grilla de bloques iguales se
              // lee como un patrón y no como texto que está por aparecer.
              style={{ width: `${[70, 45, 60, 35, 50, 40][c % 6]}%` }}
            />
          ))}
        </div>
      ))}
    </div>
  );
}

/**
 * Estado vacío: qué no hay, por qué, y qué se puede hacer.
 *
 * `accion` es opcional a propósito. Cuando lo que falta es que alguien cargue
 * algo, el botón ahorra el viaje a buscar dónde se hace; cuando lo que falta
 * es que pase algo en el mundo —ninguna incidencia abierta, por ejemplo— no
 * hay nada que ofrecer y un botón sería ruido.
 */
export function SinDatos({
  titulo,
  detalle,
  accion,
  filtrado = false,
}: {
  titulo: string;
  detalle?: string;
  accion?: ReactNode;
  filtrado?: boolean;
}) {
  return (
    <div className="sin-datos">
      <span className="sin-datos-marca" aria-hidden="true">
        {filtrado ? <IconoLupa /> : <IconoBandeja />}
      </span>
      <p className="sin-datos-titulo">{titulo}</p>
      {detalle ? <p className="sin-datos-detalle">{detalle}</p> : null}
      {accion ? <div className="sin-datos-accion">{accion}</div> : null}
    </div>
  );
}

function IconoBandeja() {
  return (
    <svg width="28" height="28" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"
         strokeLinejoin="round">
      <path d="M3 14h4l1.5 3h7L17 14h4" />
      <path d="M4.5 6.5 3 14v4a1 1 0 0 0 1 1h16a1 1 0 0 0 1-1v-4l-1.5-7.5A1 1 0 0 0 18.5 6h-13a1 1 0 0 0-1 .5Z" />
    </svg>
  );
}

function IconoLupa() {
  return (
    <svg width="28" height="28" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"
         strokeLinejoin="round">
      <circle cx="11" cy="11" r="6.5" />
      <path d="m16 16 4.5 4.5" />
    </svg>
  );
}

/**
 * El aviso de que el formulario no se pudo enviar, y por qué.
 *
 * Va arriba del formulario y no abajo del botón. Cuando el motivo está al
 * final, quien envía un formulario largo aprieta «Guardar», no pasa nada
 * visible, y la explicación quedó tres pantallas más abajo.
 *
 * Toma el foco al aparecer. Es lo que hace que un lector de pantalla lo lea
 * —`role="alert"` lo anuncia, pero deja al cursor donde estaba— y de paso
 * lleva la vista de cualquiera al lugar correcto. `tabIndex={-1}` es para que
 * pueda recibir el foco sin entrar en el recorrido del tabulador.
 *
 * El `id` no es decorativo: es el que cada campo invalidado referencia con
 * `aria-describedby`, así el mensaje se vuelve a leer al llegar al campo que
 * lo causó. Ver {@link marcaDeError}.
 */
export function ErrorDeFormulario({ id, mensaje }: { id: string; mensaje: string | null }) {
  const caja = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (mensaje) caja.current?.focus();
  }, [mensaje]);

  if (!mensaje) return null;

  return (
    <div ref={caja} id={id} className="aviso error" role="alert" tabIndex={-1}>
      {mensaje}
    </div>
  );
}

/**
 * Enlaza un campo con el mensaje que lo explica.
 *
 * `aria-invalid` solo dice «este campo está mal» y deja a quien usa un lector
 * de pantalla buscando el motivo por su cuenta. Con `aria-describedby` el
 * motivo se lee junto al campo, que es donde hace falta.
 *
 * Devuelve un objeto vacío cuando el campo no es el que falló, para poder
 * expandirlo siempre sin condicionales en el JSX.
 */
export function marcaDeError(campo: string, campoConError: string | null, idMensaje: string) {
  return campoConError === campo
    ? { 'aria-invalid': true, 'aria-describedby': idMensaje }
    : {};
}
