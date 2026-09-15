namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores
{
    /// <summary>Un retiro automático reciente, para el modal "Retiros Automáticos" (mismo lugar que
    /// "Interconsultas Pendientes"): informa a quién se retiró, de qué empresa, y por qué.</summary>
    public class RetiroAutomaticoRecienteDto
    {
        public int WorkerId { get; set; }
        public string WorkerNombre { get; set; } = string.Empty;
        public string? Dni { get; set; }
        public string? RazonSocial { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? EntregablesVencidos { get; set; }
        public DateTimeOffset EjecutadoEn { get; set; }
    }
}
