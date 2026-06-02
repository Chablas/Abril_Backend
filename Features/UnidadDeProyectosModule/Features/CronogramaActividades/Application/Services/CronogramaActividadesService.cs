using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Services
{
    public class CronogramaActividadesService : ICronogramaActividadesService
    {
        private readonly ICronogramaActividadesRepository _repository;
        private readonly ICronogramaSchedulingService _scheduling;

        public CronogramaActividadesService(
            ICronogramaActividadesRepository repository,
            ICronogramaSchedulingService scheduling)
        {
            _repository = repository;
            _scheduling = scheduling;
        }

        public Task<List<ProyectoSimpleCronogramaDto>> GetProyectosAsync()
            => _repository.GetProyectosAsync();

        public Task<List<ActividadDto>> GetActividadesAsync(int proyectoId)
            => _repository.GetActividadesAsync(proyectoId);

        public Task<ActividadDto> CrearActividadAsync(int proyectoId, CrearActividadRequest request, int userId)
            => _repository.CrearActividadAsync(proyectoId, request, userId);

        public async Task<ActividadDto> EditarActividadAsync(int projectActivityId, EditarActividadRequest request, int userId)
        {
            var actividad = await _repository.EditarActividadAsync(projectActivityId, request, userId);
            // Editar una hoja puede cambiar las fechas de sus padres
            await _scheduling.RecalcularFechasPadresAsync(actividad.ProjectId);
            return actividad;
        }

        public Task<CulminarActividadDto> CulminarActividadAsync(int projectActivityId, int userId)
            => _repository.CulminarActividadAsync(projectActivityId, userId);

        public Task EliminarActividadAsync(int projectActivityId, int userId)
            => _repository.EliminarActividadAsync(projectActivityId, userId);

        public Task<List<DebugProyectoDto>> GetDebugProyectosAsync()
            => _repository.GetDebugProyectosAsync();

        public async Task<ImportarMppResultDto> ImportarMppAsync(int proyectoId, IFormFile archivo, int userId)
        {
            var result = await _repository.ImportarMppAsync(proyectoId, archivo, userId);
            // Tras importar, los nodos padre deben reflejar MIN/MAX de sus hijos (cualquier nivel)
            await _scheduling.RecalcularFechasPadresAsync(proyectoId);
            return result;
        }

        public Task<List<ActividadDto>> ReordenarActividadesAsync(int proyectoId, List<ReordenarItem> items)
            => _repository.ReordenarActividadesAsync(proyectoId, items);

        public Task<List<ActividadDto>> CambiarJerarquiaAsync(int proyectoId, CambiarJerarquiaRequest request)
            => _repository.CambiarJerarquiaAsync(proyectoId, request);

        public Task<List<DebugActividadOrdenDto>> GetDebugOrderAsync(int proyectoId)
            => _repository.GetDebugOrderAsync(proyectoId);

        public Task<List<ActividadDto>> SubirNivelAsync(int proyectoId, int actividadId)
            => _repository.SubirNivelAsync(proyectoId, actividadId);

        public Task<List<ActividadDto>> BajarNivelAsync(int proyectoId, int actividadId)
            => _repository.BajarNivelAsync(proyectoId, actividadId);

        // ─────────────────────────── Feriados ───────────────────────────

        public Task<List<FeriadoDto>> GetFeriadosAsync()
            => _repository.GetFeriadosAsync();

        public Task<FeriadoDto> CrearFeriadoAsync(CrearFeriadoRequest request)
            => _repository.CrearFeriadoAsync(request);

        public Task EliminarFeriadoAsync(int id)
            => _repository.EliminarFeriadoAsync(id);

        // ─────────────────────────── Predecesoras + cascada ───────────────────────────

        public Task<ActividadDto> ActualizarLineaBaseAsync(int projectActivityId, ActualizarLineaBaseRequest request, int userId)
            => _repository.ActualizarLineaBaseAsync(projectActivityId, request, userId);

        public async Task<ActualizarPredecesorasResultDto> ActualizarPredecesorasAsync(int activityId, List<int> predecessorIds)
        {
            var proyectoId = await _repository.GetProyectoIdDeActividadAsync(activityId);

            var limpias = (predecessorIds ?? new List<int>())
                .Where(p => p != activityId).Distinct().ToList();

            // Bloquear dependencias circulares antes de persistir
            if (await _scheduling.DetectCycleAsync(proyectoId, activityId, limpias))
                throw new AbrilException(
                    "La dependencia genera un ciclo entre actividades y no es válida.", 400);

            await _repository.SetPredecesorasAsync(activityId, limpias);

            // Preview de la cascada que se aplicaría (sin persistir todavía)
            var preview = await _scheduling.RecalcularCascadaAsync(proyectoId);

            return new ActualizarPredecesorasResultDto
            {
                ProjectActivityId = activityId,
                Predecesoras = await _repository.GetPredecesorasAsync(activityId),
                PreviewCascada = preview
            };
        }

        public Task<CascadaResultDto> PreviewCascadaAsync(int proyectoId)
            => _scheduling.RecalcularCascadaAsync(proyectoId);

        public Task<CascadaResultDto> AplicarCascadaAsync(int proyectoId)
            => _scheduling.AplicarCascadaAsync(proyectoId);
    }
}
