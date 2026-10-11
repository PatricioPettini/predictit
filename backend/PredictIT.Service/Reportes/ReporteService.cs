using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PredictIT.Service.Reportes;

/// <summary>Una columna del reporte: cómo se titula y cómo se saca el valor de la fila.</summary>
/// <param name="Ancho">
/// Peso relativo de la columna. Es relativo y no absoluto para que el reporte
/// se adapte al ancho de la hoja sin recalcular milímetros.
/// </param>
/// <param name="ALaDerecha">
/// Los números se alinean a la derecha. No es cosmético: una columna de
/// importes alineada a la izquierda obliga a leer cada fila entera para
/// comparar dos valores.
/// </param>
public sealed record ColumnaReporte<T>(
    string Titulo,
    Func<T, string> Valor,
    float Ancho = 1f,
    bool ALaDerecha = false);

/// <summary>Encabezado del reporte: qué es, de quién y con qué filtros se generó.</summary>
public sealed record EncabezadoReporte(
    string Titulo,
    string Organizacion,
    string GeneradoPor,
    DateTime Fecha,
    IReadOnlyList<(string Campo, string Valor)> Filtros);

public interface IReporteService
{
    /// <summary>
    /// Arma el PDF de un listado. Devuelve los bytes: quién lo guarda o lo
    /// manda es problema de la capa de arriba.
    /// </summary>
    byte[] Listado<T>(EncabezadoReporte encabezado,
                      IReadOnlyList<T> filas,
                      IReadOnlyList<ColumnaReporte<T>> columnas,
                      IReadOnlyList<(string Rotulo, string Valor)>? resumen = null);
}

/// <summary>
/// Generación de reportes en PDF (RF-14).
///
/// El servicio no sabe qué reporta: recibe el encabezado, las filas y la
/// definición de columnas. Por eso los tres reportes del sistema —parque,
/// incidencias y mantenimientos— son tres llamadas y no tres clases: lo que
/// cambia entre ellos son los datos y los títulos, no el documento.
///
/// Se usa QuestPDF con licencia Community. Es la razón por la que se eligió
/// sobre las alternativas: no necesita binarios nativos ni un navegador
/// headless, así que funciona igual dentro del contenedor de la API.
/// </summary>
public class ReporteService : IReporteService
{
    /// <summary>
    /// La licencia se declara una sola vez por proceso. QuestPDF falla al
    /// primer documento si no está declarada, y el mensaje no dice que falta
    /// esta línea.
    /// </summary>
    static ReporteService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // Los mismos valores que la aplicación: el reporte impreso y la pantalla
    // tienen que verse de la misma familia.
    private const string Tinta = "#0f1317";
    private const string Tinta2 = "#4d5661";
    private const string Tinta3 = "#6f7a86";
    private const string Linea = "#dde2e7";
    private const string LineaFuerte = "#0f1317";
    private const string Fila = "#f4f6f8";
    private const string Acento = "#17558f";

    public byte[] Listado<T>(EncabezadoReporte encabezado,
                             IReadOnlyList<T> filas,
                             IReadOnlyList<ColumnaReporte<T>> columnas,
                             IReadOnlyList<(string Rotulo, string Valor)>? resumen = null)
    {
        if (columnas.Count == 0)
            throw new ArgumentException("Un reporte sin columnas no es un reporte.", nameof(columnas));

        // Apaisado cuando hay muchas columnas: seis columnas en A4 vertical
        // salen ilegibles, y un reporte que no se puede leer no sirve de nada.
        var apaisado = columnas.Count > 5;

        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(apaisado ? PageSizes.A4.Landscape() : PageSizes.A4);
                pagina.Margin(1.6f, Unit.Centimetre);
                pagina.DefaultTextStyle(x => x.FontSize(9).FontColor(Tinta).FontFamily(Fonts.Calibri));

                pagina.Header().Element(e => Encabezado(e, encabezado));
                pagina.Content().PaddingVertical(14).Element(e => Cuerpo(e, filas, columnas, resumen));
                pagina.Footer().Element(e => Pie(e, encabezado));
            });
        });

        return documento.GeneratePdf();
    }

    private static void Encabezado(IContainer c, EncabezadoReporte e) =>
        c.Column(col =>
        {
            col.Item().Row(fila =>
            {
                fila.RelativeItem().Column(izq =>
                {
                    izq.Item().Text("PredictIT")
                       .FontSize(8).FontColor(Acento).SemiBold().LetterSpacing(0.12f);
                    izq.Item().PaddingTop(2).Text(e.Titulo).FontSize(16).Bold();
                    izq.Item().PaddingTop(1).Text(e.Organizacion).FontSize(9).FontColor(Tinta2);
                });

                fila.ConstantItem(190).AlignRight().Column(der =>
                {
                    der.Item().Text($"Emitido el {e.Fecha:dd/MM/yyyy} a las {e.Fecha:HH:mm}")
                       .FontSize(8).FontColor(Tinta3);
                    der.Item().Text($"Por {e.GeneradoPor}").FontSize(8).FontColor(Tinta3);
                });
            });

            // Los filtros van en el encabezado y no al pie: un reporte filtrado
            // que no dice con qué filtros se hizo es un reporte que se puede
            // malinterpretar, y quien lo lea impreso no tiene forma de saberlo.
            if (e.Filtros.Count > 0)
            {
                col.Item().PaddingTop(8).Text(t =>
                {
                    t.Span("Filtros aplicados:  ").FontSize(8).FontColor(Tinta3).SemiBold();
                    for (var i = 0; i < e.Filtros.Count; i++)
                    {
                        var (campo, valor) = e.Filtros[i];
                        t.Span($"{campo} ").FontSize(8).FontColor(Tinta3);
                        t.Span(valor).FontSize(8).FontColor(Tinta2).SemiBold();
                        if (i < e.Filtros.Count - 1) t.Span("     ").FontSize(8);
                    }
                });
            }

            col.Item().PaddingTop(10).BorderBottom(1.4f).BorderColor(LineaFuerte);
        });

    private static void Cuerpo<T>(IContainer c,
                                  IReadOnlyList<T> filas,
                                  IReadOnlyList<ColumnaReporte<T>> columnas,
                                  IReadOnlyList<(string Rotulo, string Valor)>? resumen) =>
        c.Column(col =>
        {
            if (resumen is { Count: > 0 })
            {
                col.Item().PaddingBottom(14).Row(fila =>
                {
                    foreach (var (rotulo, valor) in resumen)
                    {
                        fila.RelativeItem().Column(t =>
                        {
                            t.Item().Text(valor).FontSize(15).Bold();
                            t.Item().Text(rotulo).FontSize(8).FontColor(Tinta3);
                        });
                    }
                });
            }

            if (filas.Count == 0)
            {
                // Un reporte vacío dice que está vacío. Una hoja con el
                // encabezado y nada debajo se lee como un error de generación.
                col.Item().PaddingTop(30).AlignCenter().Text(
                    "Ningún registro coincide con los filtros aplicados.")
                   .FontSize(10).FontColor(Tinta3);
                return;
            }

            col.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(def =>
                {
                    foreach (var columna in columnas) def.RelativeColumn(columna.Ancho);
                });

                // La cabecera se repite en cada página: un listado de ochenta
                // filas se lee en tres hojas, y las hojas dos y tres sin
                // encabezado son columnas de datos sin nombre.
                tabla.Header(cab =>
                {
                    foreach (var columna in columnas)
                    {
                        var celda = cab.Cell().BorderBottom(1).BorderColor(Linea)
                                       .PaddingVertical(5).PaddingRight(6);

                        (columna.ALaDerecha ? celda.AlignRight() : celda)
                            .Text(columna.Titulo.ToUpperInvariant())
                            .FontSize(7.5f).SemiBold().FontColor(Tinta3).LetterSpacing(0.08f);
                    }
                });

                var impar = false;
                foreach (var f in filas)
                {
                    // Fondo alterno: en una tabla de siete columnas, seguir una
                    // fila con la vista es la operación que más se hace.
                    string fondo = impar ? Fila : "#ffffff";
                    impar = !impar;

                    foreach (var columna in columnas)
                    {
                        var celda = tabla.Cell().Background(fondo)
                                         .PaddingVertical(4).PaddingHorizontal(4);

                        (columna.ALaDerecha ? celda.AlignRight() : celda)
                            .Text(columna.Valor(f) ?? string.Empty).FontSize(8.5f);
                    }
                }
            });

            col.Item().PaddingTop(8).Text(
                filas.Count == 1 ? "1 registro" : $"{filas.Count} registros")
               .FontSize(8).FontColor(Tinta3);
        });

    private static void Pie(IContainer c, EncabezadoReporte e) =>
        c.BorderTop(1).BorderColor(Linea).PaddingTop(6).Row(fila =>
        {
            fila.RelativeItem().Text(
                $"PredictIT · {e.Organizacion} · {e.Titulo}")
               .FontSize(7.5f).FontColor(Tinta3);

            fila.ConstantItem(120).AlignRight().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Tinta3));
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });
}
