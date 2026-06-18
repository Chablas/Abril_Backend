using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Dtos;

namespace Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Interfaces
{
    public interface IGestionVecinosRepository
    {
        Task<VecinoFormOptionsDto> GetOptions();
        Task<PagedResult<VecinoListItemDto>> GetPaged(VecinoFilterDto filter);
        Task<int> Create(VecinoCreateDto dto, int userId);
        Task AddImagenes(int vecinoId, List<(string ArchivoUrl, string? OriginalFileName)> imagenes, int userId);

        Task<bool> VecinoExists(int vecinoId);
        Task<VecinoSolicitudesResponseDto> GetSolicitudes(int vecinoId);
        Task<int> CreateSolicitud(int vecinoId, VecinoSolicitudCreateDto dto, int userId);
        Task<bool> UpdateSolicitudEstado(int solicitudId, int estadoId, int userId);

        Task<bool> SolicitudExists(int solicitudId);
        Task<List<VecinoCompromisoItemDto>> GetCompromisos(int solicitudId);
        Task<int> CreateCompromiso(int solicitudId, VecinoCompromisoCreateDto dto, int userId);
        Task<bool> UpdateCompromisoEstado(int compromisoId, int estadoId, int userId);
        Task<bool> UpdateEntregableEstado(int entregableId, int estadoId, int userId);

        Task<VecinosDashboardDto> GetDashboard();

        Task<VecinoRequisitosResponseDto> GetRequisitos(int vecinoId);
        Task<bool> TipoRequisitoExists(int tipoId);
        Task UploadRequisito(int vecinoId, int tipoId, string archivoUrl, string? originalFileName, int userId);
        Task SetRequisitoNoAplica(int vecinoId, int tipoId, bool noAplica, int userId);
    }
}
