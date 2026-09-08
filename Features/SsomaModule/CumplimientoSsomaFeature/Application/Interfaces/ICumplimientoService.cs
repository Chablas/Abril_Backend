using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Interfaces
{
    public interface ICumplimientoService
    {
        Task<List<CumplimientoActividadDto>> GetActividadesAsync();
        Task<CumplimientoActividadDto> CreateActividadAsync(CumplimientoActividadUpsertDto dto);
        Task UpdateActividadAsync(int actividadId, CumplimientoActividadUpsertDto dto);

        Task<CumplimientoResumenDto> GetResumenProyectoAsync(int proyectoId, string? rol);
        Task<CumplimientoItemDto> MarcarAsync(int proyectoId, int actividadId, CumplimientoMarcarDto dto, int? userId);
    }
}
