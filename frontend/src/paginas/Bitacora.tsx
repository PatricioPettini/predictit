import { useCallback, useEffect, useMemo, useState } from 'react';
import { usePlural } from '../idioma/plural';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type {
  CatalogoItemDto,
  EntradaBitacoraDto,
  UsuarioListaDto,
} from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { EsqueletoTabla, SinDatos } from '../componentes/Estados';
import { IconoBuscar } from '../componentes/Iconos';
import { ConfigNav } from './Organizacion';
import { useCerrarSiExpiro } from '../sesion/SesionContext';

function claseCriticidad(c: string): string {
  if (c === 'CRITICO' || c === 'ERROR') return 'chip alto';
  if (c === 'ADVERTENCIA') return 'chip medio';
  return 'chip';
}

function haceDias(dias: number): string {
  const d = new Date();
  d.setDate(d.getDate() - dias);
  return d.toISOString().slice(0, 10);
}

/**
 * Bitácora (Req. Arq. 002).
 *
 * Sólo lectura, y no porque falte implementar la escritura: la tabla tiene un
 * disparador que rechaza cualquier UPDATE o DELETE. No hay forma de alterarla
 * ni conectándose directo a la base.
 */
export function Bitacora() {
  const p = usePlural();
  const { fechaHoraSegundos: fechaHora } = useFormato();
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [entradas, setEntradas] = useState<EntradaBitacoraDto[] | null>(null);
  const [tipos, setTipos] = useState<CatalogoItemDto[]>([]);
  const [usuarios, setUsuarios] = useState<UsuarioListaDto[]>([]);
  const [usuario, setUsuario] = useState('');
  const [desde, setDesde] = useState(() => haceDias(30));
  const [hasta, setHasta] = useState(() => new Date().toISOString().slice(0, 10));
  const [tipo, setTipo] = useState('');
  const [texto, setTexto] = useState('');
  const [soloRelevantes, setSoloRelevantes] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);

  const cargar = useCallback(() => {
    setCargando(true);
    setError(null);
    api.bitacora
      .consultar({
        desde,
        hasta,
        tipo: tipo || undefined,
        usuario: usuario || undefined,
      })
      .then(setEntradas)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarBitacora', 'No se pudo cargar la bitácora.'));
      })
      .finally(() => setCargando(false));
  }, [desde, hasta, tipo, usuario, cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  useEffect(() => {
    api.bitacora.tipos().then(setTipos).catch(cerrarSiExpiro);
  }, [cerrarSiExpiro]);

  // La lista de usuarios puede fallar por permisos —ver la bitácora y gestionar
  // usuarios son patentes distintas—. Si falla, el filtro por usuario no
  // aparece y el resto de la pantalla sigue andando.
  useEffect(() => {
    api.usuarios
      .listar()
      .then(setUsuarios)
      .catch(() => setUsuarios([]));
  }, []);

  // El texto filtra en memoria: el rango de fechas ya acotó el conjunto, y
  // volver al servidor por cada tecla no aportaría nada.
  const visibles = useMemo(() => {
    if (!entradas) return [];
    const t = texto.trim().toLowerCase();
    return entradas.filter((e) => {
      if (soloRelevantes && e.criticidad === 'INFO') return false;
      if (!t) return true;
      return (
        e.descripcion.toLowerCase().includes(t) ||
        (e.usuario ?? '').toLowerCase().includes(t) ||
        e.tipoEvento.toLowerCase().includes(t)
      );
    });
  }, [entradas, texto, soloRelevantes]);

  const contexto = useMemo(() => {
    if (!entradas) return t('comun.cargando', 'Cargando…');
    const conAtencion = entradas.filter((e) => e.criticidad !== 'INFO').length;
    const partes = [
      entradas.length === 1 ? '1 evento' : `${entradas.length} eventos`,
      `del ${desde.split('-').reverse().join('/')} al ${hasta.split('-').reverse().join('/')}`,
    ];
    if (conAtencion > 0) {
      partes.push(
        p(conAtencion, 'bitacora.requiereAtencion',
        '1 requiere atención', '{n} requieren atención'),
      );
    }
    return partes.join(' · ');
  }, [entradas, desde, hasta]);

  return (
    <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
      <ConfigNav />
      <div className="principal">
        <Encabezado titulo={t('bitacora.titulo', 'Bitácora')} contexto={contexto} />

        <div className="cuerpo">
          <div className="aviso info" style={{ marginBottom: 12 }}>
            {t('bitacora.inmutableNota', 'La bitácora es de sólo lectura y no puede alterarse. No es una restricción de esta pantalla: la tabla tiene un disparador que rechaza cualquier modificación o borrado, incluso conectándose directamente a la base de datos.')}
          </div>

          <div className="filtros">
            <div className="campo buscador">
              <IconoBuscar />
              <input
                value={texto}
                onChange={(e) => setTexto(e.target.value)}
                placeholder={t('bitacora.buscarPlaceholder', 'Descripción, usuario o evento…')}
                aria-label={t('bitacora.buscar', 'Buscar en la bitácora')}
              />
            </div>

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
              value={tipo}
              onChange={(e) => setTipo(e.target.value)}
              aria-label={t('bitacora.tipoEvento', 'Tipo de evento')}
            >
              <option value="">{t('bitacora.todosEventos', 'Todos los eventos')}</option>
              {tipos.map((t) => (
                <option key={t.id} value={t.nombre}>
                  {t.nombre}
                </option>
              ))}
            </select>

            {usuarios.length > 0 ? (
              <select
                className="campo"
                value={usuario}
                onChange={(e) => setUsuario(e.target.value)}
                aria-label={t('login.usuario', 'Usuario')}
              >
                <option value="">{t('bitacora.todosUsuarios', 'Todos los usuarios')}</option>
                {usuarios.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.nombreCompleto}
                  </option>
                ))}
              </select>
            ) : null}

            <button
              type="button"
              className={soloRelevantes ? 'btn pri' : 'btn'}
              onClick={() => setSoloRelevantes((v) => !v)}
            >
              {t('bitacora.soloRelevantes', 'Sólo avisos y errores')}
            </button>
          </div>

          {error ? (
            <div className="aviso error" role="alert">
              {error}
            </div>
          ) : null}

          <div className="tarjeta">
            <table>
              <thead>
                <tr>
                  <th style={{ width: 145, whiteSpace: 'nowrap' }}>{t('bitacora.fechaHora', 'Fecha y hora')}</th>
                  <th style={{ width: 185 }}>{t('bitacora.evento', 'Evento')}</th>
                  <th style={{ width: 110 }}>{t('activo.criticidad', 'Criticidad')}</th>
                  <th style={{ width: 145 }}>{t('login.usuario', 'Usuario')}</th>
                  <th>{t('incidencia.descripcion', 'Descripción')}</th>
                  <th style={{ width: 110 }}>{t('bitacora.origen', 'Origen')}</th>
                </tr>
              </thead>
              <tbody>
                {visibles.map((e) => (
                  <tr key={e.id}>
                    <td className="sub" style={{ whiteSpace: 'nowrap' }}>
                      {fechaHora(e.fecha)}
                    </td>
                    <td>{e.tipoEvento}</td>
                    <td>
                      <span className={claseCriticidad(e.criticidad)}>
                        {e.criticidad === 'INFO'
                          ? t('bitacora.informativo', 'Informativo')
                          : e.criticidad === 'ADVERTENCIA'
                            ? t('bitacora.advertencia', 'Advertencia')
                            : e.criticidad === 'ERROR'
                              ? t('bitacora.error', 'Error')
                              : t('bitacora.critico', 'Crítico')}
                      </span>
                    </td>
                    <td>{e.usuario ?? <span className="sub">—</span>}</td>
                    <td>{e.descripcion}</td>
                    <td className="sub mono" style={{ fontSize: 'var(--txt-tabla)' }}>
                      {e.ip ?? '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {cargando && !entradas ? <EsqueletoTabla columnas={5} filas={8} /> : null}
            {entradas && visibles.length === 0 ? (
              entradas.length === 0 ? (
                <SinDatos
                  filtrado
                  titulo={t('vacio.sinEventosEnRango', 'No hay eventos en ese rango')}
                  detalle={t('vacio.bitacoraAyuda', 'La bitácora asienta todo lo que pasa en el sistema. Si acá no hay nada, probá ampliando las fechas.')}
                />
              ) : (
                <SinDatos
                  filtrado
                  titulo={t('vacio.eventoSinCoincidencia', 'Ningún evento coincide con los criterios')}
                  detalle={t('vacio.bitacoraFiltroAyuda', 'Probá quitando el filtro por tipo o por usuario.')}
                />
              )
            ) : null}

            {visibles.length > 0 ? (
              <div className="pie-tabla">
                {/* Dos frases enteras y no una armada por pedazos: «Mostrando
                    12 de 40 eventos» y «Mostrando 40 eventos» ponen los
                    números en lugares distintos según el idioma. */}
                <span>
                  {visibles.length !== entradas?.length
                    ? t('bitacora.mostrandoDeTotal', 'Mostrando {n} de {total} eventos',
                        { n: visibles.length, total: entradas?.length ?? 0 })
                    : t('bitacora.mostrando', 'Mostrando {n} eventos',
                        { n: visibles.length })}
                </span>
              </div>
            ) : null}
          </div>
        </div>
      </div>
    </div>
  );
}
