import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Activos } from '../paginas/Activos';
import type { CatalogosEquipoDto, EquipoListaDto, PaginaDto } from '../api/tipos';

/**
 * La pantalla de activos con la API simulada.
 *
 * Se simula el cliente HTTP y no `fetch`: el backend ya tiene sus propias
 * pruebas de integración, y acá lo que interesa es qué hace la pantalla con lo
 * que recibe.
 */

const equipo = (codigo: string, extra: Partial<EquipoListaDto> = {}): EquipoListaDto => ({
  id: `id-${codigo}`,
  codigo,
  tipo: 'PC de escritorio',
  marcaModelo: 'Dell OptiPlex 3080',
  numeroSerie: `SN-${codigo}`,
  ubicacion: 'Administración',
  responsable: 'Gabriela Suárez',
  estado: 'Operativo',
  estadoOperativo: true,
  ...extra,
});

const catalogos: CatalogosEquipoDto = {
  tipos: [{ id: 't1', nombre: 'PC de escritorio' }],
  estados: [{ id: 'e1', nombre: 'Operativo', operativo: true }],
  ubicaciones: [{ id: 'u1', nombre: 'Administración' }],
  responsables: [{ id: 'r1', nombreCompleto: 'Gabriela Suárez' }],
};

function paginaDe(items: EquipoListaDto[], total = items.length): PaginaDto<EquipoListaDto> {
  return { items, total, pagina: 1, porPagina: 20, totalPaginas: Math.max(1, Math.ceil(total / 20)) };
}

const buscar = vi.fn();
const traerCatalogos = vi.fn();

/** Patentes de la sesión simulada. Cada prueba la ajusta si le importa. */
let patentes: string[] = ['EQUIPO_VER', 'EQUIPO_GESTIONAR'];

vi.mock('../api/cliente', async () => {
  const real = await vi.importActual<typeof import('../api/cliente')>('../api/cliente');
  return {
    ...real,
    api: {
      equipos: {
        buscar: (...args: unknown[]) => buscar(...args),
        catalogos: () => traerCatalogos(),
        // Los recuentos de las píldoras. Estas pruebas no los miran —tienen su
        // propio archivo—, pero la pantalla los pide al montarse y sin esto
        // reventaba antes de dibujar nada.
        segmentos: () => Promise.resolve(
          { todos: 0, riesgoAlto: 0, preventivoVencido: 0, garantiaVencida: 0 }),
      },
    },
  };
});

vi.mock('../sesion/SesionContext', () => {
  // La referencia tiene que ser estable entre renders: `cerrarSiExpiro` es una
  // dependencia del efecto de búsqueda, y devolver una función nueva cada vez
  // hace que la pantalla consulte la API en cada render. El original usa
  // `useCallback`, así que acá se replica esa estabilidad.
  const cerrar = () => {};
  return {
    useSesion: () => ({
      sesion: {
        nombreCompleto: 'Patricio Pettini',
        roles: ['ADMINISTRADOR'],
        organizacion: { id: 'o1', nombreCorto: 'Estudio Pettini & Asoc.' },
        organizacionesDisponibles: [],
        patentes,
      },
      puede: (p: string) => patentes.includes(p),
    }),
    useCerrarSiExpiro: () => cerrar,
  };
});

/** Espera a que la pantalla deje de consultar la API. */
async function quieta() {
  let previas = -1;
  await waitFor(() => {
    const ahora = buscar.mock.calls.length;
    const estable = ahora === previas && ahora > 0;
    previas = ahora;
    expect(estable).toBe(true);
  }, { timeout: 3000, interval: 120 });
}

function montar() {
  return render(
    <MemoryRouter>
      <Activos />
    </MemoryRouter>,
  );
}

describe('Pantalla de activos', () => {
  beforeEach(() => {
    vi.useRealTimers();
    buscar.mockReset();
    traerCatalogos.mockReset();
    traerCatalogos.mockResolvedValue(catalogos);
    patentes = ['EQUIPO_VER', 'EQUIPO_GESTIONAR'];
  });

  it('muestra los equipos que devuelve la API', async () => {
    buscar.mockResolvedValue(paginaDe([equipo('PC-ADM-014'), equipo('NB-COM-007')]));

    montar();

    expect(await screen.findByText('PC-ADM-014')).toBeInTheDocument();
    expect(screen.getByText('NB-COM-007')).toBeInTheDocument();
    expect(screen.getByText('S/N SN-PC-ADM-014')).toBeInTheDocument();
  });

  it('pluraliza bien cuando hay un solo equipo', async () => {
    // Decía "1 equipos registrados". Es cosmético, pero es lo primero que se ve
    // al filtrar y aparece en las capturas del documento.
    buscar.mockResolvedValue(paginaDe([equipo('PC-ADM-014')]));

    montar();

    expect(await screen.findByText(/1 equipo registrado/)).toBeInTheDocument();
    expect(screen.queryByText(/1 equipos registrados/)).not.toBeInTheDocument();
  });

  it('avisa cuando no hay resultados en lugar de mostrar una tabla vacía', async () => {
    buscar.mockResolvedValue(paginaDe([], 0));

    montar();

    expect(
      await screen.findByText(/Todavía no hay equipos cargados/),
    ).toBeInTheDocument();
  });

  it('distingue el parque vacío de una búsqueda sin resultados', async () => {
    // Son cosas distintas y la persona hace cosas distintas con cada una: si
    // el parque está vacío hay que cargar equipos, y si la búsqueda no
    // encontró nada hay que cambiar los filtros. Con un solo mensaje para las
    // dos, el estado vacío no ayuda a decidir.
    buscar.mockResolvedValue(paginaDe([], 0));

    montar();
    await screen.findByText(/Todavía no hay equipos cargados/);

    fireEvent.change(screen.getByPlaceholderText(/Código, serie/i),
                      { target: { value: 'ZZZ' } });

    expect(
      await screen.findByText(/Ningún equipo coincide con la búsqueda/),
    ).toBeInTheDocument();
    expect(screen.getByText('Limpiar los filtros')).toBeInTheDocument();
  });

  it('mientras carga muestra el esqueleto y no el estado vacío', async () => {
    // El defecto que esto cubre: con un solo renglón de texto para los dos
    // casos, una tabla que todavía no cargó se veía igual que una sin datos.
    let resolver: (v: unknown) => void = () => {};
    buscar.mockReturnValue(new Promise((r) => { resolver = r; }));

    montar();

    expect(screen.getByRole('status', { name: /cargando/i })).toBeInTheDocument();
    expect(screen.queryByText(/Todavía no hay equipos cargados/)).not.toBeInTheDocument();

    resolver(paginaDe([], 0));
    await screen.findByText(/Todavía no hay equipos cargados/);
  });

  it('esconde las acciones de alta a quien no puede gestionar equipos', async () => {
    // El backend igual devuelve 403, pero ofrecer un botón que va a fallar es
    // una mala interfaz.
    patentes = ['EQUIPO_VER'];
    buscar.mockResolvedValue(paginaDe([equipo('PC-ADM-014')]));

    montar();

    await screen.findByText('PC-ADM-014');
    expect(screen.queryByText('Nuevo equipo')).not.toBeInTheDocument();
    expect(screen.queryByText('Importar desde planilla')).not.toBeInTheDocument();
  });

  it('ofrece las acciones de alta a quien sí puede', async () => {
    buscar.mockResolvedValue(paginaDe([equipo('PC-ADM-014')]));

    montar();

    await screen.findByText('PC-ADM-014');
    expect(screen.getByText('Nuevo equipo')).toBeInTheDocument();
  });

  it('consulta la API una sola vez al montar', async () => {
    // El debounce del buscador se asienta con el texto vacío. Si eso genera un
    // filtro nuevo, cada carga de la pantalla dispara dos consultas iguales.
    buscar.mockResolvedValue(paginaDe([equipo('PC-ADM-014')]));

    montar();
    await screen.findByText('PC-ADM-014');
    await quieta();

    expect(buscar).toHaveBeenCalledTimes(1);
  });

  it('agrupa las teclas del buscador en una sola consulta', async () => {
    buscar.mockResolvedValue(paginaDe([equipo('PC-ADM-014')]));
    montar();
    await screen.findByText('PC-ADM-014');
    await quieta();
    const llamadasAntes = buscar.mock.calls.length;

    const buscador = screen.getByLabelText('Buscar equipos');

    // Sin el debounce, cada tecla dispara una consulta con LIKE sobre la tabla
    // entera. Con él, escribir "PC-" tiene que resultar en una sola.
    fireEvent.change(buscador, { target: { value: 'P' } });
    fireEvent.change(buscador, { target: { value: 'PC' } });
    fireEvent.change(buscador, { target: { value: 'PC-' } });

    await waitFor(() => expect(buscar.mock.calls.length).toBeGreaterThan(llamadasAntes), {
      timeout: 2000,
    });
    await quieta();

    expect(buscar.mock.calls.length - llamadasAntes).toBe(1);
    const ultima = buscar.mock.calls.at(-1)?.[0] as { texto?: string };
    expect(ultima.texto).toBe('PC-');
  });

  it('no consulta por espacios en blanco en el buscador', async () => {
    buscar.mockResolvedValue(paginaDe([equipo('PC-ADM-014')]));
    montar();
    await screen.findByText('PC-ADM-014');
    await quieta();
    const llamadasAntes = buscar.mock.calls.length;

    fireEvent.change(screen.getByLabelText('Buscar equipos'), { target: { value: '   ' } });

    await new Promise((r) => setTimeout(r, 600));
    expect(buscar.mock.calls.length).toBe(llamadasAntes);
  });

  it('reinicia a la primera página al cambiar un filtro', async () => {
    buscar.mockResolvedValue(paginaDe([equipo('PC-ADM-014')], 60));
    montar();
    await screen.findByText('PC-ADM-014');

    fireEvent.change(screen.getByLabelText('Estado'), { target: { value: 'e1' } });

    await waitFor(() => {
      const ultima = buscar.mock.calls.at(-1)?.[0] as { pagina?: number; estado?: string };
      expect(ultima.estado).toBe('e1');
      // Quedarse en la página 7 con un filtro nuevo muestra una tabla vacía sin
      // explicación aparente.
      expect(ultima.pagina).toBe(1);
    });
  });
});
