using Abril_Backend.Features.GestionAdministrativa.CapturasArea.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.CapturasArea.Infrastructure.Interfaces
{
    public interface ICapturaAreaRepository
    {
        /// <summary>Tabla de áreas + opciones de los filtros, en una sola conexión.</summary>
        Task<CapturaAreaInicialDto> GetInitialDataAsync();

        /// <summary>Upsert del flag en ga_salidas_area_config (crea la fila del área si no existía).</summary>
        Task SetCapturasObligatoriasAsync(int areaScopeId, bool capturasObligatorias);
    }
}
