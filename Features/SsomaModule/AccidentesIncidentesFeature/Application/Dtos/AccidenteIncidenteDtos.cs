namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Dtos;

public class AccidenteIncidenteListItemDto
{
    public int Id { get; set; }
    public string ProyectoNombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public int TotalDocumentos { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DocumentoAdjuntoDto
{
    public int Id { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoArchivo { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }
    public string UrlSharepoint { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AccidenteIncidenteDetalleDto
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string ProyectoNombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public int? ResponsableId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<DocumentoAdjuntoDto> Documentos { get; set; } = [];
}

public class CrearAccidenteIncidenteRequest
{
    public int ProyectoId { get; set; }
    public DateTime Fecha { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Incidente";
    public string Estado { get; set; } = "Abierto";
    public int? ResponsableId { get; set; }
}

public class ActualizarAccidenteIncidenteRequest
{
    public int ProyectoId { get; set; }
    public DateTime Fecha { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Incidente";
    public string Estado { get; set; } = "Abierto";
    public int? ResponsableId { get; set; }
}

public class SubirDocumentoRequest
{
    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoArchivo { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }
    public string ContenidoBase64 { get; set; } = string.Empty;
}
