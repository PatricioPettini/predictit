import type { ReactNode } from 'react';

/**
 * Una cifra del encabezado de pantalla, ya formateada por quien la pasa: acá
 * no hay lógica de API ni de idioma.
 *
 * `tono` pinta la barra de color del borde izquierdo y la cifra. Es una barra
 * y no un ícono de color porque la tarjeta ya tiene un ícono, y dos señales de
 * riesgo en la misma esquina compiten entre sí; la barra ocupa un lado que no
 * usaba nadie y se lee de reojo, que es para lo que sirve un tablero.
 */
export function Indicador({ titulo, valor, detalle, icono, tono }: {
  titulo: string;
  valor: ReactNode;
  detalle?: ReactNode;
  icono?: ReactNode;
  tono?: 'alto' | 'medio' | 'bajo';
}) {
  return (
    <section className={`tarjeta indicador${tono ? ` indicador-${tono}` : ''}`}>
      <div className="indicador-cabecera">
        <h2>{titulo}</h2>
        {icono ? <span className="indicador-icono" aria-hidden="true">{icono}</span> : null}
      </div>
      <div className="indicador-valor">{valor}</div>
      {detalle ? <div className="indicador-detalle">{detalle}</div> : null}
    </section>
  );
}
