import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api, ErrorApi, token } from '../api/cliente';

/**
 * El cliente HTTP.
 *
 * Se simula `fetch` porque lo que interesa es qué pedido sale: la URL que se
 * arma, el header de autorización y cómo se traduce cada respuesta de error.
 */

function respuesta(estado: number, cuerpo: unknown) {
  const texto = cuerpo === undefined ? '' : JSON.stringify(cuerpo);
  return {
    status: estado,
    ok: estado >= 200 && estado < 300,
    text: async () => texto,
  } as Response;
}

const fetchSimulado = vi.fn();

/** La URL del último pedido. */
function urlPedida(): string {
  return String(fetchSimulado.mock.calls.at(-1)?.[0]);
}

/** Los headers del último pedido. */
function headersPedidos(): Record<string, string> {
  return (fetchSimulado.mock.calls.at(-1)?.[1]?.headers ?? {}) as Record<string, string>;
}

beforeEach(() => {
  fetchSimulado.mockReset();
  vi.stubGlobal('fetch', fetchSimulado);
  sessionStorage.clear();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('Cliente HTTP', () => {
  it('manda el token como Bearer cuando hay sesión', async () => {
    token.guardar('jwt-de-prueba');
    fetchSimulado.mockResolvedValue(respuesta(200, { items: [] }));

    await api.equipos.buscar();

    expect(headersPedidos().Authorization).toBe('Bearer jwt-de-prueba');
  });

  it('no manda el header de autorización si no hay token', async () => {
    fetchSimulado.mockResolvedValue(respuesta(200, {}));

    await api.auth.login('admin', 'x');

    expect(headersPedidos().Authorization).toBeUndefined();
  });

  it('guarda el token en sessionStorage y no en localStorage', () => {
    // Al cerrar la pestaña la sesión se termina. En una PyME sin área de IT las
    // máquinas se comparten, así que dejarlo en localStorage sería peor.
    token.guardar('jwt-de-prueba');

    expect(sessionStorage.getItem('predictit.token')).toBe('jwt-de-prueba');
    expect(localStorage.getItem('predictit.token')).toBeNull();
  });

  it('omite de la query los filtros sin valor', async () => {
    fetchSimulado.mockResolvedValue(respuesta(200, { items: [] }));

    await api.equipos.buscar({ texto: '', tipo: undefined, pagina: 2, porPagina: 20 });

    const url = urlPedida();
    expect(url).toContain('pagina=2');
    expect(url).toContain('porPagina=20');
    // Un parámetro vacío no es "sin filtro" para la API: sería filtrar por
    // cadena vacía.
    expect(url).not.toContain('texto=');
    expect(url).not.toContain('tipo=');
  });

  it('recorta los espacios del texto antes de mandarlo', async () => {
    fetchSimulado.mockResolvedValue(respuesta(200, { items: [] }));

    await api.equipos.buscar({ texto: '  PC-ADM  ' });

    expect(urlPedida()).toContain('texto=PC-ADM');
  });

  it('traduce un 401 en un error de sesión inválida', async () => {
    fetchSimulado.mockResolvedValue(respuesta(401, { mensaje: 'Token vencido.' }));

    const error = await api.equipos.buscar().catch((e) => e);

    expect(error).toBeInstanceOf(ErrorApi);
    expect(error.esSesionInvalida).toBe(true);
    expect(error.esSinPermiso).toBe(false);
    expect(error.message).toBe('Token vencido.');
  });

  it('traduce un 403 en un error de permisos y conserva el campo', async () => {
    fetchSimulado.mockResolvedValue(
      respuesta(403, { mensaje: 'No tenés permiso para dar de alta equipos.', campo: 'codigo' }),
    );

    const error = await api.equipos.detalle('id-1').catch((e) => e);

    expect(error.esSinPermiso).toBe(true);
    expect(error.campo).toBe('codigo');
  });

  it('usa un mensaje genérico cuando la respuesta de error no trae uno', async () => {
    fetchSimulado.mockResolvedValue(respuesta(500, undefined));

    const error = await api.organizacion.plan().catch((e) => e);

    expect(error).toBeInstanceOf(ErrorApi);
    expect(error.message).toBe('No fue posible completar la operación.');
  });

  it('resuelve sin cuerpo ante un 204', async () => {
    fetchSimulado.mockResolvedValue(respuesta(204, undefined));

    await expect(api.equipos.catalogos()).resolves.toBeUndefined();
  });

  it('pega contra rutas bajo /api', async () => {
    fetchSimulado.mockResolvedValue(respuesta(200, {}));

    await api.organizacion.plan();

    expect(urlPedida()).toBe('/api/organizacion/plan');
  });
});
