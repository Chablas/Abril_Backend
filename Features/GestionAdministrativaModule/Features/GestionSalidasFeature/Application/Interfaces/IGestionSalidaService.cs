using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces
{
    public interface IGestionSalidaService
    {
        Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters);
        Task<GestionSalidaFilterDataDto> GetFilterData();
        Task<byte[]> GetExcel(GestionSalidaFiltersDto filters);
        Task Aprobar(int id, int reviewerUserId);
        Task Rechazar(int id, int reviewerUserId);
    }
}
