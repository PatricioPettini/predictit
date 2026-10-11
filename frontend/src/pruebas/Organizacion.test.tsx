import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Organizacion } from '../paginas/Organizacion';
import type { SituacionPlanDto } from '../api/tipos';

/**
 * El bloque del plan contratado.
 *
 * Esta pantalla es el argumento economico del capitulo 6 calculado por el
 * sistema, asi que interesa que muestre la cuenta y no solo el total, y que
 * diga la verdad cuando el plan mas caro no conviene.
 */

const traerPlan = vi.fn();

const CONSUMO = {
  desde: '2026-09-01T00:00:00',
  hasta: '2026-09-30T23:59:59',
  clasificacionesIa: 0,
  clasificacionesHeuristica: 0,
  asignacionesIa: 0,
  asignacionesHeuristica: 0,
  asignacionesRespaldo: 0,
  guiasIa: 0,
  guiasHeuristica: 0,
  totalIa: 0,
  totalPropio: 0,
};

vi.mock('../api/cliente', async () => {
  const real = await vi.importActual<typeof import('../api/cliente')>('../api/cliente');
  return {
    ...real,
    api: {
      organizacion: {
        plan: () => traerPlan(),
        // El bloque de consumo de IA vive en la misma pantalla. Sin este
        // doble el efecto tira «no es una funcion» y los cinco casos del
        // plan se caen por algo que no estan probando.
        consumoIa: () => Promise.resolve(CONSUMO),
      },
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
        patentes: ['ORGANIZACION_VER'],
        organizacion: {
          id: 'o1',
          razonSocial: 'Estudio Pettini & Asociados S.R.L.',
          nombreCorto: 'Estudio Pettini',
          cuit: '30-71234567-4',
        },
        // Un administrador de una sola organización: el selector no va.
        organizacionesDisponibles: [],
      },
      puede: (patente: string) => patente === 'ORGANIZACION_VER',
      cambiarOrganizacion: async () => {},
    }),
    useCerrarSiExpiro: () => cerrar,
  };
});

/**
 * El escenario real del seed: 52 equipos en Plan Inicial.
 *
 * Los importes son los de `db/04-seed-negocio.sql`, con los precios revisados
 * en D-14: abono 89.000 con 15 equipos incluidos y 4.200 por adicional. Antes
 * este fixture traía los precios anteriores —34.900 y 1.800— y la prueba pasaba
 * igual, porque simula la API: describía un sistema que ya no existía.
 */
const inicialCon52: SituacionPlanDto = {
  plan: {
    codigo: 'INICIAL',
    nombre: 'Plan Inicial',
    abonoMensual: 89000,
    equiposIncluidos: 15,
    precioEquipoAdicional: 4200,
  },
  equiposAdministrados: 52,
  equiposIncluidos: 15,
  equiposAdicionales: 37,
  abonoBase: 89000,
  costoAdicionales: 155400,
  totalMensual: 244400,
  alternativa: {
    nombre: 'Plan Estandar',
    totalMensual: 246000,
    diferencia: 1600,
    conviene: false,
    // 239.000 + 5 × 3.500 = 256.500 contra 89.000 + 40 × 4.200 = 257.000.
    equiposDesdeLosQueConviene: 55,
  },
};

function montar() {
  return render(
    <MemoryRouter>
      <Organizacion />
    </MemoryRouter>,
  );
}

describe('Pantalla de organizacion', () => {
  beforeEach(() => {
    traerPlan.mockReset();
  });

  it('muestra la cuenta desglosada y no solo el total', async () => {
    traerPlan.mockResolvedValue(inicialCon52);

    montar();

    expect(await screen.findByText('Plan Inicial')).toBeInTheDocument();
    expect(screen.getByText('Abono base')).toBeInTheDocument();
    // 37 adicionales x $1.800: el desglose es lo que hace que el cliente
    // entienda de donde sale el total.
    expect(screen.getByText(/Equipos adicionales \(37 ×/)).toBeInTheDocument();
    expect(screen.getByText('Total mensual estimado')).toBeInTheDocument();
  });

  it('avisa que el plan mas caro NO conviene cuando la cuenta dice eso', async () => {
    // Con 52 equipos el Inicial sale $101.500 y el Estandar $102.900. El
    // sistema tiene que decir eso, no empujar al plan mas caro.
    traerPlan.mockResolvedValue(inicialCon52);

    montar();

    const aviso = await screen.findByText(/costaría/);
    expect(aviso.textContent).toMatch(/más que tu plan actual/);
    expect(aviso.textContent).toMatch(/a partir de los/);
    expect(aviso.textContent).toMatch(/55 equipos/);
    expect(aviso.textContent).not.toMatch(/te conviene el/i);
  });

  it('recomienda cambiar cuando el otro plan es realmente mas barato', async () => {
    traerPlan.mockResolvedValue({
      ...inicialCon52,
      // 70 equipos: Inicial 89.000 + 55 × 4.200 = 320.000; Estándar
      // 239.000 + 20 × 3.500 = 309.000. Ahí sí conviene cambiar.
      equiposAdministrados: 70,
      equiposAdicionales: 55,
      costoAdicionales: 231000,
      totalMensual: 320000,
      alternativa: {
        nombre: 'Plan Estandar',
        totalMensual: 309000,
        diferencia: -11000,
        conviene: true,
        equiposDesdeLosQueConviene: 55,
      },
    } satisfies SituacionPlanDto);

    montar();

    const aviso = await screen.findByText(/te conviene el/i);
    expect(aviso.textContent).toMatch(/menos que ahora/);
  });

  it('muestra un error legible si el plan no se puede cargar', async () => {
    const { ErrorApi } = await vi.importActual<typeof import('../api/cliente')>('../api/cliente');
    traerPlan.mockRejectedValue(new ErrorApi(500, 'No fue posible completar la operación.'));

    montar();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });

  // Nueve y no siete: la prueba enumeraba las que habia cuando se escribio y
  // se le habian escapado «Errores del sistema» y «Apariencia», que son
  // justamente las dos ultimas que se agregaron.
  it('lista las nueve secciones de configuracion', async () => {
    traerPlan.mockResolvedValue(inicialCon52);

    montar();

    for (const texto of [
      'Organización',
      'Reglas predictivas',
      'Notificaciones',
      'Respaldos',
      'Bitácora',
      'Errores del sistema',
      'Integraciones',
      'Integración de IA',
      'Apariencia',
    ]) {
      expect(screen.getAllByText(texto).length).toBeGreaterThan(0);
    }
  });
});
