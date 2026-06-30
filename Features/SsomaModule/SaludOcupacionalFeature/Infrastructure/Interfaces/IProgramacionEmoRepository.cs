using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces
{
    public interface IProgramacionEmoRepository
    {
        Task<PagedResponseDto<ProgramacionListDto>> List(ProgramacionFilterDto filter);
        Task<int> Create(ProgramacionCreateDto dto, int? userId);
        Task Update(int id, ProgramacionUpdateDto dto, int? userId);
        Task UpdateEstado(int id, string estado, int? emoResultadoId, int? userId);
        Task ClinicaAccion(int id, ProgramacionClinicaAccionDto dto, int? userId);
        Task<List<ProgramacionHabilitacionDto>> GetHabilitacionAsync(ProgramacionHabilitacionFiltrosDto filtros);
        Task PatchNotificadoAsync(int id, bool notificado);
        Task UndoCheckInAsync(int id);
    }
}
