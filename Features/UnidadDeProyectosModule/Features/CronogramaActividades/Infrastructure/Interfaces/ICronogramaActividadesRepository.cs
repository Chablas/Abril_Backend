using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Interfaces
{
    public interface ICronogramaActividadesRepository
    {
        Task<List<ProyectoSimpleCronogramaDto>> GetProyectosAsync();
        Task<List<ActividadDto>> GetActividadesAsync(int proyectoId);
        Task<ActividadDto> CrearActividadAsync(int proyectoId, CrearActividadRequest request, int userId);
        Task<ActividadDto> EditarActividadAsync(int projectActivityId, EditarActividadRequest request, int userId);
        Task<CulminarActividadDto> CulminarActividadAsync(int projectActivityId, int userId);
        Task EliminarActividadAsync(int projectActivityId, int userId);
        Task<List<DebugProyectoDto>> GetDebugProyectosAsync();
        Task<ImportarMppResultDto> ImportarMppAsync(int proyectoId, IFormFile archivo, int userId);
        Task<List<ActividadDto>> ReordenarActividadesAsync(int proyectoId, List<ReordenarItem> items);
        Task<List<ActividadDto>> CambiarJerarquiaAsync(int proyectoId, CambiarJerarquiaRequest request);
        Task<List<DebugActividadOrdenDto>> GetDebugOrderAsync(int proyectoId);
        Task<List<ActividadDto>> SubirNivelAsync(int proyectoId, int actividadId);
        Task<List<ActividadDto>> BajarNivelAsync(int proyectoId, int actividadId);

        // Feriados
        Task<List<FeriadoDto>> GetFeriadosAsync();
        Task<FeriadoDto> CrearFeriadoAsync(CrearFeriadoRequest request);
        Task EliminarFeriadoAsync(int id);

        // Línea base
        Task<ActividadDto> ActualizarLineaBaseAsync(int projectActivityId, ActualizarLineaBaseRequest request, int userId);

        // Predecesoras
        Task<List<int>> GetPredecesorasAsync(int activityId);
        Task SetPredecesorasAsync(int activityId, List<int> predecessorIds);
        /// <summary>Devuelve el ProjectId de una actividad (valida que exista y esté activa).</summary>
        Task<int> GetProyectoIdDeActividadAsync(int activityId);
    }
}
