namespace Abril_Backend.Features.GestionAdministrativa.Shared.Dtos
{
    /// <summary>
    /// La planilla grupal que preparó el consolidador y que todavía espera su Consolidado del S10.
    /// Las planillas de rendición que la comparten reciben la misma: es un solo documento.
    /// </summary>
    public class PlanillaGrupalDto
    {
        public int Id { get; set; }

        /// <summary>
        /// Código de la rendición grupal, <c>CONS-ÁREA-AAAA-NNN</c>: va impreso en la planilla y es
        /// el que hereda el consolidado al subirse el S10.
        /// </summary>
        public string Codigo { get; set; } = string.Empty;

        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;
        public DateTimeOffset PreparadaAt { get; set; }

        /// <summary>
        /// Planillas de rendición que cubre (sus vínculos vigentes), ordenadas por código. El S10 se
        /// sube para todas a la vez: es el documento que se registró.
        /// </summary>
        public List<ConsolidadoS10RendicionDto> Rendiciones { get; set; } = new();
    }
}
