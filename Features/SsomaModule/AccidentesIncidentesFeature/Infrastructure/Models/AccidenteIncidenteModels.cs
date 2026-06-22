using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Models;

public class SsomaAccidenteIncidente
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public DateTime Fecha { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Incidente";
    public string Estado { get; set; } = "Abierto";
    public int? ResponsableId { get; set; }
    public int? UsuarioId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Project? Proyecto { get; set; }
    public ICollection<SsomaAccidenteDocumento> Documentos { get; set; } = [];
}

public class SsomaAccidenteDocumento
{
    public int Id { get; set; }
    public int AccidenteId { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoArchivo { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }
    public string UrlSharepoint { get; set; } = string.Empty;
    public int? UsuarioId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsomaAccidenteIncidente? Accidente { get; set; }
}
