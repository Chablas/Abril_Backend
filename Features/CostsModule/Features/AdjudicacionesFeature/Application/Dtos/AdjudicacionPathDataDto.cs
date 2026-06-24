namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos
{
    /// <summary>Datos mínimos para construir la ruta de carpetas en OneDrive del proyecto.</summary>
    public class AdjudicacionPathDataDto
    {
        public int    ProjectSubContractorId { get; set; }
        public int    ProjectId             { get; set; }
        public string ProjectDescription    { get; set; } = null!;
        public string? Abbreviation         { get; set; }
        public string ContributorRuc        { get; set; } = null!;
        public string ContributorName       { get; set; } = null!;
        public string WorkItemDescription   { get; set; } = null!;
        /// <summary>Especialidad asignada a la adjudicación (puede ser null si aún no se asignó).</summary>
        public string? WorkSpecialtyDescription { get; set; }

        // ── Carpeta de OneDrive del proyecto (Configuración → Carpeta de adjudicaciones) ──
        /// <summary>Drive de OneDrive donde vive la carpeta del proyecto. Null si no está configurada.</summary>
        public string? DriveId { get; set; }
        /// <summary>ItemId de la carpeta raíz del proyecto en OneDrive. Null si no está configurada.</summary>
        public string? ProjectFolderId { get; set; }
        /// <summary>Nombre de la carpeta configurada (para detectar si ya es la carpeta de 'Contratos').</summary>
        public string? ProjectFolderName { get; set; }

        /// <summary>Nombre ya asignado a la carpeta de esta adjudicación ("ADJUDICACIÓN N° X"). Null si aún no tiene.</summary>
        public string? AdjudicacionFolderName { get; set; }
    }
}
