using Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Infrastructure.Interfaces
{
    public interface IRevisoresAreasRepository
    {
        /// <param name="userId">Usuario autenticado (app_user).</param>
        /// <param name="verTodas">true = ve todas las áreas y puede editarlas.</param>
        Task<RevisoresAreasInicialDto> GetInitialDataAsync(int userId, bool verTodas);

        /// <param name="projectId">Null = la fila del área; con valor = la subfila de esa obra.</param>
        Task<RevisoresAreaDetalleDto> GetDetalleAsync(int userId, bool verTodas, int areaScopeId, int? projectId);

        /// <summary>
        /// Guarda de una vez todas las celdas de una fila: cada celda que viene queda exactamente con
        /// su lista (vacía = sin personalizar); las que no vienen no se tocan.
        /// </summary>
        Task GuardarAsync(int areaScopeId, RevisoresAreaGuardarDto dto);
    }
}
