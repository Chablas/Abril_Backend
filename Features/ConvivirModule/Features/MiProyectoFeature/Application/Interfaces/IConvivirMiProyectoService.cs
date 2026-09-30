using Abril_Backend.Features.ConvivirModule.Features.MiProyectoFeature.Application.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.MiProyectoFeature.Application.Interfaces
{
    public interface IConvivirMiProyectoService
    {
        /// <summary><paramref name="propietarioId"/> null = la primera propiedad.</summary>
        Task<ConvivirMiProyectoDto> GetMiProyecto(int userId, int? propietarioId);
    }
}
