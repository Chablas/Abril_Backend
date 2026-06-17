using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Dtos;

namespace Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Application.Dtos
{
    /// <summary>Un proyecto con su croquis asignado (si tiene), para la pantalla de asignación.</summary>
    public class ProjectCroquisItemDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;

        public int? ProjectCroquisId { get; set; }
        public string? ImageUrl { get; set; }
        public string? OriginalFileName { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
    }

    /// <summary>Un lote dibujado sobre el croquis. Puntos en coordenadas relativas (0–1).</summary>
    public class CroquisLoteDto
    {
        public int? ProjectCroquisLoteId { get; set; }
        public string NumeroLote { get; set; } = null!;
        public List<List<double>> Puntos { get; set; } = new();
    }

    /// <summary>Cuerpo para guardar el conjunto completo de lotes de un croquis.</summary>
    public class SaveCroquisLotesDto
    {
        public List<CroquisLoteDto> Lotes { get; set; } = new();
    }

    // ── Vista de Gestión (croquis-céntrica) ──────────────────────────────────

    /// <summary>Respuesta de la vista de gestión: croquis + catálogos para el formulario de alta.</summary>
    public class CroquisGestionResponseDto
    {
        public List<CroquisGestionDto> Croquis { get; set; } = new();
        public List<ProjectOptionDto> Projects { get; set; } = new();
        public List<CatalogOptionDto> Colindancias { get; set; } = new();
        public List<CatalogOptionDto> TiposConstruccion { get; set; } = new();
    }

    /// <summary>Un croquis registrado con sus lotes y los vecinos del proyecto (para asignar).</summary>
    public class CroquisGestionDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public int ProjectCroquisId { get; set; }
        public string ImageUrl { get; set; } = null!;
        public List<CroquisGestionLoteDto> Lotes { get; set; } = new();
        public List<VecinoListItemDto> Vecinos { get; set; } = new();
    }

    /// <summary>Lote dentro de la vista de Gestión, con su vecino asignado (si tiene).</summary>
    public class CroquisGestionLoteDto
    {
        public int ProjectCroquisLoteId { get; set; }
        public string NumeroLote { get; set; } = null!;
        public List<List<double>> Puntos { get; set; } = new();
        public int? VecinoId { get; set; }
        public string? VecinoNombre { get; set; }
    }

    /// <summary>Cuerpo para asignar (o quitar) el vecino de un lote.</summary>
    public class AssignVecinoLoteDto
    {
        public int? VecinoId { get; set; }
    }
}
