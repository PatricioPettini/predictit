namespace PredictIT.Domain.Negocio;

/// <summary>Tipos de regla que el motor sabe evaluar (patrón Strategy).</summary>
public enum TipoRegla
{
    RecurrenciaFallas,
    MantenimientoVencido,
    AcumulacionIncidencias,
    AntiguedadEquipo,
    GarantiaPorVencer,
}

public static class TipoReglaTexto
{
    public static string A(TipoRegla t) => t switch
    {
        TipoRegla.RecurrenciaFallas => "RECURRENCIA_FALLAS",
        TipoRegla.MantenimientoVencido => "MANTENIMIENTO_VENCIDO",
        TipoRegla.AcumulacionIncidencias => "ACUMULACION_INCIDENCIAS",
        TipoRegla.AntiguedadEquipo => "ANTIGUEDAD_EQUIPO",
        TipoRegla.GarantiaPorVencer => "GARANTIA_POR_VENCER",
        _ => throw new ArgumentOutOfRangeException(nameof(t)),
    };

    /// <summary>
    /// Devuelve null si el texto no corresponde a ningún tipo conocido. No lanza:
    /// el valor viene de la base y una regla de un tipo que este código no
    /// conoce —por ejemplo tras un rollback de versión— se saltea en lugar de
    /// tumbar la evaluación de todo el parque.
    /// </summary>
    public static TipoRegla? De(string? valor) => valor?.Trim().ToUpperInvariant() switch
    {
        "RECURRENCIA_FALLAS" => TipoRegla.RecurrenciaFallas,
        "MANTENIMIENTO_VENCIDO" => TipoRegla.MantenimientoVencido,
        "ACUMULACION_INCIDENCIAS" => TipoRegla.AcumulacionIncidencias,
        "ANTIGUEDAD_EQUIPO" => TipoRegla.AntiguedadEquipo,
        "GARANTIA_POR_VENCER" => TipoRegla.GarantiaPorVencer,
        _ => null,
    };
}

/// <summary>
/// Regla de alerta configurada por la organización (RF-11).
///
/// <see cref="Condicion"/> guarda los parámetros en JSON porque cada tipo de
/// regla tiene los suyos. La alternativa era una tabla de parámetros por tipo,
/// que multiplica tablas sin agregar nada: nadie consulta esos valores por
/// separado, se leen siempre junto con la regla.
/// </summary>
public class ReglaAlerta
{
    public Guid Id { get; set; }
    public Guid IdOrganizacion { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public TipoRegla Tipo { get; set; }

    /// <summary>Parámetros en JSON. Las claves dependen del <see cref="Tipo"/>.</summary>
    public string Condicion { get; set; } = "{}";

    /// <summary>Peso relativo, 0 a 100. Se normaliza contra el total de reglas aplicables.</summary>
    public int Peso { get; set; } = 25;

    public bool Activa { get; set; } = true;

    public override string ToString() => Nombre;
}

public class EstadoAlerta
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool EsFinal { get; set; }
    public override string ToString() => Nombre;
}

/// <summary>Alerta generada por una regla que se cumplió (RF-12).</summary>
public class AlertaPredictiva
{
    public Guid Id { get; set; }
    public Guid IdOrganizacion { get; set; }

    public Guid IdEquipo { get; set; }
    public Equipo? Equipo { get; set; }

    /// <summary>Regla que la originó. Es lo que hace auditable la alerta.</summary>
    public Guid IdRegla { get; set; }
    public ReglaAlerta? Regla { get; set; }

    public Guid IdEstado { get; set; }
    public EstadoAlerta? Estado { get; set; }

    public DateTime FechaGeneracion { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string? Recomendacion { get; set; }

    /// <summary>Score del equipo al momento de generarla, 0 a 100.</summary>
    public int NivelRiesgo { get; set; }

    public DateTime? FechaAtencion { get; set; }
    public Guid? IdUsuarioAtencion { get; set; }

    public bool Atendida => FechaAtencion is not null;

    public override string ToString() => Motivo;
}

public enum NivelRiesgo
{
    Bajo,
    Medio,
    Alto,
}

public static class NivelRiesgoTexto
{
    /// <summary>
    /// Cortes del score. Están acá y no repartidos por el código porque son la
    /// definición de «riesgo alto» del sistema: si cambian, cambian en un lugar.
    /// </summary>
    public const int CorteMedio = 40;
    public const int CorteAlto = 70;

    public static NivelRiesgo De(int score) =>
        score >= CorteAlto ? NivelRiesgo.Alto
        : score >= CorteMedio ? NivelRiesgo.Medio
        : NivelRiesgo.Bajo;

    public static string A(NivelRiesgo n) => n switch
    {
        NivelRiesgo.Alto => "ALTO",
        NivelRiesgo.Medio => "MEDIO",
        _ => "BAJO",
    };

    public static NivelRiesgo Parsear(string? valor) => valor?.ToUpperInvariant() switch
    {
        "ALTO" => NivelRiesgo.Alto,
        "MEDIO" => NivelRiesgo.Medio,
        _ => NivelRiesgo.Bajo,
    };
}

/// <summary>Evaluación de riesgo de un equipo, con el aporte de cada regla.</summary>
public class EvaluacionRiesgo
{
    public Guid Id { get; set; }
    public Guid IdOrganizacion { get; set; }
    public Guid IdEquipo { get; set; }
    public DateTime Fecha { get; set; }
    public int Score { get; set; }
    public NivelRiesgo Nivel { get; set; }

    /// <summary>JSON con el aporte de cada regla. Es la explicación del score.</summary>
    public string? Detalle { get; set; }
}
