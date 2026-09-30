using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Interfaces
{
    public interface IProjectContractRepository
    {
        Task<List<ProjectContractDTO>> GetAllByProjectIdAsync(int projectId);
        Task<ProjectContractDTO?> GetByIdAsync(int projectContractId);
        Task<int> CreateAsync(ProjectContractCreateDTO dto, int userId);
        Task EditAsync(int projectContractId, ProjectContractEditDTO dto, int userId);
        Task<ProjectContractMilestoneDTO> AddMilestoneAsync(int projectContractId, ProjectContractMilestoneCreateDTO dto, int userId);
        Task DeleteMilestoneAsync(int projectContractMilestoneId, int userId);

        /// <summary>Datos crudos de un contrato + sus hitos, tal como los necesita el merge de la
        /// plantilla .docx (WordTemplateHelper) — no es un DTO de presentación.</summary>
        Task<ProjectContractGenerationDataDTO> GetGenerationDataAsync(int projectContractId);

        // ── Pasos 4-9 ─────────────────────────────────────────────────────────
        /// <summary>Paso 4: registra si se omitió el envío (ya se mandó fuera del sistema) y
        /// avanza el estado a 4. El envío del correo en sí lo hace el service (necesita generar
        /// el documento), acá solo se persiste el resultado.</summary>
        Task SetStep4SentAsync(int projectContractId, bool notificationSkipped, int userId);
        Task SetStep5ArrivalAsync(int projectContractId, ProjectContractStep5ArrivalDTO dto, int userId);
        Task SetStep6SignaturesAsync(int projectContractId, ProjectContractStep6SignaturesDTO dto, int userId);
        Task SetStep8NotifiedAsync(int projectContractId, int userId);
        Task SetStep9ClosedAsync(int projectContractId, int userId);

        // ── Almacenamiento (SharePoint) ──────────────────────────────────────
        /// <summary>Persiste el nombre de carpeta recién asignado ("CONTRATO N° X") — solo se
        /// llama la primera vez que se sube un documento del contrato.</summary>
        Task SetFolderNameAsync(int projectContractId, string folderName);
        /// <summary>Persiste la referencia del último .docx generado y subido a SharePoint.</summary>
        Task SetContractDocumentAsync(int projectContractId, string fileUrl, string originalFileName, string? storageItemId);
    }
}
