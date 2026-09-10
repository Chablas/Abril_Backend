using Abril_Backend.Features.Ssoma.Penalidad.Dtos;

namespace Abril_Backend.Features.Ssoma.Penalidad.Services;

public interface IPenalidadNotificationService
{
    /// <summary>Al registrar: aviso interno a Residente para que apruebe.</summary>
    Task NotificarPendienteResidenteAsync(PenalidadDetalleDto p);

    /// <summary>Al aprobar Residente: aviso interno a Gerencia Inmobiliaria para que apruebe.</summary>
    Task NotificarPendienteGerenciaAsync(PenalidadDetalleDto p);

    /// <summary>Al aprobar Gerencia: notificación formal al contratista con el plazo de descargo.</summary>
    Task NotificarDescargoAlContratistaAsync(PenalidadDetalleDto p, string contratistaEmail);

    /// <summary>Recordatorio al contratista antes de vencer el plazo de descargo.</summary>
    Task RecordatorioDescargoAsync(PenalidadDetalleDto p, string contratistaEmail);

    /// <summary>Al presentar (o vencer) el descargo: aviso interno a SSOMA para que evalúe.</summary>
    Task NotificarEvaluacionPendienteAsync(PenalidadDetalleDto p);

    /// <summary>Al evaluar SSOMA: aviso interno a Gerencia Inmobiliaria para la decisión final.</summary>
    Task NotificarDecisionPendienteAsync(PenalidadDetalleDto p);

    /// <summary>Al decidir Gerencia (Aplicada/Anulada): comunica a Contrata, Oficina Técnica,
    /// Residente y Costos/Presupuestos — recién acá se hace visible el argumento de SSOMA.</summary>
    Task NotificarDecisionFinalAsync(PenalidadDetalleDto p, string? contratistaEmail);

    /// <summary>El contratista presentó una apelación (única, con evidencia nueva): aviso
    /// interno a Gerencia Inmobiliaria para que decida de nuevo, directo (sin SSOMA/Residente).</summary>
    Task NotificarApelacionPresentadaAsync(PenalidadDetalleDto p);
}
