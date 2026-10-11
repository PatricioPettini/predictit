import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { Incidencias } from '../paginas/Incidencias';
import type {
  CatalogosIncidenciaDto, FiltroIncidencias, IncidenciaListaDto,
} from '../api/tipos';

/**
 * El tablero de incidencias.
 *
 * Lo que hay que probar es de dónde salen las columnas. No están escritas en
 * la pantalla: salen del catálogo, que es de la base, y la última junta todo
 * lo terminado. Si alguien las escribiera a mano, una organización que agregue
 * un estado tendría incidencias que no aparecen en ninguna columna —y eso no
 * se ve, porque el tablero sigue dibujándose bien.
 */

const catalogos: CatalogosIncidenciaDto = {
  estados: [
    { id: 'e1', nombre: 'Pendiente de asignación', esFinal: false },
    { id: 'e2', nombre: 'Asignada', esFinal: false },
    { id: 'e3', nombre: 'En curso', esFinal: false },
    { id: 'e4', nombre: 'Resuelta', esFinal: true },
    { id: 'e5', nombre: 'Cerrada', esFinal: true },
  ],
  prioridades: [{ id: 'p1', nombre: 'Alta' }],
  categorias: [{ id: 'c1', nombre: 'Hardware' }],
  tecnicos: [],
};

const inc = (n: number, estado: string, extra: Partial<IncidenciaListaDto> = {})
  : IncidenciaListaDto => ({
  id: `i-${n}`,
  numero: n,
  titulo: `Incidencia ${n}`,
  codigoEquipo: 'PC-ADM-014',
  estado,
  abierta: !['Resuelta', 'Cerrada'].includes(estado),
  prioridad: 'Alta',
  nivelPrioridad: 3,
  tipoAsignacion: 'AUTOMATICA',
  pendienteRevision: false,
  fecha: '2026-09-15T10:00:00',
  fueraDeObjetivo: false,
  ...extra,
});

const items = [
  inc(1, 'Asignada'),
  inc(2, 'En curso'),
  inc(3, 'En curso'),
  inc(4, 'Resuelta'),
  inc(5, 'Cerrada'),
];

const buscar = vi.fn();

vi.mock('../api/cliente', async () => {
  const real = await vi.importActual<typeof import('../api/cliente')>('../api/cliente');
  return {
    ...real,
    api: {
      incidencias: {
        buscar: (f: FiltroIncidencias) => buscar(f),
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
  useT: () => (_c: string, porDefecto: string, datos?: Record<string, unknown>) =>
    Object.entries(datos ?? {}).reduce(
      (s, [k, v]) => s.replaceAll(`{${k}}`, String(v)), porDefecto),
}));
vi.mock('../idioma/plural', () => ({ usePlural: () => (n: number, u: string, v: string) => (n === 1 ? u : v) }));
vi.mock('../idioma/hace', () => ({ useHace: () => () => 'hace 1 d' }));

function dibujar(total = items.length) {
  buscar.mockImplementation((f: FiltroIncidencias) =>
    Promise.resolve({ items, total, pagina: 1, porPagina: f.porPagina ?? 20 }));
  return render(<MemoryRouter><Incidencias /></MemoryRouter>);
}

/** Pasa a la vista de tablero y espera a que aparezca. */
async function alTablero() {
  dibujar();
  fireEvent.click(await screen.findByRole('button', { name: 'Tablero' }));
  return screen.findByRole('link', { name: /Incidencia 1/ });
}

/**
 * La columna cuyo encabezado dice ese rótulo.
 *
 * Se busca dentro del tablero y no en la pantalla entera: los nombres de los
 * estados están también en el desplegable de filtros, y `getByText` encuentra
 * los dos.
 */
function columna(rotulo: string): HTMLElement {
  const tablero = document.querySelector('.tablero') as HTMLElement;
  const enc = within(tablero).getByText(rotulo);
  return enc.closest('section')!;
}

describe('El tablero de incidencias', () => {
  it('arranca en la lista, no en el tablero', async () => {
    dibujar();
    // La lista es lo que ya estaba y lo que la mayoría espera al entrar.
    expect((await screen.findByRole('button', { name: 'Lista' })).getAttribute('aria-pressed'))
      .toBe('true');
  });

  it('las columnas salen del catálogo y en su orden', async () => {
    await alTablero();

    const rotulos = [...document.querySelectorAll('.tablero-columna > header > span:first-child')]
      .map((e) => e.textContent);

    // El orden del ciclo lo define la base, y la última junta lo terminado.
    expect(rotulos).toEqual([
      'Pendiente de asignación', 'Asignada', 'En curso', 'Terminadas',
    ]);
  });

  it('cada incidencia cae en la columna de su estado', async () => {
    await alTablero();

    expect(within(columna('En curso')).getAllByRole('link')).toHaveLength(2);
    expect(within(columna('Asignada')).getAllByRole('link')).toHaveLength(1);
  });

  it('las terminadas se juntan en una sola columna', async () => {
    await alTablero();

    // Resuelta y Cerrada son dos estados y una sola columna: un tablero
    // muestra dónde está el trabajo, y lo terminado no tiene tres lugares.
    expect(within(columna('Terminadas')).getAllByRole('link')).toHaveLength(2);
  });

  it('la columna de terminadas se corta, y su recuento sigue siendo el real', async () => {
    // Creciendo sin techo aplastaba a las columnas que importan. Lo que no se
    // puede es que el número de arriba mienta: si dice 6 habiendo 9, la
    // columna afirma algo falso sobre cuánto se cerró.
    buscar.mockImplementation((f: FiltroIncidencias) => Promise.resolve({
      items: [inc(7, 'Asignada'), ...Array.from({ length: 9 }, (_, k) => inc(100 + k, 'Cerrada'))],
      total: 10, pagina: 1, porPagina: f.porPagina ?? 20,
    }));
    render(<MemoryRouter><Incidencias /></MemoryRouter>);
    fireEvent.click(await screen.findByRole('button', { name: 'Tablero' }));
    await waitFor(() => expect(document.querySelector('.tablero')).toBeTruthy());

    const col = columna('Terminadas');
    expect(within(col).getAllByRole('link')).toHaveLength(6);
    expect(within(col).getByText('y 3 más')).toBeTruthy();
    expect(within(col).getByText('9')).toBeTruthy();
  });

  it('una columna sin nada lo dice, en vez de quedar en blanco', async () => {
    await alTablero();

    expect(within(columna('Pendiente de asignación')).getByText('Nada acá')).toBeTruthy();
  });

  it('el tablero pide más que la lista', async () => {
    await alTablero();

    // Un tablero paginado no es un tablero: esconde justo lo que se vino a ver.
    const ultimo: FiltroIncidencias = buscar.mock.calls.at(-1)![0];
    expect(ultimo.porPagina).toBe(200);
    expect(ultimo.pagina).toBe(1);
  });

  it('avisa cuando lo que muestra es un recorte, con lo que realmente muestra', async () => {
    // Un tablero recortado que no lo dice miente sobre el estado del trabajo.
    // Y el número tiene que salir de la respuesta y no del tope: interpolando
    // la constante, el aviso declaraba «las 200 más recientes» aunque en
    // pantalla hubiera otra cantidad.
    buscar.mockImplementation((f: FiltroIncidencias) =>
      Promise.resolve({ items, total: 640, pagina: 1, porPagina: f.porPagina ?? 20 }));
    render(<MemoryRouter><Incidencias /></MemoryRouter>);

    fireEvent.click(await screen.findByRole('button', { name: 'Tablero' }));

    await waitFor(() =>
      expect(screen.getByText(`El tablero muestra las ${items.length} más recientes de 640. `
                              + 'Filtrá para ver el resto.')).toBeTruthy());
  });

  it('limpiar los filtros no achica el tablero', async () => {
    // `limpiar` reescribía el filtro entero con el tamaño de página de la
    // lista, sin mirar la vista: el tablero seguía dibujándose como tablero y
    // pasaba a pedir veinte. Las que no entraban desaparecían de las columnas
    // sin que nada lo explicara.
    dibujar(27);

    fireEvent.change(await screen.findByLabelText(/Buscar/i), { target: { value: 'x' } });
    await waitFor(() => expect(buscar.mock.calls.at(-1)![0].texto).toBe('x'));

    fireEvent.click(screen.getByRole('button', { name: 'Tablero' }));
    await waitFor(() => expect(buscar.mock.calls.at(-1)![0].porPagina).toBe(200));

    fireEvent.click(screen.getByRole('button', { name: /Limpiar/ }));

    await waitFor(() => expect(buscar.mock.calls.at(-1)![0].texto).toBeUndefined());
    expect(screen.getByRole('button', { name: 'Tablero' }).getAttribute('aria-pressed'))
      .toBe('true');
    expect(buscar.mock.calls.at(-1)![0].porPagina).toBe(200);
  });

  it('cada tarjeta lleva a su incidencia', async () => {
    await alTablero();

    expect(screen.getByRole('link', { name: /Incidencia 2/ }).getAttribute('href'))
      .toBe('/incidencias/i-2');
  });
});
