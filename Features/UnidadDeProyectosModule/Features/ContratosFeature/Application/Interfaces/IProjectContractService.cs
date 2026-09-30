using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Interfaces
{
    public interface IProjectContractService
    {
        Task<List<ProjectContractDTO>> GetAllByProjectIdAsync(int projectId);
        Task<ProjectContractDTO> GetByIdAsync(int projectContractId);
        Task<int> CreateAsync(ProjectContractCreateDTO dto, int userId);
        Task EditAsync(int projectContractId, ProjectContractEditDTO dto, int userId);
        Task<ProjectContractMilestoneDTO> AddMilestoneAsync(int projectContractId, ProjectContractMilestoneCreateDTO dto, int userId);
        Task DeleteMilestoneAsync(int projectContractMilestoneId, int userId);

        /// <summary>Genera el .docx del contrato (paso 3) mergeando la plantilla correspondiente
        /// a la especialidad con los datos del contrato y sus hitos de pago.</summary>
        Task<(byte[] Bytes, string FileName)> GenerateContractAsync(int projectContractId);

        // ── Pasos 4-9 ─────────────────────────────────────────────────────────
        /// <summary>Paso 4: envía el contrato recién generado al correo del contratista (o solo
        /// registra que ya se envió fuera del sistema, si <paramref name="skipNotification"/>).</summary>
        Task AdvanceToStep4Async(int projectContractId, bool skipNotification, int userId);
        Task RegisterStep5ArrivalAsync(int projectContractId, ProjectContractStep5ArrivalDTO dto, int userId);
        Task UpdateStep6SignaturesAsync(int projectContractId, ProjectContractStep6SignaturesDTO dto, int userId);
        /// <summary>Paso 8: notifica al correo de Unidad de Proyectos (unidadproyectosnm@abril.pe).</summary>
        Task NotifyStep8Async(int projectContractId, int userId);
        Task CloseStep9Async(int projectContractId, int userId);
    }
}
