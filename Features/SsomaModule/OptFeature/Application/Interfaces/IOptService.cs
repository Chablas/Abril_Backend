using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.SsomaModule.OptFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.OptFeature.Application.Interfaces;

public interface IOptService
{
    Task<PagedResult<OptListItemDto>> GetListAsync(int? proyectoId, int? petId, string? tipoObservacion,
        DateTime? fechaDesde, DateTime? fechaHasta, int? trabajadorId, int page, int pageSize);
    Task<OptDetalleDto> GetDetalleAsync(int id);
    Task<int> CrearOptAsync(CrearOptRequest request);
    Task<OptDashboardDto> GetDashboardAsync(int? proyectoId, int? anio);
    Task<List<OptPetDto>> GetPetsAsync();
    Task<List<OptCriterioVerificacionDto>> GetCriteriosVerificacionAsync();
}
