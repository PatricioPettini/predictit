import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { App } from './App';
import { ProveedorSesion } from './sesion/SesionContext';
import { ProveedorIdioma } from './idioma/IdiomaContext';
import { ProveedorTema } from './tema/TemaContext';
// El orden importa: tokens define las variables, base las consume para el
// reset, app viste los componentes y temas cierra con las dos correcciones de
// los temas oscuros que no se pueden expresar como variable.
import './estilos/tokens.css';
import './estilos/base.css';
import './estilos/app.css';
import './estilos/temas.css';
// Ultimo: lo unico que hace es sobrescribir. Comentarlo devuelve el
// layout fijo sin tocar ningun componente.
import './estilos/responsive.css';

const raiz = document.getElementById('raiz');
if (!raiz) throw new Error('No se encontró el elemento #raiz.');

createRoot(raiz).render(
  <StrictMode>
    <BrowserRouter>
      {/* El tema envuelve a la sesión: se aplica también a la pantalla de
          login, que es la primera que se ve. */}
      <ProveedorTema>
        {/* El idioma también envuelve a la sesión, y por el mismo motivo: la
            pantalla de login está traducida y ahí todavía no hay usuario. */}
        <ProveedorIdioma>
          <ProveedorSesion>
            <App />
          </ProveedorSesion>
        </ProveedorIdioma>
      </ProveedorTema>
    </BrowserRouter>
  </StrictMode>,
);
