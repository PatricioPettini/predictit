import { useCallback, useEffect, useState } from 'react';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type { ReglaDto } from '../api/tipos';
import { Patentes, TIPOS_DE_REGLA } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { EsqueletoTabla, SinDatos } from '../componentes/Estados';
import { PanelCalibracion } from '../componentes/PanelCalibracion';
import { ConfigNav } from './Organizacion';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';

function nombreDeTipo(tipo: string): string {
  return TIPOS_DE_REGLA.find((t) => t.id === tipo)?.nombre ?? tipo;
}

/**
 * Muestra los parámetros del JSON en lenguaje llano.
 *
 * La condición se guarda en JSON porque cada tipo de regla tiene sus propias
 * claves, pero pedirle a quien configura que lea JSON para entender qué hace la
 * regla sería trasladarle un detalle de implementación.
 */
type Traducir = (
  clave: string,
  porDefecto: string,
  datos?: Record<string, string | number>,
) => string;

function describir(t: Traducir, regla: ReglaDto): string {
  let p: Record<string, unknown>;
  try {
    p = JSON.parse(regla.condicion);
  } catch {
    return t('regla.jsonInvalidoDetalle',
             'La condición no es un JSON válido: esta regla no se va a evaluar.');
  }

  const n = (clave: string) => Number(p[clave]);

  switch (regla.tipo) {
    case 'RECURRENCIA_FALLAS':
      return t('regla.describeRecurrencia',
               'Avisa cuando un equipo acumula {minimo} o más incidencias en {dias} días.',
               { minimo: n('minIncidencias'), dias: n('ventanaDias') });

    case 'ACUMULACION_INCIDENCIAS':
      // Dos frases completas y no una armada con un pedazo suelto: el final
      // cambia la oración entera y en otro idioma no va necesariamente al
      // final.
      return p.mismaCategoria
        ? t('regla.describeAcumulacionMismaCategoria',
            'Avisa cuando un equipo acumula {minimo} o más incidencias de la misma categoría en {dias} días.',
            { minimo: n('minIncidencias'), dias: n('ventanaDias') })
        : t('regla.describeAcumulacion',
            'Avisa cuando un equipo acumula {minimo} o más incidencias, de cualquier categoría, en {dias} días.',
            { minimo: n('minIncidencias'), dias: n('ventanaDias') });

    case 'MANTENIMIENTO_VENCIDO':
      return t('regla.describeMantenimiento',
               'Avisa cuando pasaron {dias} días desde el último mantenimiento preventivo.',
               { dias: n('diasSinMantenimiento') });

    case 'ANTIGUEDAD_EQUIPO':
      return t('regla.describeAntiguedad',
               'Avisa cuando el equipo cumple {anios} años desde su compra.',
               { anios: n('aniosUmbral') });

    case 'GARANTIA_POR_VENCER':
      return t('regla.describeGarantia',
               'Avisa cuando faltan {dias} días o menos para que venza la garantía.',
               { dias: n('diasAviso') });

    default:
      return regla.condicion;
  }
}

function Editor({
  regla,
  onGuardado,
  onCancelar,
}: {
  regla: ReglaDto;
  onGuardado: () => void;
  onCancelar: () => void;
}) {
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [nombre, setNombre] = useState(regla.nombre);
  const [condicion, setCondicion] = useState(() => {
    // Se muestra indentado: es más fácil de editar que una línea larga.
    try {
      return JSON.stringify(JSON.parse(regla.condicion), null, 2);
    } catch {
      return regla.condicion;
    }
  });
  const [peso, setPeso] = useState(regla.peso);
  const [activa, setActiva] = useState(regla.activa);
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);

  const claves = TIPOS_DE_REGLA.find((t) => t.id === regla.tipo)?.claves ?? '';

  function guardar() {
    setGuardando(true);
    setError(null);

    api.reglas
      .guardar({ ...regla, nombre, condicion, peso, activa })
      .then(onGuardado)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.guardarRegla', 'No se pudo guardar la regla.'));
      })
      .finally(() => setGuardando(false));
  }

  return (
    <div className="tarjeta">
      <header>
        <h2>{nombreDeTipo(regla.tipo)}</h2>
        <span className="nota mono" style={{ fontSize: 'var(--txt-tabla)' }}>
          {regla.tipo}
        </span>
      </header>

      <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 14 }}>
        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('usuario.nombre', 'Nombre')}</span>
          <input className="campo" value={nombre} onChange={(e) => setNombre(e.target.value)} />
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">Parámetros — claves de este tipo: {claves}</span>
          <textarea
            className="campo"
            value={condicion}
            onChange={(e) => setCondicion(e.target.value)}
            rows={5}
            spellCheck={false}
            style={{
              resize: 'vertical',
              minHeight: 92,
              paddingTop: 8,
              fontFamily: "'IBM Plex Mono', Consolas, monospace",
              fontSize: 'var(--txt-tabla)',
            }}
          />
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">
            {t('regla.pesoAyuda',
              'Peso: {peso} — los pesos no tienen que sumar 100; se normalizan entre '
              + 'las reglas aplicables', { peso })}
          </span>
          <input
            type="range"
            min={0}
            max={100}
            step={5}
            value={peso}
            onChange={(e) => setPeso(Number(e.target.value))}
          />
        </label>

        <label style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <input type="checkbox" checked={activa} onChange={(e) => setActiva(e.target.checked)} />
          <span style={{ fontSize: 'var(--txt-tabla)' }}>{t('reglas.reglaActiva', 'Regla activa')}</span>
        </label>

        {error ? (
          <div className="aviso error" role="alert">
            {error}
          </div>
        ) : null}

        <div style={{ display: 'flex', gap: 8 }}>
          <button type="button" className="btn pri" onClick={guardar} disabled={guardando}>
            {guardando ? t('comun.guardando', 'Guardando…') : t('comun.guardar', 'Guardar')}
          </button>
          <button type="button" className="btn" onClick={onCancelar} disabled={guardando}>
            {t('comun.cancelar', 'Cancelar')}
          </button>
        </div>
      </div>
    </div>
  );
}

export function Reglas() {
  const t = useT();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [reglas, setReglas] = useState<ReglaDto[] | null>(null);
  const [editando, setEditando] = useState<ReglaDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const cargar = useCallback(() => {
    setError(null);
    api.reglas
      .listar()
      .then(setReglas)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarReglas', 'No se pudieron cargar las reglas.'));
      });
  }, [cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  const activas = reglas?.filter((r) => r.activa) ?? [];
  const pesoTotal = activas.reduce((s, r) => s + r.peso, 0);

  const contexto = !reglas
    ? t('comun.cargando', 'Cargando…')
    : t('regla.contexto', '{reglas} reglas · {activas} activas · peso total {peso}', {
        reglas: reglas.length,
        activas: activas.length,
        peso: pesoTotal,
      });

  return (
    <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
      <ConfigNav />
      <div className="principal">
        <Encabezado titulo={t('regla.titulo', 'Reglas predictivas')} contexto={contexto} />

        <div className="cuerpo">
          {error ? (
            <div className="aviso error" role="alert" style={{ marginBottom: 12 }}>
              {error}
            </div>
          ) : null}

          <div
            style={{
              display: 'grid',
              gridTemplateColumns: editando ? 'minmax(0, 1fr) 400px' : '1fr',
              gap: 16,
            }}
          >
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              <div className="tarjeta">
                <table>
                  <thead>
                    <tr>
                      <th style={{ width: 195 }}>{t('comun.regla', 'Regla')}</th>
                      <th>{t('regla.queHace', 'Qué hace')}</th>
                      <th style={{ width: 130 }}>{t('regla.pesoRelativo', 'Peso relativo')}</th>
                      <th style={{ width: 90 }}>{t('comun.estado', 'Estado')}</th>
                      <th style={{ width: 80 }} className="derecha">
                        {puede(Patentes.reglaGestionar) ? t('comun.editar', 'Editar') : ''}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {reglas?.map((r) => (
                      <tr key={r.id}>
                        <td>
                          <span style={{ color: 'var(--tx)' }}>{r.nombre}</span>
                          <div className="sub">{nombreDeTipo(r.tipo)}</div>
                        </td>
                        <td className="sub">{describir(t, r)}</td>
                        <td>
                          {/*
                            La barra muestra el peso relativo al total de las
                            activas, que es lo que efectivamente pondera el
                            score. El número solo no dice cuánto pesa.
                          */}
                          <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
                            <div
                              style={{
                                flex: 1,
                                height: 5,
                                borderRadius: 3,
                                background: 'var(--linea-suave)',
                                overflow: 'hidden',
                              }}
                            >
                              <div
                                style={{
                                  width: `${pesoTotal > 0 && r.activa ? (r.peso / pesoTotal) * 100 : 0}%`,
                                  height: '100%',
                                  background: r.activa ? 'var(--acento)' : 'var(--tx3)',
                                }}
                              />
                            </div>
                            <span className="mono" style={{ fontSize: 'var(--txt-tabla)' }}>
                              {r.activa && pesoTotal > 0
                                ? `${Math.round((r.peso / pesoTotal) * 100)} %`
                                : '—'}
                            </span>
                          </div>
                        </td>
                        <td>
                          <span className={r.activa ? 'chip bajo' : 'chip'}>
                            {r.activa ? t('reglas.activa', 'Activa') : t('reglas.inactiva', 'Inactiva')}
                          </span>
                        </td>
                        <td className="derecha">
                          {puede(Patentes.reglaGestionar) ? (
                            <button
                              type="button"
                              className="btn plano"
                              style={{ padding: 0, height: 'auto' }}
                              onClick={() => setEditando(r)}
                            >
                              {t('comun.editar', 'Editar')}
                            </button>
                          ) : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                {!reglas ? <EsqueletoTabla columnas={5} filas={5} /> : null}
                {reglas?.length === 0 ? (
                  <SinDatos
                    titulo={t('vacio.sinReglas', 'No hay reglas configuradas')}
                    detalle={t('vacio.sinReglasAyuda', 'Sin reglas el análisis predictivo no tiene con qué evaluar el parque: todos los equipos quedan en riesgo cero.')}
                  />
                ) : null}
              </div>

              <PanelCalibracion />

              <div className="tarjeta">
                <header>
                  <h2>{t('regla.comoCalibrar', 'Cómo calibrarlas')}</h2>
                </header>
                <div style={{ padding: 14 }} className="sub">
                  {t('regla.comoCalibrarTexto', 'La señal de que una regla está mal configurada es que sus alertas siempre se descartan. Por eso «atender» y «descartar» son acciones distintas en la pantalla de análisis: la proporción entre ambas es lo que permite evaluar la calibración. Si una regla genera demasiadas alertas, conviene subir su umbral o bajar su peso; si un equipo falló sin haber generado alerta, bajar el umbral de la regla que debió detectarlo.')}
                </div>
              </div>
            </div>

            {editando ? (
              <Editor
                regla={editando}
                onGuardado={() => {
                  setEditando(null);
                  cargar();
                }}
                onCancelar={() => setEditando(null)}
              />
            ) : null}
          </div>
        </div>
      </div>
    </div>
  );
}
