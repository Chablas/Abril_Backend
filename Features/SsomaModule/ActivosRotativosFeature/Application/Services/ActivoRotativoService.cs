using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Services
{
    public class ActivoRotativoService : IActivoRotativoService
    {
        private readonly IActivoRotativoRepository _repo;

        public ActivoRotativoService(IActivoRotativoRepository repo)
        {
            _repo = repo;
        }

        // ── MATERIALES ───────────────────────────────────────────────────────

        public Task<List<ActivoRotativoMaterialDto>> GetMaterialesAsync()
            => _repo.GetMaterialesAsync();

        public async Task<ActivoRotativoMaterialDto> CreateMaterialAsync(ActivoRotativoMaterialUpsertDto dto)
        {
            var entity = await _repo.CreateMaterialAsync(dto);
            return new ActivoRotativoMaterialDto
            {
                Id = entity.Id,
                Nombre = entity.Nombre,
                Orden = entity.Orden,
                Activo = entity.Activo,
                TotalActivos = 0,
                PresupuestoItemId = entity.PresupuestoItemId
            };
        }

        public Task UpdateMaterialAsync(int materialId, ActivoRotativoMaterialUpsertDto dto)
            => _repo.UpdateMaterialAsync(materialId, dto);

        public Task DeleteMaterialAsync(int materialId)
            => _repo.DeleteMaterialAsync(materialId);

        public Task<List<PresupuestoItemBuscarDto>> BuscarItemsPresupuestoAsync(string q)
            => _repo.BuscarItemsPresupuestoAsync(q);

        public Task<List<ResponsableSsomaDto>> GetResponsablesSsomaAsync()
            => _repo.GetResponsablesSsomaAsync();

        // ── ACTIVOS ──────────────────────────────────────────────────────────

        public Task<List<ActivoRotativoListDto>> GetActivosAsync()
            => _repo.GetActivosAsync();

        public Task<ActivoRotativoDetalleDto?> GetActivoDetalleAsync(int activoId)
            => _repo.GetActivoDetalleAsync(activoId);

        public async Task<ActivoRotativoDetalleDto> CreateActivoAsync(ActivoRotativoUpsertDto dto)
        {
            var entity = await _repo.CreateActivoAsync(dto);
            return (await _repo.GetActivoDetalleAsync(entity.Id))!;
        }

        public Task UpdateActivoAsync(int activoId, ActivoRotativoUpsertDto dto)
            => _repo.UpdateActivoAsync(activoId, dto);

        public async Task<ActivoRotativoDetalleDto> MoverActivoAsync(int activoId, ActivoRotativoMoverDto dto, int? userId)
        {
            var entity = await _repo.MoverActivoAsync(activoId, dto, userId);
            return (await _repo.GetActivoDetalleAsync(entity.Id))!;
        }

        public Task DeleteActivoAsync(int activoId)
            => _repo.DeleteActivoAsync(activoId);
    }
}
