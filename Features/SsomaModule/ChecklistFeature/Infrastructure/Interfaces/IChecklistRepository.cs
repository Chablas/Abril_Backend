using Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Interfaces
{
    public interface IChecklistRepository
    {
        // Proyecto actual del usuario logueado (resuelto vía su Worker), para
        // seleccionar por defecto en "Por Proyecto" sin depender del orden alfabético.
        Task<int?> GetProyectoActualDeUsuarioAsync(int userId);

        // --- Plantillas (catálogo maestro) ---
        Task<List<ChecklistPlantillaListDto>> GetPlantillasAsync();
        Task<ChecklistPlantillaDetalleDto?> GetPlantillaDetalleAsync(int plantillaId);
        Task<SsChecklistPlantilla> CreatePlantillaAsync(ChecklistPlantillaUpsertDto dto, int userId);
        Task UpdatePlantillaAsync(int plantillaId, ChecklistPlantillaUpsertDto dto);
        Task<SsChecklistPlantillaItem> AddItemToPlantillaAsync(int plantillaId, ChecklistPlantillaItemCreateDto dto);
        Task UpdatePlantillaItemAsync(int itemId, ChecklistPlantillaItemEditDto dto);
        Task SetOrdenItemAsync(int itemId, int nuevoOrden);

        // --- Partidas (etapas constructivas) ---
        Task<List<ChecklistPartidaDto>> GetPartidasAsync();
        Task<SsChecklistPartida> CreatePartidaAsync(ChecklistPartidaUpsertDto dto);
        Task UpdatePartidaAsync(int partidaId, ChecklistPartidaUpsertDto dto);
        Task DeletePartidaAsync(int partidaId);

        // --- Imágenes de referencia de un item de plantilla ---
        Task<ChecklistItemImagenDto> AddImagenReferenciaAsync(int plantillaItemId, string url);
        Task DeleteImagenReferenciaAsync(int imagenId);

        // --- Checklists de proyecto ---
        Task<ChecklistProyectoResumenDto> GetResumenProyectoAsync(int proyectoId);
        Task<ChecklistProyectoDetalleDto?> GetChecklistDetalleAsync(int checklistProyectoId);
        Task<SsChecklistProyecto> ActivarChecklistAsync(int proyectoId, int plantillaId, int userId);
        Task DesactivarChecklistAsync(int checklistProyectoId);
        Task MarcarNoAplicaAsync(int checklistProyectoId, string motivo, int? userId);
        Task ReactivarChecklistAsync(int checklistProyectoId);
        Task SeedChecklistsObligatoriosAsync(int proyectoId, int userId);

        // Propaga una plantilla obligatoria/automática a todos los proyectos activos
        // que aún no la tienen (alta o edición de un checklist por partida).
        Task PropagarATodosLosProyectosAsync(int plantillaId, int? userId);

        // --- Items de proyecto ---
        Task<(decimal porcentaje, bool recienCompletado)> ToggleItemAsync(int checklistProyectoItemId, ChecklistItemToggleDto dto, int userId);

        // Para la notificación: obtener email del gerente y datos del proyecto
        Task<(string? emailGerente, string nombreProyecto, string nombreChecklist)> GetDatosNotificacionAsync(int checklistProyectoId);
        Task MarcarNotificacionEnviadaAsync(int checklistProyectoId);

        // Retorna el checklistProyectoId dueño del item
        Task<int> GetChecklistProyectoIdByItemAsync(int checklistProyectoItemId);
    }
}
