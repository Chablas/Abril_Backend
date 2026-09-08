using Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Interfaces
{
    public interface IChecklistService
    {
        Task<int?> GetProyectoActualDeUsuarioAsync(int userId);

        // Plantillas
        Task<List<ChecklistPlantillaListDto>> GetPlantillasAsync();
        Task<ChecklistPlantillaDetalleDto?> GetPlantillaDetalleAsync(int plantillaId);
        Task<ChecklistPlantillaDetalleDto> CreatePlantillaAsync(ChecklistPlantillaUpsertDto dto, int userId);
        Task UpdatePlantillaAsync(int plantillaId, ChecklistPlantillaUpsertDto dto);
        Task<ChecklistPlantillaItemDto> AddItemToPlantillaAsync(int plantillaId, ChecklistPlantillaItemCreateDto dto);
        Task UpdatePlantillaItemAsync(int itemId, ChecklistPlantillaItemEditDto dto);
        Task SetOrdenItemAsync(int itemId, int nuevoOrden);

        // Partidas
        Task<List<ChecklistPartidaDto>> GetPartidasAsync();
        Task<ChecklistPartidaDto> CreatePartidaAsync(ChecklistPartidaUpsertDto dto, int userId);
        Task UpdatePartidaAsync(int partidaId, ChecklistPartidaUpsertDto dto);
        Task DeletePartidaAsync(int partidaId);

        // Imágenes de referencia de un item de plantilla
        Task<ChecklistItemImagenDto> SubirImagenReferenciaAsync(int plantillaItemId, Stream fileStream, string fileName);
        Task EliminarImagenReferenciaAsync(int imagenId);
        Task<string> SubirAdjuntoItemAsync(Stream fileStream, string fileName);

        // Proyecto
        Task<ChecklistProyectoResumenDto> GetResumenProyectoAsync(int proyectoId);
        Task<ChecklistProyectoDetalleDto?> GetChecklistDetalleAsync(int checklistProyectoId);
        Task<ChecklistProyectoDetalleDto> ActivarChecklistAsync(int proyectoId, int plantillaId, int userId);
        Task DesactivarChecklistAsync(int checklistProyectoId);
        Task MarcarNoAplicaAsync(int checklistProyectoId, string motivo, int? userId);
        Task ReactivarChecklistAsync(int checklistProyectoId);
        Task SeedChecklistsObligatoriosAsync(int proyectoId, int userId);
        Task PropagarATodosLosProyectosAsync(int plantillaId, int? userId);

        // Items
        Task<(decimal porcentaje, string estado)> ToggleItemAsync(int checklistProyectoItemId, ChecklistItemToggleDto dto, int userId);
    }
}
