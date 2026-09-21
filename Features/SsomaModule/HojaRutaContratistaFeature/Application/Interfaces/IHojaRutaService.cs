using Abril_Backend.Features.SsomaModule.HojaRutaContratistaFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.HojaRutaContratistaFeature.Application.Interfaces;

public interface IHojaRutaService
{
    Task<HojaRutaResumenDto> GetResumenAsync(int contributorId, int proyectoId, int anio, int numeroSemana);

    /// <summary>Contratistas (no Abril) con al menos un trabajador con vinculación activa en
    /// ese proyecto hoy — no el catálogo completo de empresas del sistema.</summary>
    Task<List<ContratistaActivoDto>> GetContratistasActivosAsync(int proyectoId);
}
