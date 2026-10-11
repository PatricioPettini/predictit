using Microsoft.Data.SqlClient;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Helpers;
using PredictIT.Domain.Seguridad;

namespace PredictIT.DAO.Implementations;

/// <summary>
/// Idiomas y traducciones (Req. Arq. 001).
///
/// Vive en la base de servicio, no en la de negocio: el idioma es una
/// preferencia del usuario y los usuarios están allá.
///
/// No recibe contexto de sesión, y eso es deliberado: el diccionario tiene que
/// poder servirse **antes** de que haya sesión, porque la pantalla de inicio de
/// sesión también se traduce.
/// </summary>
public class IdiomaDao(SqlHelperSeguridad sql) : IIdiomaDao
{
    public IList<Idioma> Idiomas(bool soloActivos = true) => sql.Consultar(@"
        SELECT id_idioma, codigo, nombre, activo, es_default
        FROM dbo.Idioma
        WHERE (@soloActivos = 0 OR activo = 1)
        ORDER BY es_default DESC, nombre;",
        f => new Idioma
        {
            Id = f.Guid("id_idioma"),
            Codigo = f.Texto("codigo"),
            Nombre = f.Texto("nombre"),
            Activo = f.Booleano("activo"),
            EsPorDefecto = f.Booleano("es_default")
        },
        new SqlParameter("@soloActivos", soloActivos ? 1 : 0));

    public Idioma? PorCodigo(string codigo) => sql.ConsultarUno(@"
        SELECT id_idioma, codigo, nombre, activo, es_default
        FROM dbo.Idioma WHERE codigo = @codigo;",
        f => new Idioma
        {
            Id = f.Guid("id_idioma"),
            Codigo = f.Texto("codigo"),
            Nombre = f.Texto("nombre"),
            Activo = f.Booleano("activo"),
            EsPorDefecto = f.Booleano("es_default")
        },
        new SqlParameter("@codigo", codigo));

    public Idioma? PorDefecto() => sql.ConsultarUno(@"
        SELECT TOP 1 id_idioma, codigo, nombre, activo, es_default
        FROM dbo.Idioma WHERE activo = 1 ORDER BY es_default DESC, nombre;",
        f => new Idioma
        {
            Id = f.Guid("id_idioma"),
            Codigo = f.Texto("codigo"),
            Nombre = f.Texto("nombre"),
            Activo = f.Booleano("activo"),
            EsPorDefecto = f.Booleano("es_default")
        });

    /// <summary>
    /// El diccionario completo de un idioma, en una sola consulta.
    ///
    /// Se trae entero y no clave por clave: son del orden de doscientas filas y
    /// la interfaz necesita todas para dibujar la primera pantalla. Pedirlas de
    /// a una serían doscientos viajes al servidor por cada carga.
    /// </summary>
    public IDictionary<string, string> Diccionario(Guid idIdioma)
    {
        var filas = sql.Consultar(
            "SELECT clave, texto FROM dbo.Traduccion WHERE id_idioma = @id;",
            f => (Clave: f.Texto("clave"), Texto: f.Texto("texto")),
            new SqlParameter("@id", idIdioma));

        return filas.ToDictionary(x => x.Clave, x => x.Texto, StringComparer.Ordinal);
    }

    public void CambiarIdiomaDeUsuario(Guid idUsuario, Guid idIdioma) => sql.EjecutarNoConsulta(
        "UPDATE dbo.Usuario SET id_idioma = @idioma WHERE id_usuario = @usuario;",
        new SqlParameter("@usuario", idUsuario), new SqlParameter("@idioma", idIdioma));

    /// <summary>
    /// Claves que existen en algún idioma pero faltan en otro.
    ///
    /// Es el control que hace medible la cobertura de traducción. Sin esto, una
    /// clave sin traducir sólo se descubre cuando alguien cambia de idioma y ve
    /// un texto en el idioma equivocado.
    /// </summary>
    public IList<(string Clave, string CodigoIdioma)> ClavesFaltantes() => sql.Consultar(@"
        SELECT c.clave, i.codigo
        FROM (SELECT DISTINCT clave FROM dbo.Traduccion) c
        CROSS JOIN dbo.Idioma i
        WHERE i.activo = 1
          AND NOT EXISTS (SELECT 1 FROM dbo.Traduccion t
                          WHERE t.clave = c.clave AND t.id_idioma = i.id_idioma)
        ORDER BY i.codigo, c.clave;",
        f => (f.Texto("clave"), f.Texto("codigo")));
}
