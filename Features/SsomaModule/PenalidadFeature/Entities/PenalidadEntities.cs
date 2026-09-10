using Abril_Backend.Features.Ssoma.Rac.Entities;

namespace Abril_Backend.Features.Ssoma.Penalidad.Entities;

/// <summary>
/// ssoma_penalidad — penalidad económica a una empresa contratista. Entidad independiente de
/// SsomaRac: puede originarse en un RAC, una Amonestación, o registrarse directa (ej. hallazgo
/// documentario), por eso <see cref="OrigenTipo"/>/<see cref="OrigenId"/> son una referencia
/// débil (sin FK) y no una relación obligatoria.
/// </summary>
public class SsomaPenalidad
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";

    /// <summary>RAC | AMONESTACION | DIRECTO — de dónde nació la penalidad, solo para trazabilidad.</summary>
    public string OrigenTipo { get; set; } = "DIRECTO";
    public int? OrigenId { get; set; }

    public int EmpresaId { get; set; }              // FK contributor (contributor_id)
    public int ProyectoId { get; set; }             // FK project (project_id)
    public int InfraccionId { get; set; }           // FK ssoma_rac_infraccion
    public string Severidad { get; set; } = "";     // BAJO | MEDIO | ALTO | CRITICO

    /// <summary>Calculado al registrar: Infraccion.MontoFijo o FactorUit × UIT del año vigente.</summary>
    public decimal MontoCalculado { get; set; }
    /// <summary>Solo Gerencia puede fijar un monto distinto al calculado, al decidir.</summary>
    public decimal? MontoFinal { get; set; }
    public string? MontoAjustadoMotivo { get; set; }
    public decimal UitReferencia { get; set; }

    public string? DescripcionOcurrido { get; set; }

    /// <summary>
    /// Registrada | Rechazada | PendienteResidente | PendienteGerenciaInmobiliaria |
    /// NotificadaEnDescargo | DescargoPresentado | EnEvaluacionSsoma |
    /// PendienteDecisionGerencia | Aplicada | Anulada | EnApelacion
    /// </summary>
    public string Estado { get; set; } = "Registrada";

    public int PlazoDescargoHoras { get; set; } = 48;
    public DateTime? PlazoDescargoVenceEn { get; set; }
    public bool DescargoPorIncomparecencia { get; set; }

    // ── Aprobación previa (antes de notificar al contratista) ──────────────
    public int? AprobadoResidentePorId { get; set; }
    public DateTime? AprobadoResidenteEn { get; set; }
    public string? MotivoRechazoResidente { get; set; }

    public int? AprobadoGerenciaPorId { get; set; }
    public DateTime? AprobadoGerenciaEn { get; set; }
    public string? MotivoRechazoGerencia { get; set; }

    // ── Descargo del contratista ────────────────────────────────────────────
    public string? DescargoTexto { get; set; }
    public string? DocumentoUrl { get; set; }
    public DateTime? DescargoFecha { get; set; }
    public int? DescargoUsuarioId { get; set; }

    // ── Evaluación SSOMA del descargo — NO se expone al contratista hasta que
    // Gerencia decide (evita juez y parte: quien origina no es quien resuelve). ──
    public string? ArgumentoSsoma { get; set; }
    /// <summary>Aprobar | Rechazar — lo que SSOMA recomienda a Gerencia.</summary>
    public string? RecomendacionSsoma { get; set; }
    public int? EvaluadoPorSsomaId { get; set; }
    public DateTime? EvaluadoSsomaEn { get; set; }

    // ── Decisión final de Gerencia Inmobiliaria ─────────────────────────────
    public string? ResolucionTexto { get; set; }
    /// <summary>Aplicada | Anulada</summary>
    public string? ResolucionTipo { get; set; }
    public string? MotivoObjecionGerencia { get; set; }
    public int? ResueltoPorId { get; set; }
    public DateTime? ResueltaEn { get; set; }

    public string? PdfNotificacionUrl { get; set; }
    public string? PdfResolucionUrl { get; set; }

    // ── Apelación — una sola vez, solo desde "Aplicada", exige evidencia nueva.
    // Va directo a Gerencia (no vuelve a pasar por Residente ni por la evaluación de SSOMA). ──
    public bool ApelacionUsada { get; set; }
    public int PlazoApelacionDias { get; set; } = 5;
    public DateTime? PlazoApelacionVenceEn { get; set; }
    public string? ApelacionTexto { get; set; }
    public string? ApelacionDocumentoUrl { get; set; }
    public DateTime? ApelacionFecha { get; set; }
    public int? ApelacionUsuarioId { get; set; }

    // ── Auditoría ────────────────────────────────────────────────────────────
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navegación interna ───────────────────────────────────────────────────
    public SsomaRacInfraccion? Infraccion { get; set; }
    public List<SsomaPenalidadEstadoHistorial> Historial { get; set; } = new();
}

/// <summary>
/// ssoma_penalidad_estado_historial — bitácora inmutable de transiciones de estado, una fila
/// por cada cambio. La escribe <c>PenalidadEstadoHistorialInterceptor</c> en el mismo
/// SaveChanges que mueve la penalidad, calcado del mismo patrón que ya usa GTH
/// (RequerimientoEstadoHistorialInterceptor) para que ningún cambio de estado quede sin
/// registrar y el frontend pueda pintar el stepper de seguimiento leyendo esta tabla.
/// </summary>
public class SsomaPenalidadEstadoHistorial
{
    public int Id { get; set; }

    public int PenalidadId { get; set; }
    public SsomaPenalidad? Penalidad { get; set; }

    /// <summary>Null solo en la primera fila (el alta: no venía de ningún estado).</summary>
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = "";

    public DateTime CambioDateTime { get; set; }
    public int? CambioUserId { get; set; }

    public DateTime CreatedDateTime { get; set; }
}

/// <summary>
/// ssoma_gestion_previa_empresa — bitácora libre de gestión previa a una penalidad formal
/// (correos de advertencia, cartas de preocupación, reuniones, llamadas) que SSOMA registra por
/// empresa, independiente de que exista o no un RAC/Penalidad puntual. Se muestra en el momento
/// de tipificar una penalidad nueva, junto al conteo de penalidades previas, para que
/// Residente/Gerencia decidan con el cuadro completo de la relación con esa contratista.
/// </summary>
public class GestionPreviaEmpresa
{
    public int Id { get; set; }

    public int EmpresaId { get; set; }         // FK contributor (contributor_id)
    public int? ProyectoId { get; set; }       // FK project (project_id) — opcional, puede ser a nivel corporativo

    /// <summary>Correo | CartaPreocupacion | Reunion | Llamada | Otro</summary>
    public string Tipo { get; set; } = "";
    public DateTime Fecha { get; set; }
    public string Descripcion { get; set; } = "";
    public string? AdjuntoUrl { get; set; }

    public int? RegistradoPorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
