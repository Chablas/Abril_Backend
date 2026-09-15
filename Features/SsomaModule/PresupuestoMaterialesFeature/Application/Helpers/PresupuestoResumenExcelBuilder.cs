using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using ClosedXML.Excel;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Helpers;

/// <summary>Arma la hoja de "Desagregado de Recursos" SSOMA calcando el layout del presupuesto
/// general de obra: título → fila "SSOMA" con el subtotal → lista PLANA numerada en secuencia (una
/// fila por partida concreta, sin agrupar por fuente — igual que "BARANDAS...", "EPI - OBRERO",
/// "EPI - STAFF", etc. van una tras otra en el modelo) → fila de Costo Directo total. La columna
/// "Fuente" es la única diferencia con el modelo: como acá las partidas vienen de 5 tablas distintas
/// del sistema (Materiales/Personal/Vigilancia/Servicios fijos/Kits) en vez de una sola planilla
/// armada a mano, se deja esa referencia informativa sin romper la secuencia de filas.</summary>
public static class PresupuestoResumenExcelBuilder
{
    private static readonly string[] Headers =
        ["Item", "Fuente", "Descripción", "Unidad", "Cantidad", "MO", "MAT", "EQU", "SC", "HER", "PU", "Costo Directo"];

    private const string Money = "#,##0.00";
    private const int ColCostoDirecto = 12;

    public static void Build(IXLWorksheet ws, PresupuestoResumenRecursosDto data)
    {
        var row = 1;

        ws.Cell(row, 1).Value = "DESAGREGADO DE RECURSOS SSOMA";
        var tituloRange = ws.Range(row, 1, row, Headers.Length).Merge();
        tituloRange.Style.Font.Bold = true;
        tituloRange.Style.Font.FontSize = 14;
        row++;

        ws.Cell(row, 1).Value = $"{data.ProjectDescription}  —  Presupuesto v{data.Version} ({data.Estado})";
        ws.Range(row, 1, row, Headers.Length).Merge().Style.Font.Italic = true;
        row += 2;

        var headerRow = row;
        for (var c = 0; c < Headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = Headers[c];
        var headerRange = ws.Range(headerRow, 1, headerRow, Headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");
        row++;

        // Fila "SSOMA" — mismo rol que la fila padre del modelo (P5=SUM(P6:P19)): el subtotal de
        // todo el listado que sigue.
        var ssomaRow = row;
        ws.Cell(ssomaRow, 3).Value = "SSOMA";
        ws.Range(ssomaRow, 1, ssomaRow, Headers.Length).Style.Font.Bold = true;
        ws.Range(ssomaRow, 1, ssomaRow, Headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#FCE4D6");
        row++;

        foreach (var linea in data.Lineas)
        {
            ws.Cell(row, 1).Value = linea.Item;
            ws.Cell(row, 2).Value = linea.Grupo;
            ws.Cell(row, 3).Value = linea.Descripcion;
            ws.Cell(row, 4).Value = linea.Unidad ?? "";
            ws.Cell(row, 5).Value = linea.Cantidad;
            ws.Cell(row, 6).Value = linea.Mo;
            ws.Cell(row, 7).Value = linea.Mat;
            ws.Cell(row, 8).Value = linea.Equ;
            ws.Cell(row, 9).Value = linea.Sc;
            ws.Cell(row, 10).Value = linea.Her;
            ws.Cell(row, 11).Value = linea.Pu;
            ws.Cell(row, ColCostoDirecto).Value = linea.CostoDirecto;
            ws.Range(row, 5, row, ColCostoDirecto).Style.NumberFormat.Format = Money;
            row++;
        }
        var ultimaFila = row - 1;

        // Subtotal "SSOMA" — mismo total que ColCostoDirecto en la fila de cierre, ya calculado en
        // el DTO (data.TotalCostoDirecto = suma de CostoDirecto de todas las líneas).
        ws.Cell(ssomaRow, ColCostoDirecto).Value = data.TotalCostoDirecto;
        ws.Cell(ssomaRow, ColCostoDirecto).Style.NumberFormat.Format = Money;

        ws.Range(headerRow + 1, 1, ultimaFila, Headers.Length).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Range(headerRow, 1, ultimaFila, Headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

        var totalRow = row + 1;
        ws.Cell(totalRow, 3).Value = "COSTO DIRECTO";
        ws.Cell(totalRow, 6).Value = data.TotalMo;
        ws.Cell(totalRow, 7).Value = data.TotalMat;
        ws.Cell(totalRow, 8).Value = data.TotalEqu;
        ws.Cell(totalRow, 9).Value = data.TotalSc;
        ws.Cell(totalRow, 10).Value = data.TotalHer;
        ws.Cell(totalRow, ColCostoDirecto).Value = data.TotalCostoDirecto;
        ws.Range(totalRow, 6, totalRow, ColCostoDirecto).Style.NumberFormat.Format = Money;
        var totalRange = ws.Range(totalRow, 1, totalRow, Headers.Length);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");
        totalRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

        ws.Columns().AdjustToContents();
        ws.Column(3).Width = ws.Column(3).Width < 40 ? 40 : ws.Column(3).Width;
    }
}
