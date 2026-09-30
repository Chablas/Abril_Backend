using Abril_Backend.Features.ConvivirModule.Shared.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Shared.Interfaces
{
    /// <summary>Lo que ve el propietario de sus propiedades. Lo usan Inicio y Mi Proyecto.</summary>
    public interface IConvivirPropiedadesRepository
    {
        /// <summary>
        /// Nombre, propiedades y los hitos del proyecto de <paramref name="propietarioId"/> (si es
        /// una propiedad suya; si no, o si es null, de la primera), en un solo viaje a la base.
        /// </summary>
        Task<ConvivirContextoDto> GetContexto(int userId, int? propietarioId);
    }
}
