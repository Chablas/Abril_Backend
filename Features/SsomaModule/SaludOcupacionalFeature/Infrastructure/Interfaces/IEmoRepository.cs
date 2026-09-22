using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Emo;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces
{
    public interface IEmoRepository
    {
        Task<PagedResult<EmoListItemDto>> ListPaged(EmoFilterDto filter);
        Task<PagedResult<EmoPorTrabajadorDto>> ListPorTrabajador(EmoPorTrabajadorFilterDto filter);
        Task<EmoDetalleDto> GetById(int id);
        Task<WorkerEmoHistorialDto> GetHistorialByWorker(int workerId);
        Task<EmoCreateResultDto> Create(EmoCreateDto dto, int? userId);
        Task Update(int id, EmoUpdateDto dto, int? userId);
        Task CompletarLecturaAbril(int id, DateOnly fechaLectura, string urlResultado, int? userId);
        Task UpdateEstado(int id, string estado, int? userId);

        /// <summary>
        /// Recorre el EMO activo más reciente de cada trabajador y vuelve a correr la misma lógica
        /// que ya usan Create/Update/CompletarLecturaAbril (SincronizarEntregableEmoAsync) contra su
        /// fila de ss_hab_trabajador (Certificado de Aptitud). Existe porque esa sincronización solo
        /// se dispara de forma reactiva en esos 3 puntos: cualquier EMO que llegue a estar activo por
        /// otro camino (o cuyo disparo reactivo falle a medias) deja la habilitación con datos
        /// viejos para siempre, sin que nada la vuelva a tocar. Pensado para correr on-demand y
        /// programado periódicamente (mismo patrón de cron externo que /alertas/retiro-automatico).
        /// </summary>
        Task<ReconciliacionCertAptitudResultDto> ReconciliarCertAptitudAsync();
    }
}
