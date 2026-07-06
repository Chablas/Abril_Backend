using Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Interfaces;

namespace Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Services;

public class IndicadoresProactivosService : IIndicadoresProactivosService
{
    private readonly IIndicadoresProactivosRepository _repo;
    private readonly IChecklistRepository _checklistRepo;

    public IndicadoresProactivosService(
        IIndicadoresProactivosRepository repo,
        IChecklistRepository checklistRepo)
    {
        _repo = repo;
        _checklistRepo = checklistRepo;
    }

    public Task<List<InspeccionTipoDto>> GetTiposInspeccionAsync()
        => _repo.GetTiposInspeccionAsync();

    public Task<ProgInspeccionResumenDto> GetProgInspeccionAsync(int proyectoId, int mes, int anio)
        => _repo.GetProgInspeccionAsync(proyectoId, mes, anio);

    public Task GuardarProgInspeccionAsync(GuardarProgInspeccionRequest request, int userId)
        => _repo.GuardarProgInspeccionAsync(request, userId);

    public async Task<IndicadorProactivoProyectoDto> GetIndicadoresProyectoAsync(int proyectoId, int mes, int anio)
    {
        var empresas = await _repo.GetMetasEmpresaAsync(proyectoId, mes, anio);
        var activas = empresas.Where(e => e.EsActiva).ToList();

        var resumenChecklists = await _checklistRepo.GetResumenProyectoAsync(proyectoId);
        var checklists = resumenChecklists.Checklists.Select(c => new ChecklistResumenDto(
            c.ChecklistProyectoId,
            c.NombrePlantilla,
            c.PorcentajeCompletado,
            c.Estado,
            c.EsObligatorio,
            c.TotalItems,
            c.ItemsCompletados
        )).ToList();

        return new IndicadorProactivoProyectoDto
        {
            ProyectoId = proyectoId,
            ProyectoNombre = "",
            TotalEmpresasActivas = activas.Count,
            MetaRacsTotal = activas.Sum(e => e.MetaRacs),
            MetaOptTotal = activas.Sum(e => e.MetaOpt),
            MetaAtsTotal = activas.Sum(e => e.MetaAts),
            MetaCharlasTotal = activas.Sum(e => e.MetaCharlas),
            MetaInspeccionesTotal = activas.Sum(e => e.MetaInspecciones),
            ActualRacsTotal = activas.Sum(e => e.ActualRacs),
            ActualRacsCerradosTotal = activas.Sum(e => e.ActualRacsCerrados),
            ActualOptTotal = activas.Sum(e => e.ActualOpt),
            ActualAtsTotal = activas.Sum(e => e.ActualAts),
            ActualCharlasTotal = activas.Sum(e => e.ActualCharlas),
            ActualInspeccionesTotal = activas.Sum(e => e.ActualInspecciones),
            PctRacs = activas.Any() ? activas.Average(e => e.PctRacs) : 0,
            PctRacsCerrados = activas.Any() ? activas.Average(e => e.PctRacsCerrados) : 0,
            PctOpt = activas.Any() ? activas.Average(e => e.PctOpt) : 0,
            PctAts = activas.Any() ? activas.Average(e => e.PctAts) : 0,
            PctCharlas = activas.Any() ? activas.Average(e => e.PctCharlas) : 0,
            PctInspecciones = activas.Any() ? activas.Average(e => e.PctInspecciones) : 0,
            PctProactivoGeneral = activas.Any() ? activas.Average(e => e.PctProactivoGeneral) : 0,
            Empresas = activas,
            Checklists = checklists
        };
    }

    public Task<List<IndicadorProactivoProyectoDto>> GetSeguimientoTodosProyectosAsync(int mes, int anio)
        => _repo.GetSeguimientoTodosProyectosAsync(mes, anio);

    public Task<PuntajeMesDto> GetPuntajeMesAsync(int proyectoId, int mes, int anio)
        => _repo.GetPuntajeMesAsync(proyectoId, mes, anio);

    public Task<List<PuntajeMesDto>> GetPuntajeTodosProyectosAsync(
        int mes, int anio, List<IndicadorProactivoProyectoDto>? seguimiento = null)
        => _repo.GetPuntajeTodosProyectosAsync(mes, anio, seguimiento);

    public Task<IndicadorReactivoProyectoDto> GetIndicadoresReactivosAsync(int proyectoId, int mes, int anio)
        => _repo.GetIndicadoresReactivosAsync(proyectoId, mes, anio);

    public Task<List<IndicadorReactivoProyectoDto>> GetIndicadoresReactivosTodosAsync(int mes, int anio)
        => _repo.GetIndicadoresReactivosTodosAsync(mes, anio);

    public Task<MetaAnualDto> GetMetaAnualAsync(int anio)
        => _repo.GetMetaAnualAsync(anio);

    public Task<MetaAnualDto> GuardarMetaAnualAsync(GuardarMetaAnualRequest request, int userId)
        => _repo.GuardarMetaAnualAsync(request, userId);
}
