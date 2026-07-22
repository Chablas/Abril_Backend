namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Payload del formulario "Nueva solicitud de personal". La justificación y el sustento
    /// (adjunto multipart, aparte del JSON) son de la solicitud completa; cada elemento de
    /// <see cref="Vacantes"/> genera un requerimiento independiente.
    /// </summary>
    public class SolicitudPersonalCreateDto
    {
        public string? Justificacion { get; set; }
        public List<VacanteCreateDto> Vacantes { get; set; } = new();
    }

    public class VacanteCreateDto
    {
        public int PuestoId { get; set; }
        public int TipoRequerimientoId { get; set; }
        public int ProjectId { get; set; }
        public DateOnly FechaRequeridaIngreso { get; set; }
    }

    /// <summary>Resultado de crear la solicitud: los códigos REQ-AAAA-NNNN generados.</summary>
    public class SolicitudPersonalCreateResultDto
    {
        public int SolicitudId { get; set; }
        public List<string> Codigos { get; set; } = new();
    }
}
