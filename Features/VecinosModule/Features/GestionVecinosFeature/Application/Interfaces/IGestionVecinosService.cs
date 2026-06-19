using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Dtos;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Interfaces
{
    public interface IGestionVecinosService
    {
        Task<VecinosPageDto> GetPageData(VecinoFilterDto filter);
        Task<PagedResult<VecinoListItemDto>> GetList(VecinoFilterDto filter);
        Task<VecinoListItemDto> GetById(int vecinoId);
        Task<int> Create(VecinoCreateDto dto, int userId);
        Task Update(int vecinoId, VecinoUpdateDto dto, int userId);
        Task<List<VecinoImagenDto>> UploadImagenes(int vecinoId, IFormFileCollection files, int userId);
        Task DeleteImagen(int imagenId, int userId);

        Task<VecinoSolicitudesResponseDto> GetSolicitudes(int vecinoId);
        Task<int> CreateSolicitud(int vecinoId, VecinoSolicitudCreateDto dto, int userId);
        Task UpdateSolicitudEstado(int solicitudId, int estadoId, int userId);

        Task<List<VecinoCompromisoItemDto>> GetCompromisos(int solicitudId);
        Task<int> CreateCompromiso(int solicitudId, VecinoCompromisoCreateDto dto, int userId);
        Task UpdateCompromisoEstado(int compromisoId, int estadoId, int userId);
        Task UpdateEntregableEstado(int entregableId, int estadoId, int userId);
        Task<string> UploadEntregable(int entregableId, IFormFile file, int userId);

        Task<VecinosDashboardDto> GetDashboard();

        Task<VecinoRequisitosResponseDto> GetRequisitos(int vecinoId);
        Task<string> UploadRequisito(int vecinoId, int tipoId, IFormFile file, int userId);
        Task SetRequisitoNoAplica(int vecinoId, int tipoId, bool noAplica, int userId);
    }
}
