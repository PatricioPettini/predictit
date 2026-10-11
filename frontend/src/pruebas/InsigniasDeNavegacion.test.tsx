import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Layout } from '../componentes/Layout';
import type { ResumenNavegacionDto } from '../api/tipos';

/**
 * Las insignias de la barra lateral.
 *
 * Lo que hay que probar acá no es el número sino cuándo se dibuja. La API
 * omite el contador que el usuario no puede ver, y la diferencia entre «no
 * llegó» y «llegó en cero» es la diferencia entre no mostrar nada y mostrar un
 * cero: el cero es un dato —«no hay vencidos»— y la ausencia de permiso no lo
 * es. Si las dos se trataran igual, el solicitante vería un «0» al lado de
 * Activos afirmando que el parque está vacío.
 */

let resumen: ResumenNavegacionDto = {};
let patentes: string[] = [];

vi.mock('../api/cliente', async () => {
  const real = await vi.importActual<typeof import('../api/cliente')>('../api/cliente');
  return {
    ...real,
    api: { organizacion: { resumen: () => Promise.resolve(resumen) } },
  };
});

vi.mock('../sesion/SesionContext', () => ({
  useSesion: () => ({
    sesion: {
      nombreCompleto: 'Patricio Pettini',
      roles: ['ADMINISTRADOR'],
      patentes,
      organizacion: {
        id: 'o1',
        razonSocial: 'Estudio Pettini & Asociados S.R.L.',
        nombreCorto: 'Estudio Pettini & Asoc.',
        plan: {
          codigo: 'INICIAL',
          nombre: 'Plan Inicial',
          abonoMensual: 89000,
          equiposIncluidos: 15,
          precioEquipoAdicional: 4200,
        },
      },
      organizacionesDisponibles: [],
    },
    cargando: false,
    puede: (patente: string) => patentes.includes(patente),
    salir: () => {},
    cambiarOrganizacion: async () => {},
  }),
  useCerrarSiExpiro: () => () => {},
}));

vi.mock('../idioma/IdiomaContext', () => ({
  useT: () => (_clave: string, porDefecto: string, datos?: Record<string, unknown>) =>
    Object.entries(datos ?? {}).reduce(
      (texto, [nombre, valor]) => texto.replaceAll(`{${nombre}}`, String(valor)),
      porDefecto,
    ),
}));

function dibujar() {
  return render(
    <MemoryRouter initialEntries={['/dashboard']}>
      <Layout />
    </MemoryRouter>,
  );
}

/** El enlace del menú con ese rótulo, para mirarle la insignia. */
function itemDelMenu(rotulo: string): HTMLElement {
  return screen.getByRole('link', { name: rotulo });
}

describe('Las insignias de la barra lateral', () => {
  beforeEach(() => {
    resumen = {};
    patentes = ['EQUIPO_VER', 'INCIDENCIA_VER_TODAS', 'MANTENIMIENTO_VER'];
  });

  it('el contador que no llegó no dibuja nada', async () => {
    // Es el caso del solicitante: la API le omite «equipos» porque no tiene la
    // patente, y el menú tampoco le muestra Activos. Pero el ítem que sí ve
    // tiene que quedar sin insignia y no con un cero inventado.
    resumen = { incidenciasAbiertas: 4 };
    dibujar();

    await waitFor(() => expect(itemDelMenu('Incidencias').textContent).toContain('4'));
    expect(itemDelMenu('Activos').querySelector('.insignia')).toBeNull();
  });

  it('el cero sí se dibuja', async () => {
    // «0 vencidos» es justamente la novedad que uno quiere ver de reojo: que
    // desaparezca la insignia al llegar a cero hace pensar que dejó de medirse.
    resumen = { mantenimientosVencidos: 0 };
    dibujar();

    await waitFor(() =>
      expect(itemDelMenu('Agenda').querySelector('.insignia')?.textContent).toBe('0'));
  });

  it('lo que está abierto se marca, lo que está en cero no', async () => {
    resumen = { incidenciasAbiertas: 3, mantenimientosVencidos: 0 };
    dibujar();

    await waitFor(() =>
      expect(itemDelMenu('Incidencias').querySelector('.insignia')!.className)
        .toContain('alto'));

    // El tono es de la novedad, no del ítem: cero vencidos no es una alarma.
    expect(itemDelMenu('Agenda').querySelector('.insignia')!.className)
      .not.toContain('alto');
  });

  it('el parque se muestra contra lo que el plan incluye', async () => {
    resumen = { equipos: 52 };
    dibujar();

    await waitFor(() => expect(screen.getByText('52/15 eq.')).toBeTruthy());
    // Y se marca, porque los 37 de diferencia se facturan.
    expect(screen.getByText('52/15 eq.').className).toContain('excede');
  });

  it('sin el contador, el plan dice lo único que sabe', async () => {
    // Sin permiso sobre los equipos no se puede decir cuántos hay, pero lo que
    // el plan incluye viene en la sesión y se sigue mostrando.
    dibujar();

    await waitFor(() => expect(screen.getByText('15 eq.')).toBeTruthy());
    expect(screen.getByText('15 eq.').className).not.toContain('excede');
  });
});
