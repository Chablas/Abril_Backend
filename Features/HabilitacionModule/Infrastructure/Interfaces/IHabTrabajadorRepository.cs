using Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces
{
    public interface IHabTrabajadorRepository
    {
        Task<(List<WorkerHabilitacionListDto> Items, int Total)> GetWorkersHabilitacionAsync(
            string? search, int? empresaId, int? proyectoId,
            string? estadoHabilitacion, string? contratistaCasa,
            int page, int pageSize);

        Task<List<WorkerEntregableDto>> GetEntregablesWorkerAsync(int workerId);

        Task<SsHabTrabajador> UpdateEntregableAsync(int id, WorkerEntregableUpdateDto dto, int? userId, int? empresaId = null);

        Task<List<SsHabDocumentoVersion>> GetVersionesDocumentoAsync(int habTrabajadorId);

        Task CambiarObraAsync(int workerId, WorkerCambiarObraDto dto);

        Task ReingresoAsync(int workerId, int proyectoId, int empresaId);

        Task<int?> GetEmpresaActivaWorkerAsync(int workerId);

        Task InicializarEntregablesAsync(int workerId);
    }
}
