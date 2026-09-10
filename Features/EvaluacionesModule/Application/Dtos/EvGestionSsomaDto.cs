namespace Abril_Backend.Features.Evaluaciones.Application.Dtos
{
    // ─── CREATE ────────────────────────────────────────────────────────────────
    public class EvGestionSsomaEvaluacionCreateDto
    {
        // Cuando el evaluador es Prevencionista: null = evaluación anónima a su
        // Coordinador SSOMA (D4, el servidor resuelve el destinatario, nunca el
        // cliente); con valor = evaluación identificada a otro Prevencionista de
        // su mismo proyecto (D5).
        public int? EvaluadoUserId { get; set; }
        public string? Fortalezas { get; set; }
        public string? OportunidadesMejora { get; set; }
        public List<EvGestionSsomaDetalleCreateDto> Detalles { get; set; } = [];
    }

    public class EvGestionSsomaDetalleCreateDto
    {
        public int? PlantillaId { get; set; }
        public string Criterio { get; set; } = string.Empty;
        public int Puntaje { get; set; }
    }

    // Resuelto en el servidor a partir del rol de quien llama — el cliente nunca
    // decide si la evaluación es anónima ni a quién corresponde evaluar cuando
    // el evaluador es Prevencionista (D4).
    public class EvGestionSsomaContextoDto
    {
        public bool Valido { get; set; }
        public string? Error { get; set; }
        public bool EsAnonimo { get; set; }
        public string EvaluadorRol { get; set; } = string.Empty;
        public int EvaluadoUserId { get; set; }
        public string EvaluadoRol { get; set; } = string.Empty;
        public int? ProyectoId { get; set; }
    }

    // ─── INICIO (pantalla evaluar) ──────────────────────────────────────────────
    public class EvGestionSsomaInicioDto
    {
        public EvPeriodoDto? Periodo { get; set; }
        /// <summary>Criterios para cuando el evaluado es Coordinador SSOMA (liderazgo de equipo).</summary>
        public List<EvSupervisorContratistaCriterioDto> PlantillaCoordinador { get; set; } = [];
        /// <summary>Criterios para cuando el evaluado es Prevencionista (desempeño operativo).</summary>
        public List<EvSupervisorContratistaCriterioDto> PlantillaPrevencionista { get; set; } = [];

        // D1 (Jefe SSOMA) y D3 (Coordinador SSOMA): a quién le falta/ya evaluó.
        public List<EvGestionSsomaAEvaluarDto> Prevencionistas { get; set; } = [];

        // D2 (solo Jefe SSOMA).
        public List<EvGestionSsomaAEvaluarDto> Coordinadores { get; set; } = [];

        // D4 (solo Prevencionista, anónima): su propio Coordinador SSOMA del
        // proyecto. Null si el proyecto no tiene coordinador asignado.
        public EvGestionSsomaAEvaluarDto? MiCoordinador { get; set; }
        public bool YaEvalueMiCoordinador { get; set; }
    }

    public class EvGestionSsomaAEvaluarDto
    {
        public int UserId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public int? ProyectoId { get; set; }
        public string? ProyectoNombre { get; set; }
        public bool YaEvalue { get; set; }
        public decimal? NotaPrevia { get; set; }
    }

    // ─── PENDIENTES (Jefe SSOMA) ────────────────────────────────────────────────
    public class EvGestionSsomaPendienteDto
    {
        public int UserId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string EmailCorporativo { get; set; } = string.Empty;
        public string Relacion { get; set; } = string.Empty; // "D1", "D2", "D3", "D4"
    }

    public class EvGestionSsomaCumplimientoDto
    {
        public int TotalEsperadas { get; set; }
        public int TotalCompletadas { get; set; }
        public List<EvGestionSsomaPendienteDto> Pendientes { get; set; } = [];
    }

    // ─── RESULTADOS (Jefe SSOMA) ─────────────────────────────────────────────────
    public class EvGestionSsomaResultadosDto
    {
        public EvPeriodoDto? Periodo { get; set; }
        public int TotalRespuestas { get; set; }
        public decimal? PromedioGeneral { get; set; }
        public List<EvGestionSsomaCriterioPromedioDto> PromediosPorCriterio { get; set; } = [];
        public List<EvGestionSsomaResumenDto> Evaluaciones { get; set; } = [];
    }

    public class EvGestionSsomaCriterioPromedioDto
    {
        public string Criterio { get; set; } = string.Empty;
        public decimal Promedio { get; set; }
    }

    // Evaluado + relación + nota son visibles para el Jefe SSOMA (que gestiona
    // el consolidado); el autor solo se expone cuando la relación no es D4.
    public class EvGestionSsomaResumenDto
    {
        public string Relacion { get; set; } = string.Empty; // "D1", "D2", "D3", "D4"
        public string EvaluadoNombre { get; set; } = string.Empty;
        public string? EvaluadorNombre { get; set; } // null en D4
        public decimal? Nota { get; set; }
        public string? Fortalezas { get; set; }
        public string? OportunidadesMejora { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ─── MIS RESULTADOS (Coordinador SSOMA / Prevencionista sobre sí mismos) ────
    // Mismo shape que Resultados pero filtrado por evaluado_user_id = quien llama,
    // nunca expone quién evaluó (D1-D4 son todas anónimas de cara al evaluado, ver
    // ResolverRelacion — solo el Jefe SSOMA ve autoría en la vista consolidada).
    public class EvGestionSsomaMisResultadosDto
    {
        public EvPeriodoDto? Periodo { get; set; }
        public int TotalRespuestas { get; set; }
        public decimal? PromedioGeneral { get; set; }
        public List<EvGestionSsomaCriterioPromedioDto> PromediosPorCriterio { get; set; } = [];
        public List<string> Comentarios { get; set; } = [];
        public List<EvGestionSsomaTendenciaDto> Tendencia { get; set; } = [];
    }

    public class EvGestionSsomaTendenciaDto
    {
        public int Mes { get; set; }
        public int Anio { get; set; }
        public string NombreMes { get; set; } = string.Empty;
        public decimal? Promedio { get; set; }
    }

    // ─── PLAN DE ACCIÓN (redactado por Coordinador SSOMA/Prevencionista sobre sus propios resultados) ──
    public class EvGestionSsomaPlanAccionDto
    {
        public int Id { get; set; }
        public int PeriodoId { get; set; }
        public string Criterio { get; set; } = string.Empty;
        public string Accion { get; set; } = string.Empty;
        public string Meta { get; set; } = string.Empty;
        public DateOnly? FechaLimite { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class EvGestionSsomaPlanAccionCreateDto
    {
        public string Criterio { get; set; } = string.Empty;
        public string Accion { get; set; } = string.Empty;
        public string Meta { get; set; } = string.Empty;
        public DateOnly? FechaLimite { get; set; }
    }

    public class EvGestionSsomaPlanAccionUpdateDto
    {
        public string Accion { get; set; } = string.Empty;
        public string Meta { get; set; } = string.Empty;
        public DateOnly? FechaLimite { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}
