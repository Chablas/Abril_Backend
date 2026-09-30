using Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Application.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Application.Interfaces
{
    public interface IConvivirNotificacionesService
    {
        /// <summary>La campana: los avisos más recientes de todas sus propiedades y cuántos no leyó.</summary>
        Task<ConvivirNotificacionesDto> GetNotificaciones(int userId);

        /// <summary>Al abrir un aviso desde la campana.</summary>
        Task MarcarLeida(int userId, int notificacionId);

        Task MarcarTodasLeidas(int userId);
    }
}
