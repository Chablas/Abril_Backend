namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Dtos
{
    // ── ProjectContract ──────────────────────────────────────────────────────
    public class ProjectContractCreateDTO
    {
        public int ProjectId { get; set; }
        public int ContractorId { get; set; }
        public int WorkSpecialtyId { get; set; }
        public string? ServiceDescription { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public string? ContractorEmail { get; set; }
        public DateOnly? SigningDate { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? TermDays { get; set; }
        public string? DetalleServicios { get; set; }
    }

    public class ProjectContractEditDTO
    {
        public string? ServiceDescription { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public string? ContractorEmail { get; set; }
        public DateOnly? SigningDate { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? TermDays { get; set; }
        public string? DetalleServicios { get; set; }
    }

    public class ProjectContractDTO
    {
        public int ProjectContractId { get; set; }
        public int ProjectId { get; set; }
        public int ContractorId { get; set; }
        public string? ContractorName { get; set; }
        public int WorkSpecialtyId { get; set; }
        public string? WorkSpecialtyDescription { get; set; }
        public int ProjectContractStatusId { get; set; }
        public string? ProjectContractStatusDescription { get; set; }
        public int? ContractNumber { get; set; }
        public string? ServiceDescription { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public string? CurrencyCode { get; set; }
        public string? ContractorEmail { get; set; }
        public DateOnly? SigningDate { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? TermDays { get; set; }
        public string? DetalleServicios { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public bool Active { get; set; }
        public List<ProjectContractMilestoneDTO> Milestones { get; set; } = new();

        // Pasos 4-9
        public bool ContractorNotificationSkipped { get; set; }
        public bool? ArrivedWithObservations { get; set; }
        public string? ArrivalObservation { get; set; }
        public bool Step6SignedJefeProyectos { get; set; }
        public bool Step6SignedGerenteInmobiliario { get; set; }
        public bool Step6SignedGerenteGeneral { get; set; }
    }

    // ── ProjectContractMilestone ─────────────────────────────────────────────
    public class ProjectContractMilestoneCreateDTO
    {
        public string Description { get; set; } = null!;
        public decimal Percentage { get; set; }
        public DateOnly? PaidDate { get; set; }
        public string? ChequeRecibo { get; set; }
        public string? Observation { get; set; }
    }

    public class ProjectContractMilestoneDTO
    {
        public int ProjectContractMilestoneId { get; set; }
        public int ProjectContractId { get; set; }
        public int Order { get; set; }
        public string Description { get; set; } = null!;
        public decimal Percentage { get; set; }
        public decimal Amount { get; set; }
        public DateOnly? PaidDate { get; set; }
        public string? ChequeRecibo { get; set; }
        public string? Observation { get; set; }
        /// <summary>true solo para el último hito por Order — el que la cláusula Octava
        /// (garantías) del contrato referencia como "hito de garantía".</summary>
        public bool EsHitoDeGarantia { get; set; }
    }

    // ── Carpeta de SharePoint (Configuración, por proyecto) ──────────────────
    public class ProjectContractFolderDTO
    {
        public int ProjectContractFolderId { get; set; }
        public int ProjectId { get; set; }
        public string LinkUrl { get; set; } = null!;
        public string? FolderName { get; set; }
        public string? WebUrl { get; set; }
    }

    public class ProjectContractFolderSaveDTO
    {
        public string LinkUrl { get; set; } = null!;
    }

    // ── Pasos 4-9 del flujo ──────────────────────────────────────────────────
    public class ProjectContractStep5ArrivalDTO
    {
        public bool ArrivedWithObservations { get; set; }
        public string? ArrivalObservation { get; set; }
    }

    public class ProjectContractStep6SignaturesDTO
    {
        public bool Step6SignedJefeProyectos { get; set; }
        public bool Step6SignedGerenteInmobiliario { get; set; }
        public bool Step6SignedGerenteGeneral { get; set; }
    }

    // ── Generación del documento ─────────────────────────────────────────────
    /// <summary>Todo lo que necesita el merge de la plantilla .docx (WordTemplateHelper) — no es
    /// un DTO de presentación, es el insumo crudo de GenerateContractAsync.</summary>
    public class ProjectContractGenerationDataDTO
    {
        public int ProjectContractId { get; set; }
        public int ProjectId { get; set; }
        /// <summary>Nombre ya asignado de la carpeta del contrato en SharePoint ("CONTRATO N° X"),
        /// null si todavía no se subió ningún documento.</summary>
        public string? FolderName { get; set; }
        public int? ContractNumber { get; set; }
        public string? ServiceDescription { get; set; }
        public decimal Amount { get; set; }
        public string CurrencyCode { get; set; } = "PEN";
        public DateOnly? SigningDate { get; set; }
        public string? DetalleServicios { get; set; }

        /// <summary>Especialidad — determina qué plantilla usar (Arquitectura/Estructuras vs. la
        /// genérica del resto).</summary>
        public string? WorkSpecialtyDescription { get; set; }

        // Proyecto (razón social "EL CONTRATANTE" — de Project.ContributorId, EsAbril=true)
        public string ProjectRazonSocial { get; set; } = null!;
        public string ProjectRuc { get; set; } = null!;
        public string ProjectNombre { get; set; } = null!;
        public string? ProjectUbicacionObra { get; set; }
        public string? ProjectDistrito { get; set; }

        // Contratista — de Contractor.ContributorId
        public string ContratistaRazonSocial { get; set; } = null!;
        public string ContratistaRuc { get; set; } = null!;
        public string? ContratistaUbicacion { get; set; }
        public string? ContratistaDistrito { get; set; }
        public string? ContratistaRepresentanteNombre { get; set; }
        public string? ContratistaRepresentanteDni { get; set; }

        public string? ProyectoAbreviatura { get; set; }
        public List<ProjectContractMilestoneDTO> Milestones { get; set; } = new();
    }
}
