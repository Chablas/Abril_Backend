using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces
{
    public interface ISolicitudSalidaService
    {
        Task<SolicitudSalidaFormDataDto> GetFormData();
        Task<List<SolicitudSalidaListItemDto>> GetByUserId(int userId);
        Task<int> Create(SolicitudSalidaCreateDto dto, int? userId);
    }
}
