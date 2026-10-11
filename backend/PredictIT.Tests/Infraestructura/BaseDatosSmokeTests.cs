using FluentAssertions;
using Microsoft.Data.SqlClient;

namespace PredictIT.Tests.Infraestructura;

/// <summary>
/// Verificación de que el entorno está armado: las dos bases existen, tienen el
/// esquema aplicado y los datos iniciales cargados.
///
/// Si estas pruebas fallan, no tiene sentido mirar ninguna otra: lo que falta es
/// levantar el contenedor y correr <c>db\aplicar.ps1</c>.
/// </summary>
[Trait("Categoria", "Infraestructura")]
public class BaseDatosSmokeTests
{
    private static async Task<T> EscalarAsync<T>(string cadena, string sql)
    {
        await using var cn = new SqlConnection(cadena);
        await cn.OpenAsync();
        await using var cmd = new SqlCommand(sql, cn);
        var valor = await cmd.ExecuteScalarAsync();
        return (T)Convert.ChangeType(valor!, typeof(T))!;
    }

    [Fact]
    public async Task La_base_de_negocio_responde()
    {
        var uno = await EscalarAsync<int>(EntornoPruebas.CadenaNegocio, "SELECT 1;");
        uno.Should().Be(1);
    }

    [Fact]
    public async Task La_base_de_servicio_responde()
    {
        var uno = await EscalarAsync<int>(EntornoPruebas.CadenaServicio, "SELECT 1;");
        uno.Should().Be(1);
    }

    [Theory]
    [InlineData("Organizacion")]
    [InlineData("Equipo")]
    [InlineData("Incidencia")]
    [InlineData("Mantenimiento")]
    [InlineData("ReglaAlerta")]
    [InlineData("AlertaPredictiva")]
    [InlineData("RecomendacionAsignacion")]
    [InlineData("ClasificacionIncidencia")]
    [InlineData("ConfiguracionAsignacion")]
    [InlineData("EvaluacionRiesgo")]
    public async Task La_tabla_de_negocio_existe(string tabla)
    {
        var existe = await EscalarAsync<int>(
            EntornoPruebas.CadenaNegocio,
            $"SELECT CASE WHEN OBJECT_ID(N'dbo.{tabla}', N'U') IS NULL THEN 0 ELSE 1 END;");

        existe.Should().Be(1, "el esquema de negocio tiene que incluir dbo.{0}", tabla);
    }

    [Theory]
    [InlineData("Usuario")]
    [InlineData("Rol")]
    [InlineData("Familia")]
    [InlineData("Patente")]
    [InlineData("Familia_Familia")]
    [InlineData("Bitacora")]
    [InlineData("TipoEvento")]
    [InlineData("Idioma")]
    [InlineData("Traduccion")]
    [InlineData("Respaldo")]
    public async Task La_tabla_de_servicio_existe(string tabla)
    {
        var existe = await EscalarAsync<int>(
            EntornoPruebas.CadenaServicio,
            $"SELECT CASE WHEN OBJECT_ID(N'dbo.{tabla}', N'U') IS NULL THEN 0 ELSE 1 END;");

        existe.Should().Be(1, "el esquema de servicio tiene que incluir dbo.{0}", tabla);
    }

    [Fact]
    public async Task Los_datos_iniciales_de_seguridad_estan_cargados()
    {
        var usuarios = await EscalarAsync<int>(EntornoPruebas.CadenaServicio, "SELECT COUNT(*) FROM dbo.Usuario;");
        var patentes = await EscalarAsync<int>(EntornoPruebas.CadenaServicio, "SELECT COUNT(*) FROM dbo.Patente;");
        var roles    = await EscalarAsync<int>(EntornoPruebas.CadenaServicio, "SELECT COUNT(*) FROM dbo.Rol;");

        usuarios.Should().BeGreaterThan(0);
        patentes.Should().BeGreaterThan(0);
        roles.Should().Be(4, "son los tres perfiles del documento mas PARTNER");
    }

    [Fact]
    public async Task Ninguna_contrasena_esta_guardada_en_texto_plano()
    {
        var enClaro = await EscalarAsync<int>(
            EntornoPruebas.CadenaServicio,
            "SELECT COUNT(*) FROM dbo.Usuario WHERE [password] NOT LIKE 'PBKDF2$%';");

        enClaro.Should().Be(0);
    }

    [Fact]
    public async Task El_historial_de_demo_alcanza_para_disparar_la_regla_de_recurrencia()
    {
        // La regla RECURRENCIA_FALLAS pide 3 incidencias en 30 dias. Si el seed no
        // deja ningun equipo por encima de ese umbral, el motor predictivo no tiene
        // nada que mostrar y la demo del sistema queda vacia.
        var maximo = await EscalarAsync<int>(EntornoPruebas.CadenaNegocio, """
            SELECT ISNULL(MAX(cantidad), 0) FROM (
                SELECT COUNT(*) AS cantidad
                FROM dbo.Incidencia
                WHERE fecha >= DATEADD(DAY, -30, SYSDATETIME())
                GROUP BY id_equipo
            ) x;
            """);

        maximo.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task La_bitacora_no_admite_modificaciones()
    {
        // Req. Arq. 002: la inmutabilidad se garantiza en la base, no en la aplicacion.
        await using var cn = new SqlConnection(EntornoPruebas.CadenaServicio);
        await cn.OpenAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.Bitacora SET descripcion = 'alterada';", cn);

        var intento = async () => await cmd.ExecuteNonQueryAsync();

        await intento.Should().ThrowAsync<SqlException>()
            .Where(e => e.Message.Contains("inmutable"));
    }
}
