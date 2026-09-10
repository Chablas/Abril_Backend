using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Dtos;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Services;

public class MaterialService : IMaterialService
{
    private readonly IMaterialRepository _repository;

    public MaterialService(IMaterialRepository repository) => _repository = repository;

    public Task<AlmacenFiltrosDTO> GetFiltros() => _repository.GetFiltros();

    public Task<List<AlmacenMaterialDTO>> GetMateriales(bool soloActivos) => _repository.GetMateriales(soloActivos);

    public async Task<AlmacenMaterialDTO> CreateMaterial(CreateAlmacenMaterialDTO body)
    {
        if (string.IsNullOrWhiteSpace(body.Codigo) || string.IsNullOrWhiteSpace(body.Nombre) || string.IsNullOrWhiteSpace(body.UnidadMedida))
            throw new AbrilException("Código, nombre y unidad de medida son obligatorios.", 400);

        if (await _repository.CodigoExiste(body.Codigo))
            throw new AbrilException($"Ya existe un material con el código {body.Codigo}.", 409);

        return await _repository.CreateMaterial(body);
    }

    public async Task<AlmacenMaterialDTO> UpdateMaterial(int id, UpdateAlmacenMaterialDTO body)
    {
        if (string.IsNullOrWhiteSpace(body.UnidadMedida))
            throw new AbrilException("La unidad de medida es obligatoria.", 400);
        if (body.PuntoReorden.HasValue && body.PuntoReorden < 0)
            throw new AbrilException("El punto de reorden no puede ser negativo.", 400);
        if (body.StockSeguridad.HasValue && body.StockSeguridad < 0)
            throw new AbrilException("El stock de seguridad no puede ser negativo.", 400);

        var actualizado = await _repository.UpdateMaterial(id, body);
        if (actualizado == null) throw new AbrilException("No se encontró el material.", 404);
        return actualizado;
    }

    public Task<AlmacenMovimientoListResponseDTO> GetMovimientos(AlmacenMovimientosQueryParams query) => _repository.GetMovimientos(query);

    public Task<AlmacenMovimientoListItemDTO> CreateMovimiento(CreateAlmacenMovimientoDTO body, string? creadoPor)
    {
        if (!TipoMovimientoAlmacen.EsValido(body.Tipo))
            throw new AbrilException($"Tipo de movimiento inválido: {body.Tipo}", 400);
        if (body.Cantidad <= 0)
            throw new AbrilException("La cantidad debe ser mayor a 0.", 400);
        if (body.Tipo == TipoMovimientoAlmacen.Devolucion && !MotivoDevolucion.EsValido(body.MotivoDevolucion ?? ""))
            throw new AbrilException("Una devolución requiere indicar el motivo: Error o Sobrante.", 400);
        if (body.Tipo != TipoMovimientoAlmacen.Devolucion) body.MotivoDevolucion = null;

        return _repository.CreateMovimiento(body, creadoPor);
    }

    public Task<AlmacenStockDTO> GetStock(int? proyectoId) => _repository.GetStock(proyectoId);

    public Task<AlmacenDashboardDTO> GetDashboard(int? proyectoId, int diasVentana)
    {
        if (diasVentana < 7 || diasVentana > 365) diasVentana = 90;
        return _repository.GetDashboard(proyectoId, diasVentana);
    }

    // ── Importación de Excel de movimientos ─────────────────────────────

    private static string Normalizar(string s)
    {
        var t = s.Trim().ToUpperInvariant()
            .Replace("Á", "A").Replace("É", "E").Replace("Í", "I").Replace("Ó", "O").Replace("Ú", "U").Replace("Ñ", "N");
        return Regex.Replace(t, "[^A-Z0-9]+", " ").Trim();
    }

    private static string NormalizarTipo(string s) => Normalizar(s) switch
    {
        "INGRESO" => TipoMovimientoAlmacen.Ingreso,
        "SALIDA" => TipoMovimientoAlmacen.Salida,
        "DEVOLUCION" => TipoMovimientoAlmacen.Devolucion,
        _ => s.Trim()
    };

    private static string NormalizarMotivo(string? s) => Normalizar(s ?? "") switch
    {
        "ERROR" => MotivoDevolucion.Error,
        "SOBRANTE" => MotivoDevolucion.Sobrante,
        _ => (s ?? "").Trim()
    };

    private record FilaImportada(
        int Fila, DateTime Fecha, string Proyecto, string Codigo, string Articulo, string Unidad,
        string Tipo, decimal Cantidad, string? Origen, string? Motivo, string? Comentario);

    public async Task<ImportarMovimientosResultDTO> ImportarMovimientos(IFormFile archivo, string? creadoPor)
    {
        if (archivo == null || archivo.Length == 0)
            throw new AbrilException("Debe adjuntar un archivo.", 400);

        using var stream = archivo.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault() ?? throw new AbrilException("El archivo no tiene hojas.", 400);
        var filaHeader = ws.FirstRowUsed() ?? throw new AbrilException("El archivo está vacío.", 400);

        var columnas = new Dictionary<string, int>();
        foreach (var celda in filaHeader.CellsUsed())
        {
            var header = Normalizar(celda.GetString());
            if (!columnas.ContainsKey(header)) columnas[header] = celda.Address.ColumnNumber;
        }

        int? ColIndex(params string[] alias) => alias.Where(columnas.ContainsKey).Select(a => (int?)columnas[a]).FirstOrDefault();

        var colFecha = ColIndex("FECHA", "FECHA SALIDA", "FECHA ENTRADA", "FECHA MOVIMIENTO");
        var colProyecto = ColIndex("PROYECTO");
        var colCodigo = ColIndex("COD", "CODIGO");
        var colArticulo = ColIndex("ARTICULO", "MATERIAL", "INSUMO");
        var colUnidad = ColIndex("UNI MEDIDA", "UNIDAD", "UND", "UNI");
        var colTipo = ColIndex("TIPO");
        var colCantidad = ColIndex("CANTIDAD", "CANT");
        var colOrigen = ColIndex("ORIGEN");
        var colMotivo = ColIndex("MOTIVO", "MOTIVO DEVOLUCION");
        var colComentario = ColIndex("COMENTARIO", "OBSERVACION");

        var faltantes = new List<string>();
        if (colFecha == null) faltantes.Add("Fecha");
        if (colProyecto == null) faltantes.Add("Proyecto");
        if (colCodigo == null) faltantes.Add("Código");
        if (colArticulo == null) faltantes.Add("Artículo");
        if (colUnidad == null) faltantes.Add("Unidad");
        if (colTipo == null) faltantes.Add("Tipo");
        if (colCantidad == null) faltantes.Add("Cantidad");
        if (faltantes.Count > 0)
            throw new AbrilException($"Faltan columnas obligatorias en el archivo: {string.Join(", ", faltantes)}.", 400);

        var filas = ws.RowsUsed().Skip(1).ToList();
        var resultado = new ImportarMovimientosResultDTO { TotalFilas = filas.Count };
        var parseadas = new List<FilaImportada>();

        foreach (var fila in filas)
        {
            var numFila = fila.RowNumber();
            var proyectoTxt = fila.Cell(colProyecto!.Value).GetString().Trim();
            var codigoTxt = fila.Cell(colCodigo!.Value).GetString().Trim();
            if (string.IsNullOrWhiteSpace(proyectoTxt) && string.IsNullOrWhiteSpace(codigoTxt)) continue;

            var articuloTxt = fila.Cell(colArticulo!.Value).GetString().Trim();
            var unidadTxt = fila.Cell(colUnidad!.Value).GetString().Trim();
            var tipoTxt = NormalizarTipo(fila.Cell(colTipo!.Value).GetString());

            if (!fila.Cell(colFecha!.Value).TryGetValue(out DateTime fecha))
            {
                resultado.Errores.Add($"Fila {numFila}: fecha inválida.");
                continue;
            }
            if (!fila.Cell(colCantidad!.Value).TryGetValue(out decimal cantidad) || cantidad <= 0)
            {
                resultado.Errores.Add($"Fila {numFila}: cantidad inválida.");
                continue;
            }
            if (!TipoMovimientoAlmacen.EsValido(tipoTxt))
            {
                resultado.Errores.Add($"Fila {numFila}: tipo \"{tipoTxt}\" no reconocido (use Ingreso, Salida o Devolucion).");
                continue;
            }
            if (string.IsNullOrWhiteSpace(proyectoTxt) || string.IsNullOrWhiteSpace(codigoTxt) ||
                string.IsNullOrWhiteSpace(articuloTxt) || string.IsNullOrWhiteSpace(unidadTxt))
            {
                resultado.Errores.Add($"Fila {numFila}: faltan datos obligatorios (proyecto/código/artículo/unidad).");
                continue;
            }

            string? motivo = null;
            if (tipoTxt == TipoMovimientoAlmacen.Devolucion)
            {
                motivo = NormalizarMotivo(colMotivo.HasValue ? fila.Cell(colMotivo.Value).GetString() : null);
                if (!MotivoDevolucion.EsValido(motivo))
                {
                    resultado.Errores.Add($"Fila {numFila}: una Devolución requiere Motivo \"Error\" o \"Sobrante\".");
                    continue;
                }
            }

            parseadas.Add(new FilaImportada(
                numFila, fecha, proyectoTxt, codigoTxt, articuloTxt, unidadTxt, tipoTxt, cantidad,
                colOrigen.HasValue ? fila.Cell(colOrigen.Value).GetString().Trim() : null,
                motivo,
                colComentario.HasValue ? fila.Cell(colComentario.Value).GetString().Trim() : null));
        }

        if (parseadas.Count == 0) return resultado;

        var proyectoCache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        var materialCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var materialesCreados = 0;
        var movimientosNuevos = new List<AlmacenMovimiento>();
        var proyectoIdsTocados = new HashSet<int>();
        DateTime? fechaMin = null, fechaMax = null;

        foreach (var p in parseadas)
        {
            if (!proyectoCache.TryGetValue(p.Proyecto, out var proyectoId))
            {
                proyectoId = await _repository.ResolverProyectoIdPorNombre(p.Proyecto);
                proyectoCache[p.Proyecto] = proyectoId;
            }
            if (proyectoId == null)
            {
                resultado.Errores.Add($"Fila {p.Fila}: no se encontró el proyecto \"{p.Proyecto}\".");
                continue;
            }

            if (!materialCache.TryGetValue(p.Codigo, out var materialId))
            {
                var (id, creado) = await _repository.ResolverOCrearMaterial(p.Codigo, p.Articulo, p.Unidad);
                materialCache[p.Codigo] = id;
                materialId = id;
                if (creado) materialesCreados++;
            }

            proyectoIdsTocados.Add(proyectoId.Value);
            fechaMin = fechaMin == null || p.Fecha < fechaMin ? p.Fecha : fechaMin;
            fechaMax = fechaMax == null || p.Fecha > fechaMax ? p.Fecha : fechaMax;

            movimientosNuevos.Add(new AlmacenMovimiento
            {
                ProyectoId = proyectoId.Value,
                MaterialId = materialId,
                Fecha = p.Fecha,
                Tipo = p.Tipo,
                Cantidad = p.Cantidad,
                Origen = string.IsNullOrWhiteSpace(p.Origen) ? null : p.Origen,
                MotivoDevolucion = p.Motivo,
                Comentario = string.IsNullOrWhiteSpace(p.Comentario) ? null : p.Comentario,
                CreadoPor = creadoPor
            });
        }

        resultado.MaterialesCreados = materialesCreados;
        if (movimientosNuevos.Count == 0) return resultado;

        var clavesExistentes = await _repository.ObtenerClavesMovimientosExistentes(
            proyectoIdsTocados.ToList(), fechaMin!.Value.Date, fechaMax!.Value.Date.AddDays(1).AddTicks(-1));

        var clavesVistas = new HashSet<string>(clavesExistentes);
        var aInsertar = new List<AlmacenMovimiento>();

        foreach (var m in movimientosNuevos)
        {
            var clave = $"{m.ProyectoId}|{m.MaterialId}|{m.Fecha:yyyyMMdd}|{m.Tipo}|{m.Cantidad:0.####}";
            if (!clavesVistas.Add(clave))
            {
                resultado.Duplicados++;
                continue;
            }
            aInsertar.Add(m);
        }

        await _repository.InsertarMovimientos(aInsertar);
        resultado.Importados = aInsertar.Count;
        return resultado;
    }
}
