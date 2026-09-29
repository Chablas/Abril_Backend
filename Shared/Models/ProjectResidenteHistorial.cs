namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// project_residente_historial — bitácora de cambios del residente del proyecto
    /// (<see cref="Project.ResidenteWorkersId"/>): una fila por cambio, con el residente que salió,
    /// el que entró, quién lo cambió y cuándo. La escribe <c>ResidenteHistorialInterceptor</c> en el
    /// mismo SaveChanges que cambia el proyecto, venga de la pantalla que venga.
    ///
    /// Sirve para saber quién era el residente en una fecha: el <see cref="WorkersIdNuevo"/> del
    /// último cambio anterior a esa fecha; antes del primer cambio registrado, el
    /// <see cref="WorkersIdAnterior"/> de ese primer cambio; sin cambios, el residente actual.
    /// </summary>
    public class ProjectResidenteHistorial
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        /// <summary>Null si el proyecto no tenía residente.</summary>
        public int? WorkersIdAnterior { get; set; }
        /// <summary>Null si el proyecto quedó sin residente.</summary>
        public int? WorkersIdNuevo { get; set; }

        public DateTime CambioDateTime { get; set; }
        public int? CambioUserId { get; set; }

        public DateTime CreatedDateTime { get; set; }
    }
}
