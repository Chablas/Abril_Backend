using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Dtos;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Interfaces
{
    public interface IControlVencimientosService
    {
        Task<List<VecinoLicenciaDto>> GetLicencias();
        Task<VecinoLicenciaDto> CreateLicencia(VecinoLicenciaCreateDto dto, IFormFile file, int userId);
    }
}
