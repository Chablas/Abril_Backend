using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Interfaces
{
    public interface ICumplimientoRepository
    {
        // --- Catálogo ---
        Task<List<CumplimientoActividadDto>> GetActividadesAsync();
        Task<SsCumplimientoActividad> CreateActividadAsync(CumplimientoActividadUpsertDto dto);
        Task UpdateActividadAsync(int actividadId, CumplimientoActividadUpsertDto dto);

        // --- Cumplimiento por proyecto ---
        Task<CumplimientoResumenDto> GetResumenProyectoAsync(int proyectoId, string? rol);
        Task<CumplimientoItemDto> MarcarAsync(int proyectoId, int actividadId, CumplimientoMarcarDto dto, int? userId);
    }
}
