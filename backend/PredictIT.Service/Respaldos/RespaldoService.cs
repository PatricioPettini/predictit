using Microsoft.Data.SqlClient;

namespace PredictIT.Service.Respaldos;

public sealed class OpcionesRespaldo
{
    /// <summary>Ruta vista desde adentro del contenedor de SQL Server.</summary>
    public string RutaContenedor { get; init; } = "/var/opt/mssql/backups";

    /// <summary>Minutos que puede tardar un respaldo antes de darse por fallido.</summary>
    public int TimeoutMinutos { get; init; } = 10;
}

public sealed record ResultadoRespaldo(
    bool Ok,
    string Base,
    string NombreArchivo,
    string Ruta,
    long? TamanoBytes,
    string? Detalle);

public interface IRespaldoService
{
    IReadOnlyList<string> BasesQueRespalda { get; }

    ResultadoRespaldo Respaldar(string nombreBase);

    /// <summary>
    /// Restaura una base desde un archivo. Es destructivo: reemplaza los datos
    /// actuales por los del respaldo.
    /// </summary>
    ResultadoRespaldo Restaurar(string nombreBase, string rutaArchivo);
}

/// <summary>
/// Respaldo y restauración de las dos bases (Req. Arq. 003).
///
/// Usa `BACKUP DATABASE` y `RESTORE DATABASE` de SQL Server, no una exportación
/// propia: es el mecanismo que garantiza consistencia transaccional del punto en
/// el tiempo, y el único que permite restaurar sin reconstruir el esquema.
///
/// La ruta es la del contenedor. El archivo lo escribe el motor, no la
/// aplicación, así que tiene que ser una ruta que el motor pueda ver — y por eso
/// no sirve una carpeta del host que el contenedor no tenga montada.
///
/// Este servicio tiene una relevancia particular en este sistema: el valor del
/// análisis predictivo depende del historial técnico acumulado, y perderlo no es
/// perder registros administrativos sino la capacidad de anticipar fallas.
/// </summary>
public class RespaldoService(ConexionesMaestro conexiones, OpcionesRespaldo opciones)
    : IRespaldoService
{
    /// <summary>
    /// Las dos bases del sistema. Están acá y no se descubren de la conexión
    /// porque respaldar una y no la otra deja el sistema inconsistente: los
    /// usuarios y sus permisos viven en una, y los datos que referencian en la
    /// otra.
    /// </summary>
    private static readonly string[] Bases = ["PredictIT_Negocio", "PredictIT_Servicio"];

    public IReadOnlyList<string> BasesQueRespalda => Bases;

    public ResultadoRespaldo Respaldar(string nombreBase)
    {
        ValidarNombre(nombreBase);

        var archivo = $"{nombreBase}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
        var ruta = $"{opciones.RutaContenedor}/{archivo}";

        try
        {
            // COMPRESSION reduce el archivo a una fracción; CHECKSUM hace que el
            // motor verifique las páginas al escribir, de modo que un respaldo
            // corrupto se detecta al hacerlo y no al necesitarlo.
            Ejecutar($@"
                BACKUP DATABASE [{nombreBase}]
                TO DISK = @ruta
                WITH FORMAT, INIT, COMPRESSION, CHECKSUM,
                     NAME = @nombre,
                     DESCRIPTION = 'Respaldo de PredictIT';",
                new SqlParameter("@ruta", ruta),
                new SqlParameter("@nombre", $"{nombreBase} full"));

            // Se verifica lo que se acaba de escribir. Un respaldo que no se
            // puede leer es peor que no tener respaldo, porque da confianza.
            Ejecutar("RESTORE VERIFYONLY FROM DISK = @ruta WITH CHECKSUM;",
                new SqlParameter("@ruta", ruta));

            return new ResultadoRespaldo(true, nombreBase, archivo, ruta, TamanoDe(ruta), null);
        }
        catch (SqlException ex)
        {
            return new ResultadoRespaldo(false, nombreBase, archivo, ruta, null, ex.Message);
        }
    }

    public ResultadoRespaldo Restaurar(string nombreBase, string rutaArchivo)
    {
        ValidarNombre(nombreBase);

        if (string.IsNullOrWhiteSpace(rutaArchivo))
            throw new ArgumentException("Hay que indicar el archivo a restaurar.", nameof(rutaArchivo));

        var archivo = Path.GetFileName(rutaArchivo);

        try
        {
            // Restaurar exige que nadie más esté conectado. SINGLE_USER con
            // ROLLBACK IMMEDIATE corta las sesiones abiertas: es destructivo y
            // deliberado, porque sin eso el RESTORE falla y deja la base en un
            // estado peor que el inicial.
            Ejecutar($"ALTER DATABASE [{nombreBase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;");

            try
            {
                Ejecutar($"RESTORE DATABASE [{nombreBase}] FROM DISK = @ruta WITH REPLACE;",
                    new SqlParameter("@ruta", rutaArchivo));
            }
            finally
            {
                // Vuelve a multiusuario pase lo que pase. Dejarla en SINGLE_USER
                // tras un error dejaría el sistema caído para todos.
                try { Ejecutar($"ALTER DATABASE [{nombreBase}] SET MULTI_USER;"); }
                catch (SqlException) { /* si el RESTORE la dejó en línea, ya está */ }
            }

            return new ResultadoRespaldo(true, nombreBase, archivo, rutaArchivo, null, null);
        }
        catch (SqlException ex)
        {
            return new ResultadoRespaldo(false, nombreBase, archivo, rutaArchivo, null, ex.Message);
        }
    }

    /// <summary>
    /// El nombre de la base va interpolado en el SQL porque `BACKUP DATABASE` no
    /// admite parametrizar el nombre del objeto. Por eso se valida contra una
    /// lista blanca en lugar de escapar: es la única forma de garantizar que no
    /// entre nada más que uno de los dos valores previstos.
    /// </summary>
    private static void ValidarNombre(string nombreBase)
    {
        if (!Bases.Contains(nombreBase, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"«{nombreBase}» no es una de las bases del sistema.", nameof(nombreBase));
        }
    }

    private long? TamanoDe(string ruta)
    {
        try
        {
            // El archivo está dentro del contenedor, así que el tamaño se le
            // pregunta al motor y no al sistema de archivos de la aplicación.
            using var cn = new SqlConnection(conexiones.Maestro);
            cn.Open();
            // `compressed_backup_size` primero: con COMPRESSION activada,
            // `backup_size` es el tamaño de los datos SIN comprimir. Informarlo
            // haría que la pantalla dijera 7,5 MB de un archivo de 700 KB.
            using var cmd = new SqlCommand(
                "SELECT COALESCE(compressed_backup_size, backup_size) FROM msdb.dbo.backupset " +
                "WHERE backup_set_id = (SELECT MAX(backup_set_id) FROM msdb.dbo.backupset);", cn);
            var valor = cmd.ExecuteScalar();
            return valor is null or DBNull ? null : Convert.ToInt64(valor);
        }
        catch (SqlException)
        {
            // El tamaño es informativo: si no se puede averiguar, el respaldo
            // sigue siendo válido.
            return null;
        }
    }

    private void Ejecutar(string sql, params SqlParameter[] parametros)
    {
        using var cn = new SqlConnection(conexiones.Maestro);
        cn.Open();
        using var cmd = new SqlCommand(sql, cn)
        {
            CommandTimeout = opciones.TimeoutMinutos * 60
        };
        cmd.Parameters.AddRange(parametros);
        cmd.ExecuteNonQuery();
    }
}

/// <summary>
/// Cadena de conexión a `master`.
///
/// Va aparte de las dos del sistema porque `BACKUP` y `RESTORE` no se pueden
/// ejecutar desde la base que se está respaldando: hay que estar conectado a
/// otra. Se deriva de la de negocio cambiándole el catálogo inicial, así no hay
/// una tercera credencial que mantener.
/// </summary>
public sealed class ConexionesMaestro
{
    public ConexionesMaestro(string cadenaNegocio)
    {
        var b = new SqlConnectionStringBuilder(cadenaNegocio) { InitialCatalog = "master" };
        Maestro = b.ConnectionString;
    }

    public string Maestro { get; }
}
