using Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Application.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Infrastructure.Interfaces
{
    public interface IConvivirNotificacionesRepository
    {
        /// <summary>
        /// Crea los avisos que falten y devuelve los más recientes, cuántos no leyó y si tiene
        /// varias propiedades, en un solo viaje. Filtra por el usuario del token.
        /// </summary>
        Task<ConvivirNotificacionesFilas> GetNotificaciones(int userId, int limite);

        /// <summary>Guarda la primera lectura. Nada si ya estaba leído o no es un aviso suyo.</summary>
        Task MarcarLeida(int userId, int notificacionId);

        /// <summary>Todos sus avisos sin leer quedan leídos.</summary>
        Task MarcarTodasLeidas(int userId);
    }
}
