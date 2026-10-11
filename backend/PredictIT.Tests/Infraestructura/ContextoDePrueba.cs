using Microsoft.Data.SqlClient;
using PredictIT.DAO.Contracts;
using PredictIT.DAO.Helpers;

namespace PredictIT.Tests.Infraestructura;

/// <summary>Contexto de sesión armado a mano, para probar los DAO sin API.</summary>
public class ContextoDePrueba : IContextoSesion
{
    public ContextoDePrueba(Guid idOrganizacion, params string[] patentes)
    {
        IdOrganizacion = idOrganizacion;
        Patentes = new HashSet<string>(patentes, StringComparer.OrdinalIgnoreCase);
    }

    public Guid IdUsuario { get; init; } = Datos.Admin;
    public string Username { get; init; } = "admin";
    public Guid IdOrganizacion { get; }
    public Guid? IdIdioma { get; init; }
    public HashSet<string> Patentes { get; }

    public bool Tiene(string dataKey) => Patentes.Contains(dataKey);

    /// <summary>Contexto con todos los permisos, para lo que no está probando permisos.</summary>
    public static ContextoDePrueba TodoPermitido(Guid idOrganizacion) =>
        new(idOrganizacion,
            "EQUIPO_VER", "EQUIPO_GESTIONAR", "EQUIPO_CAMBIAR_ESTADO",
            "ORGANIZACION_GESTIONAR", "BITACORA_VER", "DASHBOARD_VER");
}

/// <summary>
/// Identificadores de los datos iniciales.
///
/// Son fijos y legibles en los scripts de <c>db/</c> justamente para poder
/// referenciarlos desde las pruebas sin tener que buscarlos primero.
/// </summary>
public static class Datos
{
    public static readonly Guid Estudio = Guid.Parse("A0000000-0000-0000-0000-000000000001");
    public static readonly Guid Clinica = Guid.Parse("A0000000-0000-0000-0000-000000000002");

    public static readonly Guid Admin = Guid.Parse("B0000000-0000-0000-0000-000000000001");
    public static readonly Guid Tecnico1 = Guid.Parse("B0000000-0000-0000-0000-000000000002");
    public static readonly Guid Solicitante = Guid.Parse("B0000000-0000-0000-0000-000000000005");
    public static readonly Guid Partner = Guid.Parse("B0000000-0000-0000-0000-000000000006");

    public static readonly Guid PcAdm014 = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid NbCom007 = Guid.Parse("30000000-0000-0000-0000-000000000002");

    public static readonly Guid TipoPc = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid EstadoOperativo = Guid.Parse("21000000-0000-0000-0000-000000000001");
    public static readonly Guid UbicAdministracion = Guid.Parse("28000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Identificadores de las filas que crean las pruebas de integración.
    ///
    /// Son fijos para que cada corrida sobreescriba la fila de la anterior en
    /// lugar de agregar una nueva: la base de demostración es la misma que se
    /// usa para mostrar el sistema, y no tiene por qué llenarse de residuos de
    /// las pruebas.
    /// </summary>
    public static readonly Guid ReglaDePrueba = Guid.Parse("4F000000-0000-0000-0000-0000000000AA");
    public static readonly Guid ReglaInactivaDePrueba = Guid.Parse("4F000000-0000-0000-0000-0000000000AB");

    public static ConexionesSql Conexiones() =>
        new(EntornoPruebas.CadenaNegocio, EntornoPruebas.CadenaServicio);

    /// <summary>
    /// Borra las filas que dejan las pruebas de integración.
    ///
    /// Existe porque la base de demostración es la misma que se usa para mostrar
    /// el sistema: sin esto, cada corrida de la suite dejaba una incidencia
    /// «Prueba de alta desde el DAO» y el listado del solicitante terminaba con
    /// más residuos de pruebas que datos reales.
    ///
    /// Va por SQL directo y no por el DAO a propósito: el DAO **no** expone el
    /// borrado de incidencias, y está bien que no lo haga — son el historial que
    /// alimenta el análisis predictivo. Esto es limpieza de pruebas, no una
    /// operación del sistema.
    /// </summary>
    public static void LimpiarResiduosDePrueba()
    {
        var sql = new SqlHelper(Conexiones());
        SqlParameter Patron() => new("@patron", "Prueba de alta desde el DAO%");

        // El orden importa: las tablas hijas primero, o la foránea lo rechaza.
        sql.EjecutarNoConsulta(@"
            DELETE FROM dbo.ClasificacionIncidencia
            WHERE id_incidencia IN (SELECT id_incidencia FROM dbo.Incidencia WHERE titulo LIKE @patron);",
            Patron());

        sql.EjecutarNoConsulta(@"
            DELETE FROM dbo.RecomendacionAsignacion
            WHERE id_incidencia IN (SELECT id_incidencia FROM dbo.Incidencia WHERE titulo LIKE @patron);",
            Patron());

        sql.EjecutarNoConsulta("DELETE FROM dbo.Incidencia WHERE titulo LIKE @patron;", Patron());

        // Las tres de abajo cuelgan de una incidencia o de un equipo de la
        // semilla, no de una fila que la prueba haya creado, así que el patrón
        // del título no las alcanza. Se borran por su propia marca.
        //
        // Sin esto la suite crecía una fila por tabla en cada corrida. No
        // rompía ninguna prueba —por eso pasó inadvertido— pero una prueba que
        // deja filas contamina a la que después cuenta, y el día que falle
        // nadie va a saber si es un defecto o basura de otra prueba.
        SqlParameter Justificacion() => new("@justificacion", "Prueba de%");

        sql.EjecutarNoConsulta(
            "DELETE FROM dbo.ClasificacionIncidencia WHERE justificacion LIKE @justificacion;",
            Justificacion());

        sql.EjecutarNoConsulta(
            "DELETE FROM dbo.RecomendacionAsignacion WHERE justificacion LIKE @justificacion;",
            Justificacion());

        sql.EjecutarNoConsulta(
            "DELETE FROM dbo.EvaluacionRiesgo WHERE detalle LIKE '%\"regla\":\"Prueba\"%';");

        // Las dos reglas que crean las pruebas usan identificadores fijos, así
        // que no se acumulan. Se borran igual porque quedan a la vista en la
        // pantalla de reglas de la base de demostración, que es la que se
        // muestra en la defensa y de la que salen las capturas del documento.
        sql.EjecutarNoConsulta(
            "DELETE FROM dbo.AlertaPredictiva WHERE id_regla IN (@regla, @inactiva);",
            new SqlParameter("@regla", ReglaDePrueba),
            new SqlParameter("@inactiva", ReglaInactivaDePrueba));

        sql.EjecutarNoConsulta(
            "DELETE FROM dbo.ReglaAlerta WHERE id_regla IN (@regla, @inactiva);",
            new SqlParameter("@regla", ReglaDePrueba),
            new SqlParameter("@inactiva", ReglaInactivaDePrueba));
    }
}
