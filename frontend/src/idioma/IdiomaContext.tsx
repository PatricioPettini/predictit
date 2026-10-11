import {
  createContext, useCallback, useContext, useEffect, useMemo, useRef, useState,
} from 'react';
import type { ReactNode } from 'react';
import { api } from '../api/cliente';
import type { DiccionarioDto, IdiomaDto } from '../api/tipos';

/**
 * Idioma de la interfaz.
 *
 * El diccionario vive en la base y lo sirve la API, no en archivos JSON del
 * frontend. Es una decisión del proyecto y no una comodidad: el sistema es
 * multi-organización y un cliente puede querer cambiarle el nombre a una
 * pantalla sin que eso implique compilar y desplegar el frontend.
 *
 * El endpoint del diccionario es anónimo a propósito: la pantalla de inicio de
 * sesión también está traducida, y ahí todavía no hay token.
 *
 * Lo que falta no se rompe: `t('clave.que.no.existe')` devuelve un texto de
 * reserva y anota la clave. Un cartel vacío o un «undefined» en la pantalla es
 * peor que el texto en el otro idioma.
 */

interface Estado {
  /**
   * Traduce una clave.
   *
   * `porDefecto` es el texto en castellano, escrito en el lugar donde se usa.
   * No es una redundancia con el diccionario de la base: es lo que hace que
   * una clave que falta muestre la frase correcta en lugar de un pedazo de
   * clave, y que la pantalla se pueda leer aunque la API no conteste. El
   * diccionario traduce; el código dice qué.
   *
   * `datos` reemplaza los {marcadores} del texto.
   */
  t: (clave: string, porDefecto: string,
      datos?: Record<string, string | number>) => string;
  idioma: string;
  idiomas: IdiomaDto[];
  cambiar: (codigo: string) => void;
  cargando: boolean;
  /** Claves que se pidieron y no estaban. Se muestran en Apariencia. */
  faltantes: string[];
}

/** Los {marcadores} de un texto, reemplazados por sus valores. */
function conDatos(texto: string, datos?: Record<string, string | number>): string {
  if (!datos) return texto;
  return Object.entries(datos).reduce(
    (acumulado, [nombre, valor]) => acumulado.replaceAll(`{${nombre}}`, String(valor)),
    texto);
}

const CLAVE_GUARDADA = 'predictit.idioma';

const Contexto = createContext<Estado | null>(null);

function leerGuardado(): string | null {
  try {
    return localStorage.getItem(CLAVE_GUARDADA);
  } catch {
    // Un navegador que no deja guardar no es motivo para no funcionar.
    return null;
  }
}

export function ProveedorIdioma({ children }: { children: ReactNode }) {
  const [diccionario, setDiccionario] = useState<DiccionarioDto | null>(null);
  const [idiomas, setIdiomas] = useState<IdiomaDto[]>([]);
  const [faltantes, setFaltantes] = useState<string[]>([]);

  // Las claves ya anotadas, para no encolar la misma en cada render.
  const faltantesVistas = useRef(new Set<string>());

  // El idioma elegido se guarda en el navegador igual que el tema: es una
  // preferencia de la persona y tiene que sobrevivir al cierre de sesión.
  const [pedido, setPedido] = useState<string | null>(leerGuardado);

  useEffect(() => {
    api.idiomas
      .diccionario(pedido ?? undefined)
      .then((d) => {
        setDiccionario(d);
        setFaltantes([]);
      })
      // Si el diccionario no carga, la interfaz muestra las claves de reserva
      // en lugar de quedar en blanco.
      .catch(() => setDiccionario(null));
  }, [pedido]);

  useEffect(() => {
    api.idiomas.listar().then(setIdiomas).catch(() => setIdiomas([]));
  }, []);

  const cambiar = useCallback((codigo: string) => {
    setPedido(codigo);
    try {
      localStorage.setItem(CLAVE_GUARDADA, codigo);
    } catch {
      // Idem: se pierde la preferencia, no la funcionalidad.
    }

    // Y se guarda también en el usuario, para que lo acompañe a otra máquina.
    // Falla en silencio cuando no hay sesión: en la pantalla de login el
    // idioma es sólo del navegador.
    api.idiomas.cambiar(codigo).catch(() => {});
  }, []);

  const valor = useMemo<Estado>(() => {
    const textos = diccionario?.textos ?? {};

    return {
      idioma: diccionario?.codigo ?? pedido ?? 'es-AR',
      idiomas,
      cambiar,
      cargando: diccionario === null,
      faltantes,

      t: (clave, porDefecto, datos) => {
        const texto = textos[clave];

        if (texto === undefined) {
          // No se avisa con una excepción ni con un cartel: se anota y se usa
          // el texto del código. Una clave que falta es un defecto de datos,
          // no un motivo para dejar la pantalla inservible.
          //
          // La anotación va en el siguiente turno y no acá: `t` se llama
          // durante el render, y cambiar el estado de este proveedor mientras
          // otro componente se está dibujando es lo que React avisa con
          // «Cannot update a component while rendering a different one». La
          // consola se llenaba de ese error en cada pantalla.
          if (!faltantesVistas.current.has(clave)) {
            faltantesVistas.current.add(clave);
            queueMicrotask(() =>
              setFaltantes((previas) =>
                previas.includes(clave) ? previas : [...previas, clave]));
          }
        }

        // Los marcadores se reemplazan también sobre el texto de reserva. Sin
        // esto, una clave que falta mostraba «acumula {minimo} incidencias»
        // con las llaves a la vista: el peor de los dos mundos, porque el
        // texto de reserva existe justamente para que la pantalla se pueda
        // leer cuando el diccionario no tiene la entrada.
        return conDatos(texto ?? porDefecto, datos);
      },
    };
  }, [diccionario, idiomas, cambiar, faltantes, pedido]);

  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}

const SIN_PROVEEDOR: Estado = {
  // También acá se reemplazan los marcadores. Sin esto una pantalla montada
  // sin el proveedor mostraba «Con {equipos} equipos, el {plan} costaría
  // {costo}», que es peor que no traducir: no se lee.
  t: (_clave, porDefecto, datos) => conDatos(porDefecto, datos),
  idioma: 'es-AR',
  idiomas: [],
  cambiar: () => {},
  cargando: false,
  faltantes: [],
};

/**
 * Fuera del proveedor devuelve el castellano del código en lugar de fallar.
 *
 * Es para que una prueba pueda montar una pantalla suelta sin armar el árbol
 * entero de proveedores. En la aplicación real el proveedor está siempre: lo
 * monta `main.tsx` por encima de la sesión.
 */
export function useIdioma(): Estado {
  return useContext(Contexto) ?? SIN_PROVEEDOR;
}

/** Atajo para el caso más común, que es traducir y nada más. */
export function useT() {
  return useIdioma().t;
}

/** El tipo del traductor, para las funciones que lo reciben por parámetro. */
export type Traducir = Estado['t'];
