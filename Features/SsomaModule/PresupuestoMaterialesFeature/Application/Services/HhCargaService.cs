using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;

/// <summary>
/// Importa el Excel semanal de Horas Hombre (planilla/Tareo, ej. "PROYECTOTAREOOBREROTOTAL").
/// Se importan TODAS las partidas de control (no solo Seguridad y Salud Ocupacional) porque esta
/// misma carga alimenta dos usos distintos:
///   1. HH de Seguridad y Salud Ocupacional, para ver consumo de materiales SSOMA según hitos del
///      cronograma (filtra por Partida de Control al consultar, no al importar).
///   2. HH totales y personal total (obreros) de TODA la obra, usado como driver de "Ratios de
///      dotación" (ver RatioDriverRepository.ObtenerHhRealPorProyectoAsync) — necesita todas las
///      partidas, no solo SSOMA.
/// Lo único que se descarta siempre, en ambos usos, es el personal "EMPLEADO" en Recurso
/// Equivalente (personal de planilla/staff, no obrero) — criterio confirmado por el responsable
/// SSOMA. Que un archivo dé 0 filas válidas es un resultado legítimo (todo el personal del
/// período era EMPLEADO) y no bloquea el import: igual se registra la carga para que el diff dé
/// de baja lo que ya estaba activo y no vuelve a aparecer.
/// </summary>
public class HhCargaService : IHhCargaService
{
    private readonly IHhCargaRepository _repo;

    public HhCargaService(IHhCargaRepository repo) => _repo = repo;

    public async Task<ImportHhResultDto> ImportarHhAsync(IFormFile archivo, int projectId, int usuarioId)
    {
        byte[] contenidoBytes;
        using (var ms = new MemoryStream())
        {
            await archivo.CopyToAsync(ms);
            contenidoBytes = ms.ToArray();
        }
        var hash = Convert.ToHexString(SHA256.HashData(contenidoBytes));

        var (lineasRaw, proyectosDistintos, descartadas, totalFilasLeidas, anioMinRaw, anioMaxRaw, semanaMinRaw, semanaMaxRaw)
            = ParsearHh(contenidoBytes, archivo.FileName);
        if (totalFilasLeidas == 0)
            throw new AbrilException("No se encontraron filas con datos en el archivo (verifica Año, Semana, Trabajador y Horas).", 400);

        var advertencias = new List<string>();
        if (descartadas > 0)
            advertencias.Add($"{descartadas} fila(s) descartadas por ser Recurso Equivalente \"EMPLEADO\".");
        if (lineasRaw.Count == 0)
            advertencias.Add("Todo el personal del archivo es \"EMPLEADO\" — se registra la carga en 0 y se da de baja todo lo que estaba activo antes.");
        if (proyectosDistintos.Count > 1)
            advertencias.Add($"El archivo trae más de un proyecto en la columna \"Proyecto\" ({string.Join(", ", proyectosDistintos)}). Verifica que sea el archivo correcto — solo se cargó contra el proyecto seleccionado.");

        // Período del archivo completo (no solo de las líneas SSOMA que sobrevivan el filtro),
        // para que una carga que filtra a 0 filas igual registre el período real cubierto.
        var anioMin = anioMinRaw!.Value;
        var anioMax = anioMaxRaw!.Value;
        var semanaMin = semanaMinRaw!.Value;
        var semanaMax = semanaMaxRaw!.Value;

        var lineasConOcurrencia = lineasRaw
            .OrderBy(l => l.Anio).ThenBy(l => l.SemanaNum).ThenBy(l => l.Trabajador)
                .ThenBy(l => l.Ocupacion).ThenBy(l => l.PartidaControl).ThenBy(l => l.HorasLaboradas)
            .GroupBy(l => (l.Anio, l.SemanaNum, l.Trabajador, l.Ocupacion, l.PartidaControl))
            .SelectMany(g => g.Select((l, i) => (Linea: l, Ocurrencia: i + 1)))
            .ToList();

        var existentes = await _repo.ObtenerLineasActivasPorProyectoAsync(projectId);
        var existentesPorClave = existentes.ToDictionary(ClaveDe);

        var carga = new SsHhCarga
        {
            ProjectId = projectId,
            NombreArchivo = archivo.FileName,
            HashArchivo = hash,
            AnioMin = anioMin,
            SemanaMin = semanaMin,
            AnioMax = anioMax,
            SemanaMax = semanaMax,
            Estado = "ACTIVA",
            SubidoPor = usuarioId,
            CreadoEn = DateTimeOffset.UtcNow
        };
        carga = await _repo.CrearCargaAsync(carga);

        var nuevas = new List<SsHhCargaLinea>();
        var actualizaciones = new List<(long LineaId, decimal HorasLaboradas, decimal? CostoHhNormal, decimal? Parcial)>();
        var clavesVistas = new HashSet<string>();
        var sinCambio = 0;

        foreach (var (linea, ocurrencia) in lineasConOcurrencia)
        {
            var clave = ClaveDe(linea.Anio, linea.SemanaNum, linea.Trabajador, linea.Ocupacion, linea.PartidaControl, ocurrencia);
            clavesVistas.Add(clave);

            if (existentesPorClave.TryGetValue(clave, out var existente))
            {
                if (existente.HorasLaboradas != linea.HorasLaboradas || existente.CostoHhNormal != linea.CostoHhNormal)
                    actualizaciones.Add((existente.Id, linea.HorasLaboradas, linea.CostoHhNormal, linea.Parcial));
                else
                    sinCambio++;
            }
            else
            {
                nuevas.Add(new SsHhCargaLinea
                {
                    CargaId = carga.Id,
                    ProjectId = projectId,
                    Anio = linea.Anio,
                    SemanaNum = linea.SemanaNum,
                    Trabajador = linea.Trabajador,
                    Ocupacion = linea.Ocupacion,
                    PartidaControl = linea.PartidaControl,
                    HorasLaboradas = linea.HorasLaboradas,
                    CostoHhNormal = linea.CostoHhNormal,
                    Parcial = linea.Parcial,
                    Ocurrencia = ocurrencia,
                    Activo = true,
                    CreadoEn = DateTimeOffset.UtcNow
                });
            }
        }

        var idsDarDeBaja = existentesPorClave
            .Where(kv => !clavesVistas.Contains(kv.Key))
            .Select(kv => kv.Value.Id)
            .ToList();
        var motivoBaja = $"No aparece en la carga acumulada del {DateOnly.FromDateTime(DateTime.UtcNow):dd/MM/yyyy} ({archivo.FileName}).";

        await _repo.AplicarDiffCargaAsync(nuevas, actualizaciones, idsDarDeBaja, motivoBaja);

        var totalLineas = nuevas.Count + actualizaciones.Count + sinCambio;
        await _repo.ActualizarResumenCargaAsync(carga.Id, totalLineas, nuevas.Count, actualizaciones.Count, idsDarDeBaja.Count);

        if (idsDarDeBaja.Count > 0)
            advertencias.Add($"{idsDarDeBaja.Count} línea(s) ya cargadas no aparecen en este archivo y se dieron de baja.");

        var (hhTotal, _) = await _repo.ObtenerHhTotalPorProyectoAsync(projectId);

        return new ImportHhResultDto
        {
            CargaId = carga.Id,
            NombreArchivo = archivo.FileName,
            TotalLineas = totalLineas,
            LineasNuevas = nuevas.Count,
            LineasActualizadas = actualizaciones.Count,
            LineasEliminadas = idsDarDeBaja.Count,
            LineasSinCambio = sinCambio,
            HorasLaboradasTotales = hhTotal,
            Estado = "ACTIVA",
            Advertencias = advertencias
        };
    }

    public async Task<List<HhCargaResumenDto>> ObtenerCargasAsync(int projectId) =>
        await _repo.ObtenerCargasPorProyectoAsync(projectId);

    private static string ClaveDe(SsHhCargaLinea l) => ClaveDe(l.Anio, l.SemanaNum, l.Trabajador, l.Ocupacion, l.PartidaControl, l.Ocurrencia);

    private static string ClaveDe(int anio, int semana, string trabajador, string? ocupacion, string? partida, int ocurrencia) =>
        $"{anio}|{semana}|{trabajador}|{ocupacion}|{partida}|{ocurrencia}";

    // ─── Parser Excel HH (Año / Periodo Semanal / Apellidos y Nombres / Horas laboradas) ──────

    private record LineaRaw(int Anio, int SemanaNum, string Trabajador, string? Ocupacion, string? PartidaControl,
        decimal HorasLaboradas, decimal? CostoHhNormal, decimal? Parcial);

    private const string RECURSO_EXCLUIDO = "EMPLEADO";

    private static (List<LineaRaw> Lineas, List<string> ProyectosDistintos, int Descartadas, int TotalFilasLeidas, int? AnioMin, int? AnioMax, int? SemanaMin, int? SemanaMax)
        ParsearHh(byte[] bytes, string nombreArchivo)
    {
        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        int headerRow = EncontrarFilaEncabezado(ws);
        if (headerRow == 0)
            throw new AbrilException($"No se encontró la fila de encabezados en '{nombreArchivo}'. Columnas requeridas: Año, Periodo Semanal, Apellidos y Nombres, Horas laboradas.", 400);

        var cols = MapearColumnas(ws, headerRow);
        ValidarColumnasRequeridas(cols, nombreArchivo);

        var lineas = new List<LineaRaw>();
        var proyectos = new HashSet<string>();
        var descartadas = 0;
        var totalFilasLeidas = 0;
        int? anioMin = null, anioMax = null, semanaMin = null, semanaMax = null;
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;

        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            var trabajador = ws.Cell(r, cols["trabajador"]).GetString().Trim();
            if (string.IsNullOrWhiteSpace(trabajador)) continue;

            if (cols.TryGetValue("proyecto", out var pCol))
            {
                var proyecto = ws.Cell(r, pCol).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(proyecto)) proyectos.Add(proyecto);
            }

            var anioStr = ws.Cell(r, cols["anio"]).GetString().Trim();
            var semanaStr = ws.Cell(r, cols["semana"]).GetString().Trim();
            if (!int.TryParse(anioStr, out var anio)) continue;

            var semanaMatch = Regex.Match(semanaStr, @"\d+");
            if (!semanaMatch.Success) continue;
            var semana = int.Parse(semanaMatch.Value);

            if (!TryLeerDecimal(ws.Cell(r, cols["horas"]), out var horas)) continue;

            // Cuenta y rango de período sobre TODAS las filas con datos válidos, antes de
            // descartar por Recurso Equivalente — así una carga que filtra a 0 filas igual
            // conserva el período real del archivo y no se confunde con un archivo vacío/roto.
            totalFilasLeidas++;
            anioMin = anioMin is null ? anio : Math.Min(anioMin.Value, anio);
            anioMax = anioMax is null ? anio : Math.Max(anioMax.Value, anio);
            if (anioMin == anio) semanaMin = semanaMin is null ? semana : Math.Min(semanaMin.Value, semana);
            if (anioMax == anio) semanaMax = semanaMax is null ? semana : Math.Max(semanaMax.Value, semana);

            decimal? costoHh = cols.TryGetValue("costohh", out var chCol) && TryLeerDecimal(ws.Cell(r, chCol), out var ch) ? ch : null;
            decimal? parcial = cols.TryGetValue("parcial", out var paCol) && TryLeerDecimal(ws.Cell(r, paCol), out var pa) ? pa : null;
            var ocupacion = cols.TryGetValue("ocupacion", out var oCol) ? ws.Cell(r, oCol).GetString().Trim() : null;
            var partida = cols.TryGetValue("partida", out var prCol) ? ws.Cell(r, prCol).GetString().Trim() : null;
            var recurso = cols.TryGetValue("recurso", out var rCol) ? ws.Cell(r, rCol).GetString().Trim() : null;

            // Se importan todas las partidas (SSOMA y no-SSOMA); solo se descarta personal
            // EMPLEADO (staff, no obrero). "Contains" y no "==" porque el valor de Recurso
            // Equivalente trae código + nombre (ej. "0101010005 PEON"), no el texto solo.
            if (!string.IsNullOrWhiteSpace(recurso) && NormalizarTexto(recurso).Contains(RECURSO_EXCLUIDO))
            {
                descartadas++;
                continue;
            }

            lineas.Add(new LineaRaw(anio, semana, trabajador,
                string.IsNullOrWhiteSpace(ocupacion) ? null : ocupacion,
                string.IsNullOrWhiteSpace(partida) ? null : partida,
                horas, costoHh, parcial));
        }

        return (lineas, proyectos.ToList(), descartadas, totalFilasLeidas, anioMin, anioMax, semanaMin, semanaMax);
    }

    private static int EncontrarFilaEncabezado(IXLWorksheet ws)
    {
        int lastRow = Math.Min(ws.LastRowUsed()?.RowNumber() ?? 1, 30);
        for (int r = 1; r <= lastRow; r++)
        {
            for (int c = 1; c <= 20; c++)
            {
                var val = NormalizarTexto(ws.Cell(r, c).GetString());
                if (val.Contains("HORAS LABORADAS") || val.Contains("PERIODO SEMANAL"))
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
            var val = NormalizarTexto(ws.Cell(headerRow, c).GetString());

            if ((val.Contains("APELLIDOS") || val.Contains("NOMBRES")) && !map.ContainsKey("trabajador"))
                map["trabajador"] = c;
            else if (val.Contains("PERIODO") && val.Contains("SEMANA") && !map.ContainsKey("semana"))
                map["semana"] = c;
            else if (val == "ANO" && !map.ContainsKey("anio")) // "Año" normalizado (Ñ→N)
                map["anio"] = c;
            else if (val.Contains("HORAS") && val.Contains("LABORADA") && !map.ContainsKey("horas"))
                map["horas"] = c;
            else if (val.Contains("OCUPACION") && !map.ContainsKey("ocupacion"))
                map["ocupacion"] = c;
            else if (val.Contains("PARTIDA") && !map.ContainsKey("partida"))
                map["partida"] = c;
            else if (val.Contains("RECURSO") && !map.ContainsKey("recurso"))
                map["recurso"] = c;
            else if (val.Contains("COSTO") && val.Contains("HH") && !map.ContainsKey("costohh"))
                map["costohh"] = c;
            else if (val == "PARCIAL" && !map.ContainsKey("parcial"))
                map["parcial"] = c;
            else if (val == "PROYECTO" && !map.ContainsKey("proyecto"))
                map["proyecto"] = c;
        }
        return map;
    }

    private static void ValidarColumnasRequeridas(Dictionary<string, int> cols, string archivo)
    {
        // "recurso" es obligatoria porque es la única forma de excluir EMPLEADO — sin ella no se
        // puede garantizar que el HH importado sea el correcto, así que se rechaza el archivo en
        // vez de importar todo. "partida" no es obligatoria para importar (ya no se filtra por
        // ella acá), pero se guarda si está presente para el futuro filtro de consumo por hitos.
        var requeridas = new[] { "anio", "semana", "trabajador", "horas", "recurso" };
        var faltantes = requeridas.Where(r => !cols.ContainsKey(r)).ToList();
        if (faltantes.Count > 0)
            throw new AbrilException(
                $"Archivo '{archivo}': no se encontraron columnas {string.Join(", ", faltantes)}. " +
                "Se requiere Año, Periodo Semanal, Apellidos y Nombres, Horas laboradas y Recurso Equivalente.", 400);
    }

    private static string NormalizarTexto(string s) =>
        s.Trim().ToUpperInvariant()
            .Replace("Á", "A").Replace("É", "E").Replace("Í", "I").Replace("Ó", "O").Replace("Ú", "U").Replace("Ñ", "N");

    private static bool TryLeerDecimal(IXLCell cell, out decimal result)
    {
        if (cell.DataType == XLDataType.Number)
        {
            result = cell.GetValue<decimal>();
            return true;
        }
        var s = cell.GetString().Trim().Replace(".", "").Replace(",", ".");
        return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out result);
    }
}
