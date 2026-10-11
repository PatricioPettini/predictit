import { useEffect, useMemo, useState } from 'react';
import { usePlural } from '../idioma/plural';
import { useT } from '../idioma/IdiomaContext';
import { Link } from 'react-router-dom';
import { api, ErrorApi } from '../api/cliente';
import type { EquipoListaDto } from '../api/tipos';
import { Patentes } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { EsqueletoTabla, SinDatos } from '../componentes/Estados';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';
import { EstadoEquipo } from '../componentes/Semantica';

/**
 * Mis equipos — módulo del cliente.
 *
 * Los equipos de los que el usuario es responsable, no el inventario. La
 * diferencia no es de presentación: el servidor fuerza el filtro por el usuario
 * de la sesión, así que esta pantalla no necesita la patente de inventario y
 * pedirla a mano no devolvería nada más.
 */
export function MisEquipos() {
  const p = usePlural();
  const t = useT();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [equipos, setEquipos] = useState<EquipoListaDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.equipos
      .mios()
      .then((p) => setEquipos(p.items))
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarMisEquipos', 'No se pudieron cargar tus equipos.'));
      });
  }, [cerrarSiExpiro]);

  const contexto = useMemo(() => {
    if (!equipos) return t('comun.cargando', 'Cargando…');
    if (equipos.length === 0) return t('activo.sinEquiposAsignados', 'No tenés equipos asignados');

    const conProblema = equipos.filter((e) => !e.estadoOperativo).length;
    const partes = [
      equipos.length === 1 ? '1 equipo a tu nombre' : `${equipos.length} equipos a tu nombre`,
    ];
    if (conProblema > 0) {
      partes.push(
        p(conProblema, 'activo.fueraDeServicio',
        '1 fuera de servicio', '{n} fuera de servicio'),
      );
    }
    return partes.join(' · ');
  }, [equipos]);

  return (
    <>
      <Encabezado
        titulo={t('nav.misEquipos', 'Mis equipos')}
        contexto={contexto}
        acciones={
          puede(Patentes.incidenciaRegistrar) ? (
            <Link to="/reportar" className="btn pri">
              {t('activo.reportarProblema', 'Reportar un problema')}
            </Link>
          ) : null
        }
      />

      <div className="cuerpo">
        {error ? (
          <div className="aviso error" role="alert" style={{ marginBottom: 12 }}>
            {error}
          </div>
        ) : null}

        <div className="tarjeta">
          <table>
            <thead>
              <tr>
                <th style={{ width: 130, whiteSpace: 'nowrap' }}>{t('activo.codigo', 'Código')}</th>
                <th style={{ width: 150 }}>{t('comun.tipo', 'Tipo')}</th>
                <th>{t('activo.marcaModelo', 'Marca y modelo')}</th>
                <th style={{ width: 160 }}>{t('activo.ubicacion', 'Ubicación')}</th>
                <th style={{ width: 160 }}>{t('comun.estado', 'Estado')}</th>
              </tr>
            </thead>
            <tbody>
              {equipos?.map((e) => (
                <tr key={e.id}>
                  <td className="mono" style={{ whiteSpace: 'nowrap' }}>
                    {e.codigo}
                  </td>
                  <td>{e.tipo}</td>
                  <td>
                    <span style={{ color: 'var(--tx)' }}>{e.marcaModelo}</span>
                    {e.numeroSerie ? <div className="sub">S/N {e.numeroSerie}</div> : null}
                  </td>
                  <td>{e.ubicacion ?? '—'}</td>
                  <td>
                    <EstadoEquipo estado={e.estado} operativo={e.estadoOperativo} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {!equipos ? <EsqueletoTabla columnas={4} filas={4} /> : null}
          {equipos?.length === 0 ? (
            <SinDatos
              titulo={t('vacio.sinEquiposPropios', 'No hay equipos a tu nombre')}
              detalle={t('vacio.sinEquiposPropiosAyuda', 'Si usás uno que debería figurar acá, avisale al responsable técnico para que te lo asigne.')}
            />
          ) : null}
        </div>

        <p className="sub" style={{ marginTop: 10 }}>
          {t('activo.misEquiposNota', 'Acá aparecen sólo los equipos de los que figurás como responsable. Para reportar una falla de cualquiera de ellos, usá «Reportar un problema».')}
        </p>
      </div>
    </>
  );
}
