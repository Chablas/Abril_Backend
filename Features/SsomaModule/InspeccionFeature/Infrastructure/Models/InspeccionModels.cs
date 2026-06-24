using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.InspeccionFeature.Infrastructure.Models;

public class SsomaInspeccionTipo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Ambito { get; set; } = "Seguridad";
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SsomaInspeccionChecklistItem> Items { get; set; } = [];
}

public class SsomaInspeccionChecklistItem
{
    public int Id { get; set; }
    public int TipoId { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;

    public SsomaInspeccionTipo? Tipo { get; set; }
}

public class SsomaInspeccion
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public int TipoId { get; set; }
    public int? EmpresaId { get; set; }
    public bool EsPlanificada { get; set; } = true;
    public DateTime Fecha { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }
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
    public string Estado { get; set; } = "Borrador";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }

    public Project? Proyecto { get; set; }
    public SsomaInspeccionTipo? Tipo { get; set; }
    public Contributor? Empresa { get; set; }
    public ICollection<SsomaInspeccionRespuesta> Respuestas { get; set; } = [];
    public ICollection<SsomaInspeccionHallazgo> Hallazgos { get; set; } = [];
    public ICollection<SsomaInspeccionFotoArea> FotosArea { get; set; } = [];
}

public class SsomaInspeccionRespuesta
{
    public int Id { get; set; }
    public int InspeccionId { get; set; }
    public int ItemId { get; set; }
    public string Resultado { get; set; } = "NA";
    public string? Observacion { get; set; }

    public SsomaInspeccion? Inspeccion { get; set; }
    public SsomaInspeccionChecklistItem? Item { get; set; }
}

public class SsomaInspeccionHallazgo
{
    public int Id { get; set; }
    public int InspeccionId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Menor";
    public string? Area { get; set; }
    public string? ResponsableNombre { get; set; }
    public string? ResponsableCargo { get; set; }
    public DateTime? FechaLimite { get; set; }
    public string Estado { get; set; } = "Abierto";
    public string? AccionCorrectiva { get; set; }
    public string? EvidenciaCierreUrl { get; set; }
    public DateTime? FechaCierre { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsomaInspeccion? Inspeccion { get; set; }
    public ICollection<SsomaInspeccionHallazgoFoto> Fotos { get; set; } = [];
}

public class SsomaInspeccionHallazgoFoto
{
    public int Id { get; set; }
    public int HallazgoId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsomaInspeccionHallazgo? Hallazgo { get; set; }
}

public class SsomaInspeccionFotoArea
{
    public int Id { get; set; }
    public int InspeccionId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int Orden { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsomaInspeccion? Inspeccion { get; set; }
}
