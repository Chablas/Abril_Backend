namespace Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Dtos;

/// <summary>Un día/proyecto en el que la empresa fue tareada y debe subir su charla.</summary>
public class CharlaContratistaPendienteDto
{
    public int ProyectoId { get; set; }
    public string ProyectoNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public int CantidadPersonasTareadas { get; set; }
    public bool YaSubida { get; set; }
    public int? CharlaId { get; set; }
}

public class CharlaContratistaUploadRequest
{
    public int ProyectoId { get; set; }
    public string Fecha { get; set; } = string.Empty; // "yyyy-MM-dd"
    public string Tema { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? EvidenciaBase64 { get; set; }
    public string? EvidenciaNombre { get; set; }
}

public class CharlaContratistaDto
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string ProyectoNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public string Tema { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? EvidenciaUrl { get; set; }
    public string? EvidenciaNombre { get; set; }
    public DateTime CreatedAt { get; set; }
}
