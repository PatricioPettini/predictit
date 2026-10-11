using System.Reflection;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using PredictIT.Domain.Seguridad;
using PredictIT.Tests.Infraestructura;

namespace PredictIT.Tests.Seguridad;

/// <summary>
/// El catálogo de tipos de evento tiene que cubrir todos los códigos que el
/// sistema asienta (Req. Arq. 002).
///
/// Existe por un defecto real: ocho de los nueve códigos que usaba el negocio no
/// estaban en <c>TipoEvento</c>, y <c>BitacoraService</c> resuelve el código
/// contra ese catálogo y, cuando no lo encuentra, avisa al logger y no escribe.
/// El resultado era una bitácora con los eventos de seguridad y ninguno de
/// negocio, sin un solo error visible. Un código nuevo mal escrito volvería a
/// producir exactamente eso, en silencio: esta prueba lo convierte en un fallo.
/// </summary>
[Trait("Categoria", "Infraestructura")]
public class BitacoraCatalogoTests
{
    public static TheoryData<string> CodigosDelSistema()
    {
        var datos = new TheoryData<string>();

        foreach (var campo in typeof(TipoEvento.Codigos)
                     .GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f.IsLiteral && f.FieldType == typeof(string)))
        {
            datos.Add((string)campo.GetRawConstantValue()!);
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(CodigosDelSistema))]
    public async Task Cada_codigo_declarado_existe_en_el_catalogo(string codigo)
    {
        await using var cn = new SqlConnection(EntornoPruebas.CadenaServicio);
        await cn.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.TipoEvento WHERE codigo = @codigo;", cn);
        cmd.Parameters.AddWithValue("@codigo", codigo);

        var encontrados = (int)(await cmd.ExecuteScalarAsync())!;

        encontrados.Should().Be(1,
            $"el código {codigo} se asienta desde el código pero no está en TipoEvento, " +
            "así que ese evento se descarta en silencio");
    }

    /// <summary>
    /// La otra mitad del control: que no queden literales sueltos en las
    /// llamadas a la bitácora. Una constante mal escrita la agarra el
    /// compilador; un literal mal escrito, no.
    /// </summary>
    [Fact]
    public void El_negocio_no_asienta_con_literales_sueltos()
    {
        var raiz = BuscarRaiz();
        var infractores = new List<string>();

        foreach (var archivo in Directory.EnumerateFiles(
                     Path.Combine(raiz, "PredictIT.BLL"), "*.cs", SearchOption.AllDirectories))
        {
            var lineas = File.ReadAllLines(archivo);

            for (var i = 0; i < lineas.Length; i++)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        lineas[i], @"bitacora\.Registrar\(""[A-Z_]+"""))
                {
                    infractores.Add($"{Path.GetFileName(archivo)}:{i + 1}");
                }
            }
        }

        infractores.Should().BeEmpty(
            "las llamadas a la bitácora tienen que usar TipoEvento.Codigos, " +
            "que es lo que hace que un código inexistente no compile");
    }

    private static string BuscarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "PredictIT.BLL")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del backend.");
    }
}
