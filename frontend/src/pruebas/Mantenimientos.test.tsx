import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Mantenimientos } from '../paginas/Mantenimientos';
import type { CatalogosMantenimientoDto, EquipoListaDto } from '../api/tipos';

/**
 * Registrar un mantenimiento que venía agendado (RF-16 · CU-020).
 *
 * La agenda no cierra nada por su cuenta: lo que cierra un trabajo agendado es
 * haberlo hecho, y eso son los datos del mantenimiento —qué se hizo, repuestos,
 * costo—, que se cargan en esta pantalla. El enlace entre las dos es lo que se
 * prueba acá, porque es lo único que no está probado de ningún lado: que el
 * identificador del trabajo llegue hasta el servidor.
 *
 * Va hasta allá y no se resuelve en el servidor por equipo y tipo: dos
 * preventivos iguales sobre el mismo equipo —uno atrasado y otro del mes que
 * viene— sólo se distinguen por el identificador.
 */

const equipos: EquipoListaDto[] = [
  {
    id: 'eq1',
    codigo: 'NB-COM-007',
    tipo: 'Notebook',
    marcaModelo: 'Dell Latitude',
    numeroSerie: 'SN-1',
    ubicacion: 'Administración',
    responsable: 'Gabriela Suárez',
    estado: 'Operativo',
    estadoOperativo: true,
  },
];

const catalogos: CatalogosMantenimientoDto = {
  tipos: [
    { id: 'tm1', nombre: 'Preventivo semestral', esPreventivo: true },
    { id: 'tm2', nombre: 'Correctivo', esPreventivo: false },
  ],
  tecnicos: [],
};

const listar = vi.fn();
const registrar = vi.fn();

vi.mock('../api/cliente', async () => {
  const real = await vi.importActual<typeof import('../api/cliente')>('../api/cliente');
  return {
    ...real,
    api: {
      mantenimientos: {
        listar: () => listar(),
        catalogos: () => Promise.resolve(catalogos),
        registrar: (...args: unknown[]) => registrar(...args),
      },
      equipos: {
        buscar: () =>
          Promise.resolve({ items: equipos, total: 1, pagina: 1, porPagina: 200, totalPaginas: 1 }),
      },
    },
  };
});

vi.mock('../sesion/SesionContext', () => {
  const cerrar = () => {};
  const patentes = ['MANTENIMIENTO_VER', 'MANTENIMIENTO_REGISTRAR', 'MANTENIMIENTO_PLANIFICAR'];
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

function montar(direccion: string) {
  return render(
    <MemoryRouter initialEntries={[direccion]}>
      <Mantenimientos />
    </MemoryRouter>,
  );
}

describe('Registrar un mantenimiento', () => {
  beforeEach(() => {
    listar.mockReset();
    registrar.mockReset();
    listar.mockResolvedValue([]);
    registrar.mockResolvedValue('nuevo-id');
  });

  it('llegando desde la agenda abre el alta con el equipo y el tipo puestos', async () => {
    montar('/mantenimientos?programado=pr1&equipo=eq1&tipo=tm1');

    // El formulario aparece solo: quien viene de apretar «Registrar» en la
    // agenda ya dijo qué quiere hacer, y pedirle que lo vuelva a decir acá
    // sería hacerle repetir el gesto.
    const selectEquipo = (await screen.findByLabelText('Equipo')) as HTMLSelectElement;
    const selectTipo = screen.getByLabelText('Tipo') as HTMLSelectElement;

    await waitFor(() => expect(selectEquipo.value).toBe('eq1'));
    expect(selectTipo.value).toBe('tm1');
  });

  it('manda el trabajo agendado que se está cerrando', async () => {
    montar('/mantenimientos?programado=pr1&equipo=eq1&tipo=tm1');

    fireEvent.change(await screen.findByLabelText('Qué se hizo'), {
      target: { value: 'Limpieza interna y cambio de pasta térmica.' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Registrar' }));

    await waitFor(() =>
      expect(registrar).toHaveBeenCalledWith(
        expect.objectContaining({ idEquipo: 'eq1', idTipo: 'tm1', idProgramado: 'pr1' }),
      ),
    );
  });

  it('sin venir de la agenda el alta arranca cerrada y sin trabajo que cerrar', async () => {
    montar('/mantenimientos');

    // El botón de la cabecera abre el alta; lo que no puede pasar es que
    // registrar a mano cierre un trabajo agendado que nadie eligió.
    fireEvent.click(await screen.findByRole('button', { name: 'Registrar mantenimiento' }));

    fireEvent.change(screen.getByLabelText('Equipo'), { target: { value: 'eq1' } });
    fireEvent.change(screen.getByLabelText('Tipo'), { target: { value: 'tm1' } });
    fireEvent.change(screen.getByLabelText('Qué se hizo'), {
      target: { value: 'Cambio de fuente.' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Registrar' }));

    await waitFor(() => expect(registrar).toHaveBeenCalled());
    expect(registrar.mock.calls[0]![0]).toMatchObject({ idProgramado: undefined });
  });
});
