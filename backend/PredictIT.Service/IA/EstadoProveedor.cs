namespace PredictIT.Service.IA;

/// <summary>Lo que se puede observar del proveedor de IA en un momento dado.</summary>
public sealed record ObservacionProveedor(
    string Proveedor,
    EstadoDisyuntor Estado,
    int FallosConsecutivos,
    DateTime? SinRespuestaDesde,
    string Mensaje);

/// <summary>
/// Describe el estado del proveedor de IA.
///
/// Vive acá y no en la capa de negocio porque lo consultan dos business
/// distintos —el de predicción y el de administración— y duplicar el texto en
/// los dos terminaría con dos pantallas diciendo cosas diferentes del mismo
/// estado.
/// </summary>
public static class EstadoProveedor
{
    public static ObservacionProveedor Observar(IProveedorIA proveedor)
    {
        // El disyuntor sólo existe si el proveedor está envuelto en el decorador.
        // Sin él el circuito no aplica, y eso equivale a estar cerrado.
        var disyuntor = (proveedor as ProveedorIAConDisyuntor)?.Disyuntor;
        var estado = disyuntor?.Estado ?? EstadoDisyuntor.Cerrado;
        var desde = disyuntor?.AbiertoDesde;

        return new ObservacionProveedor(
            proveedor.Nombre,
            estado,
            disyuntor?.FallosConsecutivos ?? 0,
            desde,
            Mensaje(estado, desde));
    }

    /// <summary>
    /// Mensaje para el administrador. Un sistema degradado que no lo dice es
    /// peor que uno caído (ADR 0009).
    /// </summary>
    private static string Mensaje(EstadoDisyuntor estado, DateTime? desde) => estado switch
    {
        EstadoDisyuntor.Abierto when desde is { } d =>
            $"Sin respuesta desde hace {Minutos(d)} min: se está aplicando la regla de respaldo.",
        EstadoDisyuntor.Abierto =>
            "Sin respuesta del proveedor: se está aplicando la regla de respaldo.",
        EstadoDisyuntor.Semiabierto =>
            "Se va a probar una consulta para ver si el proveedor volvió.",
        _ => "El proveedor está respondiendo con normalidad.",
    };

    private static int Minutos(DateTime desde) =>
        Math.Max(0, (int)(DateTime.UtcNow - desde).TotalMinutes);
}
