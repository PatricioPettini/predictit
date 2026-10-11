import { useCallback, useEffect, useMemo, useState } from 'react';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type { RolDto, UsuarioDetalleDto, UsuarioListaDto } from '../api/tipos';
import { Patentes } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { EsqueletoTabla, SinDatos } from '../componentes/Estados';
import { IconoBuscar } from '../componentes/Iconos';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';

/** Panel lateral con el detalle del usuario y la edición de sus roles. */
function Detalle({
  id,
  roles,
  onCambio,
  onCerrar,
}: {
  id: string;
  roles: RolDto[];
  onCambio: () => void;
  onCerrar: () => void;
}) {
  const { fechaHoraSegundos: fechaHora } = useFormato();
  const t = useT();
  const { puede, sesion } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [detalle, setDetalle] = useState<UsuarioDetalleDto | null>(null);
  const [elegidos, setElegidos] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);

  const cargar = useCallback(() => {
    setError(null);
    api.usuarios
      .detalle(id)
      .then((d) => {
        setDetalle(d);
        setElegidos(roles.filter((r) => d.datos.roles.includes(r.nombre)).map((r) => r.id));
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarUsuario', 'No se pudo cargar el usuario.'));
      });
  }, [id, roles, cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  function guardarRoles() {
    setGuardando(true);
    setError(null);
    api.usuarios
      .asignarRoles(id, elegidos)
      .then(() => {
        cargar();
        onCambio();
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.guardarRoles', 'No se pudieron guardar los roles.'));
      })
      .finally(() => setGuardando(false));
  }

  if (!detalle) {
    return (
      <div className="tarjeta">
        <div className="vacio">
          {error ?? t('usuario.cargando', 'Cargando el usuario…')}
        </div>
      </div>
    );
  }

  const u = detalle.datos;
  const esUnoMismo = u.id === sesion?.idUsuario;

  const sinCambios =
    elegidos.length === u.roles.length &&
    roles.filter((r) => elegidos.includes(r.id)).every((r) => u.roles.includes(r.nombre));

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div className="tarjeta">
        <header>
          <h2>{u.nombreCompleto}</h2>
          <button type="button" className="btn plano" onClick={onCerrar}>
            {t('comun.cerrar', 'Cerrar')}
          </button>
        </header>

        <dl className="dl">
          <dt>{t('login.usuario', 'Usuario')}</dt>
          <dd className="mono">{u.username}</dd>
          <dt>{t('comun.correo', 'Correo')}</dt>
          <dd>{u.email}</dd>
          <dt>{t('usuario.telefono', 'Teléfono')}</dt>
          <dd>{u.telefono ?? '—'}</dd>
          <dt>{t('comun.estado', 'Estado')}</dt>
          <dd>
            <span className={u.activo ? 'chip bajo' : 'chip'}>
              {u.activo ? t('comun.activo', 'Activo') : t('comun.inactivo', 'Inactivo')}
            </span>
            {u.bloqueado ? (
              <span className="chip alto" style={{ marginLeft: 6 }}>
                {t('usuario.bloqueado', 'Bloqueado')}
              </span>
            ) : null}
          </dd>
          <dt>{t('activo.criticidadAlta', 'Alta')}</dt>
          <dd>{fechaHora(u.fechaAlta)}</dd>
          <dt>{t('usuario.ultimoAcceso', 'Último acceso')}</dt>
          <dd>{fechaHora(u.ultimoAcceso)}</dd>
          <dt>{t('dash.incidenciasAbiertas', 'Incidencias abiertas')}</dt>
          <dd className="mono">{u.incidenciasAbiertas}</dd>
        </dl>
      </div>

      {puede(Patentes.rolGestionar) ? (
        <div className="tarjeta">
          <header>
            <h2>{t('usuario.roles', 'Roles')}</h2>
            <span className="nota">{u.cantidadPatentes} permisos</span>
          </header>

          <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 8 }}>
            {roles.map((r) => (
              <label key={r.id} style={{ display: 'flex', gap: 9, alignItems: 'flex-start' }}>
                <input
                  type="checkbox"
                  checked={elegidos.includes(r.id)}
                  onChange={(e) =>
                    setElegidos((prev) =>
                      e.target.checked ? [...prev, r.id] : prev.filter((x) => x !== r.id),
                    )
                  }
                  style={{ marginTop: 3 }}
                />
                <span>
                  <span style={{ fontSize: 'var(--txt-tabla)' }}>{r.nombre}</span>
                  <span className="sub" style={{ display: 'block' }}>
                    {r.cantidadPatentes} permisos
                    {r.descripcion ? ` · ${r.descripcion}` : ''}
                  </span>
                </span>
              </label>
            ))}

            {esUnoMismo ? (
              <div className="aviso atencion">
                {t('usuario.propioUsuarioNota', 'Es tu propio usuario. El sistema no te va a dejar quitarte la gestión de usuarios: quedarías sin poder revertirlo.')}
              </div>
            ) : null}

            {error ? (
              <div className="aviso error" role="alert">
                {error}
              </div>
            ) : null}

            <div>
              <button
                type="button"
                className="btn pri"
                onClick={guardarRoles}
                disabled={guardando || sinCambios}
              >
                {guardando ? t('comun.guardando', 'Guardando…') : t('usuarios.guardarRoles', 'Guardar roles')}
              </button>
            </div>
          </div>
        </div>
      ) : null}

      <div className="tarjeta">
        <header>
          <h2>{t('usuario.permisosEfectivos', 'Permisos efectivos')}</h2>
          <span className="nota">{detalle.patentes.length}</span>
        </header>

        {/*
          Se muestra de qué rol viene cada permiso. Sin eso, «por qué este
          usuario puede hacer esto» no tiene respuesta, y es la pregunta que
          aparece cuando algo no funciona como se esperaba.
        */}
        <table>
          <thead>
            <tr>
              <th>{t('usuarios.permiso', 'Permiso')}</th>
              <th style={{ width: 150 }}>{t('usuario.loDa', 'Lo da')}</th>
            </tr>
          </thead>
          <tbody>
            {detalle.patentes.map((p) => (
              <tr key={p.dataKey}>
                <td>
                  <span className="mono" style={{ fontSize: 'var(--txt-tabla)' }}>
                    {p.dataKey}
                  </span>
                  <div className="sub">{p.nombre}</div>
                </td>
                <td className="sub">{p.origen}</td>
              </tr>
            ))}
          </tbody>
        </table>

        {detalle.patentes.length === 0 ? (
          <div className="vacio">{t('usuario.sinPermisos', 'Este usuario no tiene ningún permiso.')}</div>
        ) : null}
      </div>
    </div>
  );
}

export function Usuarios() {
  const { fechaHoraSegundos: fechaHora } = useFormato();
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [lista, setLista] = useState<UsuarioListaDto[] | null>(null);
  const [roles, setRoles] = useState<RolDto[]>([]);
  const [texto, setTexto] = useState('');
  const [elegido, setElegido] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  const cargar = useCallback(() => {
    setError(null);
    api.usuarios
      .listar()
      .then(setLista)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.cargarListado', 'No se pudo cargar el listado.'));
      });
  }, [cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  useEffect(() => {
    api.usuarios.roles().then(setRoles).catch(cerrarSiExpiro);
  }, [cerrarSiExpiro]);

  function accion(promesa: Promise<void>) {
    setOcupado(true);
    setError(null);
    promesa
      .then(cargar)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.operacion', 'No se pudo completar la operación.'));
      })
      .finally(() => setOcupado(false));
  }

  const visibles = useMemo(() => {
    if (!lista) return [];
    const t = texto.trim().toLowerCase();
    if (!t) return lista;
    return lista.filter(
      (u) =>
        u.username.toLowerCase().includes(t) ||
        u.nombreCompleto.toLowerCase().includes(t) ||
        u.email.toLowerCase().includes(t) ||
        u.roles.some((r) => r.toLowerCase().includes(t)),
    );
  }, [lista, texto]);

  const contexto = useMemo(() => {
    if (!lista) return t('comun.cargando', 'Cargando…');
    const activos = lista.filter((u) => u.activo).length;
    const bloqueados = lista.filter((u) => u.bloqueado).length;

    const partes = [
      lista.length === 1 ? '1 usuario' : `${lista.length} usuarios`,
      `${activos} activos`,
    ];
    if (bloqueados > 0) {
      partes.push(bloqueados === 1 ? '1 bloqueado' : `${bloqueados} bloqueados`);
    }
    return partes.join(' · ');
  }, [lista]);

  const bloqueados = lista?.filter((u) => u.bloqueado) ?? [];

  return (
    <>
      <Encabezado titulo={t('usuario.titulo', 'Usuarios')} contexto={contexto} />

      <div className="cuerpo pagina-usuarios">
        {error ? (
          <div className="aviso error" role="alert" style={{ marginBottom: 12 }}>
            {error}
          </div>
        ) : null}

        {bloqueados.length > 0 ? (
          <div className="aviso atencion" style={{ marginBottom: 12 }}>
            {bloqueados.length === 1
              ? t('usuario.unoBloqueado', '{nombre} está bloqueado por intentos fallidos.',
                  { nombre: bloqueados[0]?.nombreCompleto
                            ?? t('usuario.unUsuario', 'Un usuario') })
              : t('usuario.variosBloqueados',
                  '{n} usuarios están bloqueados por intentos fallidos.',
                  { n: bloqueados.length })}
          </div>
        ) : null}

        <div
          style={{
            display: 'grid',
            gridTemplateColumns: elegido ? 'minmax(0, 1fr) 400px' : '1fr',
            gap: 16,
          }}
        >
          <div>
            <div className="filtros">
              <div className="campo buscador">
                <IconoBuscar />
                <input
                  value={texto}
                  onChange={(e) => setTexto(e.target.value)}
                  placeholder="Usuario, nombre, correo o rol…"
                  aria-label={t('usuarios.buscar', 'Buscar usuarios')}
                />
              </div>
            </div>

            <div className="tarjeta">
              <table>
                <thead>
                  <tr>
                    <th style={{ width: 120 }}>{t('login.usuario', 'Usuario')}</th>
                    <th>{t('usuario.nombre', 'Nombre')}</th>
                    <th style={{ width: 175 }}>{t('usuario.roles', 'Roles')}</th>
                    <th style={{ width: 75 }}>{t('usuario.permisos', 'Permisos')}</th>
                    <th style={{ width: 120 }}>{t('usuario.ultimoAcceso', 'Último acceso')}</th>
                    <th style={{ width: 105 }}>{t('comun.estado', 'Estado')}</th>
                    <th style={{ width: 165 }} className="derecha">
                      {t('comun.acciones', 'Acciones')}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {visibles.map((u) => (
                    <tr key={u.id}>
                      <td style={{ whiteSpace: 'nowrap' }}>
                        <button
                          type="button"
                          className="btn plano"
                          style={{ padding: 0, height: 'auto' }}
                          onClick={() => setElegido(u.id)}
                        >
                          <span className="mono">{u.username}</span>
                        </button>
                      </td>
                      <td>
                        <span style={{ color: 'var(--tx)' }}>{u.nombreCompleto}</span>
                        <div className="sub">{u.email}</div>
                      </td>
                      <td className="sub">{u.roles.join(', ') || '—'}</td>
                      <td className="mono">{u.cantidadPatentes}</td>
                      <td className="sub">{fechaHora(u.ultimoAcceso)}</td>
                      <td>
                        <span className={u.activo ? 'chip bajo' : 'chip'}>
                          {u.activo ? t('comun.activo', 'Activo') : t('comun.inactivo', 'Inactivo')}
                        </span>
                        {u.bloqueado ? (
                          <div style={{ marginTop: 3 }}>
                            <span className="chip alto">{t('usuario.bloqueado', 'Bloqueado')}</span>
                          </div>
                        ) : null}
                      </td>
                      <td className="derecha">
                        <span style={{ display: 'inline-flex', gap: 6 }}>
                          {u.bloqueado ? (
                            <button
                              type="button"
                              className="btn"
                              disabled={ocupado}
                              onClick={() => accion(api.usuarios.desbloquear(u.id))}
                            >
                              {t('usuario.desbloquear', 'Desbloquear')}
                            </button>
                          ) : null}
                          <button
                            type="button"
                            className="btn plano"
                            disabled={ocupado}
                            onClick={() => accion(api.usuarios.cambiarEstado(u.id, !u.activo))}
                          >
                            {u.activo ? t('comun.desactivar', 'Desactivar') : t('comun.activar', 'Activar')}
                          </button>
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              {!lista ? <EsqueletoTabla columnas={5} filas={5} /> : null}
              {lista && visibles.length === 0 ? (
                <SinDatos
                  filtrado
                  titulo={t('vacio.usuarioSinCoincidencia', 'Ningún usuario coincide con la búsqueda')}
                  detalle={t('vacio.usuarioFiltroAyuda', 'Probá con parte del nombre, del usuario o del correo.')}
                />
              ) : null}
            </div>

            <p className="sub" style={{ marginTop: 10 }}>
              {t('usuario.altaDesdeBaseNota', 'El alta de usuarios se hace desde la base de datos: crear una cuenta implica establecer una contraseña, y hacerlo desde una pantalla sin un circuito de invitación por correo dejaría la contraseña inicial en manos de quien la crea.')}
            </p>
          </div>

          {elegido ? (
            <Detalle
              id={elegido}
              roles={roles}
              onCambio={cargar}
              onCerrar={() => setElegido(null)}
            />
          ) : null}
        </div>
      </div>
    </>
  );
}
