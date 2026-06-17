namespace Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Dtos;

public class InspeccionTipoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Ambito { get; set; } = string.Empty;
}

public class InspeccionChecklistItemDto
{
    public int Id { get; set; }
    public int TipoId { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public int Orden { get; set; }
}

public class InspeccionRespuestaRequest
{
    public int ItemId { get; set; }
    public string Resultado { get; set; } = "NA";
    public string? Observacion { get; set; }
}

public class InspeccionHallazgoRequest
{
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Menor";
    public string? Area { get; set; }
    public string? ResponsableNombre { get; set; }
    public string? ResponsableCargo { get; set; }
    public DateTime? FechaLimite { get; set; }
    public string? AccionCorrectiva { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public List<string> FotosBase64 { get; set; } = [];
}

public class CrearInspeccionRequest
{
    public int ProyectoId { get; set; }
    public int TipoId { get; set; }
    public int? EmpresaId { get; set; }
    public bool EsPlanificada { get; set; } = true;
    public DateTime Fecha { get; set; }
    public string? HoraInicio { get; set; }
    public string? HoraFin { get; set; }
    public string? Area { get; set; }
    public string? ResponsableArea { get; set; }
    public string? InspectorNombre { get; set; }
    public string? InspectorCargo { get; set; }
    public string? InspectorEmpresa { get; set; }
    public string? FirmaInspectorBase64 { get; set; }
    public string? RepresentanteNombre { get; set; }
    public string? RepresentanteCargo { get; set; }
    public string? FirmaRepresentanteBase64 { get; set; }
    public string? DescripcionCausas { get; set; }
    public string? Conclusiones { get; set; }
    public List<InspeccionRespuestaRequest> Respuestas { get; set; } = [];
    public List<InspeccionHallazgoRequest> Hallazgos { get; set; } = [];
}

public class CerrarHallazgoRequest
{
    public string AccionCorrectiva { get; set; } = string.Empty;
    public string? EvidenciaCierreBase64 { get; set; }
}

public class InspeccionHallazgoFotoDto
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
}

public class InspeccionHallazgoDto
{
    public int Id { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string? ResponsableNombre { get; set; }
    public string? ResponsableCargo { get; set; }
    public DateTime? FechaLimite { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? AccionCorrectiva { get; set; }
    public string? EvidenciaCierreUrl { get; set; }
    public DateTime? FechaCierre { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public List<InspeccionHallazgoFotoDto> Fotos { get; set; } = [];
}

public class InspeccionRespuestaDto
{
    public int ItemId { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public int Orden { get; set; }
    public string Resultado { get; set; } = string.Empty;
    public string? Observacion { get; set; }
}

public class InspeccionDetalleDto
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string ProyectoNombre { get; set; } = string.Empty;
    public int TipoId { get; set; }
    public string TipoNombre { get; set; } = string.Empty;
    public string TipoAmbito { get; set; } = string.Empty;
    public int? EmpresaId { get; set; }
    public string? EmpresaNombre { get; set; }
    public bool EsPlanificada { get; set; }
    public DateTime Fecha { get; set; }
    public string? HoraInicio { get; set; }
    public string? HoraFin { get; set; }
    public string? Area { get; set; }
    public string? ResponsableArea { get; set; }
    public string? InspectorNombre { get; set; }
    public string? InspectorCargo { get; set; }
    public string? InspectorEmpresa { get; set; }
    public string? FirmaInspectorUrl { get; set; }
    public string? RepresentanteNombre { get; set; }
    public string? RepresentanteCargo { get; set; }
    public string? FirmaRepresentanteUrl { get; set; }
    public string? DescripcionCausas { get; set; }
    public string? Conclusiones { get; set; }
    public int TotalItems { get; set; }
    public int TotalCumple { get; set; }
    public int TotalNoCumple { get; set; }
    public int TotalNa { get; set; }
    public decimal? TasaCumplimiento { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<InspeccionRespuestaDto> Respuestas { get; set; } = [];
    public List<InspeccionHallazgoDto> Hallazgos { get; set; } = [];
}

public class InspeccionListItemDto
{
    public int Id { get; set; }
    public string ProyectoNombre { get; set; } = string.Empty;
    public string TipoNombre { get; set; } = string.Empty;
    public string TipoAmbito { get; set; } = string.Empty;
    public string? EmpresaNombre { get; set; }
    public bool EsPlanificada { get; set; }
    public DateTime Fecha { get; set; }
    public string? Area { get; set; }
    public string? InspectorNombre { get; set; }
    public int TotalHallazgos { get; set; }
    public int HallazgosCriticos { get; set; }
    public int HallazgosAbiertos { get; set; }
    public decimal? TasaCumplimiento { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class InspeccionDashboardDto
{
    public int TotalInspecciones { get; set; }
    public int TotalEsteMes { get; set; }
    public int HallazgosAbiertos { get; set; }
    public int HallazgosCriticosAbiertos { get; set; }
    public decimal? TasaCumplimientoPromedio { get; set; }
    public decimal? TasaCumplimientoEsteMes { get; set; }
    public List<InspeccionTendenciaMensualDto> TendenciaMensual { get; set; } = [];
    public List<InspeccionPorTipoDto> PorTipo { get; set; } = [];
    public List<InspeccionHallazgoPorAreaDto> HallazgosPorArea { get; set; } = [];
    public List<InspeccionHallazgoRecurrenteDto> HallazgosRecurrentes { get; set; } = [];
}

public class InspeccionTendenciaMensualDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string MesNombre { get; set; } = string.Empty;
    public int Total { get; set; }
    public decimal? TasaPromedio { get; set; }
}

public class InspeccionPorTipoDto
{
    public string TipoNombre { get; set; } = string.Empty;
    public string Ambito { get; set; } = string.Empty;
    public int Total { get; set; }
    public decimal? TasaPromedio { get; set; }
}

public class InspeccionHallazgoPorAreaDto
{
    public string Area { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Criticos { get; set; }
    public int Abiertos { get; set; }
}

public class InspeccionHallazgoRecurrenteDto
{
    public string Descripcion { get; set; } = string.Empty;
    public int Ocurrencias { get; set; }
    public string UltimoTipo { get; set; } = string.Empty;
}

public class HallazgoListItemDto
{
    public int Id { get; set; }
    public int InspeccionId { get; set; }
    public string? Proyecto { get; set; }
    public DateTime? FechaInspeccion { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public string? Area { get; set; }
    public string? ResponsableNombre { get; set; }
    public string? ResponsableCargo { get; set; }
    public DateTime? FechaLimite { get; set; }
    public string? AccionCorrectiva { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime? FechaCierre { get; set; }
    public List<string> FotosUrls { get; set; } = [];
}

public class LevantarHallazgoDto
{
    public string Estado { get; set; } = string.Empty;
    public string? EvidenciaUrl { get; set; }
    public string? EvidenciaNombre { get; set; }
}
