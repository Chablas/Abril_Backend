using Abril_Backend.Features.Ssoma.Paso.Dtos;

namespace Abril_Backend.Features.Ssoma.Paso.Services;

public interface IPasoService
{
    Task<PagedResult<PasoListItemDto>> GetListAsync(PasoListQuery q);
    Task<PasoDetalleDto?> GetDetalleAsync(int id);
    Task<PasoListItemDto> CrearAsync(CrearPasoRequest req, int userId);
    Task<PasoListItemDto> EditarAsync(int id, EditarPasoRequest req);
    Task<PasoListItemDto> AprobarAsync(int id, int userId);
    Task<PasoListItemDto> InstanciarAsync(int plantillaId, InstanciarPasoRequest req, int userId);
    Task<List<GanttItemDto>> GetGanttAsync(int id);
    Task<PasoSpiDto> GetSpiAsync(int id);
    Task<PasoDashboardDto> GetDashboardAsync(int? anio);
    Task<List<PasoAlertaDto>> GetAlertasAsync();
    Task<List<PasoCategoriaDto>> GetCategoriasAsync();
    Task<PasoActividadDto> CrearActividadAsync(CrearActividadRequest req);
    Task<PasoActividadDto> EditarActividadAsync(int id, EditarActividadRequest req);
    Task DeleteActividadAsync(int id);
    Task<PasoEjecucionDto> RegistrarEjecucionAsync(RegistrarEjecucionRequest req, int userId);
    Task<PasoEjecucionDto> ReprogramarEjecucionAsync(int id, ReprogramarEjecucionRequest req, int userId);
    Task<PasoEjecucionDto> SubirEvidenciaAsync(int ejecucionId, SubirEvidenciaRequest req, int userId);
    Task ProcesarCronAsync();
}
