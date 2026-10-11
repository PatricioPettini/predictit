import { useCallback } from 'react';
import { useT } from './IdiomaContext';

/** La firma, para los helpers de módulo que la reciben en lugar del hook. */
export type Plural = (
  cantidad: number,
  clave: string,
  uno: string,
  varios: string,
) => string;

/**
 * Singular y plural, con una clave para cada forma.
 *
 * Los subtítulos de las pantallas son casi todos del mismo tipo: «1 equipo
 * registrado» o «52 equipos registrados». Estaban armados con un ternario y
 * una plantilla en cada pantalla, y eso no se puede traducir: en castellano
 * cambian el número, el sustantivo y el adjetivo a la vez, y en otros idiomas
 * las formas no son dos.
 *
 * Son dos claves y no una con un contador a propósito. Armar la frase pegando
 * el número a un sustantivo —`${n} + ' equipos'`— funciona en castellano y se
 * rompe en cuanto el idioma pone el número en otro lugar o declina el
 * sustantivo. Cada forma es una frase completa con su `{n}` adentro, y quien
 * traduce la mueve donde corresponda.
 *
 * El castellano queda en el lugar donde se usa, como en `t`: es lo que se lee
 * si al diccionario le falta la entrada.
 *
 *     const p = usePlural();
 *     p(total, 'activo.registrado', '1 equipo registrado', '{n} equipos registrados')
 *
 * La clave que se pasa es la raíz: se le agrega `Uno` o `Varios`.
 */
export function usePlural(): Plural {
  const t = useT();

  return useCallback(
    (cantidad: number, clave: string, uno: string, varios: string): string =>
      (cantidad === 1
        ? t(clave + 'Uno', uno, { n: cantidad })
        : t(clave + 'Varios', varios, { n: cantidad })),
    [t],
  );
}
