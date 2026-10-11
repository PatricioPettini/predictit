/**
 * Íconos del sistema.
 *
 * Los trazos son los mismos del canvas de diseño, sin retocar. Van como SVG en
 * línea y no como fuente de iconos ni emoji: heredan el color del texto con
 * `currentColor`, así el estado activo de la navegación no necesita una segunda
 * versión de cada ícono.
 */

interface Props {
  size?: number;
}

function Svg({ size = 18, children }: Props & { children: React.ReactNode }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.6}
      aria-hidden="true"
    >
      {children}
    </svg>
  );
}

export const IconoDashboard = (p: Props) => (
  <Svg {...p}>
    <rect x="3" y="3" width="7" height="8" rx="1" />
    <rect x="14" y="3" width="7" height="5" rx="1" />
    <rect x="3" y="14" width="7" height="7" rx="1" />
    <rect x="14" y="11" width="7" height="10" rx="1" />
  </Svg>
);

export const IconoActivos = (p: Props) => (
  <Svg {...p}>
    <rect x="3" y="4" width="18" height="12" rx="1.5" />
    <path d="M8 20h8M12 16v4" />
  </Svg>
);

export const IconoIncidencias = (p: Props) => (
  <Svg {...p}>
    <path d="M12 3.5 21 19H3z" />
    <path d="M12 9.5v4M12 16.2v.4" />
  </Svg>
);

export const IconoMantenimientos = (p: Props) => (
  <Svg {...p}>
    <path d="M14.5 4.5a4.5 4.5 0 0 0 5.9 5.9L21 10l-7.5 7.5-3-3L18 7l-.4-.6a4.5 4.5 0 0 0-3.1-1.9z" />
    <path d="M9.5 14.5 4 20l1.5 1.5" />
  </Svg>
);

export const IconoAnalisis = (p: Props) => (
  <Svg {...p}>
    <path d="M3 20h18" />
    <path d="M4 15.5 9.5 9l4 3.5L20 5" />
  </Svg>
);

export const IconoUsuarios = (p: Props) => (
  <Svg {...p}>
    <circle cx="9" cy="8" r="3.2" />
    <path d="M3.5 19c.6-3 2.8-4.6 5.5-4.6S14 16 14.6 19" />
    <path d="M16 8.2a3 3 0 0 1 0 5.6M18 19c-.3-1.9-1-3.2-2.2-4.2" />
  </Svg>
);

export const IconoConfiguracion = (p: Props) => (
  <Svg {...p}>
    <circle cx="12" cy="12" r="3" />
    <path d="M19.4 14a1.6 1.6 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.6 1.6 0 0 0-2.7 1.1v.3a2 2 0 1 1-4 0v-.2a1.6 1.6 0 0 0-2.8-1.1l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1A1.6 1.6 0 0 0 3.5 13H3a2 2 0 1 1 0-4h.2A1.6 1.6 0 0 0 4.3 6.3l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.6 1.6 0 0 0 2.7-1.1V2a2 2 0 1 1 4 0v.2a1.6 1.6 0 0 0 2.8 1.1l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.6 1.6 0 0 0 1.1 2.7h.3a2 2 0 1 1 0 4h-.2a1.6 1.6 0 0 0-1.2 1.2z" />
  </Svg>
);

export const IconoBuscar = (p: Props) => (
  <Svg {...p} size={p.size ?? 15}>
    <circle cx="11" cy="11" r="6.5" />
    <path d="m16 16 4.5 4.5" />
  </Svg>
);

export const IconoImportar = (p: Props) => (
  <Svg {...p} size={p.size ?? 15}>
    <path d="M12 15V3" />
    <path d="m7 8 5-5 5 5" />
    <path d="M4 17v2.5A1.5 1.5 0 0 0 5.5 21h13a1.5 1.5 0 0 0 1.5-1.5V17" />
  </Svg>
);

export const IconoVolver = (p: Props) => (
  <Svg {...p} size={p.size ?? 15}>
    <path d="M19 12H5" />
    <path d="m12 19-7-7 7-7" />
  </Svg>
);
