using System.Security.Cryptography;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;

public class ConsumoService : IConsumoService
{
    private readonly IConsumoRepository _repo;
    private readonly IEstandarizacionService _estandarizacion;

    public ConsumoService(IConsumoRepository repo, IEstandarizacionService estandarizacion)
    {
        _repo = repo;
        _estandarizacion = estandarizacion;
    }

    public async Task<ImportConsumoResultDto> ImportarS10Async(IFormFile archivo, int projectId, int usuarioId)
    {
        // 1. Calcular SHA256 para deduplicación exacta
        byte[] contenidoBytes;
        using (var ms = new MemoryStream())
        {
            await archivo.CopyToAsync(ms);
            contenidoBytes = ms.ToArray();
        }
        var hash = Convert.ToHexString(SHA256.HashData(contenidoBytes));

        // 2. Verificar duplicado exacto (mismo archivo ya cargado)
        if (await _repo.ExisteHashAsync(hash))
            throw new AbrilException("Este archivo ya fue cargado anteriormente. No se permiten duplicados exactos.", 409);

        // 3. Parsear Excel
        var lineasRaw = ParsearExcelS10(contenidoBytes, archivo.FileName);
        if (lineasRaw.Count == 0)
            throw new AbrilException("El archivo no contiene filas válidas. Verifica el formato S10.", 400);

        var fechaMin = lineasRaw.Min(l => l.FechaGuia);
        var fechaMax = lineasRaw.Max(l => l.FechaGuia);

        // 4. Verificar solapamiento de fechas (advertencia, no bloqueo)
        var solapaFechas = await _repo.ExisteSolapamientoFechasAsync(projectId, fechaMin, fechaMax);
        var advertencias = new List<string>();
        if (solapaFechas)
            advertencias.Add($"Existe otra carga activa que se solapa con el rango {fechaMin:dd/MM/yyyy} - {fechaMax:dd/MM/yyyy}. Verifica que no sean datos duplicados.");

        // 5. Crear registro de carga
        var carga = new SsConsumoCarga
        {
            ProjectId = projectId,
            NombreArchivo = archivo.FileName,
            HashArchivo = hash,
            FechaMin = fechaMin,
            FechaMax = fechaMax,
            TotalLineas = lineasRaw.Count,
            LineasEstandarizadas = 0,
            LineasPendientes = 0,
            Estado = "ACTIVA",
            SubidoPor = usuarioId,
            CreadoEn = DateTimeOffset.UtcNow
        };
        carga = await _repo.CrearCargaAsync(carga);

        // 6. Insertar líneas crudas
        var lineas = lineasRaw.Select(l => new SsConsumoLinea
        {
            CargaId = carga.Id,
            ProjectId = projectId,
            RecursoCrudo = l.RecursoCrudo,
            Cantidad = l.Cantidad,
            PrecioUnitario = l.PrecioUnitario,
            PrecioTotal = l.PrecioTotal,
            FechaGuia = l.FechaGuia,
            Estandarizado = false,
            CreadoEn = DateTimeOffset.UtcNow
        }).ToList();

        await _repo.InsertarLineasBulkAsync(lineas);

        // 7. Disparar estandarización automática
        var resultadoEstand = await _estandarizacion.EstandarizarCargaAsync(carga.Id);

        return new ImportConsumoResultDto
        {
            CargaId = carga.Id,
            NombreArchivo = archivo.FileName,
            TotalLineas = lineasRaw.Count,
            LineasEstandarizadas = resultadoEstand.AutoResueltas,
            LineasPendientes = resultadoEstand.EnRevision,
            LineasSinMatch = resultadoEstand.SinMatch,
            Estado = "ACTIVA",
            Advertencias = advertencias
        };
    }

    public async Task<List<ConsumoCargaResumenDto>> ObtenerCargasAsync(int projectId) =>
        await _repo.ObtenerCargasPorProyectoAsync(projectId);

    public async Task<int> AsignarHitosAsync(int projectId) =>
        await _repo.AsignarHitosPorFechaAsync(projectId);

    // ─── Parser flexible S10 ──────────────────────────────────────────────────

    private record LineaRaw(string RecursoCrudo, decimal Cantidad, decimal PrecioUnitario, decimal PrecioTotal, DateOnly FechaGuia);

    private static List<LineaRaw> ParsearExcelS10(byte[] bytes, string nombreArchivo)
    {
        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        // Encontrar fila de encabezados buscando palabras clave en las primeras 20 filas
        int headerRow = EncontrarFilaEncabezado(ws);
        if (headerRow == 0)
            throw new AbrilException($"No se encontró la fila de encabezados en '{nombreArchivo}'. Columnas requeridas: recurso, cantidad, fecha, precio.", 400);

        // Mapear columnas por nombre
        var cols = MapearColumnas(ws, headerRow);
        ValidarColumnasRequeridas(cols, nombreArchivo);

        var lineas = new List<LineaRaw>();
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;

        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            var recurso = ws.Cell(r, cols["recurso"]).GetString().Trim();
            if (string.IsNullOrWhiteSpace(recurso)) continue;

            // Ignorar filas de totales o encabezados secundarios
            if (recurso.StartsWith("TOTAL", StringComparison.OrdinalIgnoreCase)) continue;

            var cantidadStr = ws.Cell(r, cols["cantidad"]).GetString().Trim();
            var precioStr = ws.Cell(r, cols["precio"]).GetString().Trim();
            var precioTotalStr = ws.Cell(r, cols.TryGetValue("preciototal", out var ptCol) ? ptCol : cols["precio"]).GetString().Trim();
            var fechaStr = ws.Cell(r, cols["fecha"]).GetString().Trim();

            if (!ParseDecimal(cantidadStr, out var cantidad)) continue;
            if (!ParseDecimal(precioStr, out var precio)) continue;
            ParseDecimal(precioTotalStr, out var precioTotal);
            if (precioTotal == 0) precioTotal = cantidad * precio;

            if (!ParseFecha(fechaStr, out var fecha)) continue;

            lineas.Add(new LineaRaw(recurso, cantidad, precio, precioTotal, fecha));
        }

        return lineas;
    }

    private static int EncontrarFilaEncabezado(IXLWorksheet ws)
    {
        int lastRow = Math.Min(ws.LastRowUsed()?.RowNumber() ?? 1, 30);
        for (int r = 1; r <= lastRow; r++)
        {
            for (int c = 1; c <= 20; c++)
            {
                var val = ws.Cell(r, c).GetString().Trim().ToUpperInvariant();
                if (val.Contains("RECURSO") || val.Contains("DESCRIPCION") || val.Contains("MATERIAL"))
                    return r;
            }
        }
        return 0;
    }

    private static Dictionary<string, int> MapearColumnas(IXLWorksheet ws, int headerRow)
    {
        var map = new Dictionary<string, int>();
        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 20;

        for (int c = 1; c <= lastCol; c++)
        {
            var val = ws.Cell(headerRow, c).GetString().Trim().ToUpperInvariant()
                .Replace("Á", "A").Replace("É", "E").Replace("Í", "I").Replace("Ó", "O").Replace("Ú", "U");

            if ((val.Contains("RECURSO") || val.Contains("DESCRIPCION") || val.Contains("MATERIAL")) && !map.ContainsKey("recurso"))
                map["recurso"] = c;
            else if ((val.Contains("FECHA") || val.Contains("GUIA") || val.Contains("GUÍA")) && !map.ContainsKey("fecha"))
                map["fecha"] = c;
            else if (val == "CANTIDAD" || val.Contains("CANT") && !map.ContainsKey("cantidad"))
                map["cantidad"] = c;
            else if ((val.Contains("PRECIO") && !val.Contains("TOTAL")) && !map.ContainsKey("precio"))
                map["precio"] = c;
            else if ((val.Contains("TOTAL") || val == "IMPORTE") && !map.ContainsKey("preciototal"))
                map["preciototal"] = c;
        }
        return map;
    }

    private static void ValidarColumnasRequeridas(Dictionary<string, int> cols, string archivo)
    {
        var requeridas = new[] { "recurso", "cantidad", "precio", "fecha" };
        var faltantes = requeridas.Where(r => !cols.ContainsKey(r)).ToList();
        if (faltantes.Count > 0)
            throw new AbrilException($"Archivo '{archivo}': no se encontraron columnas {string.Join(", ", faltantes)}. Verifica el formato S10.", 400);
    }

    private static bool ParseDecimal(string s, out decimal result)
    {
        // S10 usa comas como separador decimal en Perú: "237,29" → 237.29
        s = s.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out result);
    }

    private static bool ParseFecha(string s, out DateOnly result)
    {
        // Formatos: "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"
        if (DateOnly.TryParseExact(s, ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy"],
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out result))
            return true;

        // Intentar como número serial Excel (ej: 45555)
        if (double.TryParse(s, out var serial) && serial > 1000)
        {
            try { result = DateOnly.FromDateTime(DateTime.FromOADate(serial)); return true; }
            catch { }
        }
        result = default;
        return false;
    }
}
