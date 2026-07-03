namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models
{
    /// <summary>
    /// Solicitud de salida — cabecera. Los detalles del/los trayecto(s) viven en GaSolicitudTrayecto.
    /// </summary>
    public class GaSolicitudSalida
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public DateOnly FechaSalida { get; set; }
        /// <summary>FK a <c>ga_estado_aprobacion</c>. Ver <see cref="EstadosSalida.Aprobacion"/>.</summary>
        public int EstadoAprobacionId { get; set; } = EstadosSalida.Aprobacion.Pendiente;
        /// <summary>FK a <c>ga_estado_rendicion</c>. Ver <see cref="EstadosSalida.Rendicion"/>.</summary>
        public int EstadoRendicionId { get; set; } = EstadosSalida.Rendicion.NoRendido;
        public int? RegistradoPorId { get; set; }
        /// <summary>FK a <c>workers.id</c> del trabajador que es la jefatura que debe aprobar.
        /// El correo del aprobador se deriva de <c>workers.email_corporativo</c>.</summary>
        public int? AprobadorWorkerId { get; set; }
        public DateTimeOffset? FechaDecision { get; set; }
        public string? MotivoRechazo { get; set; }
        public int? RendicionId { get; set; }
        /// <summary>Hora real en la que la persona salió, registrada por recepción. Dato extra — no bloquea ningún flujo.</summary>
        public TimeOnly? HoraSalidaReal { get; set; }
        public int? HoraSalidaRealRegistradaPorId { get; set; }
        public DateTimeOffset? HoraSalidaRealRegistradaAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
