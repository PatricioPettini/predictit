import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Agenda } from '../paginas/Agenda';
import type { MantenimientoProgramadoDto, PanelProgramadoDto } from '../api/tipos';

/**
 * La agenda de mantenimiento (RF-16 · CU-020).
 *
 * Lo que se prueba acá es lo que distingue una agenda de una lista de fechas:
 * que se vea de un vistazo qué está vencido y cuánto, que reprogramar exija
 * decir por qué, y que quien no puede planificar no vea las acciones. El resto
 * —que el servidor calcule bien los días, que el plan gobierne la regla— está
 * probado del otro lado, que es donde se decide.
 */

const fila = (
  codigo: string,
  extra: Partial<MantenimientoProgramadoDto> = {},
): MantenimientoProgramadoDto => ({
  id: `id-${codigo}`,
  idEquipo: `eq-${codigo}`,
  codigoEquipo: codigo,
  ubicacion: 'Administración',
  idTipoMantenimiento: 'tm1',
  tipoMantenimiento: 'Preventivo semestral',
  fechaProgramada: '2026-10-15',
  estado: 'PROGRAMADO',
  vencido: false,
  diasDeAtraso: -34,
  ...extra,
});

const panel: PanelProgramadoDto = {
  vencidos: 1,
  proximosSieteDias: 2,
  programadosTotal: 6,
  planesActivos: 3,
  proximos: [],
};

const listar = vi.fn();
const traerPanel = vi.fn();
const reprogramar = vi.fn();
const anular = vi.fn();
const generar = vi.fn();

let patentes: string[] = ['MANTENIMIENTO_VER', 'MANTENIMIENTO_PLANIFICAR'];

vi.mock('../api/cliente', async () => {
  const real = await vi.importActual<typeof import('../api/cliente')>('../api/cliente');
  return {
    ...real,
    api: {
      programados: {
        listar: (...args: unknown[]) => listar(...args),
        panel: () => traerPanel(),
        reprogramar: (...args: unknown[]) => reprogramar(...args),
        anular: (...args: unknown[]) => anular(...args),
        generar: () => generar(),
      },
      mantenimientos: {
        catalogos: () => Promise.resolve({ tipos: [], tecnicos: [] }),
      },
      equipos: {
        buscar: () =>
          Promise.resolve({ items: [], total: 0, pagina: 1, porPagina: 200, totalPaginas: 1 }),
      },
    },
  };
});

vi.mock('../sesion/SesionContext', () => {
  // La referencia tiene que ser estable entre renders: `cerrarSiExpiro` es una
  // dependencia del efecto de carga, y devolver una función nueva cada vez
  // haría que la pantalla consulte la API en cada render.
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
      <Agenda />
    </MemoryRouter>,
  );
}

describe('Agenda de mantenimiento', () => {
  beforeEach(() => {
    listar.mockReset();
    traerPanel.mockReset();
    reprogramar.mockReset();
    anular.mockReset();
    generar.mockReset();
    traerPanel.mockResolvedValue(panel);
    patentes = ['MANTENIMIENTO_VER', 'MANTENIMIENTO_PLANIFICAR'];
  });

  it('distingue lo vencido de lo que todavía no le toca', async () => {
    listar.mockResolvedValue([
      fila('NB-COM-007', { vencido: true, diasDeAtraso: 30 }),
      fila('PC-ADM-014'),
    ]);

    montar();

    // El rótulo lleva los días porque «Programado» describe igual al que vence
    // mañana que al que venció hace un mes, y en una agenda se mira eso.
    expect(await screen.findByText(/Vencido · 30 d/)).toBeInTheDocument();
    expect(screen.getByText(/En 34 d/)).toBeInTheDocument();
  });

  it('sin permiso para planificar no ofrece ninguna acción', async () => {
    patentes = ['MANTENIMIENTO_VER'];
    listar.mockResolvedValue([fila('NB-COM-007', { vencido: true, diasDeAtraso: 30 })]);

    montar();

    await screen.findByText('NB-COM-007');
    expect(screen.queryByRole('button', { name: 'Reprogramar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Anular' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Generar desde los planes/ })).not.toBeInTheDocument();
  });

  it('reprogramar manda la fecha nueva con su motivo', async () => {
    listar.mockResolvedValue([fila('NB-COM-007', { vencido: true, diasDeAtraso: 30 })]);
    reprogramar.mockResolvedValue(undefined);

    montar();

    fireEvent.click(await screen.findByRole('button', { name: 'Reprogramar' }));

    fireEvent.change(screen.getByLabelText('Nueva fecha'), {
      target: { value: '2026-11-20' },
    });
    fireEvent.change(screen.getByLabelText('Motivo'), {
      target: { value: 'El equipo está en uso hasta fin de mes' },
    });
    // El segundo: el primero es el de la fila, que abre el formulario; el de
    // adentro es el que confirma.
    const [, confirmar] = screen.getAllByRole('button', { name: 'Reprogramar' });
    fireEvent.click(confirmar!);

    await waitFor(() =>
      expect(reprogramar).toHaveBeenCalledWith(
        'id-NB-COM-007',
        '2026-11-20',
        'El equipo está en uso hasta fin de mes',
      ),
    );
  });

  it('el motivo es obligatorio para anular', async () => {
    listar.mockResolvedValue([fila('NB-COM-007')]);

    montar();

    fireEvent.click(await screen.findByRole('button', { name: 'Anular' }));

    // Una fecha que se corre o se cae sin explicación deja una agenda que nadie
    // puede auditar, que es justo lo que el plan viene a resolver.
    expect(screen.getByLabelText('Motivo')).toBeRequired();
  });

  it('al generar dice también cuántas fechas ya estaban', async () => {
    listar.mockResolvedValue([]);
    generar.mockResolvedValue({ planesActivos: 3, generados: 0, yaEstaban: 12 });

    montar();

    fireEvent.click(await screen.findByRole('button', { name: /Generar desde los planes/ }));

    // Sin ese dato, apretar el botón dos veces seguidas parece no haber hecho
    // nada y da la impresión de que falló, cuando la agenda ya estaba completa.
    expect(await screen.findByRole('status')).toHaveTextContent('12 ya estaban');
  });
});
