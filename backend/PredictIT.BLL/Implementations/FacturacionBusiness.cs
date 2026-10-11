using PredictIT.BLL.Contracts;
using PredictIT.BLL.DTO;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Factory;
using PredictIT.Domain.Negocio;
using PredictIT.Domain.Seguridad;
using PredictIT.Service.Seguridad;

namespace PredictIT.BLL.Implementations;

/// <summary>
/// La facturación del servicio (RF-17, CU-021).
///
/// El sistema ya sabía cuánto sale el mes: la pantalla de Organización lo
/// muestra a partir del plan comercial y de los equipos administrados. Lo que
/// no existía era el registro de que ese mes se facturó, por cuánto, cuándo
/// vence y si está cobrado, y sin eso el modelo de ingresos del plan de
/// negocio no tiene ningún reflejo en el sistema.
///
/// Dos decisiones de fondo:
///
/// - El comprobante nace como borrador y se emite en un segundo paso. Emitir
///   congela: asigna número, fecha y vencimiento, y ya no se toca. Para
///   corregir uno emitido hay que anularlo con su motivo, que es lo que hace
///   auditable la facturación.
///
/// - Los números del plan quedan copiados en el comprobante. Si se leyeran del
///   plan al consultar, cambiar el precio reescribiría los comprobantes del año
///   pasado: eso no es un detalle contable, es el registro de lo que la
///   organización efectivamente pagó.
/// </summary>
public class FacturacionBusiness(
    IFactoryDaoSeguridad seguridad,
    IContextoSesion contexto,
    IFactoryDao dao,
    IBitacoraService bitacora)
    : BaseAdministracion(seguridad, contexto), IFacturacionBusiness
{
    /// <summary>Días entre la emisión y el vencimiento.</summary>
    private const int DiasParaPagar = 10;

    /// <summary>Alícuota general de IVA en Argentina.</summary>
    private const decimal Alicuota = 21m;

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// El período que corresponde facturar: el mes cerrado, no el corriente.
    ///
    /// Un mes en curso todavía puede sumar o perder equipos, y el comprobante
    /// mide el parque al cierre. Facturarlo antes daría un número que después
    /// no coincide con nada.
    /// </summary>
    private static Periodo Sugerido => Periodo.AnteriorA(Hoy);

    // -------------------------------------------------------- consultas

    public IReadOnlyList<ComprobanteDto> Comprobantes(string? estado, string? periodo)
    {
        Exigir(Patentes.FacturacionVer);

        return dao.Facturacion.Buscar(Normalizar(estado), Recortado(periodo))
                  .Select(c => ADto(c, []))
                  .ToList();
    }

    public ComprobanteDto Comprobante(Guid id)
    {
        Exigir(Patentes.FacturacionVer);
        var c = Traer(id);
        return ADto(c, c.Lineas);
    }

    public PanelFacturacionDto Panel()
    {
        Exigir(Patentes.FacturacionVer);

        var todos = dao.Facturacion.Buscar(null, null);
        var sugerido = Sugerido.ToString();

        var emitidos = todos.Where(c => c.Estado == EstadoComprobante.Emitido).ToList();
        var vencidos = emitidos.Where(c => c.EstaVencido(Hoy)).ToList();

        // Los últimos doce meses se cuentan por período y no por fecha de
        // emisión: lo que interesa es cuánto se facturó del servicio prestado,
        // y un comprobante atrasado se emite meses después del mes que cubre.
        var desde = Periodo.Mover(Periodo.De(Hoy), -11).ToString();

        return new PanelFacturacionDto(
            sugerido,
            dao.Facturacion.PorPeriodo(sugerido) is not null,
            emitidos.Count,
            todos.Count(c => c.Estado == EstadoComprobante.Pagado),
            vencidos.Count,
            emitidos.Sum(c => c.Total),
            vencidos.Sum(c => c.Total),
            todos.Where(c => c.Estado != EstadoComprobante.Anulado
                             && string.CompareOrdinal(c.Periodo, desde) >= 0)
                 .Sum(c => c.Total),
            todos.Take(6).Select(c => ADto(c, [])).ToList());
    }

    // ------------------------------------------------------ operaciones

    /// <summary>
    /// Arma el borrador del período con lo que el plan cobra.
    ///
    /// Es idempotente: si el período ya está facturado devuelve el que hay sin
    /// tocarlo. Regenerarlo pisaría un comprobante emitido, y el importe de un
    /// mes cerrado no cambia porque alguien vuelva a apretar el botón.
    /// </summary>
    public GeneracionComprobanteDto Generar(string? periodo)
    {
        Exigir(Patentes.FacturacionGestionar);

        var p = Periodo.Leer(Recortado(periodo)) ?? Sugerido;

        if (p.Desde > Hoy)
        {
            throw BusinessException.De("periodo",
                "No se puede facturar un período que todavía no empezó.");
        }

        if (dao.Facturacion.PorPeriodo(p.ToString()) is { } existente)
        {
            return new GeneracionComprobanteDto(existente.Id, existente.Periodo, true, existente.Total);
        }

        var plan = dao.Organizaciones.PlanDe(Contexto.IdOrganizacion)
                   ?? throw new BusinessException("La organización no tiene un plan asignado.");

        // El parque se mide al cierre del período y no hoy: un comprobante de
        // agosto tiene que decir el parque de agosto, aunque se genere en
        // noviembre.
        var equipos = dao.Facturacion.EquiposAdministradosAl(p.Cierre);
        var adicionales = Math.Max(0, equipos - plan.EquiposIncluidos);

        var comprobante = new Comprobante
        {
            Periodo = p.ToString(),
            IdPlan = plan.Id,
            EquiposAdministrados = equipos,
            EquiposIncluidos = plan.EquiposIncluidos,
            AlicuotaIva = Alicuota,
            Estado = EstadoComprobante.Borrador,
        };

        // Los conceptos se escriben en castellano y no se traducen, aunque la
        // interfaz esté en inglés: son parte del comprobante, no de la
        // pantalla. Si se armaran al mostrarlos, un comprobante emitido diría
        // cosas distintas según quién lo abra, y lo que se emitió es uno solo.
        var lineas = new List<LineaComprobante>
        {
            new()
            {
                Orden = 1,
                Concepto = $"Abono {plan.Nombre} — {p}",
                Cantidad = 1,
                PrecioUnitario = plan.AbonoMensual,
                Importe = plan.AbonoMensual,
            },
        };

        if (adicionales > 0)
        {
            lineas.Add(new LineaComprobante
            {
                Orden = 2,
                Concepto = $"Equipos adicionales ({equipos} administrados, {plan.EquiposIncluidos} incluidos)",
                Cantidad = adicionales,
                PrecioUnitario = plan.PrecioEquipoAdicional,
                Importe = adicionales * plan.PrecioEquipoAdicional,
            });
        }

        Totalizar(comprobante, lineas);

        var id = dao.Facturacion.Guardar(comprobante);
        dao.Facturacion.ReemplazarLineas(id, lineas);

        bitacora.Registrar(TipoEvento.Codigos.ComprobanteGenerado,
            $"Comprobante del período {p} generado en borrador por {comprobante.Total:C}.",
            Contexto.IdUsuario, Contexto.IdOrganizacion, "Comprobante", id);

        return new GeneracionComprobanteDto(id, comprobante.Periodo, false, comprobante.Total);
    }

    public ComprobanteDto AgregarAjuste(Guid id, AjusteEntradaDto entrada)
    {
        Exigir(Patentes.FacturacionGestionar);

        var c = Traer(id);
        ExigirModificable(c);

        var concepto = Recortado(entrada.Concepto);
        if (concepto is null || concepto.Length < 5)
        {
            throw BusinessException.De("concepto",
                "El ajuste tiene que decir de qué se trata, con al menos 5 caracteres.");
        }

        if (entrada.Importe == 0)
        {
            throw BusinessException.De("importe", "Un ajuste de cero no cambia nada.");
        }

        var lineas = c.Lineas.ToList();
        lineas.Add(new LineaComprobante
        {
            Orden = lineas.Count == 0 ? 1 : lineas.Max(l => l.Orden) + 1,
            Concepto = concepto,
            Cantidad = 1,
            PrecioUnitario = entrada.Importe,
            Importe = entrada.Importe,
            EsAjuste = true,
        });

        Totalizar(c, lineas);
        dao.Facturacion.Guardar(c);
        dao.Facturacion.ReemplazarLineas(c.Id, lineas);

        bitacora.Registrar(TipoEvento.Codigos.ComprobanteAjustado,
            $"Ajuste «{concepto}» por {entrada.Importe:C} sobre el comprobante del período {c.Periodo}.",
            Contexto.IdUsuario, Contexto.IdOrganizacion, "Comprobante", c.Id);

        c.Lineas = lineas;
        return ADto(c, lineas);
    }

    /// <summary>
    /// Emite el comprobante: le pone número, fecha y vencimiento.
    ///
    /// A partir de acá no se toca. Es el punto donde el borrador deja de ser un
    /// cálculo y pasa a ser lo que se le reclama a la organización.
    /// </summary>
    public ComprobanteDto Emitir(Guid id)
    {
        Exigir(Patentes.FacturacionGestionar);

        var c = Traer(id);
        ExigirModificable(c);

        if (c.Total <= 0)
        {
            throw BusinessException.De("total",
                "No se emite un comprobante sin importe. Revisá los ajustes cargados.");
        }

        c.Numero = dao.Facturacion.ProximoNumero();
        c.Estado = EstadoComprobante.Emitido;
        c.FechaEmision = Hoy;
        c.FechaVencimiento = Hoy.AddDays(DiasParaPagar);

        dao.Facturacion.Guardar(c);

        bitacora.Registrar(TipoEvento.Codigos.ComprobanteEmitido,
            $"Comprobante N.º {c.Numero} del período {c.Periodo} emitido por {c.Total:C}, "
            + $"con vencimiento el {c.FechaVencimiento:dd/MM/yyyy}.",
            Contexto.IdUsuario, Contexto.IdOrganizacion, "Comprobante", c.Id);

        return ADto(c, c.Lineas);
    }

    public ComprobanteDto RegistrarPago(Guid id, DateOnly? fecha)
    {
        Exigir(Patentes.FacturacionGestionar);

        var c = Traer(id);

        if (c.Estado != EstadoComprobante.Emitido)
        {
            throw BusinessException.De("estado",
                c.Estado == EstadoComprobante.Pagado
                    ? "El comprobante ya figura pagado."
                    : "Sólo se puede registrar el pago de un comprobante emitido.");
        }

        var dia = fecha ?? Hoy;

        if (dia > Hoy) throw BusinessException.De("fecha", "El pago no puede ser de una fecha futura.");
        if (c.FechaEmision is { } emision && dia < emision)
        {
            throw BusinessException.De("fecha",
                "El pago no puede ser anterior a la emisión del comprobante.");
        }

        c.Estado = EstadoComprobante.Pagado;
        c.FechaPago = dia;
        dao.Facturacion.Guardar(c);

        var atraso = c.DiasDeAtraso(dia);
        bitacora.Registrar(TipoEvento.Codigos.ComprobantePagado,
            $"Pago del comprobante N.º {c.Numero} registrado el {dia:dd/MM/yyyy}"
            + (atraso > 0 ? $", con {atraso} día(s) de atraso." : "."),
            Contexto.IdUsuario, Contexto.IdOrganizacion, "Comprobante", c.Id);

        return ADto(c, c.Lineas);
    }

    /// <summary>
    /// Anula el comprobante con su motivo.
    ///
    /// Un emitido no se borra: el número ya se usó, y hacerlo desaparecer
    /// dejaría un hueco en la numeración que nadie puede explicar. Un borrador
    /// sí se borra, porque nunca tuvo número.
    /// </summary>
    public void Anular(Guid id, string motivo)
    {
        Exigir(Patentes.FacturacionGestionar);

        var c = Traer(id);
        var texto = Recortado(motivo);

        if (texto is null || texto.Length < 5)
        {
            throw BusinessException.De("motivo",
                "Anular un comprobante requiere un motivo de al menos 5 caracteres.");
        }

        if (c.Estado == EstadoComprobante.Anulado)
        {
            throw BusinessException.De("estado", "El comprobante ya está anulado.");
        }

        if (c.Estado == EstadoComprobante.Pagado)
        {
            throw BusinessException.De("estado",
                "No se anula un comprobante cobrado. Cargá un ajuste en el período siguiente.");
        }

        if (c.Estado == EstadoComprobante.Borrador)
        {
            dao.Facturacion.Eliminar(c.Id);
            bitacora.Registrar(TipoEvento.Codigos.ComprobanteAnulado,
                $"Borrador del período {c.Periodo} descartado. Motivo: {texto}",
                Contexto.IdUsuario, Contexto.IdOrganizacion, "Comprobante", c.Id);
            return;
        }

        c.Estado = EstadoComprobante.Anulado;
        c.Motivo = texto;
        dao.Facturacion.Guardar(c);

        bitacora.Registrar(TipoEvento.Codigos.ComprobanteAnulado,
            $"Comprobante N.º {c.Numero} del período {c.Periodo} anulado. Motivo: {texto}",
            Contexto.IdUsuario, Contexto.IdOrganizacion, "Comprobante", c.Id);
    }

    // ----------------------------------------------------------- apoyo

    private Comprobante Traer(Guid id) =>
        dao.Facturacion.PorId(id)
        ?? throw new BusinessException("El comprobante no existe o no pertenece a tu organización.");

    private static void ExigirModificable(Comprobante c)
    {
        if (!c.EsModificable)
        {
            throw BusinessException.De("estado",
                "Un comprobante emitido no se modifica. Si está mal, anulalo con su motivo "
                + "y generá uno nuevo.");
        }
    }

    /// <summary>
    /// Recalcula subtotal, IVA y total a partir de las líneas.
    ///
    /// El IVA se redondea a dos decimales acá y no al mostrarlo: si cada
    /// pantalla redondeara por su cuenta, el total impreso podría no ser la
    /// suma de lo impreso.
    /// </summary>
    private static void Totalizar(Comprobante c, IReadOnlyList<LineaComprobante> lineas)
    {
        c.Subtotal = lineas.Sum(l => l.Importe);
        c.Iva = Math.Round(c.Subtotal * c.AlicuotaIva / 100m, 2, MidpointRounding.AwayFromZero);
        c.Total = c.Subtotal + c.Iva;
    }

    private static string? Normalizar(string? estado)
    {
        var texto = estado?.Trim().ToUpperInvariant();
        return EstadoComprobante.Todos.Contains(texto) ? texto : null;
    }

    private ComprobanteDto ADto(Comprobante c, IReadOnlyList<LineaComprobante> lineas) => new(
        c.Id,
        c.Periodo,
        c.Numero,
        c.NombrePlan ?? "—",
        c.EquiposAdministrados,
        c.EquiposIncluidos,
        c.EquiposAdicionales,
        c.Subtotal,
        c.AlicuotaIva,
        c.Iva,
        c.Total,
        c.Estado,
        c.EstaVencido(Hoy),
        c.DiasDeAtraso(Hoy),
        c.FechaEmision,
        c.FechaVencimiento,
        c.FechaPago,
        c.Motivo,
        lineas.Select(l => new LineaComprobanteDto(
            l.Id, l.Orden, l.Concepto, l.Cantidad, l.PrecioUnitario, l.Importe, l.EsAjuste)).ToList());
}
