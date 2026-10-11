import { useEffect, useState } from 'react';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { NavLink } from 'react-router-dom';
import { api, ErrorApi } from '../api/cliente';
import type { ConsumoIaDto, SituacionPlanDto } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';

// La clave viaja al lado del texto, igual que en el menú principal: la lista
// se declara acá y `t` sólo existe adentro de un componente.
const SECCIONES = [
  { a: '/configuracion', clave: 'org.titulo', texto: 'Organización' },
  { a: '/configuracion/reglas', clave: 'regla.titulo', texto: 'Reglas predictivas' },
  { a: '/configuracion/planes', clave: 'plan.titulo', texto: 'Planes de mantenimiento' },
  { a: '/configuracion/facturacion', clave: 'factura.titulo', texto: 'Facturación del servicio' },
  { a: '/configuracion/notificaciones', clave: 'config.notificaciones', texto: 'Notificaciones' },
  { a: '/configuracion/respaldos', clave: 'respaldo.titulo', texto: 'Respaldos' },
  { a: '/configuracion/bitacora', clave: 'bitacora.titulo', texto: 'Bitácora' },
  { a: '/configuracion/errores', clave: 'error.titulo', texto: 'Errores del sistema' },
  { a: '/configuracion/integraciones', clave: 'config.integraciones', texto: 'Integraciones' },
  { a: '/configuracion/ia', clave: 'ia.titulo', texto: 'Integración de IA' },
  { a: '/configuracion/apariencia', clave: 'config.apariencia', texto: 'Apariencia' },
] as const;

export function ConfigNav() {
  const t = useT();
  return (
    <nav className="confignav">
      <div className="rotulo">{t('nav.configuracion', 'Configuración')}</div>
      {SECCIONES.map(({ a, clave, texto }) => (
        <NavLink key={a} to={a} end className={({ isActive }) => (isActive ? 'activo' : undefined)}>
          {t(clave, texto)}
        </NavLink>
      ))}
    </nav>
  );
}

/**
 * Fila de la cuenta del plan.
 *
 * La cuenta se muestra desglosada y no sólo el total: el número que importa es
 * el total, pero de dónde sale es lo que hace que el cliente lo entienda.
 */
function Fila({
  concepto,
  valor,
  destacado,
  atencion,
}: {
  concepto: string;
  valor: string;
  destacado?: boolean;
  atencion?: boolean;
}) {
  return (
    <div
      style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'baseline',
        padding: '9px 16px',
        borderBottom: '1px solid var(--linea-suave)',
        fontSize: destacado ? 14 : 13,
        fontWeight: destacado ? 600 : 400,
        background: destacado ? 'var(--fondo)' : undefined,
      }}
    >
      <span style={{ color: destacado ? 'var(--tx)' : 'var(--tx2)' }}>{concepto}</span>
      <span
        className="mono"
        style={{
          fontSize: destacado ? 15 : 13,
          fontWeight: destacado ? 600 : 500,
          color: atencion ? 'var(--medio-tx)' : 'var(--tx)',
        }}
      >
        {valor}
      </span>
    </div>
  );
}

/**
 * Consumo del servicio de IA en el mes corriente.
 *
 * Sale de la bitacora, que ya asienta cada consulta con su codigo de evento.
 * Un contador propio seria un segundo numero que puede contradecir al registro
 * de auditoria, y en una defensa dos numeros que no coinciden es peor que
 * ninguno.
 *
 * Separa lo que resolvio el proveedor de lo que resolvio el propio sistema,
 * porque de cara al costo son cosas distintas: solo las primeras se pagan. Y
 * cuenta las de respaldo aparte, que son las veces que el proveedor no
 * contesto: ese numero es el que dice si conviene seguir pagandolo.
 */
function ConsumoIa() {
  const t = useT();
  const { mesYAnio } = useFormato();
  const cerrarSiExpiro = useCerrarSiExpiro();
  const [consumo, setConsumo] = useState<ConsumoIaDto | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    api.organizacion
      .consumoIa()
      .then(setConsumo)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(true);
      });
  }, [cerrarSiExpiro]);

  // Con `toLocaleDateString(undefined, …)` el mes salia en el idioma del
  // navegador y no en el de la interfaz: la pantalla en ingles decia
  // «septiembre de 2026».
  const mes = consumo ? mesYAnio(consumo.desde) : '';

  return (
    <div className="tarjeta">
      <header>
        <h2>{t('org.consumoIa', 'Consumo del servicio de IA')}</h2>
        {consumo ? <span className="nota">{mes}</span> : null}
      </header>

      {error ? (
        <div className="vacio">{t('org.consumoError', 'No se pudo leer el consumo.')}</div>
      ) : !consumo ? (
        <div className="vacio">{t('comun.cargando', 'Cargando…')}</div>
      ) : (
        <>
          <Fila
            concepto={t('org.consultasAlProveedor', 'Consultas al proveedor de IA')}
            valor={String(consumo.totalIa)}
            destacado
          />
          <Fila
            concepto={t('org.clasificaciones', 'Clasificación de incidencias')}
            valor={String(consumo.clasificacionesIa)}
          />
          <Fila
            concepto={t('org.asignaciones', 'Asignación de técnico')}
            valor={String(consumo.asignacionesIa)}
          />
          <Fila
            concepto={t('org.guias', 'Guías de reparación')}
            valor={String(consumo.guiasIa)}
          />
          <Fila
            concepto={t('org.resueltasPorElSistema', 'Resueltas por el propio sistema')}
            valor={String(consumo.totalPropio)}
          />
          <Fila
            concepto={t('org.porRespaldo', 'Veces que hubo que usar el respaldo')}
            valor={String(consumo.asignacionesRespaldo)}
            atencion={consumo.asignacionesRespaldo > 0}
          />

          <div style={{ padding: 'var(--esp-4)' }}>
            <p className="sub" style={{ lineHeight: 'var(--interlineado-parrafo)' }}>
              {t('org.consumoNota',
                 'Cada consulta queda asentada en la bitácora con su origen. Las que resuelve el '
                 + 'propio sistema no salen a la red y no tienen costo; las de respaldo son las '
                 + 'veces que el proveedor no respondió.')}
            </p>
          </div>
        </>
      )}
    </div>
  );
}

export function Organizacion() {
  const { dinero: pesos } = useFormato();
  const t = useT();
  const { sesion } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();
  const [plan, setPlan] = useState<SituacionPlanDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.organizacion
      .plan()
      .then(setPlan)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarPlan', 'No se pudo cargar el plan.'));
      });
  }, [cerrarSiExpiro]);

  const org = sesion?.organizacion;
  const excede = plan ? plan.equiposAdministrados > plan.equiposIncluidos : false;

  return (
    <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
      <ConfigNav />
      <div className="principal">
        <Encabezado titulo={t('org.titulo', 'Organización')} contexto={t('org.contexto', 'Datos de la empresa y plan contratado')} />

        <div className="cuerpo">
          {error ? (
            <div className="aviso error" role="alert">
              {error}
            </div>
          ) : null}

          {/*
            El plan es la columna ancha y los datos de la empresa el panel de
            contexto, y no al reves: la razon social y el CUIT son tres
            renglones que no cambian nunca, y el plan es la cuenta que alguien
            entra a mirar.
          */}
          <div className="dos-columnas">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--esp-4)' }}>
              <div className="tarjeta">
                <header>
                  <h2>{t('org.plan', 'Plan contratado')}</h2>
                  {plan ? <span className="nota">{plan.plan.nombre}</span> : null}
                </header>

                {plan ? (
                  <>
                    <Fila concepto={t('org.abonoBase', 'Abono base')} valor={pesos(plan.abonoBase)} />
                    <Fila
                      concepto={t('org.equiposIncluidos', 'Equipos incluidos')}
                      valor={String(plan.equiposIncluidos)}
                    />
                    <Fila
                      concepto={t('org.equiposAdministrados', 'Equipos administrados')}
                      valor={String(plan.equiposAdministrados)}
                      atencion={excede}
                    />
                    <Fila
                      concepto={`${t('org.equiposAdicionales', 'Equipos adicionales')}${
                        plan.equiposAdicionales > 0
                          ? ` (${plan.equiposAdicionales} × ${pesos(
                              plan.plan.precioEquipoAdicional,
                            )})`
                          : ''
                      }`}
                      valor={pesos(plan.costoAdicionales)}
                      atencion={plan.equiposAdicionales > 0}
                    />
                    <Fila
                      concepto={t('org.totalMensual', 'Total mensual estimado')}
                      valor={pesos(plan.totalMensual)}
                      destacado
                    />

                    {plan.alternativa ? (
                      <div style={{ padding: 14 }}>
                        {/*
                          El sistema hace la cuenta y dice el resultado, sea el que
                          sea. Cuando el plan actual sigue siendo el más barato lo
                          dice así, en vez de empujar al plan más caro.
                        */}
                        <div
                          className={plan.alternativa.conviene ? 'aviso atencion' : 'aviso info'}
                        >
                          {/*
                            La frase va entera y no partida en pedazos con el
                            nombre del plan en negrita en el medio: cortada en
                            cinco trozos de JSX no se puede traducir, porque
                            cada idioma los ordena distinto. Lo que se pierde
                            es la negrita del nombre; el aviso ya se destaca
                            solo por su recuadro.
                          */}
                          {plan.alternativa.conviene
                            ? t('org.convieneCambiar',
                                'Con {equipos} equipos te conviene el {plan}: {costo} por mes, '
                                + '{diferencia} menos que ahora.', {
                                  equipos: plan.equiposAdministrados,
                                  plan: plan.alternativa.nombre,
                                  costo: pesos(plan.alternativa.totalMensual),
                                  diferencia: pesos(Math.abs(plan.alternativa.diferencia)),
                                })
                            : t('org.noConvieneCambiar',
                                'Con {equipos} equipos, el {plan} costaría {costo} por mes: '
                                + '{diferencia} más que tu plan actual.', {
                                  equipos: plan.equiposAdministrados,
                                  plan: plan.alternativa.nombre,
                                  costo: pesos(plan.alternativa.totalMensual),
                                  diferencia: pesos(Math.abs(plan.alternativa.diferencia)),
                                })}
                          {!plan.alternativa.conviene
                            && plan.alternativa.equiposDesdeLosQueConviene > 0
                            ? ' ' + t('org.desdeCuantosConviene',
                                'Te conviene cambiar a partir de los {equipos} equipos.',
                                { equipos: plan.alternativa.equiposDesdeLosQueConviene })
                            : null}
                        </div>
                      </div>
                    ) : null}
                  </>
                ) : (
                  <div className="vacio">{t('org.cargandoPlan', 'Cargando el plan…')}</div>
                )}
              </div>

              <ConsumoIa />
            </div>

            <div className="tarjeta">
              <header>
                <h2>{t('org.datos', 'Datos de la organización')}</h2>
              </header>
              <dl className="dl">
                <dt>{t('org.razonSocial', 'Razón social')}</dt>
                <dd>{org?.razonSocial ?? '—'}</dd>
                <dt>{t('org.nombreCorto', 'Nombre corto')}</dt>
                <dd>{org?.nombreCorto ?? '—'}</dd>
                <dt>{t('org.cuit', 'CUIT')}</dt>
                <dd className="mono">{org?.cuit ?? '—'}</dd>
              </dl>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
