namespace PredictIT.Domain.Negocio;

/// <summary>Plan comercial contratado por una organización (documento, 5.3.6).</summary>
public class PlanComercial
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal AbonoMensual { get; set; }
    public int EquiposIncluidos { get; set; }
    public decimal PrecioEquipoAdicional { get; set; }
    public string? Soporte { get; set; }
    public DateOnly VigenteDesde { get; set; }

    /// <summary>Costo mensual para un parque de <paramref name="equipos"/> equipos.</summary>
    public decimal CostoMensualPara(int equipos)
    {
        var adicionales = Math.Max(0, equipos - EquiposIncluidos);
        return AbonoMensual + adicionales * PrecioEquipoAdicional;
    }

    public override string ToString() => Nombre;
}

/// <summary>Empresa cliente. Raíz del aislamiento entre organizaciones.</summary>
public class Organizacion
{
    public Guid Id { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string NombreCorto { get; set; } = string.Empty;
    public string? Cuit { get; set; }
    public bool Activa { get; set; } = true;
    public DateTime FechaAlta { get; set; }

    public Guid? IdPlan { get; set; }
    public PlanComercial? Plan { get; set; }

    public override string ToString() => NombreCorto;
}

public class TipoEquipo
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public override string ToString() => Nombre;
}

public class EstadoEquipo
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Un equipo no operativo no participa del scoring de riesgo.</summary>
    public bool Operativo { get; set; }
    public int Orden { get; set; }
    public override string ToString() => Nombre;
}

public class Ubicacion
{
    public Guid Id { get; set; }
    public Guid IdOrganizacion { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activa { get; set; } = true;
    public override string ToString() => Nombre;
}

/// <summary>
/// Activo informático administrado por el sistema.
///
/// <see cref="IdResponsable"/> apunta a un usuario de la base de servicio y es
/// una referencia lógica, sin clave foránea.
/// </summary>
public class Equipo
{
    public Guid Id { get; set; }
    public Guid IdOrganizacion { get; set; }

    /// <summary>Código interno de inventario. Único dentro de la organización.</summary>
    public string Codigo { get; set; } = string.Empty;

    public Guid IdTipoEquipo { get; set; }
    public TipoEquipo? Tipo { get; set; }

    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? NumeroSerie { get; set; }
    public string? DescripcionTecnica { get; set; }

    public DateTime FechaAlta { get; set; }
    public DateOnly? FechaAdquisicion { get; set; }
    public DateOnly? FechaFinGarantia { get; set; }
    public string? Proveedor { get; set; }

    /// <summary>
    /// De 1 a 4. Pondera el scoring: la misma cantidad de fallas pesa distinto en
    /// el servidor que en un monitor de recepción.
    /// </summary>
    public int Criticidad { get; set; } = 2;

    public Guid IdEstadoEquipo { get; set; }
    public EstadoEquipo? Estado { get; set; }

    public Guid? IdUbicacion { get; set; }
    public Ubicacion? Ubicacion { get; set; }

    public Guid? IdResponsable { get; set; }

    public string MarcaModelo =>
        string.Join(' ', new[] { Marca, Modelo }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public bool GarantiaVencida =>
        FechaFinGarantia is { } fin && fin < DateOnly.FromDateTime(DateTime.Today);

    public int? DiasParaVencimientoGarantia =>
        FechaFinGarantia is { } fin
            ? fin.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber
            : null;

    public int? AntiguedadEnMeses =>
        FechaAdquisicion is { } compra
            ? (int)((DateOnly.FromDateTime(DateTime.Today).DayNumber - compra.DayNumber) / 30.44)
            : null;

    public override string ToString() => Codigo;
}
