import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { Activos } from '../paginas/Activos';
import type { CatalogosEquipoDto, FiltroEquipos } from '../api/tipos';

/**
 * Las píldoras de segmento del inventario.
 *
 * Lo que hay que probar no es el número —ése lo calcula el servidor y tiene sus
 * propias pruebas— sino que la píldora **pida lo que promete**: el número está
 * al lado del rótulo y quien lo pulsa espera ver esos equipos. Si el listado
 * no se recortara, la píldora quedaría marcada mostrando el parque entero, que
 * es peor que no tenerla.
 */

const catalogos: CatalogosEquipoDto = {
  tipos: [{ id: 't1', nombre: 'PC de escritorio' }],
  estados: [{ id: 'e1', nombre: 'Operativo', operativo: true }],
  ubicaciones: [{ id: 'u1', nombre: 'Administración' }],
  responsables: [{ id: 'r1', nombreCompleto: 'Gabriela Suárez' }],
};

const buscar = vi.fn();
const segmentos = vi.fn();

vi.mock('../api/cliente', async () => {
  const real = await vi.importActual<typeof import('../api/cliente')>('../api/cliente');
  return {
    ...real,
    api: {
      equipos: {
        buscar: (f: FiltroEquipos) => buscar(f),
        segmentos: (f: FiltroEquipos) => segmentos(f),
        catalogos: () => Promise.resolve(catalogos),
      },
    },
  };
});

vi.mock('../sesion/SesionContext', () => ({
  useSesion: () => ({ puede: () => true }),
  useCerrarSiExpiro: () => () => {},
}));

vi.mock('../idioma/IdiomaContext', () => ({
  useT: () => (_clave: string, porDefecto: string, datos?: Record<string, unknown>) =>
    Object.entries(datos ?? {}).reduce(
      (texto, [nombre, valor]) => texto.replaceAll(`{${nombre}}`, String(valor)),
      porDefecto,
    ),
  usePlural: () => (n: number, uno: string, varios: string) => (n === 1 ? uno : varios),
}));

function dibujar() {
  buscar.mockImplementation(() =>
    Promise.resolve({ items: [], total: 0, pagina: 1, porPagina: 20 }));
  segmentos.mockImplementation(() =>
    Promise.resolve({ todos: 52, riesgoAlto: 3, preventivoVencido: 1, garantiaVencida: 38 }));

  return render(
    <MemoryRouter>
      <Activos />
    </MemoryRouter>,
  );
}

/** El último filtro con el que se pidió el listado. */
const ultimoFiltro = (): FiltroEquipos => buscar.mock.calls.at(-1)![0];

describe('Los segmentos del inventario', () => {
  it('cada píldora muestra su recuento', async () => {
    dibujar();

    const riesgo = await screen.findByRole('button', { name: /Riesgo alto/ });
    expect(riesgo.textContent).toContain('3');
    expect((await screen.findByRole('button', { name: /Todos/ })).textContent).toContain('52');
  });

  it('pulsarla recorta el listado por ese segmento', async () => {
    dibujar();

    fireEvent.click(await screen.findByRole('button', { name: /Riesgo alto/ }));

    await waitFor(() => expect(ultimoFiltro().segmento).toBe('riesgoAlto'));
    // Y vuelve a la primera página: quedarse en la cuarta de un listado que
    // ahora tiene tres filas muestra una tabla vacía.
    expect(ultimoFiltro().pagina).toBe(1);
  });

  it('pulsarla de nuevo vuelve al parque entero', async () => {
    dibujar();

    const riesgo = await screen.findByRole('button', { name: /Riesgo alto/ });
    fireEvent.click(riesgo);
    await waitFor(() => expect(ultimoFiltro().segmento).toBe('riesgoAlto'));

    fireEvent.click(riesgo);
    await waitFor(() => expect(ultimoFiltro().segmento).toBeUndefined());
  });

  it('la píldora activa lo dice, y no sólo con el color', async () => {
    // El color por sí solo no llega a un lector de pantalla ni a quien no lo
    // distingue: `aria-pressed` es lo que hace que el estado exista.
    dibujar();

    const riesgo = await screen.findByRole('button', { name: /Riesgo alto/ });
    expect(riesgo.getAttribute('aria-pressed')).toBe('false');

    fireEvent.click(riesgo);
    await waitFor(() => expect(riesgo.getAttribute('aria-pressed')).toBe('true'));
  });

  it('los recuentos no se piden con el segmento puesto', async () => {
    // Si viajaran con el segmento, al pulsar «Riesgo alto» los cuatro números
    // pasarían a contar sólo dentro de ese segmento y las otras tres píldoras
    // dirían cero.
    dibujar();

    fireEvent.click(await screen.findByRole('button', { name: /Riesgo alto/ }));

    await waitFor(() => expect(ultimoFiltro().segmento).toBe('riesgoAlto'));
    for (const [f] of segmentos.mock.calls) expect(f.segmento).toBeUndefined();
  });

  it('sin recuentos las píldoras siguen filtrando', async () => {
    // Los números son una guía, no el contenido de la pantalla: si la consulta
    // falla, el inventario tiene que seguir usable.
    buscar.mockImplementation(() =>
      Promise.resolve({ items: [], total: 0, pagina: 1, porPagina: 20 }));
    segmentos.mockImplementation(() => Promise.reject(new Error('sin red')));

    render(<MemoryRouter><Activos /></MemoryRouter>);

    const riesgo = await screen.findByRole('button', { name: /Riesgo alto/ });
    fireEvent.click(riesgo);

    await waitFor(() => expect(ultimoFiltro().segmento).toBe('riesgoAlto'));
  });
});
