import { Navigate, Route, Routes } from 'react-router-dom';
import { useT } from './idioma/IdiomaContext';
import { Layout, primeraPantalla } from './componentes/Layout';
import { Login } from './paginas/Login';
import { Activos } from './paginas/Activos';
import { EquipoDetalle } from './paginas/EquipoDetalle';
import { Organizacion } from './paginas/Organizacion';
import { Incidencias } from './paginas/Incidencias';
import { IncidenciaDetalle } from './paginas/IncidenciaDetalle';
import { IncidenciaNueva } from './paginas/IncidenciaNueva';
import { Predictivo } from './paginas/Predictivo';
import { Dashboard } from './paginas/Dashboard';
import { Apariencia } from './paginas/Apariencia';
import { Mantenimientos } from './paginas/Mantenimientos';
import { Agenda } from './paginas/Agenda';
import { Planes } from './paginas/Planes';
import { Facturacion } from './paginas/Facturacion';
import { Usuarios } from './paginas/Usuarios';
import { Bitacora } from './paginas/Bitacora';
import { Errores } from './paginas/Errores';
import { Historial } from './paginas/Historial';
import { Reportes } from './paginas/Reportes';
import { Reglas } from './paginas/Reglas';
import { IntegracionIa } from './paginas/IntegracionIa';
import { Respaldos } from './paginas/Respaldos';
import { MisEquipos } from './paginas/MisEquipos';
import { Reportar } from './paginas/cliente/Reportar';
import { MisPedidos } from './paginas/cliente/MisPedidos';
import { PedidoDetalle } from './paginas/cliente/PedidoDetalle';
import { NoEncontrada } from './paginas/NoEncontrada';
import { Integraciones, Notificaciones } from './paginas/FueraDeAlcance';
import { useSesion } from './sesion/SesionContext';

export function App() {
  const t = useT();
  const { sesion, cargando, puede } = useSesion();

  // Mientras se rehidrata la sesión no se decide nada: si se mostrara el login
  // acá, al recargar cualquier pantalla aparecería un parpadeo del formulario.
  if (cargando) {
    return <div className="vacio" style={{ paddingTop: 80 }}>{t('comun.cargando', 'Cargando…')}</div>;
  }

  if (!sesion) return <Login />;

  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/activos" element={<Activos />} />
        <Route path="/mis-equipos" element={<MisEquipos />} />
        {/*
          El modulo del solicitante. Son las mismas operaciones que
          `/incidencias/nueva` y `/incidencias`, con otra pantalla: quien
          reporta no es tecnico, y el formulario del tecnico le pide categoria
          y prioridad, que no puede juzgar.
        */}
        <Route path="/reportar" element={<Reportar />} />
        <Route path="/mis-pedidos" element={<MisPedidos />} />
        <Route path="/mis-pedidos/:id" element={<PedidoDetalle />} />
        <Route path="/activos/:id" element={<EquipoDetalle />} />
        <Route path="/incidencias" element={<Incidencias />} />
        {/* La ruta literal va antes que la paramétrica: si no, «nueva» se
            interpreta como un identificador. */}
        <Route path="/incidencias/nueva" element={<IncidenciaNueva />} />
        <Route path="/incidencias/:id" element={<IncidenciaDetalle />} />
        <Route path="/mantenimientos" element={<Mantenimientos />} />
        {/* La agenda es lo que está previsto; `/mantenimientos` es lo que se
            hizo. Van separadas por eso y no por tamaño de pantalla. */}
        <Route path="/mantenimientos/agenda" element={<Agenda />} />
        <Route path="/historial" element={<Historial />} />
        <Route path="/reportes" element={<Reportes />} />
        <Route path="/analisis" element={<Predictivo />} />
        <Route path="/usuarios" element={<Usuarios />} />
        <Route path="/configuracion" element={<Organizacion />} />
        <Route path="/configuracion/reglas" element={<Reglas />} />
        <Route path="/configuracion/planes" element={<Planes />} />
        <Route path="/configuracion/facturacion" element={<Facturacion />} />
        <Route path="/configuracion/notificaciones" element={<Notificaciones />} />
        <Route path="/configuracion/respaldos" element={<Respaldos />} />
        <Route path="/configuracion/bitacora" element={<Bitacora />} />
        <Route path="/configuracion/errores" element={<Errores />} />
        <Route path="/configuracion/integraciones" element={<Integraciones />} />
        <Route path="/configuracion/ia" element={<IntegracionIa />} />
        <Route path="/configuracion/apariencia" element={<Apariencia />} />
        {/*
          La raiz redirige y cualquier otra direccion desconocida muestra el
          404. Son dos casos distintos: entrar a `/` es normal y hay que
          llevarlo a algun lado, pero abrir `/incidencias/INC-9999` es un
          enlace roto, y redirigir en silencio hace que la persona no se entere
          y lo vuelva a intentar.
        */}
        <Route path="/" element={<Navigate to={primeraPantalla(puede)} replace />} />
        {/*
          `/login` tambien redirige, y no es un detalle: sin sesion cualquier
          direccion muestra el formulario, asi que es la que la gente termina
          guardando en favoritos. Cuando entra, esa direccion ya no significa
          nada y tiene que llevarla adentro, no a un 404.
        */}
        <Route path="/login" element={<Navigate to={primeraPantalla(puede)} replace />} />
        <Route path="*" element={<NoEncontrada />} />
      </Route>
    </Routes>
  );
}
