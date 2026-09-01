using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Application.Interfaces;
using Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Application.Services
{
    public class CostsPasoService : ICostsPasoService
    {
        private readonly ICostsPasoRepository _repository;

        public CostsPasoService(ICostsPasoRepository repository)
        {
            _repository = repository;
        }

        public Task<List<CostsPasoDto>> GetPasosAsync() => _repository.GetPasosAsync();

        public async Task UpdateOptionAsync(CostsPasoOptionUpdateDto dto, int userId)
        {
            var updated = await _repository.UpdateOptionAsync(dto, userId);
            if (!updated)
                throw new AbrilException("La opción ya no existe. Recargue la pantalla.", 404);
        }
    }
}
