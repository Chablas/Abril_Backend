using Abril_Backend.Features.Ssoma.Penalidad.Dtos;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.Ssoma.Penalidad.Services;

public interface IPenalidadService
{
    Task<PagedResult<PenalidadListItemDto>> GetListAsync(PenalidadListQuery q);
    Task<PenalidadDetalleDto?> GetDetalleAsync(int id);

    Task<PenalidadCreadaDto> RegistrarAsync(PenalidadRegistrarRequest req, int userId);

    Task<PenalidadDetalleDto> AprobarResidenteAsync(int id, int userId);
    Task<PenalidadDetalleDto> RechazarResidenteAsync(int id, PenalidadRechazarRequest req, int userId);

    Task<PenalidadDetalleDto> AprobarGerenciaAsync(int id, int userId);
    Task<PenalidadDetalleDto> RechazarGerenciaAsync(int id, PenalidadRechazarRequest req, int userId);

    Task PresentarDescargoAsync(int id, PenalidadDescargaRequest req, int userId);
    Task<string> SubirDocumentoDescargoAsync(int id, IFormFile file);

    Task<PenalidadDetalleDto> EvaluarDescargoAsync(int id, PenalidadEvaluarDescargoRequest req, int userId);

    Task<PenalidadDetalleDto> DecidirGerenciaAsync(int id, PenalidadDecidirGerenciaRequest req, int userId);

    /// <summary>Apelación post-"Aplicada": una sola vez, exige evidencia nueva, va directo a
    /// Gerencia (sin volver a pasar por Residente ni por la evaluación de SSOMA).</summary>
    Task<PenalidadDetalleDto> ApelarAsync(int id, PenalidadApelarRequest req, int userId);
    Task<PenalidadDetalleDto> DecidirApelacionAsync(int id, PenalidadDecidirApelacionRequest req, int userId);

    Task<string> GetPdfNotificacionAsync(int id);
    Task<string> GetPdfResolucionAsync(int id);

    // ── Catálogos ────────────────────────────────────────────────────────────
    Task<List<InfraccionAdminDto>> GetInfraccionesAsync(bool soloActivas);
    Task<InfraccionAdminDto> CrearInfraccionAsync(InfraccionUpsertRequest req);
    Task<InfraccionAdminDto> ActualizarInfraccionAsync(int id, InfraccionUpsertRequest req);

    Task<List<UitAnioAdminDto>> GetUitAniosAsync();
    Task<UitAnioAdminDto> CrearUitAnioAsync(UitAnioUpsertRequest req);
    Task<UitAnioAdminDto> ActualizarUitAnioAsync(int id, UitAnioUpsertRequest req);

    /// <summary>Recordatorios/vencimientos (cron): notifica antes de vencer el plazo de
    /// descargo y marca por incomparecencia a las penalidades cuyo plazo ya venció.</summary>
    Task ProcesarRecordatoriosYVencimientosAsync();
}
