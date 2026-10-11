import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ProveedorIdioma, useT } from '../idioma/IdiomaContext';

/**
 * El idioma de la interfaz (H-38).
 *
 * Lo que se prueba no es que exista un diccionario, sino las tres cosas que
 * pueden salir mal en producción: que el texto salga del diccionario cuando
 * está, que salga el castellano del código cuando la clave falta, y que salga
 * igual cuando la API no contesta.
 */

const diccionario = vi.fn();
const listar = vi.fn();
const cambiar = vi.fn();

vi.mock('../api/cliente', () => ({
  api: {
    idiomas: {
      diccionario: (...args: unknown[]) => diccionario(...args),
      listar: () => listar(),
      cambiar: (c: string) => cambiar(c),
    },
  },
  ErrorApi: class extends Error {},
}));

function Pantalla() {
  const t = useT();
  return (
    <>
      <h1>{t('nav.activos', 'Activos')}</h1>
      <p>{t('clave.inexistente', 'Texto del código')}</p>
      <span>{t('con.datos', 'Van {n} de {total}', { n: 3, total: 7 })}</span>
      <em>
        {t('con.datos.inexistente', 'Faltan {dias} días', { dias: 12 })}
      </em>
    </>
  );
}

function montar() {
  render(
    <MemoryRouter>
      <ProveedorIdioma>
        <Pantalla />
      </ProveedorIdioma>
    </MemoryRouter>,
  );
}

describe('Idioma de la interfaz', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    listar.mockResolvedValue([
      { codigo: 'es-AR', nombre: 'Español', esPorDefecto: true },
      { codigo: 'en-US', nombre: 'English', esPorDefecto: false },
    ]);
  });

  it('usa el texto del diccionario cuando la clave está', async () => {
    diccionario.mockResolvedValue({
      codigo: 'en-US',
      nombre: 'English',
      textos: { 'nav.activos': 'Assets' },
    });

    montar();

    expect(await screen.findByRole('heading', { name: 'Assets' })).toBeInTheDocument();
  });

  it('usa el castellano del código cuando la clave falta', async () => {
    // Es el caso que importa: una clave sin cargar tiene que dejar la pantalla
    // legible, no mostrar un pedazo de clave ni un hueco.
    diccionario.mockResolvedValue({
      codigo: 'en-US',
      nombre: 'English',
      textos: { 'nav.activos': 'Assets' },
    });

    montar();

    await screen.findByRole('heading', { name: 'Assets' });
    expect(screen.getByText('Texto del código')).toBeInTheDocument();
  });

  it('si la API no contesta, la pantalla se lee igual', async () => {
    diccionario.mockRejectedValue(new Error('sin red'));

    montar();

    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'Activos' })).toBeInTheDocument());
    expect(screen.getByText('Texto del código')).toBeInTheDocument();
  });

  it('los marcadores se reemplazan, esté la clave o no', async () => {
    // Los dos casos en la misma prueba porque el que fallaba era el segundo:
    // con la clave cargada el reemplazo andaba, y con el texto de reserva
    // salía «Faltan {dias} días» con las llaves a la vista. El texto de
    // reserva existe para que la pantalla se pueda leer cuando el diccionario
    // no tiene la entrada, y con las llaves adentro no se puede.
    diccionario.mockResolvedValue({
      codigo: 'es-AR',
      nombre: 'Español',
      textos: { 'con.datos': 'Van {n} de {total}' },
    });

    montar();

    expect(await screen.findByText('Van 3 de 7')).toBeInTheDocument();
    expect(screen.getByText('Faltan 12 días')).toBeInTheDocument();
    expect(screen.queryByText(/\{dias\}/)).not.toBeInTheDocument();
  });

  it('el idioma elegido se recuerda en el navegador', async () => {
    localStorage.setItem('predictit.idioma', 'en-US');
    diccionario.mockResolvedValue({ codigo: 'en-US', nombre: 'English', textos: {} });

    montar();

    // Se pide el guardado y no el del usuario: la preferencia sobrevive al
    // cierre de sesión, igual que el tema.
    await waitFor(() => expect(diccionario).toHaveBeenCalledWith('en-US'));
  });
});
