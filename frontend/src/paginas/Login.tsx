import { useState } from 'react';
import { ErrorApi } from '../api/cliente';
import { useIdioma, useT } from '../idioma/IdiomaContext';
import { useSesion } from '../sesion/SesionContext';

/**
 * Ingreso al sistema (CU-001).
 *
 * Es la única pantalla sin datos, así que es donde la dirección visual se
 * declara sin ayuda del contenido: el glow del fondo, la tarjeta de radio
 * grande con la sombra larga, la marca centrada y nada más.
 *
 * Los tres fallos NO comparten tratamiento, y es a propósito:
 *
 *   credenciales   rojo, adentro del formulario, porque se puede corregir ahí
 *                  mismo y los campos tienen que seguir a la vista.
 *   bloqueado      ámbar y reemplaza al formulario, porque no hay nada que
 *                  corregir: lo destraba una persona.
 *   sin conexión   neutro, porque no es un error de quien está entrando.
 *
 * Para elegir entre los tres se mira `codigo`, que la API manda al lado del
 * mensaje. Comparar el texto se rompería con la interfaz en inglés.
 */
export function Login() {
  const t = useT();
  const { entrar } = useSesion();
  const { idioma, idiomas, cambiar: cambiarIdioma } = useIdioma();

  const [username, setUsername] = useState('');
  const [contrasena, setContrasena] = useState('');
  const [verClave, setVerClave] = useState(false);
  const [fallo, setFallo] = useState<{ codigo: string; mensaje: string } | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setFallo(null);
    setEnviando(true);
    try {
      await entrar(username, contrasena);
    } catch (ex) {
      if (ex instanceof ErrorApi) {
        // Sin código es un fallo que no viene de la regla de autenticación
        // —un 500, por ejemplo—, y se trata como problema del servidor.
        setFallo({ codigo: ex.codigo ?? 'servidor', mensaje: ex.message });
      } else {
        setFallo({
          codigo: 'conexion',
          mensaje: t('error.conectar', 'No se pudo conectar con el servidor.'),
        });
      }
    } finally {
      setEnviando(false);
    }
  }

  const bloqueado = fallo?.codigo === 'bloqueado';

  return (
    <div
      style={{
        minHeight: '100%',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 'var(--esp-6)',
        // El fondo de la aplicación ya trae el glow desde `base.css`. Acá no se
        // repinta nada: si el login tuviera su propio fondo, sería la única
        // pantalla con un degradé distinto al del resto.
      }}
    >
      {/*
        El selector de idioma va arriba a la derecha y visible antes de escribir:
        quien necesita el inglés lo necesita antes del primer campo, no después
        de fallar. Es el único control de la pantalla que no está en la tarjeta.
      */}
      {idiomas.length > 1 ? (
        <div
          style={{
            position: 'absolute',
            top: 'var(--esp-6)',
            right: 'var(--esp-6)',
            display: 'flex',
            gap: 'var(--esp-1)',
          }}
        >
          {idiomas.map((i) => (
            <button
              key={i.codigo}
              type="button"
              className="btn plano"
              aria-pressed={i.codigo === idioma}
              onClick={() => cambiarIdioma(i.codigo)}
              style={{
                color: i.codigo === idioma ? 'var(--tx)' : 'var(--tx3)',
                background: i.codigo === idioma ? 'var(--sup-2)' : 'transparent',
              }}
            >
              {i.nombre}
            </button>
          ))}
        </div>
      ) : null}

      <div style={{ width: 396, maxWidth: '100%' }}>
        <div style={{ marginBottom: 'var(--esp-6)', textAlign: 'center' }}>
          {/* 28 px es el valor del artboard, y como el de la barra lateral queda
              fuera de la escala a proposito: un logotipo no es texto que se lee. */}
          <div style={{ fontSize: 28, fontWeight: 'var(--peso-cifra)', letterSpacing: '-0.5px' }}>
            {t('app.nombre', 'PredictIT')}
          </div>
          <div
            style={{
              fontSize: 'var(--txt-cuerpo)',
              color: 'var(--tx2)',
              marginTop: 'var(--esp-1)',
              lineHeight: 'var(--interlineado-parrafo)',
            }}
          >
            {t('app.bajada', 'Gestión y mantenimiento predictivo de equipos informáticos')}
          </div>
        </div>

        {bloqueado ? (
          <CuentaBloqueada
            onVolver={() => {
              setFallo(null);
              setContrasena('');
            }}
          />
        ) : (
          <form
            className="tarjeta"
            onSubmit={enviar}
            style={{
              padding: 'var(--esp-6)',
              borderRadius: 'var(--radio-modal)',
              boxShadow: 'var(--elev-flotante)',
            }}
          >
            <h1
              style={{
                fontSize: 'var(--txt-seccion)',
                fontWeight: 'var(--peso-seccion)',
                marginBottom: 'var(--esp-4)',
              }}
            >
              {t('login.titulo', 'Iniciar sesión')}
            </h1>

            <div style={{ marginBottom: 'var(--esp-3)' }}>
              <label className="etiqueta" htmlFor="usuario">
                {t('login.usuario', 'Usuario')}
              </label>
              <div className="campo">
                <input
                  id="usuario"
                  name="usuario"
                  autoComplete="username"
                  autoFocus
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                />
              </div>
            </div>

            <div style={{ marginBottom: 'var(--esp-4)' }}>
              <label className="etiqueta" htmlFor="contrasena">
                {t('login.contrasena', 'Contraseña')}
              </label>
              <div className="campo" style={{ display: 'flex', alignItems: 'center' }}>
                <input
                  id="contrasena"
                  name="contrasena"
                  type={verClave ? 'text' : 'password'}
                  autoComplete="current-password"
                  value={contrasena}
                  onChange={(e) => setContrasena(e.target.value)}
                  style={{ flex: 1, minWidth: 0 }}
                />
                {/*
                  Mostrar la contraseña es una ayuda real cuando se escribe una
                  larga en un teclado que no es el propio, y el riesgo de que
                  alguien mire la pantalla lo corre quien decide apretarlo.
                */}
                <button
                  type="button"
                  onClick={() => setVerClave((v) => !v)}
                  aria-pressed={verClave}
                  style={{
                    border: 'none',
                    background: 'transparent',
                    color: 'var(--acento-tx)',
                    cursor: 'pointer',
                    font: 'inherit',
                    fontSize: 'var(--txt-tabla)',
                    padding: '0 var(--esp-1)',
                  }}
                >
                  {verClave ? t('login.ocultar', 'Ocultar') : t('login.mostrar', 'Mostrar')}
                </button>
              </div>
            </div>

            {fallo ? (
              <div
                className={fallo.codigo === 'credenciales' ? 'aviso error' : 'aviso atencion'}
                style={{ marginBottom: 'var(--esp-4)' }}
                role="alert"
              >
                {fallo.mensaje}
              </div>
            ) : null}

            <button
              type="submit"
              className="btn pri"
              style={{ width: '100%', justifyContent: 'center' }}
              disabled={enviando || !username || !contrasena}
            >
              {enviando ? t('login.entrando', 'Ingresando…') : t('login.entrar', 'Ingresar')}
            </button>
          </form>
        )}

        <div
          style={{
            fontSize: 'var(--txt-tabla)',
            color: 'var(--tx3)',
            marginTop: 'var(--esp-4)',
            textAlign: 'center',
            lineHeight: 'var(--interlineado-parrafo)',
          }}
        >
          {t('login.ayuda',
             '¿Problemas para entrar? Escribile al administrador de tu organización.')}
          <div style={{ marginTop: 'var(--esp-2)' }}>
            {t('app.pie', 'Trabajo Final de Ingeniería · Universidad Abierta Interamericana')}
          </div>
        </div>
      </div>
    </div>
  );
}

/**
 * Cuenta bloqueada.
 *
 * Reemplaza al formulario en vez de aparecer arriba de él, porque ahí no hay
 * nada que corregir: volver a escribir la contraseña no destraba nada, y dejar
 * los campos a la vista invita justamente a eso. Lo destraba un administrador
 * de la organización desde Usuarios, y eso es lo que dice.
 */
function CuentaBloqueada({ onVolver }: { onVolver: () => void }) {
  const t = useT();
  return (
    <div
      className="tarjeta"
      role="alert"
      style={{
        padding: 'var(--esp-6)',
        borderRadius: 'var(--radio-modal)',
        borderColor: 'var(--medio-borde)',
        boxShadow: 'var(--elev-flotante)',
      }}
    >
      <h1
        style={{
          fontSize: 'var(--txt-seccion)',
          fontWeight: 'var(--peso-seccion)',
          color: 'var(--medio-tx)',
          marginBottom: 'var(--esp-2)',
        }}
      >
        {t('login.bloqueadaTitulo', 'Cuenta bloqueada por intentos fallidos')}
      </h1>

      {/*
        Aca no se muestra el mensaje de la API: decia lo mismo que el titulo con
        otras palabras. El texto propio ademas dice QUIEN lo destraba y desde
        donde, que es lo unico accionable que tiene esta pantalla.
      */}
      <p
        style={{
          fontSize: 'var(--txt-cuerpo)',
          color: 'var(--tx2)',
          lineHeight: 'var(--interlineado-parrafo)',
          marginBottom: 'var(--esp-4)',
        }}
      >
        {t('login.bloqueadaDetalle',
           'El bloqueo no se levanta solo ni con el tiempo: lo destraba un administrador de tu '
           + 'organización desde la pantalla de Usuarios.')}
      </p>

      <button type="button" className="btn" onClick={onVolver} style={{ width: '100%', justifyContent: 'center' }}>
        {t('login.volver', 'Volver al ingreso')}
      </button>
    </div>
  );
}
