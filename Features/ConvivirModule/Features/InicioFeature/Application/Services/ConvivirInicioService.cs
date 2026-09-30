using Abril_Backend.Features.ConvivirModule.Features.InicioFeature.Application.Dtos;
using Abril_Backend.Features.ConvivirModule.Features.InicioFeature.Application.Interfaces;
using Abril_Backend.Features.ConvivirModule.Shared.Interfaces;
using Abril_Backend.Features.ConvivirModule.Shared.Services;

namespace Abril_Backend.Features.ConvivirModule.Features.InicioFeature.Application.Services
{
    public class ConvivirInicioService : IConvivirInicioService
    {
        private readonly IConvivirPropiedadesRepository _repo;

        public ConvivirInicioService(IConvivirPropiedadesRepository repo)
        {
            _repo = repo;
        }

        public async Task<ConvivirInicioDto> GetInicio(int userId, int? propietarioId)
        {
            // Con notificaciones: el número de la campana (y la detección de hitos culminados).
            var contexto = await _repo.GetContexto(userId, propietarioId, conNotificaciones: true);

            return new ConvivirInicioDto
            {
                Nombres = contexto.Nombres,
                Propiedades = contexto.Propiedades,
                PropietarioId = contexto.Seleccionada?.PropietarioId,
                // Inicio muestra solo el resumen; la lista de hitos es de Mi Proyecto.
                Avance = contexto.Seleccionada == null ? null : AvanceObra.Calcular(contexto).Avance,
                DocumentosNuevos = contexto.DocumentosNuevos,
                NotificacionesNuevas = contexto.NotificacionesNuevas,
            };
        }
    }
}
