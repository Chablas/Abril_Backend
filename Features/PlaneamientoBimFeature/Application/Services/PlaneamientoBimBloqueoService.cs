using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Interfaces;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Application.Services
{
    public class PlaneamientoBimBloqueoService : IPlaneamientoBimBloqueoService
    {
        /// <summary>Estados asignables por el usuario. "CERRADO" solo lo asigna el endpoint Cerrar, nunca Create/Update.</summary>
        private static readonly string[] EstadosValidos = { "ABIERTO", "EN_GESTION" };

        private readonly IPlaneamientoBimBloqueoRepository _repository;

        public PlaneamientoBimBloqueoService(IPlaneamientoBimBloqueoRepository repository)
        {
            _repository = repository;
        }

        public Task<List<BloqueoDto>> GetPaged(int projectId, bool? soloActivos)
            => _repository.GetPaged(projectId, soloActivos);

        public Task<BloqueoDto> Create(int projectId, BloqueoCreateDto dto, int userId)
        {
            ValidarDescripcionYEstado(dto.Descripcion, dto.Estado);
            return _repository.Create(projectId, dto, userId);
        }

        public Task<BloqueoDto> Update(int bloqueoId, BloqueoUpdateDto dto)
        {
            ValidarDescripcionYEstado(dto.Descripcion, dto.Estado);
            return _repository.Update(bloqueoId, dto);
        }

        public Task<BloqueoDto> Cerrar(int bloqueoId)
            => _repository.Cerrar(bloqueoId);

        private static void ValidarDescripcionYEstado(string descripcion, string estado)
        {
            if (string.IsNullOrWhiteSpace(descripcion))
                throw new AbrilException("La descripción es obligatoria.", 400);

            if (!EstadosValidos.Contains(estado))
                throw new AbrilException($"Estado inválido. Use {string.Join(" o ", EstadosValidos)}.", 400);
        }
    }
}
