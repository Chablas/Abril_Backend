using Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Interfaces
{
    /// <summary>
    /// Gestión Administrativa → Configuración → Revisores de Áreas: quién cumple cada uno de los
    /// cinco actores (aprobar la salida, jefe notificado, 1.ª revisión, consolidar, firmar el
    /// consolidado) para cada tipo de trabajador de cada área, lo que decide el algoritmo y lo
    /// personalizado encima.
    /// </summary>
    public interface IRevisoresAreasService
    {
        Task<RevisoresAreasInicialDto> GetInitialDataAsync(int userId, bool verTodas);

        Task<RevisoresAreaDetalleDto> GetDetalleAsync(int userId, bool verTodas, int areaScopeId, int? projectId);

        /// <summary>Guarda la fila y devuelve su detalle ya recalculado.</summary>
        Task<RevisoresAreaDetalleDto> GuardarAsync(int userId, int areaScopeId, RevisoresAreaGuardarDto dto);
    }
}
