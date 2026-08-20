namespace Abril_Backend.Features.Evaluaciones.Application.Dtos
{
    // ─── CREATE (lo envía un usuario contratista logueado) ─────────────────────
    public class EvPrevencionistaEvaluacionCreateDto
    {
        public int EvaluadoUserId { get; set; }
        public int ProyectoId { get; set; }
        public string? Comentario { get; set; }
        public List<EvPrevencionistaDetalleCreateDto> Detalles { get; set; } = [];
    }

    public class EvPrevencionistaDetalleCreateDto
    {
        public int? PlantillaId { get; set; }
        public string Criterio { get; set; } = string.Empty;
        public int Puntaje { get; set; }
    }

    // ─── INICIO (pantalla evaluar, dentro del portal contratista) ──────────────
    public class EvPrevencionistaInicioDto
    {
        public EvPeriodoDto? Periodo { get; set; }
        public List<EvSupervisorContratistaCriterioDto> Plantilla { get; set; } = [];
        public List<EvPrevencionistaAEvaluarDto> AEvaluar { get; set; } = [];
    }

    public class EvPrevencionistaAEvaluarDto
    {
        public int EvaluadoUserId { get; set; }
        public string EvaluadoNombre { get; set; } = string.Empty;
        public string EvaluadoPuesto { get; set; } = string.Empty; // "Prevencionista" / "Coordinador SSOMA"
        public int ProyectoId { get; set; }
        public string ProyectoNombre { get; set; } = string.Empty;
        public bool YaEvalue { get; set; }
    }

    // ─── MI PERFIL (el propio prevencionista/coordinador — SIN identidad del evaluador) ─
    public class EvPrevencionistaMiPerfilDto
    {
        public decimal? PromedioGeneral { get; set; }
        public int TotalEvaluaciones { get; set; }
        public List<string> Comentarios { get; set; } = [];
    }

    // ─── DASHBOARD (solo Jefe SSOMA — con identidad del contratista evaluador) ─
    public class EvPrevencionistaDashboardDto
    {
        public int TotalEvaluaciones { get; set; }
        public decimal? PromedioGeneral { get; set; }
        public List<EvPrevencionistaResumenDto> Evaluaciones { get; set; } = [];
    }

    public class EvPrevencionistaResumenDto
    {
        public int EvaluacionId { get; set; }
        public int EvaluadoUserId { get; set; }
        public string EvaluadoNombre { get; set; } = string.Empty;
        public int ProyectoId { get; set; }
        public string ProyectoNombre { get; set; } = string.Empty;
        public string EvaluadorContributorNombre { get; set; } = string.Empty;
        public decimal? Nota { get; set; }
        public string? Comentario { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
