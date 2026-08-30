using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Interfaces;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Application.Services
{
    public class PlaneamientoBimConfiguracionService : IPlaneamientoBimConfiguracionService
    {
        // PUNTO 5 (obs. Planeamiento BIM): meta de PPC estandar fija, ya no
        // editable por proyecto. Publica porque tambien la usa
        // PlaneamientoBimDashboardRepository.GetPpcHistorico (y por ende el
        // PDF, que consume ese mismo DTO) para no depender de Project.MetaPpc.
        public const decimal MetaPpcEstandar = 85m;

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

            config.MetaPpc = MetaPpcEstandar;
            return config;
        }

        public async Task<List<ResponsableBimLookupDto>> GetResponsables()
        {
            return await _repository.GetResponsables();
        }

        public async Task<List<ProyectoBimSimpleDto>> GetProyectosDisponibles(int userId, bool esAdmin, bool esPlaneamientoUdp)
        {
            return await _repository.GetProyectosDisponibles(userId, esAdmin, esPlaneamientoUdp);
        }

        private static readonly HashSet<string> TiposEstructuraValidos = new(StringComparer.OrdinalIgnoreCase)
        {
            "SUBESTRUCTURA", "SUPERESTRUCTURA",
        };

        public async Task GuardarConfiguracion(int projectId, ConfiguracionInicialUpdateDto dto)
        {
            dto.MetaPpc = MetaPpcEstandar; // se ignora cualquier valor recibido del frontend

            if (dto.Fases.Any(f => f.FechaInicio.HasValue && f.FechaFinMeta.HasValue && f.FechaFinMeta <= f.FechaInicio))
                throw new AbrilException("La fecha fin de cada fase debe ser posterior a su fecha de inicio.", 400);

            foreach (var torre in dto.Torres)
            {
                if (torre.CantidadSectoresSubestructura < 0 || torre.CantidadSectoresSuperestructura < 0)
                    throw new AbrilException($"La torre '{torre.Nombre}' no puede tener una cantidad de sectores negativa.", 400);

                foreach (var nivel in torre.Niveles)
                {
                    if (nivel.TipoEstructura == null || !TiposEstructuraValidos.Contains(nivel.TipoEstructura))
                        throw new AbrilException(
                            $"El nivel '{nivel.Nombre}' de la torre '{torre.Nombre}' debe indicar tipo de estructura (Subestructura o Superestructura).", 400);
                }
            }

            await _repository.GuardarConfiguracion(projectId, dto);
        }
    }
}
