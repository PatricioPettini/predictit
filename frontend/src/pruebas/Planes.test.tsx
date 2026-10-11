import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Planes } from '../paginas/Planes';
import type { PlanMantenimientoDto } from '../api/tipos';

/**
 * Los planes de mantenimiento preventivo (RF-16 · CU-019).
 *
 * El plan es lo que le da a «vencido» una definición exacta: sin plan el motor
 * estima con un umbral general, y con plan mide contra lo que la organización
 * decidió para ese equipo. Por eso lo que se prueba acá es que la pantalla no
 * deje armar un plan ambiguo y que muestre cuándo un plan no sirve para nada.
 */

const plan = (extra: Partial<PlanMantenimientoDto> = {}): PlanMantenimientoDto => ({
  id: 'p1',
  idTipoEquipo: 'te1',
  alcance: 'Notebooks',
  idTipoMantenimiento: 'tm1',
  tipoMantenimiento: 'Preventivo semestral',
  cadaDias: 180,
  activo: true,
  equiposAlcanzados: 12,
  ...extra,
});

const listar = vi.fn();
const crear = vi.fn();

let patentes: string[] = ['MANTENIMIENTO_VER', 'MANTENIMIENTO_PLANIFICAR'];

vi.mock('../api/cliente', async () => {
  const real = await vi.importActual<typeof import('../api/cliente')>('../api/cliente');
  return {
    ...real,
    api: {
      planes: {
        listar: () => listar(),
        crear: (...args: unknown[]) => crear(...args),
        actualizar: vi.fn(),
      },
      equipos: {
        catalogos: () =>
          Promise.resolve({
            tipos: [{ id: 'te1', nombre: 'Notebook' }],
            estados: [],
            ubicaciones: [],
            responsables: [],
          }),
        buscar: () =>
          Promise.resolve({
            items: [
              {
                id: 'eq1',
                codigo: 'NB-COM-007',
                tipo: 'Notebook',
                marcaModelo: 'Dell',
                numeroSerie: 'SN-1',
                ubicacion: 'Administración',
                responsable: 'Gabriela Suárez',
                estado: 'Operativo',
                estadoOperativo: true,
              },
            ],
            total: 1,
            pagina: 1,
            porPagina: 200,
            totalPaginas: 1,
          }),
      },
      mantenimientos: {
        catalogos: () =>
          Promise.resolve({
            tipos: [
              { id: 'tm1', nombre: 'Preventivo semestral', esPreventivo: true },
              { id: 'tm2', nombre: 'Correctivo', esPreventivo: false },
            ],
            tecnicos: [],
          }),
      },
      organizacion: { situacionPlan: vi.fn(), consumoIa: vi.fn() },
    },
  };
});

vi.mock('../sesion/SesionContext', () => {
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

function montar() {
  return render(
    <MemoryRouter>
      <Planes />
    </MemoryRouter>,
  );
}

describe('Planes de mantenimiento', () => {
  beforeEach(() => {
    listar.mockReset();
    crear.mockReset();
    patentes = ['MANTENIMIENTO_VER', 'MANTENIMIENTO_PLANIFICAR'];
  });

  it('dice la frecuencia en la unidad en que la gente la piensa', async () => {
    listar.mockResolvedValue([plan()]);

    montar();

    // Nadie dice «cada 180 días»: dice «semestral». Van los dos, porque el
    // número es el que define la cuenta.
    expect(await screen.findByText('semestral · cada 180 días')).toBeInTheDocument();
  });

  it('marca el plan activo que no alcanza a ningún equipo', async () => {
    listar.mockResolvedValue([plan({ equiposAlcanzados: 0 })]);

    montar();

    // Está bien formado y no sirve para nada. Lo que se ve, si no se marca, es
    // un plan cargado y la agenda vacía.
    const cero = await screen.findByText('0');
    expect(cero).toHaveStyle({ color: 'var(--alto-barra)' });
  });

  it('el alcance es por tipo o por equipo, nunca los dos', async () => {
    listar.mockResolvedValue([]);

    montar();

    fireEvent.click(await screen.findByRole('button', { name: 'Nuevo plan' }));

    const porTipo = screen.getByLabelText('Tipo de equipo');
    const porEquipo = screen.getByLabelText('Un equipo puntual');
    expect(porEquipo).not.toBeDisabled();

    fireEvent.change(porTipo, { target: { value: 'te1' } });

    // El servidor lo rechaza; la pantalla lo dice antes de dejar intentarlo.
    await waitFor(() => expect(porEquipo).toBeDisabled());
  });

  it('sólo deja elegir trabajos preventivos', async () => {
    listar.mockResolvedValue([]);

    montar();

    fireEvent.click(await screen.findByRole('button', { name: 'Nuevo plan' }));

    // Agendar un correctivo no tiene sentido: el correctivo es la respuesta a
    // algo que ya pasó.
    const opciones = Array.from(
      screen.getByLabelText('Qué trabajo').querySelectorAll('option'),
    ).map((o) => o.textContent);

    expect(opciones).toContain('Preventivo semestral');
    expect(opciones).not.toContain('Correctivo');
  });

  it('guarda el plan con el alcance y la frecuencia elegidos', async () => {
    listar.mockResolvedValue([]);
    crear.mockResolvedValue('nuevo-id');

    montar();

    fireEvent.click(await screen.findByRole('button', { name: 'Nuevo plan' }));
    fireEvent.change(screen.getByLabelText('Tipo de equipo'), { target: { value: 'te1' } });
    fireEvent.change(screen.getByLabelText('Qué trabajo'), { target: { value: 'tm1' } });
    fireEvent.change(screen.getByLabelText('Cada cuántos días'), { target: { value: '90' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() =>
      expect(crear).toHaveBeenCalledWith(
        expect.objectContaining({ idTipoEquipo: 'te1', idTipoMantenimiento: 'tm1', cadaDias: 90 }),
      ),
    );
  });

  it('sin permiso para planificar no deja tocar nada', async () => {
    patentes = ['MANTENIMIENTO_VER'];
    listar.mockResolvedValue([plan()]);

    montar();

    await screen.findByText('Notebooks');
    expect(screen.queryByRole('button', { name: 'Nuevo plan' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Editar' })).not.toBeInTheDocument();
  });
});
