import { useCallback } from 'react';
import { useT } from './IdiomaContext';
import { useFormato } from './formato';

/**
 * Cuánto hace que pasó algo, en la unidad que se lee de un vistazo.
 *
 * «hace 2880 min» no le dice nada a nadie: quien mira la lista necesita saber
 * si fue hoy o la semana pasada. Tres pantallas tenían su propia copia de esta
 * escala, cada una con un corte distinto y las tres en castellano fijo.
 *
 * `min`, `h` y `d` son abreviaturas de unidad y viajan por el diccionario
 * igual que el resto: en inglés la escala se escribe distinto y el orden puede
 * cambiar, así que cada forma es una frase completa con su `{n}` adentro.
 */
export function useHace() {
  const t = useT();
  const { hora } = useFormato();

  return useCallback(
    (iso: string, opciones?: { conHoraDeHoy?: boolean }): string => {
      const ms = Date.now() - new Date(iso).getTime();
      const minutos = Math.max(0, Math.round(ms / 60000));

      if (minutos < 60) return t('hace.minutos', 'hace {n} min', { n: minutos });

      const horas = Math.round(minutos / 60);
      if (horas < 24) return t('hace.horas', 'hace {n} h', { n: horas });

      const dias = Math.round(horas / 24);

      // El panel predictivo muestra la hora cuando la alerta es de hoy: en una
      // lista de alertas del día, «hace 3 h» y «hace 5 h» se distinguen peor
      // que dos horas del reloj.
      if (dias === 0 && opciones?.conHoraDeHoy) {
        return t('hace.hoyALas', 'hoy {hora}', { hora: hora(iso) });
      }

      if (dias === 1) return t('hace.ayer', 'ayer');
      if (dias < 30) return t('hace.dias', 'hace {n} d', { n: dias });

      const meses = Math.round(dias / 30.44);
      return meses === 1
        ? t('hace.unMes', 'hace 1 mes')
        : t('hace.meses', 'hace {n} meses', { n: meses });
    },
    [t, hora],
  );
}
