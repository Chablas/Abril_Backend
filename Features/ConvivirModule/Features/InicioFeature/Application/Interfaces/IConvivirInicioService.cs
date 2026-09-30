using Abril_Backend.Features.ConvivirModule.Features.InicioFeature.Application.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.InicioFeature.Application.Interfaces
{
    public interface IConvivirInicioService
    {
        /// <summary><paramref name="propietarioId"/> null = la primera propiedad.</summary>
        Task<ConvivirInicioDto> GetInicio(int userId, int? propietarioId);
    }
}
