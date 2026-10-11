using System.Data;
using Microsoft.Data.SqlClient;

namespace PredictIT.DAO.Helpers;

/// <summary>
/// Las dos cadenas de conexión del sistema.
///
/// Están separadas a propósito y no por comodidad: un DAO de negocio no tiene
/// forma de alcanzar la base de seguridad, ni al revés. Ése es el punto de tener
/// dos bases (ADR 0003).
/// </summary>
public sealed class ConexionesSql
{
    public ConexionesSql(string negocio, string servicio)
    {
        if (string.IsNullOrWhiteSpace(negocio))
            throw new ArgumentException("Falta la cadena de conexión de negocio.", nameof(negocio));
        if (string.IsNullOrWhiteSpace(servicio))
            throw new ArgumentException("Falta la cadena de conexión de servicio.", nameof(servicio));

        Negocio = negocio;
        Servicio = servicio;
    }

    public string Negocio { get; }
    public string Servicio { get; }

    /// <summary>
    /// Las mismas dos bases, pero con pool propio y acotado, para los procesos
    /// en segundo plano (bulkhead, ADR 0009).
    ///
    /// El pool de ADO.NET se identifica por la cadena de conexión completa: dos
    /// cadenas que difieren en un solo parámetro usan pools distintos. De ahí
    /// que alcance con agregar <c>Max Pool Size</c> y un nombre de aplicación
    /// propio —que además hace visible en <c>sys.dm_exec_sessions</c> quién está
    /// ocupando cada conexión—.
    ///
    /// Lo que esto compra: una evaluación del parque completo puede agotar sus
    /// cinco conexiones y quedarse esperando las suyas, sin poder tocar las que
    /// atienden a los usuarios. Sin esto, un proceso de fondo largo deja la
    /// aplicación lenta y el motivo no aparece en ningún log.
    /// </summary>
    public ConexionesSql ParaSegundoPlano(int maxConexiones = 5) =>
        new(ConPool(Negocio, maxConexiones, "PredictIT-Fondo"),
            ConPool(Servicio, maxConexiones, "PredictIT-Fondo"));

    private static string ConPool(string cadena, int maximo, string aplicacion)
    {
        var constructor = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cadena)
        {
            MaxPoolSize = maximo,
            ApplicationName = aplicacion
        };

        return constructor.ConnectionString;
    }
}

/// <summary>
/// Ejecución de sentencias contra una base. No decide contra cuál: la cadena se
/// la da quien lo construye, y de ahí salen las dos variantes concretas.
///
/// Todo parámetro va por <see cref="SqlParameter"/>. No se concatena entrada de
/// usuario en una sentencia bajo ninguna circunstancia; es la mitigación de
/// inyección SQL de todo el sistema (ADR 0004).
/// </summary>
public abstract class SqlHelperBase
{
    protected SqlHelperBase(string cadenaConexion)
    {
        CadenaConexion = cadenaConexion;
    }

    protected string CadenaConexion { get; }

    public SqlConnection AbrirConexion()
    {
        var cn = new SqlConnection(CadenaConexion);
        cn.Open();
        return cn;
    }

    public int EjecutarNoConsulta(string sql, params SqlParameter[] parametros)
    {
        using var cn = AbrirConexion();
        using var cmd = Comando(cn, null, sql, parametros);
        return cmd.ExecuteNonQuery();
    }

    public object? EjecutarEscalar(string sql, params SqlParameter[] parametros)
    {
        using var cn = AbrirConexion();
        using var cmd = Comando(cn, null, sql, parametros);
        return cmd.ExecuteScalar();
    }

    /// <summary>
    /// Lee un conjunto de filas y las proyecta con el mapper.
    ///
    /// Devuelve una lista ya materializada y no un <see cref="IDataReader"/>: si
    /// devolviera el lector, la conexión tendría que seguir abierta más allá de
    /// este método y quedaría en manos del llamador cerrarla. Con el volumen del
    /// sistema (RNF-04: 500 activos, 5000 incidencias) el costo de materializar
    /// es irrelevante frente al riesgo de una conexión perdida.
    /// </summary>
    public IList<T> Consultar<T>(string sql, Func<IDataRecord, T> mapear, params SqlParameter[] parametros)
    {
        var resultado = new List<T>();
        using var cn = AbrirConexion();
        using var cmd = Comando(cn, null, sql, parametros);
        using var lector = cmd.ExecuteReader();
        while (lector.Read())
        {
            resultado.Add(mapear(lector));
        }
        return resultado;
    }

    public T? ConsultarUno<T>(string sql, Func<IDataRecord, T> mapear, params SqlParameter[] parametros)
        where T : class
        => Consultar(sql, mapear, parametros).FirstOrDefault();

    /// <summary>Variante para usar dentro de una transacción abierta por el Unit of Work.</summary>
    public int EjecutarNoConsultaEnTransaccion(SqlConnection cn, SqlTransaction tx, string sql,
                                               params SqlParameter[] parametros)
    {
        using var cmd = Comando(cn, tx, sql, parametros);
        return cmd.ExecuteNonQuery();
    }

    private static SqlCommand Comando(SqlConnection cn, SqlTransaction? tx, string sql,
                                      SqlParameter[] parametros)
    {
        var cmd = new SqlCommand(sql, cn) { CommandType = CommandType.Text };
        if (tx is not null) cmd.Transaction = tx;
        foreach (var p in parametros)
        {
            // ADO.NET no acepta null de C#: hay que traducirlo a DBNull.
            p.Value ??= DBNull.Value;
            cmd.Parameters.Add(p);
        }
        return cmd;
    }
}

/// <summary>Acceso a <c>PredictIT_Negocio</c>.</summary>
public sealed class SqlHelper : SqlHelperBase
{
    public SqlHelper(ConexionesSql conexiones) : base(conexiones.Negocio) { }
}

/// <summary>Acceso a <c>PredictIT_Servicio</c>.</summary>
public sealed class SqlHelperSeguridad : SqlHelperBase
{
    public SqlHelperSeguridad(ConexionesSql conexiones) : base(conexiones.Servicio) { }
}
