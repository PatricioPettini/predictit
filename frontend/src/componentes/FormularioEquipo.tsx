import { useState } from 'react';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type { CatalogosEquipoDto, EquipoDetalleDto, EquipoEntradaDto } from '../api/tipos';
import { useCerrarSiExpiro } from '../sesion/SesionContext';

/** Un campo vacío se manda como nulo, no como cadena vacía. */
function oNulo(v: string): string | null {
  const t = v.trim();
  return t.length > 0 ? t : null;
}

/**
 * Alta y edición de un equipo.
 *
 * El mismo formulario para las dos cosas: los campos son los mismos y la única
 * diferencia es de dónde salen los valores iniciales y a qué endpoint va. Dos
 * formularios se desincronizan la primera vez que se agrega un campo.
 *
 * El error de campo que devuelve el backend se muestra **en el campo**, no
 * arriba: «el código ya existe» junto a un cartel general obliga a buscar cuál
 * de los doce campos es el del problema.
 */
export function FormularioEquipo({
  catalogos,
  equipo,
  alGuardar,
  alCancelar,
}: {
  catalogos: CatalogosEquipoDto;
  equipo?: EquipoDetalleDto;
  alGuardar: () => void;
  alCancelar: () => void;
}) {
  const t = useT();
  const cerrarSiExpiro = useCerrarSiExpiro();
  const editando = Boolean(equipo);

  const [f, setF] = useState<EquipoEntradaDto>({
    codigo: equipo?.codigo ?? '',
    idTipoEquipo: equipo?.idTipoEquipo ?? catalogos.tipos[0]?.id ?? '',
    marca: equipo?.marca ?? '',
    modelo: equipo?.modelo ?? '',
    numeroSerie: equipo?.numeroSerie ?? '',
    descripcionTecnica: equipo?.descripcionTecnica ?? '',
    fechaAdquisicion: equipo?.fechaAdquisicion ?? null,
    fechaFinGarantia: equipo?.fechaFinGarantia ?? null,
    proveedor: equipo?.proveedor ?? '',
    criticidad: equipo?.criticidad ?? 2,
    idEstadoEquipo:
      equipo?.idEstadoEquipo ??
      catalogos.estados.find((e) => e.operativo)?.id ??
      catalogos.estados[0]?.id ??
      '',
    idUbicacion: equipo?.idUbicacion ?? null,
    idResponsable: equipo?.idResponsable ?? null,
  });

  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [campoMal, setCampoMal] = useState<string | null>(null);

  function set<K extends keyof EquipoEntradaDto>(k: K, v: EquipoEntradaDto[K]) {
    setF((x) => ({ ...x, [k]: v }));
    if (campoMal) setCampoMal(null);
  }

  function guardar(e: React.FormEvent) {
    e.preventDefault();
    setGuardando(true);
    setError(null);
    setCampoMal(null);

    const entrada: EquipoEntradaDto = {
      ...f,
      marca: oNulo(f.marca ?? ''),
      modelo: oNulo(f.modelo ?? ''),
      numeroSerie: oNulo(f.numeroSerie ?? ''),
      descripcionTecnica: oNulo(f.descripcionTecnica ?? ''),
      proveedor: oNulo(f.proveedor ?? ''),
      idUbicacion: f.idUbicacion || null,
      idResponsable: f.idResponsable || null,
      fechaAdquisicion: f.fechaAdquisicion || null,
      fechaFinGarantia: f.fechaFinGarantia || null,
    };

    const promesa = equipo
      ? api.equipos.actualizar(equipo.id, entrada)
      : api.equipos.registrar(entrada);

    promesa
      .then(alGuardar)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        if (ex instanceof ErrorApi) {
          setError(ex.message);
          setCampoMal(ex.campo ?? null);
        } else {
          setError(t('error.guardarEquipo', 'No se pudo guardar el equipo.'));
        }
      })
      .finally(() => setGuardando(false));
  }

  /** El id del mensaje que explica ese campo, para poder referenciarlo. */
  const idError = (campo: string) => `error-equipo-${campo}`;

  /**
   * Lo que marca el campo que el servidor rechazó.
   *
   * El borde rojo lo ve quien mira la pantalla. El `aria-describedby` es lo
   * que hace que el motivo —que ya está escrito justo abajo— también se lea
   * con un lector de pantalla: sin él, ese campo suena igual que cualquier
   * otro y el texto de abajo queda suelto, sin dueño.
   */
  const marca = (campo: string) =>
    campoMal === campo
      ? {
          style: { borderColor: 'var(--alto-borde)' },
          'aria-invalid': true,
          'aria-describedby': idError(campo),
        }
      : {};

  return (
    <form className="tarjeta" onSubmit={guardar} style={{ marginBottom: 16 }}>
      <header>
        <h2>{editando ? t('activo.editarCodigo', 'Editar {codigo}', { codigo: equipo!.codigo }) : t('activo.nuevo', 'Nuevo equipo')}</h2>
      </header>

      <div
        style={{
          padding: 16,
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))',
          gap: 14,
        }}
      >
        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('activo.codigoObligatorio', 'Código *')}</span>
          <input
            className="campo"
            required
            maxLength={30}
            value={f.codigo}
            {...marca('codigo')}
            placeholder="PC-ADM-014"
            onChange={(e) => set('codigo', e.target.value)}
          />
          {campoMal === 'codigo' ? <span className="sub" id={idError('codigo')}>{error}</span> : null}
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">Tipo *</span>
          <select
            className="campo"
            required
            value={f.idTipoEquipo}
            onChange={(e) => set('idTipoEquipo', e.target.value)}
          >
            {catalogos.tipos.map((t) => (
              <option key={t.id} value={t.id}>
                {t.nombre}
              </option>
            ))}
          </select>
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">Estado *</span>
          <select
            className="campo"
            required
            value={f.idEstadoEquipo}
            onChange={(e) => set('idEstadoEquipo', e.target.value)}
          >
            {catalogos.estados.map((s) => (
              <option key={s.id} value={s.id}>
                {s.nombre}
              </option>
            ))}
          </select>
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('comun.marca', 'Marca')}</span>
          <input
            className="campo"
            maxLength={60}
            value={f.marca ?? ''}
            onChange={(e) => set('marca', e.target.value)}
          />
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('comun.modelo', 'Modelo')}</span>
          <input
            className="campo"
            maxLength={60}
            value={f.modelo ?? ''}
            onChange={(e) => set('modelo', e.target.value)}
          />
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('activo.numeroSerie', 'Número de serie')}</span>
          <input
            className="campo"
            maxLength={60}
            value={f.numeroSerie ?? ''}
            {...marca('numeroSerie')}
            onChange={(e) => set('numeroSerie', e.target.value)}
          />
          {campoMal === 'numeroSerie' ? <span className="sub" id={idError('numeroSerie')}>{error}</span> : null}
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('activo.ubicacion', 'Ubicación')}</span>
          <select
            className="campo"
            value={f.idUbicacion ?? ''}
            onChange={(e) => set('idUbicacion', e.target.value || null)}
          >
            <option value="">{t('comun.sinDefinir', 'Sin definir')}</option>
            {catalogos.ubicaciones.map((u) => (
              <option key={u.id} value={u.id}>
                {u.nombre}
              </option>
            ))}
          </select>
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('activo.responsable', 'Responsable')}</span>
          <select
            className="campo"
            value={f.idResponsable ?? ''}
            onChange={(e) => set('idResponsable', e.target.value || null)}
          >
            <option value="">{t('incidencia.sinAsignar', 'Sin asignar')}</option>
            {catalogos.responsables.map((r) => (
              <option key={r.id} value={r.id}>
                {r.nombreCompleto}
              </option>
            ))}
          </select>
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">Criticidad: {f.criticidad} de 4</span>
          <input
            type="range"
            min={1}
            max={4}
            step={1}
            value={f.criticidad}
            onChange={(e) => set('criticidad', Number(e.target.value))}
          />
          <span className="sub">{t('activo.criticidadAyuda', 'Pondera el riesgo que calcula el motor predictivo.')}</span>
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('activo.fechaAdquisicion', 'Fecha de adquisición')}</span>
          <input
            type="date"
            className="campo"
            value={f.fechaAdquisicion ?? ''}
            onChange={(e) => set('fechaAdquisicion', e.target.value || null)}
          />
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('activo.garantia', 'Fin de garantía')}</span>
          <input
            type="date"
            className="campo"
            value={f.fechaFinGarantia ?? ''}
            {...marca('fechaFinGarantia')}
            onChange={(e) => set('fechaFinGarantia', e.target.value || null)}
          />
          {campoMal === 'fechaFinGarantia' ? <span className="sub" id={idError('fechaFinGarantia')}>{error}</span> : null}
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('ia.proveedor', 'Proveedor')}</span>
          <input
            className="campo"
            maxLength={100}
            value={f.proveedor ?? ''}
            onChange={(e) => set('proveedor', e.target.value)}
          />
        </label>

        <label style={{ gridColumn: '1 / -1', display: 'flex', flexDirection: 'column', gap: 5 }}>
          <span className="sub">{t('activo.descripcionTecnica', 'Descripción técnica')}</span>
          <textarea
            className="campo"
            rows={2}
            maxLength={500}
            value={f.descripcionTecnica ?? ''}
            onChange={(e) => set('descripcionTecnica', e.target.value)}
          />
        </label>
      </div>

      {error && !campoMal ? (
        <div className="aviso error" role="alert" style={{ margin: '0 16px 12px' }}>
          {error}
        </div>
      ) : null}

      <div style={{ display: 'flex', gap: 8, padding: '0 16px 16px' }}>
        <button type="submit" className="btn pri" disabled={guardando}>
          {guardando ? t('comun.guardando', 'Guardando…') : editando ? t('comun.guardarCambios', 'Guardar cambios') : t('activo.crear', 'Crear equipo')}
        </button>
        <button type="button" className="btn" disabled={guardando} onClick={alCancelar}>
          {t('comun.cancelar', 'Cancelar')}
        </button>
      </div>
    </form>
  );
}
