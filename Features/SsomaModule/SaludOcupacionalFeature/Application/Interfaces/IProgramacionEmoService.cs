using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces
{
    public interface IProgramacionEmoService
    {
        Task<List<ProgramacionListDto>> List(ProgramacionFilterDto filter);
        Task<int> Create(ProgramacionCreateDto dto, int? userId);
        Task Update(int id, ProgramacionUpdateDto dto, int? userId);
        Task UpdateEstado(int id, string estado, int? emoResultadoId, int? userId);
        Task ClinicaAccion(int id, ProgramacionClinicaAccionDto dto, int? userId);
    }
}
