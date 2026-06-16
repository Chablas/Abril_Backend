using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Dtos;

namespace Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Interfaces
{
    public interface IGestionVecinosService
    {
        Task<VecinosPageDto> GetPageData(VecinoFilterDto filter);
        Task<PagedResult<VecinoListItemDto>> GetList(VecinoFilterDto filter);
        Task<int> Create(VecinoCreateDto dto, int userId);

        Task<VecinoSolicitudesResponseDto> GetSolicitudes(int vecinoId);
        Task<int> CreateSolicitud(int vecinoId, VecinoSolicitudCreateDto dto, int userId);
        Task UpdateSolicitudEstado(int solicitudId, int estadoId, int userId);

        Task<List<VecinoCompromisoItemDto>> GetCompromisos(int solicitudId);
        Task<int> CreateCompromiso(int solicitudId, VecinoCompromisoCreateDto dto, int userId);
        Task UpdateCompromisoEstado(int compromisoId, int estadoId, int userId);
        Task UpdateEntregableEstado(int entregableId, int estadoId, int userId);

        Task<VecinosDashboardDto> GetDashboard();
    }
}
