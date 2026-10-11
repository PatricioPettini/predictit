namespace PredictIT.BLL.DTO;

public record LineaComprobanteDto(
    Guid Id,
    int Orden,
    string Concepto,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Importe,
    bool EsAjuste);

/// <summary>
/// Un comprobante del servicio, como se lista y como se ve en detalle.
/// </summary>
/// <param name="Vencido">
/// Emitido, con el vencimiento pasado y sin pago. Lo calcula el servidor y no
/// la pantalla: el navegador puede tener otra fecha, y entonces dos personas
/// verían distinto qué está impago.
/// </param>
public record ComprobanteDto(
    Guid Id,
    string Periodo,
    int? Numero,
    string Plan,
    int EquiposAdministrados,
    int EquiposIncluidos,
    int EquiposAdicionales,
    decimal Subtotal,
    decimal AlicuotaIva,
    decimal Iva,
    decimal Total,
    string Estado,
    bool Vencido,
    int DiasDeAtraso,
    DateOnly? FechaEmision,
    DateOnly? FechaVencimiento,
    DateOnly? FechaPago,
    string? Motivo,
    IReadOnlyList<LineaComprobanteDto> Lineas);

/// <summary>Una línea de ajuste cargada a mano sobre un borrador.</summary>
public record AjusteEntradaDto
{
    public string Concepto { get; init; } = string.Empty;

    /// <summary>
    /// Puede ser negativo: así se carga un descuento o la devolución de un mes
    /// mal facturado, sin necesitar un tipo de comprobante aparte.
    /// </summary>
    public decimal Importe { get; init; }
}

/// <summary>Lo que pasó al generar el período.</summary>
/// <param name="YaExistia">
/// El período ya estaba facturado y no se tocó. Se dice explícitamente: sin
/// eso, generar dos veces parece no haber hecho nada.
/// </param>
public record GeneracionComprobanteDto(Guid Id, string Periodo, bool YaExistia, decimal Total);

/// <summary>
/// El panel de facturación: lo cobrado, lo pendiente y lo vencido.
/// </summary>
public record PanelFacturacionDto(
    string PeriodoSugerido,
    bool PeriodoSugeridoFacturado,
    int Emitidos,
    int Pagados,
    int Vencidos,
    decimal TotalPendiente,
    decimal TotalVencido,
    decimal FacturadoUltimos12Meses,
    IReadOnlyList<ComprobanteDto> Ultimos);
