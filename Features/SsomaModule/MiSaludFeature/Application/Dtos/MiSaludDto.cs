namespace Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Dtos
{
    public class MiSaludResumenDto
    {
        public int WorkerId { get; set; }
        public string? WorkerNombre { get; set; }

        // EMO activo
        public bool TieneEmo { get; set; }
        public int? EmoId { get; set; }
        public string? TipoEmo { get; set; }
        public string? Aptitud { get; set; }
        public DateOnly? FechaEmo { get; set; }
        public DateOnly? FechaVencimiento { get; set; }
        public int? DiasParaVencer { get; set; }

        // Restricciones vigentes del EMO activo
        public List<string> RestriccionesVigentes { get; set; } = [];

        // Último descanso
        public string? UltimoDescansoEstado { get; set; }
        public DateOnly? UltimoDescansoFechaFin { get; set; }
    }

    public class MiDescansoDto
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public DateOnly FechaInicio { get; set; }
        public DateOnly FechaFin { get; set; }
        public int? Dias { get; set; }
        public string? Motivo { get; set; }
        public string? Diagnostico { get; set; }
        public string? DiagnosticoCie10 { get; set; }
        public string? Estado { get; set; }
        public string? MotivoRechazo { get; set; }
        public string? UrlCertificado { get; set; }
        public string? UrlDocumento { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class CrearMiDescansoDto
    {
        public string Tipo { get; set; } = "Particular";
        public DateOnly FechaInicio { get; set; }
        public DateOnly FechaFin { get; set; }
        public int? Dias { get; set; }
        public string? Motivo { get; set; }
        public string? Diagnostico { get; set; }
        public string? DiagnosticoCie10 { get; set; }
        public IFormFile? Documento { get; set; }
    }

    public class MiDescansosFiltroDto
    {
        public int Page { get; set; } = 1;
    }
}
