using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Services
{
    public class CumplimientoService : ICumplimientoService
    {
        private readonly ICumplimientoRepository _repo;

        public CumplimientoService(ICumplimientoRepository repo)
        {
            _repo = repo;
        }

        public Task<List<CumplimientoActividadDto>> GetActividadesAsync()
            => _repo.GetActividadesAsync();

        public async Task<CumplimientoActividadDto> CreateActividadAsync(CumplimientoActividadUpsertDto dto)
        {
            var entity = await _repo.CreateActividadAsync(dto);
            return new CumplimientoActividadDto
            {
                Id = entity.Id,
                Nombre = entity.Nombre,
                Descripcion = entity.Descripcion,
                RolResponsable = entity.RolResponsable,
                Frecuencia = entity.Frecuencia,
                Orden = entity.Orden,
                Activo = entity.Activo
            };
        }

        public Task UpdateActividadAsync(int actividadId, CumplimientoActividadUpsertDto dto)
            => _repo.UpdateActividadAsync(actividadId, dto);

        public Task<CumplimientoResumenDto> GetResumenProyectoAsync(int proyectoId, string? rol)
            => _repo.GetResumenProyectoAsync(proyectoId, rol);

        public Task<CumplimientoItemDto> MarcarAsync(int proyectoId, int actividadId, CumplimientoMarcarDto dto, int? userId)
            => _repo.MarcarAsync(proyectoId, actividadId, dto, userId);

        public Task<CumplimientoMiResumenDto> GetMiResumenAsync(int userId)
            => _repo.GetMiResumenAsync(userId);

        public Task<CumplimientoHistoricoDto> GetHistoricoAsync(int proyectoId, string frecuencia, DateOnly desde, DateOnly hasta)
            => _repo.GetHistoricoAsync(proyectoId, frecuencia, desde, hasta);
    }
}
