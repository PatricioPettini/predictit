import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { api, ErrorApi, token } from '../api/cliente';
import type { SesionDto } from '../api/tipos';

interface EstadoSesion {
  sesion: SesionDto | null;
  cargando: boolean;
  entrar: (username: string, contrasena: string) => Promise<void>;
  salir: () => void;
  cambiarOrganizacion: (id: string) => Promise<void>;
  /** Indica si el usuario tiene esa patente. Gobierna qué se muestra. */
  puede: (dataKey: string) => boolean;
}

const Contexto = createContext<EstadoSesion | null>(null);

export function ProveedorSesion({ children }: { children: ReactNode }) {
  const [sesion, setSesion] = useState<SesionDto | null>(null);
  const [cargando, setCargando] = useState(true);

  // Al recargar la página hay un token en sessionStorage pero no hay sesión en
  // memoria: se rehidrata preguntándole a la API. Si el token venció o le
  // revocaron permisos, la API responde 401 y se limpia.
  useEffect(() => {
    if (!token.leer()) {
      setCargando(false);
      return;
    }

    api.auth
      .sesion()
      .then(setSesion)
      .catch(() => token.borrar())
      .finally(() => setCargando(false));
  }, []);

  const entrar = useCallback(async (username: string, contrasena: string) => {
    const nueva = await api.auth.login(username, contrasena);
    token.guardar(nueva.token);
    setSesion(nueva);
  }, []);

  const salir = useCallback(() => {
    token.borrar();
    setSesion(null);
  }, []);

  const cambiarOrganizacion = useCallback(async (id: string) => {
    const nueva = await api.auth.cambiarOrganizacion(id);
    // El cambio de organización emite un token nuevo: el anterior sigue
    // apuntando a la organización vieja.
    token.guardar(nueva.token);
    setSesion(nueva);
  }, []);

  const puede = useCallback(
    (dataKey: string) => sesion?.patentes.includes(dataKey) ?? false,
    [sesion],
  );

  const valor = useMemo<EstadoSesion>(
    () => ({ sesion, cargando, entrar, salir, cambiarOrganizacion, puede }),
    [sesion, cargando, entrar, salir, cambiarOrganizacion, puede],
  );

  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}

export function useSesion() {
  const ctx = useContext(Contexto);
  if (!ctx) throw new Error('useSesion tiene que usarse dentro de ProveedorSesion.');
  return ctx;
}

/**
 * Cierra la sesión cuando la API responde que el token dejó de valer.
 *
 * El frontend esconde lo que el usuario no puede hacer, pero la autorización
 * real la decide la API: si contesta 401, no hay nada que discutir.
 */
export function useCerrarSiExpiro() {
  const { salir } = useSesion();
  return useCallback(
    (error: unknown) => {
      if (error instanceof ErrorApi && error.esSesionInvalida) salir();
    },
    [salir],
  );
}
