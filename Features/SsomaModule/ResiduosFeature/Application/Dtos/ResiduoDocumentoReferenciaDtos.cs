namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

public class ResiduoDocumentoReferenciaDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public string ArchivoUrl { get; set; } = "";
    public string? Version { get; set; }
    public bool Activo { get; set; }
}

public class ResiduoDocumentoReferenciaUpsertDto
{
    public string Tipo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public string? Version { get; set; }
    public bool Activo { get; set; } = true;
}
