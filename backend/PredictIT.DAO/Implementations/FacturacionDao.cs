using System.Data;
using Microsoft.Data.SqlClient;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Helpers;
using PredictIT.Domain.Negocio;

namespace PredictIT.DAO.Implementations;

public class ComprobanteMapper : IObjectMapper<Comprobante>
{
    public Comprobante Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_comprobante"),
        IdOrganizacion = f.Guid("id_organizacion"),
        RazonSocial = f.TextoNulo("razon_social"),
        Periodo = f.Texto("periodo").Trim(),
        Numero = f.EnteroNulo("numero"),
        IdPlan = f.Guid("id_plan"),
        NombrePlan = f.TextoNulo("plan_nombre"),
        EquiposAdministrados = f.Entero("equipos_administrados"),
        EquiposIncluidos = f.Entero("equipos_incluidos"),
        Subtotal = f.Decimal("subtotal"),
        AlicuotaIva = f.Decimal("alicuota_iva"),
        Iva = f.Decimal("iva"),
        Total = f.Decimal("total"),
        Estado = f.Texto("estado"),
        FechaEmision = f.SoloFechaNula("fecha_emision"),
        FechaVencimiento = f.SoloFechaNula("fecha_vencimiento"),
        FechaPago = f.SoloFechaNula("fecha_pago"),
        Motivo = f.TextoNulo("motivo"),
        FechaAlta = f.Fecha("fecha_alta"),
    };
}

public class LineaComprobanteMapper : IObjectMapper<LineaComprobante>
{
    public LineaComprobante Map(IDataRecord f) => new()
    {
        Id = f.Guid("id_linea"),
        IdComprobante = f.Guid("id_comprobante"),
        Orden = f.Entero("orden"),
        Concepto = f.Texto("concepto"),
        Cantidad = f.Entero("cantidad"),
        PrecioUnitario = f.Decimal("precio_unitario"),
        Importe = f.Decimal("importe"),
        EsAjuste = f.Booleano("es_ajuste"),
    };
}

/// <summary>
/// Comprobantes del servicio y sus líneas.
///
/// El filtro por organización se aplica acá y no en el controlador, igual que
/// en el resto de los DAO: la organización viene del contexto de sesión y nunca
/// de la petición.
/// </summary>
public class FacturacionDao(SqlHelper sql, IContextoSesion contexto) : IFacturacionDao
{
    private const string Seleccion = @"
        SELECT c.id_comprobante, c.id_organizacion, c.periodo, c.numero, c.id_plan,
               c.equipos_administrados, c.equipos_incluidos, c.subtotal, c.alicuota_iva,
               c.iva, c.total, c.estado, c.fecha_emision, c.fecha_vencimiento,
               c.fecha_pago, c.motivo, c.fecha_alta,
               o.razon_social, p.nombre AS plan_nombre
        FROM dbo.Comprobante c
        JOIN dbo.Organizacion o  ON o.id_organizacion = c.id_organizacion
        JOIN dbo.PlanComercial p ON p.id_plan = c.id_plan";

    private readonly ComprobanteMapper _comprobantes = new();
    private readonly LineaComprobanteMapper _lineas = new();

    private SqlParameter Organizacion() => new("@org", contexto.IdOrganizacion);

    private static object Fecha(DateOnly? f) =>
        f.HasValue ? f.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;

    public IList<Comprobante> Buscar(string? estado, string? periodo) => sql.Consultar(
        Seleccion + @"
        WHERE c.id_organizacion = @org
          AND (@estado IS NULL OR c.estado = @estado)
          AND (@periodo IS NULL OR c.periodo = @periodo)
        ORDER BY c.periodo DESC, c.numero DESC;",
        _comprobantes.Map,
        Organizacion(),
        new SqlParameter("@estado", (object?)estado ?? DBNull.Value),
        new SqlParameter("@periodo", (object?)periodo ?? DBNull.Value));

    public Comprobante? PorId(Guid id)
    {
        var c = sql.ConsultarUno(
            Seleccion + " WHERE c.id_comprobante = @id AND c.id_organizacion = @org;",
            _comprobantes.Map, new SqlParameter("@id", id), Organizacion());

        if (c is not null) c.Lineas = [.. Lineas(c.Id)];
        return c;
    }

    public Comprobante? PorPeriodo(string periodo) => sql.ConsultarUno(
        Seleccion + " WHERE c.periodo = @periodo AND c.id_organizacion = @org;",
        _comprobantes.Map, new SqlParameter("@periodo", periodo), Organizacion());

    public IList<LineaComprobante> Lineas(Guid idComprobante) => sql.Consultar(@"
        SELECT l.id_linea, l.id_comprobante, l.orden, l.concepto, l.cantidad,
               l.precio_unitario, l.importe, l.es_ajuste
        FROM dbo.LineaComprobante l
        JOIN dbo.Comprobante c ON c.id_comprobante = l.id_comprobante
        WHERE l.id_comprobante = @id AND c.id_organizacion = @org
        ORDER BY l.orden;",
        _lineas.Map, new SqlParameter("@id", idComprobante), Organizacion());

    /// <summary>
    /// Cuántos equipos administrados tenía la organización a una fecha.
    ///
    /// Se cuentan los que ya existían a esa fecha y no están dados de baja, que
    /// es lo que el plan comercial cobra. Se mide al cierre del período y no
    /// hoy: un comprobante de agosto tiene que decir el parque de agosto,
    /// aunque se genere en noviembre.
    /// </summary>
    public int EquiposAdministradosAl(DateOnly fecha) => sql.Consultar(@"
        SELECT COUNT(*) AS total
        FROM dbo.Equipo e
        JOIN dbo.EstadoEquipo ee ON ee.id_estado_equipo = e.id_estado_equipo
        WHERE e.id_organizacion = @org
          AND CAST(e.fecha_alta AS DATE) <= @fecha
          AND ee.nombre <> 'Dado de baja';",
        f => f.Entero("total"),
        Organizacion(), new SqlParameter("@fecha", fecha.ToDateTime(TimeOnly.MinValue)))
        .FirstOrDefault();

    public Guid Guardar(Comprobante c)
    {
        var nuevo = c.Id == Guid.Empty;
        if (nuevo) c.Id = Guid.NewGuid();
        c.IdOrganizacion = contexto.IdOrganizacion;

        sql.EjecutarNoConsulta(nuevo
            ? @"INSERT INTO dbo.Comprobante
                    (id_comprobante, id_organizacion, periodo, numero, id_plan,
                     equipos_administrados, equipos_incluidos, subtotal, alicuota_iva,
                     iva, total, estado, fecha_emision, fecha_vencimiento, fecha_pago, motivo)
                VALUES (@id, @org, @periodo, @numero, @plan, @administrados, @incluidos,
                        @subtotal, @alicuota, @iva, @total, @estado, @emision, @vence, @pago, @motivo);"
            : @"UPDATE dbo.Comprobante SET
                    numero = @numero, id_plan = @plan,
                    equipos_administrados = @administrados, equipos_incluidos = @incluidos,
                    subtotal = @subtotal, alicuota_iva = @alicuota, iva = @iva, total = @total,
                    estado = @estado, fecha_emision = @emision, fecha_vencimiento = @vence,
                    fecha_pago = @pago, motivo = @motivo
                WHERE id_comprobante = @id AND id_organizacion = @org;",
            new SqlParameter("@id", c.Id),
            Organizacion(),
            new SqlParameter("@periodo", c.Periodo),
            new SqlParameter("@numero", c.Numero ?? (object)DBNull.Value),
            new SqlParameter("@plan", c.IdPlan),
            new SqlParameter("@administrados", c.EquiposAdministrados),
            new SqlParameter("@incluidos", c.EquiposIncluidos),
            new SqlParameter("@subtotal", c.Subtotal),
            new SqlParameter("@alicuota", c.AlicuotaIva),
            new SqlParameter("@iva", c.Iva),
            new SqlParameter("@total", c.Total),
            new SqlParameter("@estado", c.Estado),
            new SqlParameter("@emision", Fecha(c.FechaEmision)),
            new SqlParameter("@vence", Fecha(c.FechaVencimiento)),
            new SqlParameter("@pago", Fecha(c.FechaPago)),
            new SqlParameter("@motivo", c.Motivo ?? (object)DBNull.Value));

        return c.Id;
    }

    /// <summary>
    /// Reemplaza las líneas del comprobante.
    ///
    /// Borra y vuelve a insertar en vez de comparar una por una: un borrador se
    /// recalcula entero, y llevar la cuenta de cuál línea cambió sería trabajo
    /// para no ahorrar nada sobre cuatro filas.
    /// </summary>
    public void ReemplazarLineas(Guid idComprobante, IEnumerable<LineaComprobante> lineas)
    {
        sql.EjecutarNoConsulta(@"
            DELETE l FROM dbo.LineaComprobante l
            JOIN dbo.Comprobante c ON c.id_comprobante = l.id_comprobante
            WHERE l.id_comprobante = @id AND c.id_organizacion = @org;",
            new SqlParameter("@id", idComprobante), Organizacion());

        foreach (var l in lineas)
        {
            sql.EjecutarNoConsulta(@"
                INSERT INTO dbo.LineaComprobante
                    (id_linea, id_comprobante, orden, concepto, cantidad,
                     precio_unitario, importe, es_ajuste)
                VALUES (@id, @comp, @orden, @concepto, @cantidad, @precio, @importe, @ajuste);",
                new SqlParameter("@id", l.Id == Guid.Empty ? Guid.NewGuid() : l.Id),
                new SqlParameter("@comp", idComprobante),
                new SqlParameter("@orden", l.Orden),
                new SqlParameter("@concepto", l.Concepto),
                new SqlParameter("@cantidad", l.Cantidad),
                new SqlParameter("@precio", l.PrecioUnitario),
                new SqlParameter("@importe", l.Importe),
                new SqlParameter("@ajuste", l.EsAjuste));
        }
    }

    public void Eliminar(Guid id) => sql.EjecutarNoConsulta(@"
        DELETE FROM dbo.Comprobante
        WHERE id_comprobante = @id AND id_organizacion = @org AND estado = 'BORRADOR';",
        new SqlParameter("@id", id), Organizacion());

    /// <summary>
    /// El número que le toca al próximo comprobante emitido.
    ///
    /// Sale del máximo y no de un contador aparte: un contador es un segundo
    /// registro que puede contradecir a la tabla, y acá la tabla es la verdad.
    /// </summary>
    public int ProximoNumero() => sql.Consultar(@"
        SELECT ISNULL(MAX(numero), 0) + 1 AS proximo
        FROM dbo.Comprobante WHERE id_organizacion = @org;",
        f => f.Entero("proximo"), Organizacion()).FirstOrDefault();
}
