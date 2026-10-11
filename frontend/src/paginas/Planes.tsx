import { useCallback, useEffect, useState } from 'react';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type {
  CatalogosEquipoDto,
  CatalogosMantenimientoDto,
  EquipoListaDto,
  PlanMantenimientoDto,
} from '../api/tipos';
import { Patentes } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { EsqueletoTabla, SinDatos } from '../componentes/Estados';
import { ConfigNav } from './Organizacion';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';

/** Lo que se está editando. `null` es «ninguno»; sin `id`, uno nuevo. */
type EnEdicion = {
  id?: string;
  idEquipo: string;
  idTipoEquipo: string;
  idTipoMantenimiento: string;
  cadaDias: string;
  activo: boolean;
  descripcion: string;
};

const NUEVO: EnEdicion = {
  idEquipo: '',
  idTipoEquipo: '',
  idTipoMantenimiento: '',
  cadaDias: '180',
  activo: true,
  descripcion: '',
};

/**
 * Cada cuánto, dicho en la unidad en que la gente lo piensa.
 *
 * El plan guarda días porque es lo único que se puede sumar a una fecha sin
 * ambigüedad, pero nadie dice «cada 180 días»: dice «semestral». Se muestran
 * las dos cosas, porque el número es el que define la cuenta.
 */
function cadaCuanto(t: (c: string, d: string, x?: Record<string, string>) => string, dias: number) {
  const nombres: Record<number, [string, string]> = {
    7: ['plan.semanal', 'semanal'],
    15: ['plan.quincenal', 'quincenal'],
    30: ['plan.mensual', 'mensual'],
    90: ['plan.trimestral', 'trimestral'],
    180: ['plan.semestral', 'semestral'],
    365: ['plan.anual', 'anual'],
  };

  const nombre = nombres[dias];
  const enDias = t('plan.cadaDias', 'cada {dias} días', { dias: String(dias) });
  return nombre ? `${t(nombre[0], nombre[1])} · ${enDias}` : enDias;
}

function Formulario({
  valor,
  catalogosEquipo,
  catalogosMant,
  equipos,
  onListo,
  onCancelar,
}: {
  valor: EnEdicion;
  catalogosEquipo: CatalogosEquipoDto | null;
  catalogosMant: CatalogosMantenimientoDto | null;
  equipos: EquipoListaDto[];
  onListo: () => void;
  onCancelar: () => void;
}) {
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [datos, setDatos] = useState(valor);
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  // El formulario se reusa entre filas: sin esto, elegir otro plan del listado
  // dejaría los campos del anterior.
  useEffect(() => setDatos(valor), [valor]);

  const preventivos = (catalogosMant?.tipos ?? []).filter((x) => x.esPreventivo);

  // Un plan alcanza a un equipo o a un tipo de equipo, nunca a los dos: el
  // servidor lo rechaza y la pantalla lo dice antes de dejar intentarlo.
  const porTipo = datos.idTipoEquipo !== '';
  const porEquipo = datos.idEquipo !== '';

  function enviar(e: React.FormEvent) {
    e.preventDefault();
    setEnviando(true);
    setError(null);

    const entrada = {
      idEquipo: datos.idEquipo || undefined,
      idTipoEquipo: datos.idTipoEquipo || undefined,
      idTipoMantenimiento: datos.idTipoMantenimiento,
      cadaDias: Number(datos.cadaDias),
      activo: datos.activo,
      descripcion: datos.descripcion || undefined,
    };

    const operacion = datos.id
      ? api.planes.actualizar(datos.id, entrada)
      : api.planes.crear(entrada);

    operacion
      .then(onListo)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.guardar', 'No se pudo guardar.'));
      })
      .finally(() => setEnviando(false));
  }

  return (
    <form onSubmit={enviar} className="tarjeta">
      <header>
        <h2>{datos.id ? t('plan.editar', 'Editar plan') : t('plan.nuevo', 'Nuevo plan')}</h2>
      </header>

      <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 14 }}>
        <div>
          <div className="sub" style={{ marginBottom: 5 }}>
            {t('plan.alcance', 'A qué alcanza')}
          </div>
          <p className="sub" style={{ marginBottom: 8 }}>
            {t(
              'plan.alcanceAyuda',
              'Un plan por tipo cubre todo el parque de esa clase, incluidos los equipos que se den de alta después. Uno por equipo es para el caso puntual.',
            )}
          </p>

          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <label style={{ flex: '1 1 220px', display: 'flex', flexDirection: 'column', gap: 5 }}>
              <span className="sub">{t('plan.tipoDeEquipo', 'Tipo de equipo')}</span>
              <select
                className="campo"
                value={datos.idTipoEquipo}
                disabled={porEquipo}
                onChange={(e) => setDatos({ ...datos, idTipoEquipo: e.target.value })}
              >
                <option value="">{t('comun.elegir', 'Elegir…')}</option>
                {catalogosEquipo?.tipos.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.nombre}
                  </option>
                ))}
              </select>
            </label>

            <label style={{ flex: '1 1 220px', display: 'flex', flexDirection: 'column', gap: 5 }}>
              <span className="sub">{t('plan.unEquipo', 'Un equipo puntual')}</span>
              <select
                className="campo"
                value={datos.idEquipo}
                disabled={porTipo}
                onChange={(e) => setDatos({ ...datos, idEquipo: e.target.value })}
              >
                <option value="">{t('comun.elegir', 'Elegir…')}</option>
                {equipos.map((eq) => (
                  <option key={eq.id} value={eq.id}>
                    {eq.codigo} — {eq.tipo}
                  </option>
                ))}
              </select>
            </label>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <label style={{ flex: '1 1 220px', display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span className="sub">{t('plan.queTrabajo', 'Qué trabajo')}</span>
            <select
              className="campo"
              value={datos.idTipoMantenimiento}
              onChange={(e) => setDatos({ ...datos, idTipoMantenimiento: e.target.value })}
              required
            >
              <option value="">{t('comun.elegir', 'Elegir…')}</option>
              {preventivos.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.nombre}
                </option>
              ))}
            </select>
          </label>

          <label style={{ flex: '0 1 160px', display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span className="sub">{t('plan.cadaCuanto', 'Cada cuántos días')}</span>
            <input
              type="number"
              className="campo"
              min={7}
              max={3650}
              step={1}
              value={datos.cadaDias}
              onChange={(e) => setDatos({ ...datos, cadaDias: e.target.value })}
              required
            />
          </label>

          <label
            style={{ flex: '0 1 150px', display: 'flex', alignItems: 'center', gap: 8, paddingTop: 20 }}
          >
            <input
              type="checkbox"
              checked={datos.activo}
              onChange={(e) => setDatos({ ...datos, activo: e.target.checked })}
            />
            <span>{t('comun.activo', 'Activo')}</span>
          </label>
        </div>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('plan.descripcion', 'Descripción (opcional)')}</span>
          <input
            className="campo"
            value={datos.descripcion}
            onChange={(e) => setDatos({ ...datos, descripcion: e.target.value })}
            placeholder={t('plan.descripcionEjemplo', 'Limpieza interna y revisión de ventilación')}
          />
        </label>

        {error ? (
          <div className="aviso error" role="alert">
            {error}
          </div>
        ) : null}

        <div style={{ display: 'flex', gap: 8 }}>
          <button type="submit" className="btn pri" disabled={enviando}>
            {enviando ? t('comun.guardando', 'Guardando…') : t('comun.guardar', 'Guardar')}
          </button>
          <button type="button" className="btn" onClick={onCancelar} disabled={enviando}>
            {t('comun.cancelar', 'Cancelar')}
          </button>
        </div>
      </div>
    </form>
  );
}

export function Planes() {
  const t = useT();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [lista, setLista] = useState<PlanMantenimientoDto[] | null>(null);
  const [catalogosEquipo, setCatalogosEquipo] = useState<CatalogosEquipoDto | null>(null);
  const [catalogosMant, setCatalogosMant] = useState<CatalogosMantenimientoDto | null>(null);
  const [equipos, setEquipos] = useState<EquipoListaDto[]>([]);
  const [editando, setEditando] = useState<EnEdicion | null>(null);
  const [error, setError] = useState<string | null>(null);

  const planificar = puede(Patentes.mantenimientoPlanificar);

  const cargar = useCallback(() => {
    setError(null);
    api.planes
      .listar()
      .then(setLista)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(
          ex instanceof ErrorApi ? ex.message : t('error.cargarListado', 'No se pudo cargar el listado.'),
        );
      });
  }, [cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  useEffect(() => {
    api.equipos.catalogos().then(setCatalogosEquipo).catch(cerrarSiExpiro);
    api.mantenimientos.catalogos().then(setCatalogosMant).catch(cerrarSiExpiro);
    api.equipos
      .buscar({ porPagina: 200 })
      .then((p) => setEquipos(p.items))
      .catch(cerrarSiExpiro);
  }, [cerrarSiExpiro]);

  const activos = (lista ?? []).filter((p) => p.activo);
  const contexto =
    lista === null
      ? t('comun.cargando', 'Cargando…')
      : t('plan.contexto', '{planes} plan(es) · {activos} activo(s) · {equipos} equipo(s) cubierto(s)', {
          planes: String(lista.length),
          activos: String(activos.length),
          equipos: String(activos.reduce((s, p) => s + p.equiposAlcanzados, 0)),
        });

  return (
    <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
      <ConfigNav />
      <div className="principal">
        <Encabezado
          titulo={t('plan.titulo', 'Planes de mantenimiento')}
          contexto={contexto}
          acciones={
            planificar && !editando ? (
              <button type="button" className="btn pri" onClick={() => setEditando(NUEVO)}>
                {t('plan.nuevo', 'Nuevo plan')}
              </button>
            ) : null
          }
        />

        <div className="cuerpo">
          {error ? (
            <div className="aviso error" role="alert" style={{ marginBottom: 12 }}>
              {error}
            </div>
          ) : null}

          <div
            style={{
              display: 'grid',
              gridTemplateColumns: editando ? 'minmax(0, 1fr) 460px' : '1fr',
              gap: 16,
              alignItems: 'start',
            }}
          >
            <div className="tarjeta">
              <table>
                <thead>
                  <tr>
                    <th>{t('plan.alcance', 'A qué alcanza')}</th>
                    <th style={{ width: 180 }}>{t('plan.queTrabajo', 'Qué trabajo')}</th>
                    <th style={{ width: 190 }}>{t('plan.frecuencia', 'Frecuencia')}</th>
                    <th style={{ width: 90 }} className="derecha">
                      {t('plan.equipos', 'Equipos')}
                    </th>
                    <th style={{ width: 100 }}>{t('comun.estado', 'Estado')}</th>
                    {planificar ? <th style={{ width: 90 }} /> : null}
                  </tr>
                </thead>
                <tbody>
                  {(lista ?? []).map((p) => (
                    <tr key={p.id}>
                      <td>
                        {p.alcance}
                        {p.descripcion ? <div className="sub">{p.descripcion}</div> : null}
                      </td>
                      <td>{p.tipoMantenimiento}</td>
                      <td className="sub">{cadaCuanto(t, p.cadaDias)}</td>
                      <td className="derecha mono">
                        {/*
                          Un plan que no alcanza a ningún equipo está bien
                          formado y no sirve para nada: se marca, porque lo que
                          se ve es un plan cargado y la agenda vacía.
                        */}
                        {p.equiposAlcanzados === 0 && p.activo ? (
                          <span style={{ color: 'var(--alto-barra)' }}>0</span>
                        ) : (
                          p.equiposAlcanzados
                        )}
                      </td>
                      <td>
                        <span className="estado">
                          <span
                            className="punto"
                            style={{
                              background: p.activo ? 'var(--est-resuelta)' : 'var(--est-anulada)',
                            }}
                            aria-hidden="true"
                          />
                          {p.activo ? t('comun.activo', 'Activo') : t('comun.inactivo', 'Inactivo')}
                        </span>
                      </td>
                      {planificar ? (
                        <td className="derecha">
                          <button
                            type="button"
                            className="btn"
                            onClick={() =>
                              setEditando({
                                id: p.id,
                                idEquipo: p.idEquipo ?? '',
                                idTipoEquipo: p.idTipoEquipo ?? '',
                                idTipoMantenimiento: p.idTipoMantenimiento,
                                cadaDias: String(p.cadaDias),
                                activo: p.activo,
                                descripcion: p.descripcion ?? '',
                              })
                            }
                          >
                            {t('comun.editar', 'Editar')}
                          </button>
                        </td>
                      ) : null}
                    </tr>
                  ))}
                </tbody>
              </table>

              {lista === null ? <EsqueletoTabla columnas={planificar ? 6 : 5} /> : null}

              {lista !== null && lista.length === 0 ? (
                <SinDatos
                  titulo={t('plan.sinPlanes', 'Todavía no hay planes')}
                  detalle={t(
                    'plan.sinPlanesDetalle',
                    'Un plan dice cada cuánto le toca el preventivo a un equipo o a todo un tipo de equipo. Sin plan, el sistema estima con un umbral general.',
                  )}
                />
              ) : null}
            </div>

            {editando ? (
              <Formulario
                valor={editando}
                catalogosEquipo={catalogosEquipo}
                catalogosMant={catalogosMant}
                equipos={equipos}
                onListo={() => {
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
