using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Interfaces
{
    public interface IRevisorSalidaService
    {
        /// <summary>Carga inicial de la página: trabajadores con sus revisores + opciones + árbol de áreas.</summary>
        Task<RevisorSalidaInicialDto> GetInitialDataAsync();

        /// <summary>Reemplaza el conjunto de revisores de un trabajador.</summary>
        Task UpdateWorkerRevisoresAsync(int workerId, List<WorkerRevisorAsignacionDto> revisores);
    }
}
