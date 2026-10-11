import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Facturacion } from '../paginas/Facturacion';
import type { ComprobanteDto, PanelFacturacionDto } from '../api/tipos';

/**
 * La facturación del servicio (RF-17 · CU-021).
 *
 * Lo que se prueba acá es el corte entre el borrador y lo emitido, que es lo
 * que la pantalla tiene que dejar claro: sobre un borrador se cargan ajustes y
 * se emite; sobre un emitido sólo se registra el pago o se anula con motivo.
 * Una pantalla que ofrezca «Emitir» sobre algo ya emitido, o «Agregar ajuste»
 * sobre lo que ya se reclamó, deja al usuario descubriendo las reglas a fuerza
 * de errores del servidor.
 */

const comprobante = (extra: Partial<ComprobanteDto> = {}): ComprobanteDto => ({
  id: 'c1',
  periodo: '2026-08',
  plan: 'Plan Estándar',
  equiposAdministrados: 30,
  equiposIncluidos: 25,
  equiposAdicionales: 5,
  subtotal: 112500,
  alicuotaIva: 21,
  iva: 23625,
  total: 136125,
  estado: 'BORRADOR',
  vencido: false,
  diasDeAtraso: 0,
  lineas: [
    {
      id: 'l1',
      orden: 1,
      concepto: 'Abono Plan Estándar — 2026-08',
      cantidad: 1,
      precioUnitario: 100000,
      importe: 100000,
      esAjuste: false,
    },
    {
      id: 'l2',
      orden: 2,
      concepto: 'Equipos adicionales (30 administrados, 25 incluidos)',
      cantidad: 5,
      precioUnitario: 2500,
      importe: 12500,
      esAjuste: false,
    },
  ],
  ...extra,
});

const panel: PanelFacturacionDto = {
  // Otro mes que el del comprobante de prueba a propósito: el indicador del
  // panel y la fila del listado muestran los dos un mes, y si fueran el mismo
  // la prueba no podría decir cuál está mirando.
  periodoSugerido: '2026-07',
  periodoSugeridoFacturado: false,
  emitidos: 1,
  pagados: 1,
  vencidos: 1,
  totalPendiente: 270314,
  totalVencido: 270314,
  facturadoUltimos12Meses: 810942,
  ultimos: [],
};

const listar = vi.fn();
const traerPanel = vi.fn();
const detalle = vi.fn();
const generar = vi.fn();
const ajustar = vi.fn();
const emitir = vi.fn();
const registrarPago = vi.fn();
const anular = vi.fn();

let patentes: string[] = ['FACTURACION_VER', 'FACTURACION_GESTIONAR'];

vi.mock('../api/cliente', async () => {
  const real = await vi.importActual<typeof import('../api/cliente')>('../api/cliente');
  return {
    ...real,
    api: {
      facturacion: {
        listar: (...args: unknown[]) => listar(...args),
        panel: () => traerPanel(),
        detalle: (...args: unknown[]) => detalle(...args),
        generar: (...args: unknown[]) => generar(...args),
        ajustar: (...args: unknown[]) => ajustar(...args),
        emitir: (...args: unknown[]) => emitir(...args),
        registrarPago: (...args: unknown[]) => registrarPago(...args),
        anular: (...args: unknown[]) => anular(...args),
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
      <Facturacion />
    </MemoryRouter>,
  );
}

/** La celda del listado, que no es el indicador del panel. */
async function filaDe(periodo: string) {
  const celdas = await screen.findAllByRole('cell', { name: periodo });
  return celdas[0]!;
}

/** Abre el detalle del primer comprobante del listado. */
async function abrirDetalle(c: ComprobanteDto) {
  detalle.mockResolvedValue(c);
  fireEvent.click(await filaDe('Agosto de 2026'));
  await screen.findByRole('button', { name: 'Cerrar' });
}

describe('Facturación del servicio', () => {
  beforeEach(() => {
    for (const m of [listar, traerPanel, detalle, generar, ajustar, emitir, registrarPago, anular]) {
      m.mockReset();
    }
    traerPanel.mockResolvedValue(panel);
    patentes = ['FACTURACION_VER', 'FACTURACION_GESTIONAR'];
  });

  it('muestra el período como lo lee una persona y no como lo guarda la base', async () => {
    listar.mockResolvedValue([comprobante()]);

    montar();

    // «2026-08» es cómo se guarda; lo que se lee es el mes.
    expect(await filaDe('Agosto de 2026')).toBeInTheDocument();
    expect(screen.queryByText('2026-08')).not.toBeInTheDocument();
  });

  it('el vencido se marca con los días de atraso', async () => {
    listar.mockResolvedValue([
      comprobante({ estado: 'EMITIDO', numero: 2, vencido: true, diasDeAtraso: 22 }),
    ]);

    montar();

    // «Emitido» describe igual al que vence la semana que viene que al que
    // venció hace tres semanas.
    expect(await screen.findByText(/Vencido · 22 d/)).toBeInTheDocument();
  });

  it('el desglose suma el total que se muestra', async () => {
    listar.mockResolvedValue([comprobante()]);

    montar();
    await abrirDetalle(comprobante());

    expect(screen.getByText('Abono Plan Estándar — 2026-08')).toBeInTheDocument();
    expect(screen.getByText(/Equipos adicionales/)).toBeInTheDocument();
    expect(screen.getByText('Subtotal')).toBeInTheDocument();
    expect(screen.getByText('IVA 21%')).toBeInTheDocument();
  });

  it('sobre un borrador se puede ajustar y emitir', async () => {
    listar.mockResolvedValue([comprobante()]);
    ajustar.mockResolvedValue(comprobante({ subtotal: 92500, total: 111925 }));

    montar();
    await abrirDetalle(comprobante());

    fireEvent.change(screen.getByLabelText('Ajuste'), {
      target: { value: 'Descuento por pago adelantado' },
    });
    fireEvent.change(screen.getByLabelText('Importe (+/−)'), { target: { value: '-20000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Agregar ajuste' }));

    await waitFor(() =>
      expect(ajustar).toHaveBeenCalledWith('c1', 'Descuento por pago adelantado', -20000),
    );

    expect(screen.getByRole('button', { name: 'Emitir' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Registrar el pago' })).not.toBeInTheDocument();
  });

  it('sobre un emitido no se ofrece ajustar ni volver a emitir', async () => {
    const emitido = comprobante({ estado: 'EMITIDO', numero: 2, fechaEmision: '2026-09-01' });
    listar.mockResolvedValue([emitido]);

    montar();
    await abrirDetalle(emitido);

    // Un comprobante emitido es lo que se le reclama a la organización: si se
    // pudiera tocar, la facturación dejaría de ser auditable.
    expect(screen.queryByRole('button', { name: 'Agregar ajuste' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Emitir' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Registrar el pago' })).toBeInTheDocument();
  });

  it('un comprobante cobrado ya no se anula', async () => {
    const pagado = comprobante({ estado: 'PAGADO', numero: 1, fechaPago: '2026-09-05' });
    listar.mockResolvedValue([pagado]);

    montar();
    await abrirDetalle(pagado);

    // Lo cobrado se corrige con un ajuste en el período siguiente, que deja el
    // rastro de los dos hechos.
    expect(screen.queryByRole('button', { name: 'Anular' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Registrar el pago' })).not.toBeInTheDocument();
  });

  it('anular exige un motivo', async () => {
    const emitido = comprobante({ estado: 'EMITIDO', numero: 2 });
    listar.mockResolvedValue([emitido]);

    montar();
    await abrirDetalle(emitido);

    fireEvent.click(screen.getByRole('button', { name: 'Anular' }));

    expect(screen.getByLabelText('Motivo')).toBeRequired();
  });

  it('al facturar un mes ya facturado lo dice en vez de no hacer nada', async () => {
    listar.mockResolvedValue([]);
    generar.mockResolvedValue({ id: 'c1', periodo: '2026-08', yaExistia: true, total: 136125 });
    detalle.mockResolvedValue(comprobante());

    montar();

    fireEvent.click(await screen.findByRole('button', { name: 'Facturar el último mes' }));

    expect(await screen.findByRole('status')).toHaveTextContent('ya estaba facturado');
  });

  it('quien sólo puede ver no ve ninguna acción', async () => {
    patentes = ['FACTURACION_VER'];
    listar.mockResolvedValue([comprobante()]);

    montar();
    await abrirDetalle(comprobante());

    // Consultar qué le facturaron no habilita a emitir ni a dar por cobrado.
    expect(screen.queryByRole('button', { name: 'Facturar el último mes' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Emitir' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Descartar' })).not.toBeInTheDocument();
  });
});
