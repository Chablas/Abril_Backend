using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Application.Interfaces;
using Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Application.Services
{
    public class CostosCronogramaService : ICostosCronogramaService
    {
        private readonly ICostosCronogramaRepository _repository;

        public CostosCronogramaService(ICostosCronogramaRepository repository)
        {
            _repository = repository;
        }

        public async Task<CronogramaFormDataDto> GetFormData(int projectSubContractorId)
        {
            return new CronogramaFormDataDto
            {
                Actividades = await _repository.GetActividadesAsync(),
                Nodos = await _repository.GetNodosAsync(projectSubContractorId),
            };
        }

        public async Task<CronogramaActividadDto> CreateActividad(CronogramaActividadCreateDto dto, int userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new AbrilException("El nombre de la actividad es obligatorio.");
            return await _repository.CreateActividadAsync(dto.Nombre, userId);
        }

        public async Task Save(int projectSubContractorId, CronogramaSaveDto dto, int userId)
        {
            if (dto.Nodos == null || dto.Nodos.Count == 0)
                throw new AbrilException("El cronograma debe tener al menos una actividad.");
            await _repository.SaveAsync(projectSubContractorId, dto.Nodos, userId);
        }
    }
}
