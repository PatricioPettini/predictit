import { Link, NavLink, Outlet, useLocation } from 'react-router-dom';
import { useEffect, useState, type ReactNode } from 'react';
import { api } from '../api/cliente';
import type { ResumenNavegacionDto } from '../api/tipos';
import { useSesion } from '../sesion/SesionContext';
import { Patentes } from '../api/tipos';
import { useT } from '../idioma/IdiomaContext';
import {
  IconoActivos,
  IconoAnalisis,
  IconoConfiguracion,
  IconoDashboard,
  IconoIncidencias,
  IconoMantenimientos,
  IconoUsuarios,
} from './Iconos';

/**
 * Cada ítem declara la patente que lo habilita.
 *
 * La navegación se arma con lo que el usuario puede hacer, no con una lista
 * fija: un Usuario Solicitante que entrara al panel no vería un menú lleno de
 * opciones que le van a dar 403.
 */
/**
 * Cada ítem declara qué patentes lo habilitan, y alcanza con tener una.
 *
 * Son varias y no una porque el modelo de permisos tiene pares donde ninguna
 * implica a la otra: el administrador tiene INCIDENCIA_VER_TODAS y el
 * solicitante INCIDENCIA_VER_PROPIAS. Con una sola patente por ítem, a uno de
 * los dos le desaparecía el menú.
 */
// Cada ítem lleva la clave del diccionario y no el texto: una constante de
// módulo no puede llamar a un hook, así que el texto se resuelve al dibujar.
const ITEMS = [
  { a: '/dashboard', clave: 'nav.dashboard', texto: 'Dashboard', Icono: IconoDashboard, patentes: [Patentes.dashboardVer] },
  { a: '/activos', clave: 'nav.activos', texto: 'Activos', Icono: IconoActivos, patentes: [Patentes.equipoVer],
    contador: 'equipos' },
  // «Mis equipos» es la vista del modulo del cliente. Se oculta a quien ve
  // el inventario completo: para el administrador seria un subconjunto
  // arbitrario de Activos.
  {
    a: '/mis-equipos',
    clave: 'nav.misEquipos',
    texto: 'Mis equipos',
    Icono: IconoActivos,
    patentes: [Patentes.historialVerPropio],
    ocultarSi: [Patentes.equipoVer],
  },
  // «Mis pedidos» es el listado del modulo del cliente y se oculta a quien ve
  // todas las incidencias, por la misma razon que «Mis equipos»: para el
  // tecnico seria un subconjunto arbitrario de su propia bandeja.
  {
    a: '/mis-pedidos',
    clave: 'nav.misPedidos',
    texto: 'Mis pedidos',
    Icono: IconoIncidencias,
    patentes: [Patentes.incidenciaVerPropias],
    ocultarSi: [Patentes.incidenciaVerTodas],
    contador: 'incidenciasAbiertas',
  },
  {
    a: '/incidencias',
    clave: 'nav.incidencias',
    texto: 'Incidencias',
    Icono: IconoIncidencias,
    patentes: [Patentes.incidenciaVerTodas],
    contador: 'incidenciasAbiertas',
    // Lo que está abierto es trabajo que espera, y eso se mira en rojo.
    tono: 'alto',
  },
  { a: '/mantenimientos', clave: 'nav.mantenimientos', texto: 'Mantenimientos', Icono: IconoMantenimientos,
    patentes: [Patentes.mantenimientoVer] },
  // La agenda tiene su propia entrada y no es una pestaña adentro de
  // mantenimientos: lo que está por vencer es lo que hay que mirar todos los
  // dias, y detras de un clic se mira cuando alguien se acuerda.
  { a: '/mantenimientos/agenda', clave: 'nav.agenda', texto: 'Agenda', Icono: IconoMantenimientos,
    patentes: [Patentes.mantenimientoVer], contador: 'mantenimientosVencidos', tono: 'alto' },
  { a: '/analisis', clave: 'nav.analisis', texto: 'Análisis predictivo', Icono: IconoAnalisis, patentes: [Patentes.alertaVer] },
  {
    a: '/historial',
    clave: 'nav.historial',
    texto: 'Historial del parque',
    Icono: IconoMantenimientos,
    patentes: [Patentes.historialVerOrganizacion],
  },
  { a: '/reportes', clave: 'nav.reportes', texto: 'Reportes', Icono: IconoAnalisis, patentes: [Patentes.reporteGenerar] },
  { a: '/usuarios', clave: 'nav.usuarios', texto: 'Usuarios', Icono: IconoUsuarios, patentes: [Patentes.usuarioGestionar] },
  { a: '/configuracion', clave: 'nav.configuracion', texto: 'Configuración', Icono: IconoConfiguracion,
    patentes: [Patentes.organizacionGestionar] },
] as const;

/**
 * La primera pantalla que puede ver quien inicia sesión.
 *
 * El comodín de las rutas mandaba a todos a `/activos`, y el usuario
 * solicitante no tiene permiso para esa pantalla: lo primero que veía al
 * entrar era un cartel rojo de permiso denegado sobre una tabla vacía. Se
 * elige el primer ítem del menú que efectivamente le corresponde, así que
 * cualquier perfil nuevo queda cubierto sin tocar esto.
 */
export function primeraPantalla(puede: (patente: string) => boolean): string {
  const item = ITEMS.find(
    (i) =>
      i.patentes.some(puede) &&
      !('ocultarSi' in i && (i.ocultarSi as readonly string[]).some(puede)),
  );

  // Sin ningún ítem visible queda el listado de incidencias, que es lo único
  // que alcanza cualquier perfil con sesión.
  return item?.a ?? '/incidencias';
}

/**
 * Cuál de los ítems del menú corresponde a la dirección actual.
 *
 * Gana el más específico de los que son prefijo. Con `isActive` de NavLink a
 * secas, estando en `/mantenimientos/agenda` quedaban resaltados
 * «Mantenimientos» y «Agenda» a la vez; con `end` en todos, el detalle de un
 * equipo dejaba de resaltar «Activos». El menú tiene un solo trabajo, que es
 * decir dónde está uno, y las dos formas lo hacen mal.
 */
export function itemActivo(ruta: string, items: readonly string[]): string | null {
  const candidatos = items.filter((a) => ruta === a || ruta.startsWith(`${a}/`));
  if (candidatos.length === 0) return null;
  return candidatos.reduce((mejor, a) => (a.length > mejor.length ? a : mejor));
}

/**
 * El rastro que se dibuja arriba del título: por dónde se llegó hasta acá.
 *
 * Son los antecesores y no la pantalla actual, que ya está escrita abajo en el
 * título: repetirla no agrega nada. En una pantalla de primer nivel queda sólo
 * el nombre del sistema, que es lo que hace que el renglón no cambie de alto
 * al navegar.
 */
export function migas(ruta: string, seccion?: string): string[] {
  const rastro = ['PredictIT'];

  // Sólo hay sección si la dirección va más adentro que el ítem del menú. En
  // `/activos` la sección es la pantalla misma; en `/activos/PC-014` no.
  const item = itemActivo(ruta, ITEMS.map((i) => i.a));
  if (seccion && item && ruta !== item) rastro.push(seccion);

  return rastro;
}

/**
 * Los contadores de la navegación, refrescados en cada pantalla.
 *
 * Se vuelve a pedir al cambiar de ruta y no una sola vez al entrar: la gracia
 * de la insignia es decir cuánto hay **ahora**, y un número que se quedó en el
 * que había al iniciar sesión es peor que no tener número, porque se lo cree.
 *
 * Un error no rompe nada ni se muestra: son insignias, no contenido. Si no
 * llegan, la navegación queda como estaba antes de tenerlas.
 */
function useResumen(ruta: string): ResumenNavegacionDto {
  const [resumen, setResumen] = useState<ResumenNavegacionDto>({});

  useEffect(() => {
    let vigente = true;
    api.organizacion.resumen()
      .then((r) => { if (vigente) setResumen(r); })
      .catch(() => { if (vigente) setResumen({}); });
    return () => { vigente = false; };
  }, [ruta]);

  return resumen;
}

function Sidebar() {
  const { sesion, puede, salir } = useSesion();
  const t = useT();
  const ruta = useLocation().pathname;
  const activo = itemActivo(ruta, ITEMS.map((i) => i.a));
  const resumen = useResumen(ruta);

  return (
    <aside className="sidebar">
      <div className="marca">
        <b><span className="marca-icono" aria-hidden="true"><IconoAnalisis /></span>{t('app.nombre', 'PredictIT')}</b>
      </div>

      {/* La organización y su plan viven acá y no en el encabezado: son el
          contexto de todo lo que se ve, no de la pantalla en la que uno está.
          El partner, que salta entre clientes, necesita tenerlo siempre a la
          vista y no sólo cuando mira el selector. */}
      <div className="sidebar-organizacion">
        <div className="org-nombre" title={sesion?.organizacion.razonSocial}>
          {sesion?.organizacion.nombreCorto}
        </div>
        {sesion?.organizacion.plan ? (
          <div className="org-plan">
            <span>{sesion.organizacion.plan.nombre}</span>
            {/* Los equipos administrados contra los que el plan incluye. Es el
                número que explica el excedente que se factura, y tenerlo al
                lado del nombre del plan evita la sorpresa de fin de mes. */}
            <span className={resumen.equipos !== undefined
                             && resumen.equipos > sesion.organizacion.plan.equiposIncluidos
              ? 'mono excede' : 'mono'}>
              {resumen.equipos === undefined
                ? t('nav.equiposIncluidos', '{n} eq.', { n: sesion.organizacion.plan.equiposIncluidos })
                : t('nav.equiposDeIncluidos', '{n}/{incluidos} eq.', {
                    n: resumen.equipos, incluidos: sesion.organizacion.plan.equiposIncluidos })}
            </span>
          </div>
        ) : null}
      </div>

      <nav>
        {ITEMS.filter(
          (i) =>
            i.patentes.some(puede) &&
            !('ocultarSi' in i && (i.ocultarSi as readonly string[]).some(puede)),
        ).map((item) => {
          const { a, clave, texto, Icono } = item;
          const contador = 'contador' in item ? resumen[item.contador] : undefined;
          return (
          // El `title` es lo que queda cuando la barra se reduce a un riel de
          // iconos en una tablet: el rotulo deja de verse pero no desaparece.
          // El `aria-label` es para el lector de pantalla, que en el riel
          // tampoco encuentra el texto.
          <NavLink
            key={a}
            to={a}
            title={t(clave, texto)}
            aria-label={t(clave, texto)}
            className={() => (a === activo ? 'activo' : undefined)}
          >
            <Icono />
            <span>{t(clave, texto)}</span>
            {/* Sin dato no hay insignia. El cero sí se dibuja: «0 vencidos» es
                justamente la novedad que uno quiere ver de reojo. */}
            {contador === undefined ? null : (
              <span className={'insignia' + (contador > 0 && 'tono' in item ? ' alto' : '')}>
                {contador}
              </span>
            )}
          </NavLink>
          );
        })}
      </nav>

      <div className="pie">
        <div className="pie-identidad">
          <div className="avatar" aria-hidden="true">{iniciales(sesion?.nombreCompleto)}</div>
          <div className="pie-datos">
            <div className="perfil-nombre">{sesion?.nombreCompleto}</div>
            <div>{sesion?.roles.join(', ')}</div>
          </div>
        </div>
        <button
          type="button"
          className="btn plano"
          style={{ padding: 0, height: 'auto', marginTop: 8 }}
          onClick={salir}
        >
          {t('nav.salir', 'Cerrar sesión')}
        </button>
      </div>
    </aside>
  );
}

/** Selector de organización. Sólo lo ve el perfil Partner. */
function SelectorOrganizacion() {
  const { sesion, puede, cambiarOrganizacion } = useSesion();
  const t = useT();

  if (!puede(Patentes.organizacionCambiar) || !sesion) return null;
  if (sesion.organizacionesDisponibles.length < 2) return null;

  return (
    <select
      className="campo"
      value={sesion.organizacion.id}
      onChange={(e) => void cambiarOrganizacion(e.target.value)}
      aria-label={t('nav.organizacionActiva', 'Organización activa')}
    >
      {sesion.organizacionesDisponibles.map((o) => (
        <option key={o.id} value={o.id}>
          {o.nombreCorto}
        </option>
      ))}
    </select>
  );
}

/** Las dos primeras iniciales del nombre, para el círculo del pie. */
function iniciales(nombre?: string): string {
  return (nombre ?? '')
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase() ?? '')
    .join('');
}

export function Encabezado({
  titulo,
  contexto,
  acciones,
}: {
  titulo: ReactNode;
  contexto?: ReactNode;
  acciones?: ReactNode;
}) {
  const ruta = useLocation().pathname;
  const t = useT();

  const item = ITEMS.find((i) => i.a === itemActivo(ruta, ITEMS.map((x) => x.a)));
  const rastro = migas(ruta, item ? t(item.clave, item.texto) : undefined);

  return (
    <header className="encabezado">
      <div>
        {/* La sección es un enlace y no un texto: es por donde se vuelve, y
            era lo que cada pantalla de detalle venía repitiendo por su cuenta
            en el renglón de contexto. */}
        <div className="encabezado-migas">
          {rastro.map((m, i) => (
            <span key={m}>
              {i > 0 ? ' / ' : ''}
              {i > 0 && item ? <Link to={item.a}>{m}</Link> : m}
            </span>
          ))}
        </div>
        <h1>{titulo}</h1>
        {contexto ? <div className="contexto">{contexto}</div> : null}
      </div>
      <div className="acciones">
        <SelectorOrganizacion />
        {acciones}
      </div>
    </header>
  );
}

export function Layout() {
  const ruta = useLocation().pathname;
  const cliente = ruta === '/reportar' || ruta === '/mis-equipos' || ruta.startsWith('/mis-pedidos');
  return (
    <div className={cliente ? 'app modulo-cliente' : 'app'}>
      <Sidebar />
      <div className="principal">
        <Outlet />
      </div>
    </div>
  );
}
