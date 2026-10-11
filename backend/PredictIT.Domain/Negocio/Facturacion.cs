namespace PredictIT.Domain.Negocio;

/// <summary>
/// Estados de un comprobante.
///
/// «Vencido» no está, por la misma razón que no está en la agenda: es
/// <see cref="Emitido"/> con la fecha de vencimiento pasada y sin pago. Un
/// estado guardado obligaría a un proceso diario que lo mueva, y el día que
/// ese proceso no corra el sistema mostraría datos viejos como ciertos.
/// </summary>
public static class EstadoComprobante
{
    /// <summary>Se puede recalcular, ajustar y borrar. No tiene número.</summary>
    public const string Borrador = "BORRADOR";

    /// <summary>Congelado: tiene número y fecha, y ya no se toca.</summary>
    public const string Emitido = "EMITIDO";

    public const string Pagado = "PAGADO";
    public const string Anulado = "ANULADO";

    public static readonly string[] Todos = [Borrador, Emitido, Pagado, Anulado];
}

/// <summary>
/// Una línea del comprobante.
///
/// El desglose está en líneas y no en columnas del comprobante porque no se
/// sabe cuántos conceptos va a tener: el abono y los equipos adicionales son
/// siempre, pero un descuento comercial o el ajuste de un mes mal facturado
/// son cuantos hagan falta.
/// </summary>
public class LineaComprobante
{
    public Guid Id { get; set; }
    public Guid IdComprobante { get; set; }
    public int Orden { get; set; }
    public string Concepto { get; set; } = string.Empty;

    public int Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }

    /// <summary>
    /// El importe se guarda y no se recalcula al leer.
    ///
    /// Un comprobante emitido dice lo que decía el día que se emitió. Si el
    /// importe saliera de multiplicar cantidad por precio en cada consulta,
    /// cambiar el precio del plan reescribiría los comprobantes del año
    /// pasado, y eso no es un detalle contable: es el registro de lo que la
    /// organización efectivamente pagó.
    /// </summary>
    public decimal Importe { get; set; }

    /// <summary>Un ajuste lo cargó una persona; el resto sale del plan.</summary>
    public bool EsAjuste { get; set; }
}

/// <summary>
/// Lo que una organización paga por el servicio en un período (RF-17).
///
/// El período es el mes calendario y hay a lo sumo un comprobante por
/// organización y período: generarlo dos veces no duplica nada. El importe
/// sale del plan comercial vigente y de los equipos administrados al cierre,
/// que es exactamente lo que la pantalla de Organización ya muestra mes a mes;
/// lo que agrega el comprobante es dejarlo asentado con su número, su fecha y
/// su estado de cobro.
/// </summary>
public class Comprobante
{
    public Guid Id { get; set; }
    public Guid IdOrganizacion { get; set; }
    public string? RazonSocial { get; set; }

    /// <summary>El mes facturado, como `AAAA-MM`.</summary>
    public string Periodo { get; set; } = string.Empty;

    /// <summary>
    /// Correlativo por organización, asignado al emitir.
    ///
    /// Es null mientras es borrador: numerar algo que todavía se puede borrar
    /// deja huecos en la numeración, y un hueco en una numeración correlativa
    /// es una pregunta que hay que poder contestar.
    /// </summary>
    public int? Numero { get; set; }

    public Guid IdPlan { get; set; }
    public string? NombrePlan { get; set; }

    /// <summary>Cuántos equipos administrados había al cerrar el período.</summary>
    public int EquiposAdministrados { get; set; }
    public int EquiposIncluidos { get; set; }

    public decimal Subtotal { get; set; }

    /// <summary>La alícuota se guarda con el comprobante: si cambia, los viejos no cambian.</summary>
    public decimal AlicuotaIva { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }

    public string Estado { get; set; } = EstadoComprobante.Borrador;
    public DateOnly? FechaEmision { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public DateOnly? FechaPago { get; set; }
    public string? Motivo { get; set; }
    public DateTime FechaAlta { get; set; }

    public List<LineaComprobante> Lineas { get; set; } = [];

    /// <summary>Cuántos equipos se cobran aparte del abono.</summary>
    public int EquiposAdicionales => Math.Max(0, EquiposAdministrados - EquiposIncluidos);

    /// <summary>Emitido, con el vencimiento pasado y sin pagar.</summary>
    public bool EstaVencido(DateOnly hoy) =>
        Estado == EstadoComprobante.Emitido &&
        FechaVencimiento is { } vence && vence < hoy;

    /// <summary>Días de atraso en el pago. Negativo mientras falta.</summary>
    public int DiasDeAtraso(DateOnly hoy) =>
        FechaVencimiento is { } vence ? hoy.DayNumber - vence.DayNumber : 0;

    /// <summary>Un borrador es lo único que se puede tocar.</summary>
    public bool EsModificable => Estado == EstadoComprobante.Borrador;
}

/// <summary>
/// El período facturable, como lo entiende el negocio.
///
/// Existe para que «2026-09» no se arme concatenando texto en cinco lugares
/// distintos, cada uno con su criterio sobre el cero de los meses de un dígito.
/// </summary>
public readonly record struct Periodo(int Anio, int Mes)
{
    public static Periodo De(DateOnly fecha) => new(fecha.Year, fecha.Month);

    /// <summary>El mes anterior al de la fecha, que es el que se factura.</summary>
    public static Periodo AnteriorA(DateOnly fecha) =>
        Mover(new Periodo(fecha.Year, fecha.Month), -1);

    public static Periodo Mover(Periodo p, int meses)
    {
        var total = p.Anio * 12 + (p.Mes - 1) + meses;
        return new Periodo(total / 12, total % 12 + 1);
    }

    /// <summary>Lo lee de `AAAA-MM`. Null si no tiene esa forma.</summary>
    public static Periodo? Leer(string? texto)
    {
        if (texto is null || texto.Length != 7 || texto[4] != '-') return null;
        if (!int.TryParse(texto[..4], out var anio)) return null;
        if (!int.TryParse(texto[5..], out var mes)) return null;
        if (mes is < 1 or > 12 || anio is < 2000 or > 2999) return null;
        return new Periodo(anio, mes);
    }

    /// <summary>El primer día del período.</summary>
    public DateOnly Desde => new(Anio, Mes, 1);

    /// <summary>El primer día del período siguiente, exclusivo.</summary>
    public DateOnly Hasta => Mover(this, 1).Desde;

    /// <summary>El último día del período, que es cuando se mide el parque.</summary>
    public DateOnly Cierre => Hasta.AddDays(-1);

    public override string ToString() => $"{Anio:0000}-{Mes:00}";
}
