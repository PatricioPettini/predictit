using PredictIT.Service.IA;
using Prometheus;

namespace PredictIT.Api.Infraestructura;

/// <summary>
/// Las métricas propias del sistema, en formato Prometheus (ADR 0015).
///
/// Las de HTTP y las del runtime las aporta la biblioteca sin que haya que
/// escribir nada. Acá viven las tres que el trabajo necesita y nadie más puede
/// conocer: el tiempo en línea, que es lo que hace verificable el RNF-05, y el
/// estado de los dos mecanismos de resiliencia que el ADR 0009 incorporó.
///
/// Los nombres van en castellano, como el resto del sistema, con el prefijo
/// `predictit_` que es la convención de Prometheus para separar lo propio de lo
/// que aporta la biblioteca.
/// </summary>
public static class Metricas
{
    /// <summary>
    /// Cuándo arrancó el proceso.
    ///
    /// Se guarda el instante y no un contador que se incrementa: un contador
    /// necesita que algo lo mueva, y si ese algo deja de correr la métrica
    /// miente hacia arriba. Restar contra el reloj no puede quedar desfasado.
    /// </summary>
    private static readonly DateTime Arranque = DateTime.UtcNow;

    /// <summary>
    /// Segundos desde que arrancó el proceso (RNF-05).
    ///
    /// Sirve para calcular disponibilidad junto con el histórico del
    /// recolector: cada vez que este número baja, hubo un reinicio, y el hueco
    /// entre dos recolecciones es el tiempo que el sistema estuvo caído.
    /// </summary>
    private static readonly Gauge TiempoEnLinea = Prometheus.Metrics.CreateGauge(
        "predictit_tiempo_en_linea_segundos",
        "Segundos transcurridos desde que arrancó el proceso.");

    /// <summary>
    /// Circuitos abiertos en este momento, de los del ADR 0009.
    ///
    /// Es un gauge y no un contador de aperturas porque el disyuntor no avisa
    /// cuando cambia de estado: se lo consulta. Con una recolección cada quince
    /// segundos, las transiciones de esta serie dan igual las aperturas y el
    /// tiempo que estuvo abierto, que es lo que se quería saber.
    /// </summary>
    private static readonly Gauge CircuitosAbiertos = Prometheus.Metrics.CreateGauge(
        "predictit_disyuntores_abiertos",
        "Organizaciones cuyo circuito hacia el proveedor de IA no está cerrado.");

    /// <summary>
    /// Peticiones que el limitador rechazó, por la regla que las frenó.
    ///
    /// Éste sí es un contador: el rechazo es un hecho puntual y el limitador
    /// avisa cuando ocurre, así que no hace falta preguntarle.
    /// </summary>
    public static readonly Counter RechazosPorLimite = Prometheus.Metrics.CreateCounter(
        "predictit_peticiones_limitadas_total",
        "Peticiones rechazadas por el limitador de velocidad.",
        new CounterConfiguration { LabelNames = ["politica"] });

    /// <summary>
    /// Pone al día lo que hay que preguntar, justo antes de servir la página.
    ///
    /// Los gauges calculados se actualizan acá y no con un temporizador: un
    /// temporizador que corre cada cinco segundos gasta aunque nadie esté
    /// mirando, y deja el valor viejo si se traba.
    /// </summary>
    public static void Actualizar(RegistroDisyuntores disyuntores)
    {
        TiempoEnLinea.Set((DateTime.UtcNow - Arranque).TotalSeconds);
        CircuitosAbiertos.Set(disyuntores.Abiertos().Count);
    }
}
