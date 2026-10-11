import { useMemo } from 'react';
import { useIdioma } from './IdiomaContext';

/**
 * Fechas, horas y dinero en el idioma elegido.
 *
 * Cada pantalla traía su propio `toLocaleDateString('es-AR', …)` escrito a
 * mano. Eso hace dos cosas mal: repite el mismo formato en catorce lugares, y
 * —lo que importa— deja la fecha en formato argentino aunque la interfaz esté
 * en inglés. Con el idioma en `en-US`, un `04/09/2026` se lee como 9 de abril
 * y no como 4 de septiembre: no es un detalle estético, es una fecha
 * equivocada.
 *
 * La moneda es la excepción y no cambia con el idioma: el sistema factura en
 * pesos independientemente del idioma en que se lo mire. Lo que cambia es
 * cómo se escribe el número —el separador de miles y el de decimales—, no la
 * moneda.
 */

/** La moneda del sistema. No depende del idioma de la interfaz. */
const MONEDA = 'ARS';

export interface Formato {
  /** 04/09/26 */
  fecha: (iso: string | null | undefined) => string;
  /** 04/09/26, 14:32 */
  fechaHora: (iso: string | null | undefined) => string;
  /** 04/09/26, 14:32:07 */
  fechaHoraSegundos: (iso: string | null | undefined) => string;
  /** 14:32 */
  hora: (iso: string | null | undefined) => string;
  /** $ 12.500 */
  dinero: (valor: number | null | undefined) => string;
  /** 12.500 */
  numero: (valor: number | null | undefined) => string;
  /** septiembre de 2026 */
  mesYAnio: (iso: string | null | undefined) => string;
}

export function useFormato(): Formato {
  const { idioma } = useIdioma();

  return useMemo(() => {
    // Se construyen una vez por idioma: `Intl.DateTimeFormat` es caro de crear
    // y estas funciones se llaman una vez por fila de tabla.
    const soloFecha = new Intl.DateTimeFormat(idioma, {
      day: '2-digit', month: '2-digit', year: '2-digit',
    });
    const conHora = new Intl.DateTimeFormat(idioma, {
      day: '2-digit', month: '2-digit', year: '2-digit',
      hour: '2-digit', minute: '2-digit',
    });
    const conSegundos = new Intl.DateTimeFormat(idioma, {
      day: '2-digit', month: '2-digit', year: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit',
    });
    const soloHora = new Intl.DateTimeFormat(idioma, {
      hour: '2-digit', minute: '2-digit',
    });
    const moneda = new Intl.NumberFormat(idioma, {
      style: 'currency', currency: MONEDA, maximumFractionDigits: 0,
    });
    const cantidad = new Intl.NumberFormat(idioma);
    const mesLargo = new Intl.DateTimeFormat(idioma, { month: 'long', year: 'numeric' });

    // Una fecha ausente se dibuja como raya y no como «Invalid Date»: en una
    // tabla, la raya se lee como «no hay dato» y el error como un bug.
    const conFecha = (f: Intl.DateTimeFormat) =>
      (iso: string | null | undefined) => {
        if (!iso) return '—';
        const d = new Date(iso);
        return isNaN(d.getTime()) ? '—' : f.format(d);
      };

    return {
      fecha: conFecha(soloFecha),
      fechaHora: conFecha(conHora),
      fechaHoraSegundos: conFecha(conSegundos),
      hora: conFecha(soloHora),
      dinero: (v) => (v == null ? '—' : moneda.format(v)),
      numero: (v) => (v == null ? '—' : cantidad.format(v)),
      mesYAnio: conFecha(mesLargo),
    };
  }, [idioma]);
}
