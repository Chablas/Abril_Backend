using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Services
{
    public class CronogramaCorreoDestinatariosResolver : ICronogramaCorreoDestinatariosResolver
    {
        private readonly ICronogramaCorreosRepository _repo;
        private readonly ILogger<CronogramaCorreoDestinatariosResolver> _logger;

        public CronogramaCorreoDestinatariosResolver(
            ICronogramaCorreosRepository repo,
            ILogger<CronogramaCorreoDestinatariosResolver> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public async Task<CronogramaCorreoListaEnvio> ObtenerAsync(string codigo)
        {
            try
            {
                var lista = await _repo.GetListaEnvioAsync(codigo);
                if (lista != null)
                    return lista;

                _logger.LogWarning(
                    "El correo {Codigo} del cronograma no está en milestone_schedule_correo: sale solo al destinatario del sistema.",
                    codigo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "No se pudo leer la configuración del correo {Codigo} del cronograma: sale solo al destinatario del sistema.",
                    codigo);
            }

            return CronogramaCorreoListaEnvio.SoloPrincipal;
        }
    }
}
