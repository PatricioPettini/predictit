import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { FormularioEquipo } from '../componentes/FormularioEquipo';
import { ErrorApi } from '../api/cliente';
import type { CatalogosEquipoDto } from '../api/tipos';

/**
 * El enlace entre el campo rechazado y el mensaje que lo explica.
 *
 * Es la clase de cosa que se rompe sin que nadie se entere: en pantalla se ve
 * el borde rojo y el texto debajo, así que parece que está. Lo que falta no se
 * ve —`aria-invalid` y `aria-describedby`—, y sin eso quien usa un lector de
 * pantalla llega al campo, lo oye como cualquier otro, y el motivo queda
 * flotando como un texto suelto sin dueño.
 *
 * El borde rojo solo tampoco alcanza: es información por color, que es
 * justamente lo que la WCAG no permite como único canal.
 */

const catalogos: CatalogosEquipoDto = {
  tipos: [{ id: 't1', nombre: 'PC de escritorio' }],
  estados: [{ id: 'e1', nombre: 'Operativo', operativo: true }],
  ubicaciones: [{ id: 'u1', nombre: 'Administración' }],
  responsables: [{ id: 'r1', nombreCompleto: 'Gabriela Suárez' }],
};

const registrar = vi.fn();

vi.mock('../api/cliente', async () => {
  const real = await vi.importActual<typeof import('../api/cliente')>('../api/cliente');
  return { ...real, api: { equipos: { registrar: (...a: unknown[]) => registrar(...a) } } };
});

vi.mock('../sesion/SesionContext', () => ({
  useCerrarSiExpiro: () => () => {},
}));

vi.mock('../idioma/IdiomaContext', () => ({
  useT: () => (_clave: string, porDefecto: string) => porDefecto,
}));

function dibujar() {
  return render(
    <MemoryRouter>
      <FormularioEquipo catalogos={catalogos} alGuardar={() => {}} alCancelar={() => {}} />
    </MemoryRouter>,
  );
}

/** Completa lo mínimo y envía, con el servidor rechazando un campo. */
function enviarRechazando(campo: string, mensaje: string) {
  // `ErrorApi` se importa del módulo simulado y no con `importActual`: la
  // fábrica reexporta la clase real, así que es el mismo objeto que ve el
  // componente. Con dos copias de la clase, su `instanceof` da falso y el
  // formulario trata un error de validación como una falla cualquiera.
  registrar.mockImplementation(() => Promise.reject(new ErrorApi(400, mensaje, campo)));

  dibujar();
  fireEvent.change(screen.getByPlaceholderText('PC-ADM-014'), { target: { value: 'PC-X-1' } });
  fireEvent.click(screen.getByRole('button', { name: 'Crear equipo' }));
}

describe('El campo que el servidor rechaza', () => {


  it('queda marcado como inválido y apunta al mensaje', async () => {
    enviarRechazando('codigo', 'Ya hay un equipo con ese código.');

    const campo = await screen.findByPlaceholderText('PC-ADM-014');
    await waitFor(() => expect(campo.getAttribute('aria-invalid')).toBe('true'));

    // El campo tiene que apuntar a un elemento que exista y que diga el motivo:
    // un `aria-describedby` colgando de un id inexistente no dice nada, y es
    // exactamente lo que queda si alguien mueve el mensaje de lugar.
    const id = campo.getAttribute('aria-describedby');
    expect(id).toBeTruthy();
    expect(document.getElementById(id!)?.textContent)
      .toBe('Ya hay un equipo con ese código.');
  });

  it('el campo que no fallo no queda marcado', async () => {
    enviarRechazando('codigo', 'Ya hay un equipo con ese código.');

    await waitFor(() =>
      expect(screen.getByPlaceholderText('PC-ADM-014').getAttribute('aria-invalid')).toBe('true'));

    // Marcar todo el formulario cuando falla un campo hace que la marca no
    // signifique nada.
    const serie = screen.getByLabelText('Número de serie');
    expect(serie.getAttribute('aria-invalid')).toBeNull();
    expect(serie.getAttribute('aria-describedby')).toBeNull();
  });
});
