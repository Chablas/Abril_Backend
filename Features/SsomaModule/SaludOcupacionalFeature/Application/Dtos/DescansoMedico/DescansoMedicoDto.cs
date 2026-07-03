namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.DescansoMedico
{
    public class DescansoMedicoListItemDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string? WorkerNombre { get; set; }
        public string? WorkerDni { get; set; }
        public string? EmpresaNombre { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public DateOnly FechaInicio { get; set; }
        public DateOnly FechaFin { get; set; }
        public int Dias { get; set; }
        public string Estado { get; set; } = string.Empty;
        public bool ReportadoPorTrabajador { get; set; }
        public int? TopicoOrigenId { get; set; }
        public bool TrabajadorBloqueado { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class DescansoMedicoDetalleDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string? WorkerNombre { get; set; }
        public string? WorkerDni { get; set; }
        public int? ProyectoId { get; set; }
        public int? EmpresaId { get; set; }
        public string? EmpresaNombre { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public DateOnly FechaInicio { get; set; }
        public DateOnly FechaFin { get; set; }
        public int Dias { get; set; }
        public string? Motivo { get; set; }
        public string? Diagnostico { get; set; }
        public string? DiagnosticoCie10 { get; set; }
        public string? MedicoCertifica { get; set; }
        public string? Establecimiento { get; set; }
        public string? UrlCertificado { get; set; }
        public string? UrlDocumento { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? MotivoRechazo { get; set; }
        public int? AprobadoPorId { get; set; }
        public DateTimeOffset? FechaAprobacion { get; set; }
        public int? AccidenteId { get; set; }
        public bool EsRecaida { get; set; }
        public bool NotificadoGth { get; set; }
        public bool NotificadoJefe { get; set; }
        public bool ReportadoPorTrabajador { get; set; }
        public string? Observaciones { get; set; }
        public int? TopicoOrigenId { get; set; }
        public int? ProrrogaDelId { get; set; }
        public DateOnly? FechaAlta { get; set; }
        public int? AltaPorId { get; set; }
        public string? AltaObservaciones { get; set; }
        public int RegistradoPorId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class DarAltaDto
    {
        public string? Observaciones { get; set; }
    }

    public class DescansoSeguimientoDto
    {
        public int Id { get; set; }
        public int DescansoId { get; set; }
        public DateTimeOffset FechaSeguimiento { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string? RealizadoPorRol { get; set; }
        public int? RealizadoPorId { get; set; }
        public string? Nota { get; set; }
        public DateOnly? ProximaCita { get; set; }
        public string? UrlEvidencia { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class DescansoSeguimientoCreateDto
    {
        public string Tipo { get; set; } = string.Empty;
        public string? Nota { get; set; }
        public DateOnly? ProximaCita { get; set; }
        // UrlEvidencia se asigna en controller tras subir el archivo
        public string? UrlEvidencia { get; set; }
    }

    public class DescansoMedicoCreateDto
    {
        public int WorkerId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public DateOnly FechaInicio { get; set; }
        public DateOnly FechaFin { get; set; }
        public string? Motivo { get; set; }
        public string? Diagnostico { get; set; }
        public string? DiagnosticoCie10 { get; set; }
        public string? MedicoCertifica { get; set; }
        public string? Establecimiento { get; set; }
        public int? AccidenteId { get; set; }
        public bool EsRecaida { get; set; } = false;
        public int? TopicoOrigenId { get; set; }
        public int? ProrrogaDelId { get; set; }
        public int? ProyectoId { get; set; }
        public int? EmpresaId { get; set; }
        public string? Observaciones { get; set; }
        public bool ReportadoPorTrabajador { get; set; } = false;
        // Asignado en controller tras subir el archivo
        public string? UrlCertificado { get; set; }
    }

    public class DescansoMedicoUpdateDto
    {
        public DateOnly FechaInicio { get; set; }
        public DateOnly FechaFin { get; set; }
        public string? Motivo { get; set; }
        public string? Diagnostico { get; set; }
        public string? DiagnosticoCie10 { get; set; }
        public string? MedicoCertifica { get; set; }
        public string? Establecimiento { get; set; }
        public string? Observaciones { get; set; }
    }

    public class DescansoAprobarDto
    {
        public string? Observaciones { get; set; }
    }

    public class DescansoRechazarDto
    {
        public string MotivoRechazo { get; set; } = string.Empty;
    }

    public class DescansoMedicoFilterDto
    {
        public int? WorkerId { get; set; }
        public string? Estado { get; set; }
        public string? Tipo { get; set; }
        public int? EmpresaId { get; set; }
        public DateOnly? FechaDesde { get; set; }
        public DateOnly? FechaHasta { get; set; }
        public int Page { get; set; } = 1;
    }
}
