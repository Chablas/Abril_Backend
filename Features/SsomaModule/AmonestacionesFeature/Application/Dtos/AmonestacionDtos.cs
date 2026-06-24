namespace Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Dtos;

// ── Catálogos (init) ───────────────────────────────────────────────────────

public class AmonestacionInitDto
{
    public List<TipoSancionDto> TiposSancion { get; set; } = new();
    public List<InfraccionTipoDto> InfraccionesTipo { get; set; } = new();
    public List<AmonCatalogoDto> RacInfracciones { get; set; } = new();   // para penalización (monto)
    public List<AmonCatalogoDto> Proyectos { get; set; } = new();
    public List<AmonPartidaDto> Partidas { get; set; } = new();
    public decimal UitActual { get; set; }
}

public class TipoSancionDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string NivelGravedad { get; set; } = "";
    public bool GeneraSuspension { get; set; }
}

public class InfraccionTipoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
}

public class AmonCatalogoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public decimal? MontoFijo { get; set; }
    public decimal? FactorUit { get; set; }
}

public class AmonPartidaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
}

// ── Crear ──────────────────────────────────────────────────────────────────

public class AmonestacionCreateRequest
{
    public int ProyectoId { get; set; }
    public string Fecha { get; set; } = "";          // "yyyy-MM-dd"
    public int WorkerId { get; set; }
    public int? PartidaId { get; set; }
    public int TipoSancionId { get; set; }
    public int InfraccionTipoId { get; set; }
    public string Descripcion { get; set; } = "";
    public bool AplicaPenalizacion { get; set; }
    public int? SancionInfraccionId { get; set; }
    public int PuntosInfraccion { get; set; }
    public int? DiasSuspension { get; set; }
    public string? FechaInicioSuspension { get; set; }  // "yyyy-MM-dd"
    public string? FechaFinSuspension { get; set; }
    // Fotos como base64
    public List<AmonFotoUploadDto> Fotos { get; set; } = new();
}

public class AmonFotoUploadDto
{
    public string Base64 { get; set; } = "";
    public string NombreArchivo { get; set; } = "";
}

public class AmonestacionCreadaDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
}

// ── Lista ──────────────────────────────────────────────────────────────────

public class AmonestacionListQuery
{
    public int? ProyectoId { get; set; }
    public int? WorkerId { get; set; }
    public int? TipoSancionId { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AmonestacionPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}

public class AmonestacionListItemDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string ProyectoNombre { get; set; } = "";
    public DateTime Fecha { get; set; }
    public string WorkerNombre { get; set; } = "";
    public string WorkerDni { get; set; } = "";
    public string EmpresaNombre { get; set; } = "";
    public string TipoSancionNombre { get; set; } = "";
    public string NivelGravedad { get; set; } = "";
    public int PuntosInfraccion { get; set; }
    public bool AplicaPenalizacion { get; set; }
    public decimal MontoCalculado { get; set; }
}

// ── Detalle ────────────────────────────────────────────────────────────────

public class AmonestacionDetalleDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public int ProyectoId { get; set; }
    public string ProyectoNombre { get; set; } = "";
    public DateTime Fecha { get; set; }
    public int WorkerId { get; set; }
    public string WorkerNombre { get; set; } = "";
    public string WorkerDni { get; set; } = "";
    public string? WorkerCargo { get; set; }
    public string? WorkerCategoria { get; set; }
    public int? WorkerEdad { get; set; }
    public string EmpresaNombre { get; set; } = "";
    public bool EsEmpresaAbril { get; set; }
    public string? EmpresaLogoUrl { get; set; }
    public int? PartidaId { get; set; }
    public string? PartidaNombre { get; set; }
    public int TipoSancionId { get; set; }
    public string TipoSancionNombre { get; set; } = "";
    public string NivelGravedad { get; set; } = "";
    public bool GeneraSuspension { get; set; }
    public int InfraccionTipoId { get; set; }
    public string InfraccionTipoNombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public bool AplicaPenalizacion { get; set; }
    public int? SancionInfraccionId { get; set; }
    public string? SancionInfraccionNombre { get; set; }
    public decimal MontoCalculado { get; set; }
    public int PuntosInfraccion { get; set; }
    public int PuntosAcumulados { get; set; }
    public bool Inhabilitado { get; set; }
    public int? DiasSuspension { get; set; }
    public DateOnly? FechaInicioSuspension { get; set; }
    public DateOnly? FechaFinSuspension { get; set; }
    public int? PersonaReportaId { get; set; }
    public string? PersonaReportaNombre { get; set; }
    public string? PdfUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AmonFotoDto> Fotos { get; set; } = new();
}

public class AmonFotoDto
{
    public int Id { get; set; }
    public string Url { get; set; } = "";
    public string? NombreArchivo { get; set; }
    public int Orden { get; set; }
}

// ── Dashboard ──────────────────────────────────────────────────────────────

public class AmonestacionDashboardDto
{
    public int TotalAmonestaciones { get; set; }
    public int TrabajadoresConMas5Puntos { get; set; }
    public int TrabajadoresInhabilitados { get; set; }
    public int AmonestacionesMesActual { get; set; }
    public List<AmonPorTipoDto> PorTipoSancion { get; set; } = new();
    public List<AmonPorProyectoDto> PorProyecto { get; set; } = new();
    public List<AmonTendenciaDto> Tendencia { get; set; } = new();
}

public class AmonPorTipoDto
{
    public string TipoNombre { get; set; } = "";
    public int Total { get; set; }
}

public class AmonPorProyectoDto
{
    public string ProyectoNombre { get; set; } = "";
    public int Total { get; set; }
}

public class AmonTendenciaDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public int Total { get; set; }
}

// ── Puntaje del trabajador ─────────────────────────────────────────────────

public class WorkerPuntajeDto
{
    public int WorkerId { get; set; }
    public string Nombre { get; set; } = "";
    public string Dni { get; set; } = "";
    public string EmpresaNombre { get; set; } = "";
    public int PuntosAcumulados { get; set; }
    public bool Inhabilitado { get; set; }
    public List<AmonestacionListItemDto> Historial { get; set; } = new();
}
