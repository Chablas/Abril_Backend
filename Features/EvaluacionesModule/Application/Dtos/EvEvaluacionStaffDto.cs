namespace Abril_Backend.Features.Evaluaciones.Application.Dtos
{
    // ─── PENDIENTES (staff del proyecto del Residente aún no evaluado en el período) ──
    public class EvEvaluacionStaffPendienteDto
    {
        public int WorkerId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public int PuestoId { get; set; }
        public string PuestoNombre { get; set; } = string.Empty;
    }

    // ─── PLANTILLA (criterios a mostrar para el puesto del evaluado) ───────────────
    public class EvStaffPlantillaCriterioDto
    {
        public int Id { get; set; }
        public string Criterio { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public int Orden { get; set; }
    }

    // ─── CREATE ─────────────────────────────────────────────────────────────────
    public class EvEvaluacionStaffCreateDto
    {
        public int EvaluadoWorkerId { get; set; }
        public string? Comentario { get; set; }
        public List<EvEvaluacionStaffDetalleCreateDto> Detalles { get; set; } = [];
    }

    public class EvEvaluacionStaffDetalleCreateDto
    {
        public int? PlantillaId { get; set; }
        public string Criterio { get; set; } = string.Empty;
        public int Puntaje { get; set; }
    }

    // ─── RESULTADOS (promedios por trabajador/puesto) ──────────────────────────────
    public class EvEvaluacionStaffResultadoDto
    {
        public int WorkerId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public int PuestoId { get; set; }
        public string PuestoNombre { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public decimal? PromedioNota { get; set; }
        public string? Comentario { get; set; }
    }
}
