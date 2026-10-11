import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { itemActivo, migas } from '../componentes/Layout';

/**
 * Qué pasa con una dirección que no es ninguna pantalla.
 *
 * Hasta el rediseño 03 cualquier ruta desconocida redirigía en silencio a la
 * primera pantalla permitida. Para `/` eso es correcto; para
 * `/incidencias/INC-9999` no, porque quien llegó desde un enlace roto no se
 * entera y lo vuelve a intentar.
 *
 * Al separar los dos casos se rompió algo que ninguna prueba miraba: `/login`
 * pasó a caer en el 404. Sin sesión cualquier dirección muestra el formulario,
 * así que `/login` es la que la gente guarda en favoritos, y al entrar
 * terminaba en «esta página no existe». Lo detectó el script de capturas, que
 * es un rodeo largo para algo que tiene que fallar acá.
 *
 * La prueba monta el `App` de verdad y no una copia de sus rutas: una copia
 * seguiría en verde si alguien borrara la ruta del archivo real, que es
 * exactamente el error que se quiere impedir. Lo único simulado es de dónde
 * salen la sesión y los textos.
 */

vi.mock('../sesion/SesionContext', () => ({
  useSesion: () => ({
    sesion: {
      nombreCompleto: 'Martín Acosta',
      roles: ['RESPONSABLE_TECNICO'],
      patentes: ['INCIDENCIA_VER_TODAS'],
      organizacion: { id: 'o1', razonSocial: 'Estudio', nombreCorto: 'Estudio', cuit: '30-1-4' },
      organizacionesDisponibles: [],
    },
    cargando: false,
    puede: (patente: string) => patente === 'INCIDENCIA_VER_TODAS',
    salir: () => {},
    cambiarOrganizacion: async () => {},
  }),
  useCerrarSiExpiro: () => () => {},
}));

// Las pantallas piden datos al montarse. Acá sólo interesa cuál se monta, así
// que el cliente devuelve promesas que no resuelven nunca: cada pantalla se
// queda en su estado de carga y ninguna rompe.
vi.mock('../api/cliente', async () => {
  const real = await vi.importActual<typeof import('../api/cliente')>('../api/cliente');
  const nunca = () => new Promise(() => {});
  return {
    ...real,
    api: new Proxy({}, { get: () => new Proxy({}, { get: () => nunca }) }),
  };
});

import { App } from '../App';

function montar(direccion: string) {
  render(
    <MemoryRouter initialEntries={[direccion]}>
      <App />
    </MemoryRouter>,
  );
}

/** El título del 404, que es lo que ninguna de las dos redirecciones debe mostrar. */
const TITULO_404 = /Esta página no existe/i;

describe('Direcciones que no son una pantalla', () => {
  it('la raiz lleva adentro, no al 404', () => {
    montar('/');
    expect(screen.queryByText(TITULO_404)).toBeNull();
  });

  it('«/login» con la sesion abierta lleva adentro y no al 404', () => {
    montar('/login');
    expect(screen.queryByText(TITULO_404)).toBeNull();
  });

  it('una direccion inventada si muestra el 404, con la direccion que fallo', () => {
    montar('/pantalla-que-no-existe');
    expect(screen.getByText(TITULO_404)).toBeTruthy();
    // Decir cuál fue la dirección es la mitad del valor de la pantalla: sin
    // eso la persona no sabe si el enlace estaba mal o el sistema falló.
    expect(screen.getByText('/pantalla-que-no-existe')).toBeTruthy();
  });
});

describe('El ítem activo del menú', () => {
  const items = [
    '/dashboard',
    '/activos',
    '/incidencias',
    '/mantenimientos',
    '/mantenimientos/agenda',
    '/configuracion',
    '/configuracion/planes',
  ];

  it('gana el más específico y no los dos a la vez', () => {
    // Con `isActive` de NavLink a secas quedaban resaltados «Mantenimientos» y
    // «Agenda» juntos, porque uno es prefijo del otro.
    expect(itemActivo('/mantenimientos/agenda', items)).toBe('/mantenimientos/agenda');
    expect(itemActivo('/configuracion/planes', items)).toBe('/configuracion/planes');
  });

  it('una ruta de detalle sigue marcando su sección', () => {
    // Y con `end` en todos los ítems se rompía esto: en la ficha de un equipo
    // el menú dejaba de señalar dónde estaba uno.
    expect(itemActivo('/activos/abc-123', items)).toBe('/activos');
    expect(itemActivo('/incidencias/9', items)).toBe('/incidencias');
  });

  it('un prefijo de texto que no es de ruta no cuenta', () => {
    // `/activos-viejos` empieza con `/activos` como texto, pero no está debajo.
    expect(itemActivo('/activos-viejos', items)).toBeNull();
  });

  it('una dirección desconocida no marca nada', () => {
    expect(itemActivo('/vaya-uno-a-saber', items)).toBeNull();
  });
});

describe('El rastro de arriba del título', () => {
  it('en una pantalla de primer nivel queda sólo el sistema', () => {
    // La sección no se agrega porque la sección *es* esta pantalla, y su
    // nombre ya está escrito abajo, en el título.
    expect(migas('/dashboard', 'Dashboard')).toEqual(['PredictIT']);
    expect(migas('/activos', 'Activos')).toEqual(['PredictIT']);
  });

  it('en un detalle aparece la sección de la que cuelga', () => {
    expect(migas('/activos/abc-123', 'Activos')).toEqual(['PredictIT', 'Activos']);
    expect(migas('/configuracion/reglas', 'Configuración'))
      .toEqual(['PredictIT', 'Configuración']);
  });

  it('una dirección que no cuelga de ningún ítem no inventa sección', () => {
    // El 404 también dibuja encabezado, y ahí no hay sección a la que volver.
    expect(migas('/vaya-uno-a-saber', 'Activos')).toEqual(['PredictIT']);
  });
});
