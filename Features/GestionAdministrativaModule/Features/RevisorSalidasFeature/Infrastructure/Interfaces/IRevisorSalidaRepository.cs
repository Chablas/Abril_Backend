using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Infrastructure.Interfaces
{
    public interface IRevisorSalidaRepository
    {
        /// <summary>Carga inicial: trabajadores con sus revisores + opciones + árbol de áreas, en una sola conexión.</summary>
        Task<RevisorSalidaInicialDto> GetInitialDataAsync();

        /// <summary>Reemplaza el conjunto de revisores vivos de un trabajador (diff con soft-delete).</summary>
        Task UpdateWorkerRevisoresAsync(int workerId, List<WorkerRevisorAsignacionDto> revisores);
    }
}
