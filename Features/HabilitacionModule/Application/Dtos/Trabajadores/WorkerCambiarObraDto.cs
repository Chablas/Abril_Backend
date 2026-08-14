namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores
{
    public class WorkerCambiarObraDto
    {
        public int NuevoProyectoId { get; set; }
        public int? NuevaEmpresaId { get; set; }
        public DateTime FechaCambio { get; set; }

        /// <summary>FK a <c>puesto</c> al que pasa (opcional — si no se envía, se conserva el
        /// actual). Permite usar este mismo endpoint para un cambio de puesto sin cambio de
        /// obra ni de empresa.</summary>
        public int? PuestoId { get; set; }

        /// <summary>FK a <c>categoria</c> — cambio explícito de categoría, independiente del
        /// puesto (p.ej. el trabajador conserva el mismo puesto de presentación pero cambia de
        /// categoría de riesgo/EMO). Si no se envía, la categoría solo se resincroniza como
        /// efecto de <see cref="PuestoId"/> cuando ese sí cambia.</summary>
        public int? CategoriaId { get; set; }

        /// <summary>Nueva clasificación de riesgo (catálogo workers_obra_oficina_staff:
        /// Obra/Staff/Oficina Central). Si no se envía, se conserva la actual.</summary>
        public int? ObraOficinaStaffId { get; set; }
    }
}
