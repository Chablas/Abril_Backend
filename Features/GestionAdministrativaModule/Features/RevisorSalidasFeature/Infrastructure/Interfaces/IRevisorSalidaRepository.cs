using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Infrastructure.Interfaces
{
    public interface IRevisorSalidaRepository
    {
        /// <summary>Trabajadores con correo @abril.pe + su revisor de salidas asignado (si lo tiene).</summary>
        Task<List<WorkerRevisorSalidaItemDto>> GetWorkerRevisoresAsync();

        /// <summary>Opciones para el selector de revisor: workers con correo corporativo @abril.pe.</summary>
        Task<List<WorkerRevisorSalidaOptionDto>> GetWorkerRevisorOptionsAsync();

        /// <summary>Asigna (o limpia con null) el revisor de salidas de un trabajador.</summary>
        Task UpdateWorkerRevisorAsync(int workerId, int? jefeWorkerId);
    }
}
