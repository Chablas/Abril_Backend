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

        // ── CATEGORÍAS ──────────────────────────────────────────────────────

        public Task<List<ActivoRotativoCategoriaDto>> GetCategoriasAsync()
            => _repo.GetCategoriasAsync();

        public async Task<ActivoRotativoCategoriaDto> CreateCategoriaAsync(ActivoRotativoCategoriaUpsertDto dto)
        {
            var entity = await _repo.CreateCategoriaAsync(dto);
            return new ActivoRotativoCategoriaDto
            {
                Id = entity.Id,
                Nombre = entity.Nombre,
                Orden = entity.Orden,
                Activo = entity.Activo,
                TotalActivos = 0
            };
        }

        public Task UpdateCategoriaAsync(int categoriaId, ActivoRotativoCategoriaUpsertDto dto)
            => _repo.UpdateCategoriaAsync(categoriaId, dto);

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
    }
}
