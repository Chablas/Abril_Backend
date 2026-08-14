using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Interfaces;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Application.Services
{
    public class PlaneamientoBimConfiguracionService : IPlaneamientoBimConfiguracionService
    {
        private readonly IPlaneamientoBimConfiguracionRepository _repository;

        public PlaneamientoBimConfiguracionService(IPlaneamientoBimConfiguracionRepository repository)
        {
            _repository = repository;
        }

        public async Task<ConfiguracionInicialDto> GetConfiguracion(int projectId)
        {
            var config = await _repository.GetConfiguracion(projectId);
            if (config == null)
                throw new AbrilException("El proyecto no existe.", 404);

            return config;
        }

        public async Task<List<ResponsableBimLookupDto>> GetResponsables()
        {
            return await _repository.GetResponsables();
        }

        public async Task GuardarConfiguracion(int projectId, ConfiguracionInicialUpdateDto dto)
        {
            if (dto.MetaPpc.HasValue && (dto.MetaPpc < 0 || dto.MetaPpc > 100))
                throw new AbrilException("La meta de PPC debe estar entre 0 y 100.", 400);

            if (dto.Fases.Any(f => f.FechaInicio.HasValue && f.FechaFinMeta.HasValue && f.FechaFinMeta <= f.FechaInicio))
                throw new AbrilException("La fecha fin de cada fase debe ser posterior a su fecha de inicio.", 400);

            await _repository.GuardarConfiguracion(projectId, dto);
        }
    }
}
