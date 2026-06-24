using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Dtos;

namespace Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Infrastructure.Interfaces
{
    public interface IControlVencimientosRepository
    {
        Task<List<VecinoLicenciaDto>> GetLicencias();
        Task<VecinoLicenciaDto> CreateLicencia(VecinoLicenciaCreateDto dto, string archivoUrl, string? originalFileName, int userId);
    }
}
