namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models
{
    /// <summary>
    /// Acta de reunión (SIG-FO-17). Registra una reunión de un proyecto: puede agendarse
    /// a futuro (estado PROGRAMADA), reprogramarse varias veces (ver ReunionReprogramacion)
    /// y al realizarse concentra participantes, acuerdos y archivos adjuntos.
    /// </summary>
    public class Reunion
    {
        public int ReunionId { get; set; }
        public int ProjectId { get; set; }

        /// <summary>Correlativo por proyecto (ej. "Comité N°10").</summary>
        public int Numero { get; set; }
        public string Tema { get; set; } = null!;
        public string? ConvocadoPor { get; set; }
        public string? Lugar { get; set; }

        /// <summary>Fecha vigente de la reunión; cambia con cada reprogramación.</summary>
        public DateOnly Fecha { get; set; }
        public TimeOnly? HoraInicio { get; set; }
        public TimeOnly? HoraFin { get; set; }

        public int ReunionEstadoId { get; set; }
        public string? Observaciones { get; set; }

        /// <summary>Reunión de la que se promovió el tema de esta reunión.</summary>
        public int? ReunionAnteriorId { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
