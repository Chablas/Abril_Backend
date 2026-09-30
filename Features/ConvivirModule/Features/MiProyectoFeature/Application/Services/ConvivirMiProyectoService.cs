using Abril_Backend.Features.ConvivirModule.Features.MiProyectoFeature.Application.Dtos;
using Abril_Backend.Features.ConvivirModule.Features.MiProyectoFeature.Application.Interfaces;
using Abril_Backend.Features.ConvivirModule.Shared.Interfaces;
using Abril_Backend.Features.ConvivirModule.Shared.Services;

namespace Abril_Backend.Features.ConvivirModule.Features.MiProyectoFeature.Application.Services
{
    public class ConvivirMiProyectoService : IConvivirMiProyectoService
    {
        private readonly IConvivirPropiedadesRepository _repo;

        public ConvivirMiProyectoService(IConvivirPropiedadesRepository repo)
        {
            _repo = repo;
        }

        public async Task<ConvivirMiProyectoDto> GetMiProyecto(int userId, int? propietarioId)
        {
            var contexto = await _repo.GetContexto(userId, propietarioId, conNotificaciones: false);
            if (contexto.Seleccionada == null)
                return new ConvivirMiProyectoDto();

            var (avance, hitos) = AvanceObra.Calcular(contexto);

            return new ConvivirMiProyectoDto
            {
                Propiedad = contexto.Seleccionada,
                Avance = avance,
                Hitos = hitos,
            };
        }
    }
}
