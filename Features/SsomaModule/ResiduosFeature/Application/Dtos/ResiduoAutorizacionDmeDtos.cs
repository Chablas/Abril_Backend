namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

public class ResiduoAutorizacionDmeDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Municipalidad { get; set; } = "";
    public string NumeroResolucion { get; set; } = "";
    public int? EscombreraDestinoId { get; set; }
    public string? NombreEscombreraDestino { get; set; }
    public DateOnly VigenciaDesde { get; set; }
    public DateOnly VigenciaHasta { get; set; }
    public string? PlacasAutorizadas { get; set; }
    public string Estado { get; set; } = "";
    public string? ArchivoUrl { get; set; }
}

public class ResiduoAutorizacionDmeUpsertDto
{
    public int ProjectId { get; set; }
    public string Municipalidad { get; set; } = "";
    public string NumeroResolucion { get; set; } = "";
    public int? EscombreraDestinoId { get; set; }
    public DateOnly VigenciaDesde { get; set; }
    public DateOnly VigenciaHasta { get; set; }
    public string? PlacasAutorizadas { get; set; }
    public string Estado { get; set; } = "VIGENTE";
}
