using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;

public interface IRevisionMaterialesService
{
    Task<List<MaterialPendienteDto>> ObtenerPendientesAsync(int projectId);
    Task<RevisionResultDto> ProcesarRevisionAsync(RevisionLoteDto dto, int usuarioId);
    Task<List<BuscarItemDto>> BuscarItemsAsync(string texto);
    Task<List<MaterialPendienteGlobalDto>> ObtenerPendientesGlobalAsync();
    Task<RevisionResultDto> ProcesarRevisionGlobalAsync(List<RevisionDecisionDto> decisiones, int usuarioId);
    Task<List<MaterialNoSsomaDto>> ObtenerNoSsomaAsync();
    Task<List<MaterialGlobalDto>> ObtenerTodoGlobalAsync();

    /// <summary>Proyecto actual del usuario logueado (por su email corporativo, workers.project_id)
    /// — para preseleccionar el filtro de Proyecto en la vista "General" del Catálogo en vez de
    /// arrancar mostrando los ~88 mil registros de todos los proyectos de una vez.</summary>
    Task<ProyectoActualDto?> ObtenerProyectoActualAsync(string email);
}
