using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces
{
    public interface IGestionSalidaRepository
    {
        Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters);
        Task<GestionSalidaFilterDataDto> GetFilterData();
        Task Aprobar(int id, int reviewerUserId);
        Task Rechazar(int id, int reviewerUserId);
    }
}
