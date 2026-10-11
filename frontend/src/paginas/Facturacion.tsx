import { useCallback, useEffect, useState } from 'react';
import { useFormato } from '../idioma/formato';
import { useT } from '../idioma/IdiomaContext';
import { api, ErrorApi } from '../api/cliente';
import type { ComprobanteDto, PanelFacturacionDto } from '../api/tipos';
import { Patentes } from '../api/tipos';
import { Encabezado } from '../componentes/Layout';
import { EsqueletoTabla, SinDatos } from '../componentes/Estados';
import { EstadoComprobante } from '../componentes/Semantica';
import { ConfigNav } from './Organizacion';
import { useCerrarSiExpiro, useSesion } from '../sesion/SesionContext';

/**
 * El período como lo lee una persona: «agosto de 2026» y no «2026-08».
 *
 * Pasa por `mesYAnio`, que ya sigue al idioma de la interfaz, en vez de armar
 * otro formateador acá: si esta pantalla escribiera el mes por su cuenta,
 * sería el único lugar del sistema que no cambia al pasar a inglés.
 */
function nombreDePeriodo(periodo: string, mesYAnio: (iso: string) => string): string {
  const texto = mesYAnio(`${periodo}-01T12:00:00`);
  return texto.charAt(0).toUpperCase() + texto.slice(1);
}

/** El detalle del comprobante, con su desglose y sus acciones. */
function Detalle({
  comprobante,
  gestionar,
  onCambio,
  onCerrar,
}: {
  comprobante: ComprobanteDto;
  gestionar: boolean;
  onCambio: () => void;
  onCerrar: () => void;
}) {
  const t = useT();
  const { dinero, fecha, mesYAnio } = useFormato();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [c, setC] = useState(comprobante);
  const [concepto, setConcepto] = useState('');
  const [importe, setImporte] = useState('');
  const [motivo, setMotivo] = useState('');
  const [anulando, setAnulando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  useEffect(() => setC(comprobante), [comprobante]);

  function correr(operacion: Promise<ComprobanteDto | void>, cerrar = false) {
    setOcupado(true);
    setError(null);

    operacion
      .then((r) => {
        if (r) setC(r);
        onCambio();
        if (cerrar) onCerrar();
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.guardar', 'No se pudo guardar.'));
      })
      .finally(() => setOcupado(false));
  }

  function agregarAjuste(e: React.FormEvent) {
    e.preventDefault();
    correr(
      api.facturacion.ajustar(c.id, concepto, Number(importe)).then((r) => {
        setConcepto('');
        setImporte('');
        return r;
      }),
    );
  }

  const borrador = c.estado === 'BORRADOR';

  return (
    <div className="tarjeta">
      <header>
        <h2>
          {c.numero ? `N.º ${c.numero} · ` : ''}
          {nombreDePeriodo(c.periodo, mesYAnio)}
        </h2>
        <button type="button" className="btn" onClick={onCerrar}>
          {t('comun.cerrar', 'Cerrar')}
        </button>
      </header>

      <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 16 }}>
        <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap', alignItems: 'center' }}>
          <EstadoComprobante estado={c.estado} vencido={c.vencido} diasDeAtraso={c.diasDeAtraso} />
          <span className="sub">{c.plan}</span>
          {c.fechaEmision ? (
            <span className="sub">
              {t('factura.emitido', 'Emitido')} {fecha(c.fechaEmision)}
            </span>
          ) : null}
          {c.fechaVencimiento && !c.fechaPago ? (
            <span className="sub">
              {t('factura.vence', 'Vence')} {fecha(c.fechaVencimiento)}
            </span>
          ) : null}
          {c.fechaPago ? (
            <span className="sub">
              {t('factura.cobrado', 'Cobrado')} {fecha(c.fechaPago)}
            </span>
          ) : null}
        </div>

        {c.motivo ? (
          <div className="aviso" role="note">
            {t('factura.motivoAnulacion', 'Motivo de la anulación')}: {c.motivo}
          </div>
        ) : null}

        <table>
          <thead>
            <tr>
              <th>{t('factura.concepto', 'Concepto')}</th>
              <th style={{ width: 90 }} className="derecha">
                {t('factura.cantidad', 'Cantidad')}
              </th>
              <th style={{ width: 130 }} className="derecha">
                {t('factura.precioUnitario', 'Precio unitario')}
              </th>
              <th style={{ width: 140 }} className="derecha">
                {t('factura.importe', 'Importe')}
              </th>
            </tr>
          </thead>
          <tbody>
            {c.lineas.map((l) => (
              <tr key={l.id}>
                <td>
                  {l.concepto}
                  {l.esAjuste ? (
                    <span className="chip neutro" style={{ marginLeft: 8 }}>
                      {t('factura.ajuste', 'Ajuste')}
                    </span>
                  ) : null}
                </td>
                <td className="derecha mono">{l.cantidad}</td>
                <td className="derecha mono">{dinero(l.precioUnitario)}</td>
                <td className="derecha mono">{dinero(l.importe)}</td>
              </tr>
            ))}
          </tbody>
        </table>

        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <div style={{ minWidth: 260, display: 'flex', flexDirection: 'column', gap: 6 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span className="sub">{t('factura.subtotal', 'Subtotal')}</span>
              <span className="mono">{dinero(c.subtotal)}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span className="sub">
                {t('factura.iva', 'IVA')} {c.alicuotaIva}%
              </span>
              <span className="mono">{dinero(c.iva)}</span>
            </div>
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                borderTop: '1px solid var(--linea)',
                paddingTop: 6,
                fontWeight: 600,
              }}
            >
              <span>{t('factura.total', 'Total')}</span>
              <span className="mono">{dinero(c.total)}</span>
            </div>
          </div>
        </div>

        {error ? (
          <div className="aviso error" role="alert">
            {error}
          </div>
        ) : null}

        {gestionar && borrador ? (
          <form
            onSubmit={agregarAjuste}
            style={{ display: 'flex', gap: 10, alignItems: 'flex-end', flexWrap: 'wrap' }}
          >
            <label style={{ flex: '2 1 260px', display: 'flex', flexDirection: 'column', gap: 5 }}>
              <span className="sub">{t('factura.ajusteConcepto', 'Ajuste')}</span>
              <input
                className="campo"
                value={concepto}
                onChange={(e) => setConcepto(e.target.value)}
                required
                minLength={5}
                placeholder={t('factura.ajusteEjemplo', 'Descuento por pago adelantado')}
              />
            </label>

            <label style={{ flex: '0 1 170px', display: 'flex', flexDirection: 'column', gap: 5 }}>
              {/* Puede ser negativo: así se carga un descuento sin necesitar un
                  tipo de comprobante aparte. */}
              <span className="sub">{t('factura.importeConSigno', 'Importe (+/−)')}</span>
              <input
                type="number"
                step="0.01"
                className="campo"
                value={importe}
                onChange={(e) => setImporte(e.target.value)}
                required
              />
            </label>

            <button type="submit" className="btn" disabled={ocupado}>
              {t('factura.agregarAjuste', 'Agregar ajuste')}
            </button>
          </form>
        ) : null}

        {gestionar ? (
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {borrador ? (
              <button
                type="button"
                className="btn pri"
                disabled={ocupado}
                onClick={() => correr(api.facturacion.emitir(c.id))}
              >
                {t('factura.emitir', 'Emitir')}
              </button>
            ) : null}

            {c.estado === 'EMITIDO' ? (
              <button
                type="button"
                className="btn pri"
                disabled={ocupado}
                onClick={() => correr(api.facturacion.registrarPago(c.id))}
              >
                {t('factura.registrarPago', 'Registrar el pago')}
              </button>
            ) : null}

            {c.estado !== 'ANULADO' && c.estado !== 'PAGADO' && !anulando ? (
              <button type="button" className="btn" onClick={() => setAnulando(true)}>
                {borrador ? t('factura.descartar', 'Descartar') : t('factura.anular', 'Anular')}
              </button>
            ) : null}
          </div>
        ) : null}

        {anulando ? (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              correr(api.facturacion.anular(c.id, motivo), true);
            }}
            style={{ display: 'flex', gap: 10, alignItems: 'flex-end', flexWrap: 'wrap' }}
          >
            <label style={{ flex: '1 1 320px', display: 'flex', flexDirection: 'column', gap: 5 }}>
              {/* Un comprobante que desaparece sin explicación deja una
                  facturación que nadie puede auditar. */}
              <span className="sub">{t('factura.motivo', 'Motivo')}</span>
              <input
                className="campo"
                value={motivo}
                onChange={(e) => setMotivo(e.target.value)}
                required
                minLength={5}
                autoFocus
                placeholder={t('factura.motivoEjemplo', 'Se facturó con el plan equivocado')}
              />
            </label>
            <button type="submit" className="btn pri" disabled={ocupado}>
              {t('comun.confirmar', 'Confirmar')}
            </button>
            <button type="button" className="btn" onClick={() => setAnulando(false)}>
              {t('comun.cancelar', 'Cancelar')}
            </button>
          </form>
        ) : null}
      </div>
    </div>
  );
}

export function Facturacion() {
  const t = useT();
  const { dinero, mesYAnio } = useFormato();
  const { puede } = useSesion();
  const cerrarSiExpiro = useCerrarSiExpiro();

  const [lista, setLista] = useState<ComprobanteDto[] | null>(null);
  const [panel, setPanel] = useState<PanelFacturacionDto | null>(null);
  const [abierto, setAbierto] = useState<ComprobanteDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [generando, setGenerando] = useState(false);

  const gestionar = puede(Patentes.facturacionGestionar);

  const cargar = useCallback(() => {
    setError(null);

    api.facturacion
      .listar()
      .then(setLista)
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(
          ex instanceof ErrorApi ? ex.message : t('error.cargarListado', 'No se pudo cargar el listado.'),
        );
      });

    api.facturacion.panel().then(setPanel).catch(cerrarSiExpiro);
  }, [cerrarSiExpiro]);

  useEffect(cargar, [cargar]);

  function generar() {
    setGenerando(true);
    setAviso(null);
    setError(null);

    api.facturacion
      .generar()
      .then((r) => {
        // Se dice si ya estaba: el importe de un mes cerrado no cambia porque
        // se vuelva a apretar el botón, y sin el aviso parece que no hizo nada.
        setAviso(
          r.yaExistia
            ? t('factura.yaFacturado', 'El período {periodo} ya estaba facturado.', {
                periodo: r.periodo,
              })
            : t('factura.generado', 'Borrador del período {periodo} generado por {total}.', {
                periodo: r.periodo,
                total: dinero(r.total),
              }),
        );
        cargar();
        return api.facturacion.detalle(r.id).then(setAbierto);
      })
      .catch((ex) => {
        cerrarSiExpiro(ex);
        setError(ex instanceof ErrorApi ? ex.message : t('error.guardar', 'No se pudo guardar.'));
      })
      .finally(() => setGenerando(false));
  }

  function abrir(id: string) {
    api.facturacion.detalle(id).then(setAbierto).catch(cerrarSiExpiro);
  }

  const contexto = panel
    ? t('factura.contexto', '{pendiente} por cobrar · {vencidos} vencido(s)', {
        pendiente: dinero(panel.totalPendiente),
        vencidos: String(panel.vencidos),
      })
    : t('comun.cargando', 'Cargando…');

  return (
    <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
      <ConfigNav />
      <div className="principal">
        <Encabezado
          titulo={t('factura.titulo', 'Facturación del servicio')}
          contexto={contexto}
          acciones={
            gestionar ? (
              <button type="button" className="btn pri" onClick={generar} disabled={generando}>
                {generando
                  ? t('comun.guardando', 'Guardando…')
                  : t('factura.generarPeriodo', 'Facturar el último mes')}
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

          {aviso ? (
            <div className="aviso" role="status" style={{ marginBottom: 12 }}>
              {aviso}
            </div>
          ) : null}

          {panel ? (
            <div className="indicadores" style={{ '--columnas': 4 } as React.CSSProperties}>
              {[
                {
                  clave: 'factura.porCobrar',
                  texto: 'Por cobrar',
                  valor: dinero(panel.totalPendiente),
                  rojo: false,
                },
                {
                  clave: 'factura.vencido',
                  texto: 'Vencido',
                  valor: dinero(panel.totalVencido),
                  rojo: panel.totalVencido > 0,
                },
                {
                  clave: 'factura.ultimos12',
                  texto: 'Facturado en 12 meses',
                  valor: dinero(panel.facturadoUltimos12Meses),
                  rojo: false,
                },
                {
                  clave: 'factura.periodoSugerido',
                  texto: 'Último mes cerrado',
                  valor: nombreDePeriodo(panel.periodoSugerido, mesYAnio),
                  rojo: false,
                  // Los otros tres son importes; éste es un mes. En la
                  // tipografía de cifras «Agosto de 2026» ocupa dos renglones
                  // y pesa más que los números, que son el dato de la fila.
                  texto_largo: true,
                },
              ].map((i) => (
                <div key={i.clave} className="tarjeta" style={{ padding: '14px 16px' }}>
                  <div className="sub" style={{ marginBottom: 6 }}>
                    {t(i.clave, i.texto)}
                  </div>
                  <div
                    className={i.texto_largo ? undefined : 'mono'}
                    style={{
                      fontSize: i.texto_largo ? 'var(--txt-seccion)' : 'var(--txt-cifra)',
                      fontWeight: 600,
                      color: i.rojo ? 'var(--alto-barra)' : 'var(--tx)',
                      lineHeight: 1.2,
                    }}
                  >
                    {i.valor}
                  </div>
                  {i.clave === 'factura.periodoSugerido' ? (
                    <div className="sub" style={{ marginTop: 6 }}>
                      {panel.periodoSugeridoFacturado
                        ? t('factura.yaEstaFacturado', 'Ya facturado')
                        : t('factura.sinFacturar', 'Sin facturar')}
                    </div>
                  ) : null}
                </div>
              ))}
            </div>
          ) : null}

          <div
            style={{
              display: 'grid',
              gridTemplateColumns: abierto ? 'minmax(0, 1fr) minmax(0, 1.2fr)' : '1fr',
              gap: 16,
              alignItems: 'start',
            }}
          >
            <div className="tarjeta">
              <table>
                <thead>
                  <tr>
                    <th style={{ width: 150 }}>{t('factura.periodo', 'Período')}</th>
                    <th style={{ width: 70 }} className="derecha">
                      {t('factura.numero', 'N.º')}
                    </th>
                    <th style={{ width: 160 }}>{t('comun.estado', 'Estado')}</th>
                    <th className="derecha">{t('factura.total', 'Total')}</th>
                  </tr>
                </thead>
                <tbody>
                  {(lista ?? []).map((c) => (
                    <tr
                      key={c.id}
                      onClick={() => abrir(c.id)}
                      style={{ cursor: 'pointer' }}
                      className={abierto?.id === c.id ? 'activa' : undefined}
                    >
                      <td>{nombreDePeriodo(c.periodo, mesYAnio)}</td>
                      <td className="derecha mono">{c.numero ?? '—'}</td>
                      <td>
                        <EstadoComprobante
                          estado={c.estado}
                          vencido={c.vencido}
                          diasDeAtraso={c.diasDeAtraso}
                        />
                      </td>
                      <td className="derecha mono">{dinero(c.total)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>

              {lista === null ? <EsqueletoTabla columnas={4} /> : null}

              {lista !== null && lista.length === 0 ? (
                <SinDatos
                  titulo={t('factura.sinComprobantes', 'Todavía no hay comprobantes')}
                  detalle={t(
                    'factura.sinComprobantesDetalle',
                    'El comprobante sale del plan contratado y de los equipos administrados al cierre del mes. Facturá el último mes cerrado para empezar.',
                  )}
                />
              ) : null}
            </div>

            {abierto ? (
              <Detalle
                comprobante={abierto}
                gestionar={gestionar}
                onCambio={cargar}
                onCerrar={() => setAbierto(null)}
              />
            ) : null}
          </div>
        </div>
      </div>
    </div>
  );
}
