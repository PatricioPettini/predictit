import { useEffect, useState } from 'react';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type { CatalogosIncidenciaDto, CatalogosEquipoDto } from '../api/tipos';
import { IconoAnalisis } from '../componentes/Iconos';
import { Encabezado } from '../componentes/Layout';
import { useCerrarSiExpiro } from '../sesion/SesionContext';

function haceDias(dias: number): string {
  const d = new Date();
  d.setDate(d.getDate() - dias);
  return d.toISOString().slice(0, 10);
}

type Reporte = {
  clave: string;
  claveTitulo: string;
  titulo: string;
  claveQueResponde: string;
  queResponde: string;
  conFechas: boolean;
  conEstadoEquipo?: boolean;
  conEstadoIncidencia?: boolean;
  conTecnico?: boolean;
};

/**
 * Los tres reportes del sistema. Cada uno declara qué filtros acepta, así la
 * pantalla no ofrece un filtro por técnico en el reporte del parque —donde no
 * significa nada— ni un rango de fechas donde no aplica.
 */
const REPORTES: Reporte[] = [
  {
    clave: 'parque',
    claveTitulo: 'reporte.parque',
    titulo: 'Parque informático',
    claveQueResponde: 'reporte.parqueQueResponde',
    queResponde: 'Qué equipos hay, dónde están, quién responde por cada uno y cuál está en riesgo.',
    conFechas: false,
    conEstadoEquipo: true,
  },
  {
    clave: 'incidencias',
    claveTitulo: 'nav.incidencias',
    titulo: 'Incidencias',
    claveQueResponde: 'reporte.incidenciasQueResponde',
    queResponde:
      'Qué se reportó en el período, cómo terminó y cuánto tardó. Trae el tiempo medio de resolución.',
    conFechas: true,
    conEstadoIncidencia: true,
    conTecnico: true,
  },
  {
    clave: 'mantenimientos',
    claveTitulo: 'nav.mantenimientos',
    titulo: 'Mantenimientos',
    claveQueResponde: 'reporte.mantenimientosQueResponde',
    queResponde: 'Qué se le hizo al parque en el período, a cuántos equipos y a qué costo.',
    conFechas: true,
    conTecnico: true,
  },
];

/**
 * Reportes exportables (RF-14).
 *
 * El PDF se genera en el servidor y se descarga: no se guarda en ninguna parte.
 * Un reporte guardado envejece, y alguien termina abriendo el de la semana
 * pasada creyendo que es el de hoy. Por el mismo motivo el encabezado del PDF
 * lleva la fecha de emisión y los filtros aplicados.
 */
export function Reportes() {
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [catEquipo, setCatEquipo] = useState<CatalogosEquipoDto | null>(null);
  const [catInc, setCatInc] = useState<CatalogosIncidenciaDto | null>(null);
  const [desde, setDesde] = useState(() => haceDias(90));
  const [hasta, setHasta] = useState(() => new Date().toISOString().slice(0, 10));
  const [estadoEquipo, setEstadoEquipo] = useState('');
  const [estadoInc, setEstadoInc] = useState('');
  const [tecnico, setTecnico] = useState('');
  const [bajando, setBajando] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.equipos.catalogos().then(setCatEquipo).catch(cerrarSiExpiro);
    api.incidencias.catalogos().then(setCatInc).catch(() => setCatInc(null));
  }, [cerrarSiExpiro]);

  function descargar(r: Reporte) {
    setBajando(r.clave);
    setError(null);

    api.reportes
      .descargar(r.clave, {
        desde: r.conFechas ? desde : undefined,
        hasta: r.conFechas ? hasta : undefined,
        estado: r.conEstadoEquipo ? estadoEquipo : r.conEstadoIncidencia ? estadoInc : undefined,
        tecnico: r.conTecnico ? tecnico : undefined,
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.generarReporte', 'No se pudo generar el reporte.'));
      })
      .finally(() => setBajando(null));
  }

  return (
    <>
      <Encabezado titulo={t('nav.reportes', 'Reportes')} contexto={t('reportes.contexto', 'Tres reportes en PDF, con los filtros aplicados impresos')} />

      <div className="cuerpo pagina-reportes">
        <div className="aviso info" style={{ marginBottom: 12 }}>
          {t('reporte.pdfNota', 'El PDF se arma en el momento y no se guarda en el servidor. Lleva impresa la fecha de emisión, quién lo generó y los filtros aplicados: un reporte que circula fuera del sistema tiene que poder explicarse solo.')}
        </div>

        <div className="filtros">
          <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span className="sub">{t('bitacora.desde', 'Desde')}</span>
            <input
              type="date"
              className="campo"
              value={desde}
              max={hasta}
              onChange={(e) => setDesde(e.target.value)}
            />
          </label>

          <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span className="sub">{t('bitacora.hasta', 'Hasta')}</span>
            <input
              type="date"
              className="campo"
              value={hasta}
              min={desde}
              onChange={(e) => setHasta(e.target.value)}
            />
          </label>

          <select
            className="campo"
            value={estadoEquipo}
            aria-label={t('reporte.estadoEquipo', 'Estado del equipo')}
            onChange={(e) => setEstadoEquipo(e.target.value)}
          >
            <option value="">{t('reporte.equiposTodos', 'Equipos: todos los estados')}</option>
            {catEquipo?.estados.map((s) => (
              <option key={s.id} value={s.id}>
                {t('reporte.equiposEstado', 'Equipos: {estado}', { estado: s.nombre })}
              </option>
            ))}
          </select>

          <select
            className="campo"
            value={estadoInc}
            aria-label={t('reporte.estadoIncidencia', 'Estado de la incidencia')}
            onChange={(e) => setEstadoInc(e.target.value)}
          >
            <option value="">{t('reporte.incidenciasTodos', 'Incidencias: todos los estados')}</option>
            {catInc?.estados.map((s) => (
              <option key={s.id} value={s.id}>
                {t('reporte.incidenciasEstado', 'Incidencias: {estado}', { estado: s.nombre })}
              </option>
            ))}
          </select>

          <select
            className="campo"
            value={tecnico}
            aria-label={t('incidencia.tecnico', 'Técnico')}
            onChange={(e) => setTecnico(e.target.value)}
          >
            <option value="">{t('reporte.todosLosTecnicos', 'Todos los técnicos')}</option>
            {catInc?.tecnicos.map((t) => (
              <option key={t.id} value={t.id}>
                {t.nombreCompleto}
              </option>
            ))}
          </select>
        </div>

        {error ? (
          <div className="aviso error" role="alert">
            {error}
          </div>
        ) : null}

        <div className="reportes-grilla">
          {REPORTES.map((r) => (
            <div key={r.clave} className="tarjeta reporte-opcion">
              <header>
                <span className="reporte-icono" aria-hidden="true"><IconoAnalisis /></span><h2>{t(r.claveTitulo, r.titulo)}</h2>
              </header>

              <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 12 }}>
                <p style={{ margin: 0, fontSize: 'var(--txt-cuerpo)', color: 'var(--tx2)' }}>
                  {t(r.claveQueResponde, r.queResponde)}
                </p>

                <div className="sub">
                  {t('reporte.filtraPor', 'Filtra por')}{' '}
                  {[
                    r.conFechas ? t('reporte.filtroPeriodo', 'período') : null,
                    r.conEstadoEquipo
                      ? t('reporte.filtroEstadoEquipo', 'estado del equipo') : null,
                    r.conEstadoIncidencia
                      ? t('reporte.filtroEstadoIncidencia', 'estado de la incidencia') : null,
                    r.conTecnico ? t('reporte.filtroTecnico', 'técnico') : null,
                  ]
                    .filter(Boolean)
                    .join(', ')}
                  .
                </div>

                <button
                  type="button"
                  className="btn pri"
                  disabled={bajando !== null}
                  onClick={() => descargar(r)}
                >
                  {bajando === r.clave ? t('reporte.generando', 'Generando…') : t('reporte.descargarPdf', 'Descargar PDF')}
                </button>
              </div>
            </div>
          ))}
        </div>
      </div>
    </>
  );
}
