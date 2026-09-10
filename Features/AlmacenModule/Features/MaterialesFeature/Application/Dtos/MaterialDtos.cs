namespace Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Dtos;

public class AlmacenMaterialDTO
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public decimal? PuntoReorden { get; set; }
    public decimal? StockSeguridad { get; set; }
}

/// <summary>Edición de un material ya creado. Campos null = no tocar, salvo PuntoReorden y
/// StockSeguridad que se pueden vaciar explícitamente enviando 0 (no hay forma de distinguir
/// "no tocar" de "vaciar" en un decimal? desde este DTO, así que el front siempre manda el
/// valor completo de ambos umbrales, igual que al crear).</summary>
public class UpdateAlmacenMaterialDTO
{
    public string? Nombre { get; set; }
    public string? UnidadMedida { get; set; }
    public decimal? PuntoReorden { get; set; }
    public decimal? StockSeguridad { get; set; }
    public bool Activo { get; set; } = true;
}

public class CreateAlmacenMaterialDTO
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal? PuntoReorden { get; set; }
    public decimal? StockSeguridad { get; set; }
}

public class ProyectoAlmacenFiltroDTO
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class AlmacenFiltrosDTO
{
    public List<ProyectoAlmacenFiltroDTO> Proyectos { get; set; } = [];
    public List<AlmacenMaterialDTO> Materiales { get; set; } = [];
}

public class CreateAlmacenMovimientoDTO
{
    public int ProyectoId { get; set; }
    public int MaterialId { get; set; }
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public string? Origen { get; set; }
    public string? MotivoDevolucion { get; set; }
    public string? Comentario { get; set; }
}

public class AlmacenMovimientoListItemDTO
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public int MaterialId { get; set; }
    public string? MaterialCodigo { get; set; }
    public string? MaterialNombre { get; set; }
    public string? UnidadMedida { get; set; }
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public string? Origen { get; set; }
    public string? MotivoDevolucion { get; set; }
    public string? Comentario { get; set; }
    public string? CreadoPor { get; set; }
}

public class AlmacenMovimientosQueryParams
{
    public int? ProyectoId { get; set; }
    public int? MaterialId { get; set; }
    public string? Tipo { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public int Pagina { get; set; } = 1;
    public int PorPagina { get; set; } = 20;
}

public class AlmacenMovimientoListResponseDTO
{
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int PorPagina { get; set; }
    public List<AlmacenMovimientoListItemDTO> Items { get; set; } = [];
}

public class AlmacenStockItemDTO
{
    public int MaterialId { get; set; }
    public string MaterialCodigo { get; set; } = string.Empty;
    public string MaterialNombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal TotalIngresos { get; set; }
    public decimal TotalSalidas { get; set; }
    public decimal SaldoActual { get; set; }
}

public class AlmacenStockDTO
{
    public int? ProyectoId { get; set; }
    public List<AlmacenStockItemDTO> Materiales { get; set; } = [];
}

public static class EstadoStockCritico
{
    public const string Critico = "Crítico";
    public const string AlertaBaja = "Alerta Baja";
    public const string BajoMinimos = "Bajo Mínimos";
    public const string Optimo = "Óptimo";
}

public class AlmacenDashboardFlujoItemDTO
{
    public string MaterialNombre { get; set; } = string.Empty;
    public decimal TotalIngresos { get; set; }
    public decimal TotalSalidas { get; set; }
}

public class AlmacenDashboardParticipacionItemDTO
{
    public string ProyectoNombre { get; set; } = string.Empty;
    public decimal TotalConsumo { get; set; }
    public decimal Porcentaje { get; set; }
}

public class AlmacenMaterialCriticoDTO
{
    public int MaterialId { get; set; }
    public string MaterialNombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal StockActual { get; set; }
    public decimal PuntoReorden { get; set; }
    public decimal StockSeguridad { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string AccionRecomendada { get; set; } = string.Empty;
}

public class AlmacenCoberturaItemDTO
{
    public string MaterialNombre { get; set; } = string.Empty;
    public decimal? DiasCobertura { get; set; }
}

public class AlmacenDashboardDTO
{
    public List<AlmacenDashboardFlujoItemDTO> FlujoMateriales { get; set; } = [];
    public List<AlmacenDashboardParticipacionItemDTO> ParticipacionProyectos { get; set; } = [];
    public List<AlmacenMaterialCriticoDTO> MaterialesCriticos { get; set; } = [];
    public List<AlmacenCoberturaItemDTO> Cobertura { get; set; } = [];
    public int LimiteSeguridadDias { get; set; }
}

/// <summary>Resultado de importar un Excel de movimientos — cuántas filas se dieron de alta,
/// cuántas se descartaron por venir duplicadas (ya existentes o repetidas dentro del mismo
/// archivo) y qué materiales nuevos se crearon en el catálogo al vuelo.</summary>
public class ImportarMovimientosResultDTO
{
    public int TotalFilas { get; set; }
    public int Importados { get; set; }
    public int Duplicados { get; set; }
    public int MaterialesCreados { get; set; }
    public List<string> Errores { get; set; } = [];
}
