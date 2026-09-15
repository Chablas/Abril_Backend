using Abril_Backend.Features.GestionAdministrativa.CapturasArea.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.CapturasArea.Application.Interfaces
{
    public interface ICapturaAreaService
    {
        /// <summary>Tabla de áreas + opciones de los filtros, en una sola llamada.</summary>
        Task<CapturaAreaInicialDto> GetInitialDataAsync();

        /// <summary>Marca las capturas del área como obligatorias u opcionales.</summary>
        Task SetCapturasObligatoriasAsync(int areaScopeId, bool capturasObligatorias);
    }
}
