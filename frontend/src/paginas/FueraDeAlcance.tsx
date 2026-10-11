import { Encabezado } from '../componentes/Layout';
import { useT } from '../idioma/IdiomaContext';
import { ConfigNav } from './Organizacion';

/**
 * Secciones de configuración que están en el diseño y **no** en el alcance.
 *
 * Se implementan como una explicación de qué harían y por qué no están, en
 * lugar de un «no implementado» a secas o —peor— de una pantalla con controles
 * que no hacen nada. Un interruptor que no conecta con ninguna funcionalidad es
 * una mentira en la interfaz, y en una defensa es una pregunta que no se puede
 * contestar.
 */
export function FueraDeAlcance({
  titulo,
  queHaria,
  porQueNoEsta,
  queHariaFalta,
}: {
  titulo: string;
  queHaria: string;
  porQueNoEsta: string;
  queHariaFalta: string[];
}) {
  const t = useT();
  return (
    <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
      <ConfigNav />
      <div className="principal">
        <Encabezado titulo={titulo} contexto={t('alcance.fueraDeAlcance', 'Fuera del alcance de esta versión')} />

        <div className="cuerpo">
          <div style={{ maxWidth: 680, display: 'flex', flexDirection: 'column', gap: 16 }}>
            <div className="tarjeta">
              <header>
                <h2>{t('alcance.queHaria', 'Qué haría esta sección')}</h2>
              </header>
              <div style={{ padding: 14 }}>{queHaria}</div>
            </div>

            <div className="tarjeta">
              <header>
                <h2>{t('alcance.porQueNo', 'Por qué no está')}</h2>
              </header>
              <div style={{ padding: 14 }}>{porQueNoEsta}</div>
            </div>

            <div className="tarjeta">
              <header>
                <h2>{t('alcance.queFaltaria', 'Qué haría falta para implementarla')}</h2>
              </header>
              <ul style={{ padding: '14px 14px 14px 32px', margin: 0 }}>
                {queHariaFalta.map((x) => (
                  <li key={x} style={{ marginBottom: 6 }}>
                    {x}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export function Notificaciones() {
  const t = useT();
  return (
    <FueraDeAlcance
      titulo={t('config.notificaciones', 'Notificaciones')}
      queHaria={t(
        'alcance.notificacionesQueHaria',
        'Definir a quién se le avisa y por qué medio cuando pasa algo que amerita ' +
          'atención: una alerta predictiva de riesgo alto, una incidencia crítica sin ' +
          'asignar, un respaldo que falló, o el proveedor de asignación caído.',
      )}
      porQueNoEsta={t(
        'alcance.notificacionesPorQueNo',
        'El sistema no tiene un canal de salida. Configurar a quién avisar sin poder ' +
          'enviarle nada sería una pantalla de controles que no hacen nada, y eso es ' +
          'peor que no tenerla: alguien la configuraría y confiaría en que le va a ' +
          'llegar el aviso. Hoy los avisos que el sistema sí da son los de la propia ' +
          'interfaz —el indicador de asignaciones por respaldo, el estado del ' +
          'proveedor, las alertas del panel— y todos exigen que alguien entre a mirar.',
      )}
      queHariaFalta={[
        t('alcance.notificacionesFalta1',
          'Un servicio de envío de correo, con su configuración de servidor y sus credenciales.'),
        t('alcance.notificacionesFalta2',
          'Una cola o un reintento: un aviso que se pierde porque el servidor de correo estaba caído no sirve.'),
        t('alcance.notificacionesFalta3',
          'Un registro de lo enviado, para poder responder «¿me avisaron?» con un dato y no con una suposición.'),
        t('alcance.notificacionesFalta4',
          'Una política de agrupación: cincuenta alertas en una hora no pueden ser cincuenta correos.'),
      ]}
    />
  );
}

export function Integraciones() {
  const t = useT();
  return (
    <FueraDeAlcance
      titulo={t('config.integraciones', 'Integraciones')}
      queHaria={t(
        'alcance.integracionesQueHaria',
        'Conectar el sistema con herramientas que ya usa la empresa: importar el ' +
          'inventario desde una planilla o desde un agente de red, exportar los ' +
          'indicadores a una herramienta de reportes, o sincronizar los usuarios con ' +
          'el directorio de la organización.',
      )}
      porQueNoEsta={t(
        'alcance.integracionesPorQueNo',
        'El descubrimiento automático de red y los agentes de monitoreo en tiempo real ' +
          'están declarados fuera de alcance en el documento (10.2.3), y son justamente ' +
          'el caso de integración que más valor tendría. Lo que queda —importar una ' +
          'planilla, exportar indicadores— no requiere una sección de configuración: ' +
          'son acciones puntuales que corresponden a las pantallas de Activos y de ' +
          'Análisis.',
      )}
      queHariaFalta={[
        t('alcance.integracionesFalta1',
          'Para la importación: un formato de planilla definido y un informe de qué filas entraron y qué filas no, con el motivo.'),
        t('alcance.integracionesFalta2',
          'Para la exportación: decidir si es un archivo que se descarga o un endpoint que otra herramienta consulta, que son dos diseños distintos.'),
        t('alcance.integracionesFalta3',
          'Para el directorio de usuarios: un proveedor de identidad y el manejo de qué pasa cuando alguien se da de baja allá y tiene incidencias abiertas acá.'),
      ]}
    />
  );
}
