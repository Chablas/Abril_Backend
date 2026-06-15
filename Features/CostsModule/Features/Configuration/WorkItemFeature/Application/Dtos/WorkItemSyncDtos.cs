namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Dtos
{
    /// <summary>Especialidad disponible para asignar a una partida (combo del formulario de edición).</summary>
    public class WorkSpecialtyOptionDto
    {
        public int WorkSpecialtyId { get; set; }
        public string WorkSpecialtyDescription { get; set; } = null!;
    }

    /// <summary>Datos auxiliares del formulario de partidas (lista de especialidades activas).</summary>
    public class WorkItemFormDataDto
    {
        public List<WorkSpecialtyOptionDto> Specialties { get; set; } = new();
    }

    /// <summary>
    /// Raíz de la carpeta de adjudicaciones de un proyecto (ubicación estable resuelta en
    /// la configuración de "Carpeta Adj."). Punto de partida del recorrido del sincronizador.
    /// </summary>
    public class AdjudicacionFolderRootDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public string DriveId { get; set; } = null!;
        public string FolderId { get; set; } = null!;
    }

    /// <summary>Partida activa ya registrada (para dedup y para rellenar especialidad faltante).</summary>
    public class ExistingWorkItemDto
    {
        public int WorkItemId { get; set; }
        public string WorkItemDescription { get; set; } = null!;
        public int? WorkSpecialtyId { get; set; }
    }

    /// <summary>Resultado de la sincronización de partidas desde las carpetas de OneDrive.</summary>
    public class WorkItemSyncResultDto
    {
        /// <summary>Proyectos con carpeta de adjudicaciones configurada que se recorrieron.</summary>
        public int ProjectsScanned { get; set; }
        /// <summary>Partidas nuevas insertadas en la BD.</summary>
        public int Created { get; set; }
        /// <summary>Carpetas de partida que ya existían como partida (se omitieron en el alta).</summary>
        public int Existing { get; set; }
        /// <summary>Partidas existentes a las que se les completó la especialidad que tenían en null.</summary>
        public int SpecialtyFilled { get; set; }
        public List<string> CreatedDescriptions { get; set; } = new();
        /// <summary>Proyectos en los que no se encontró una carpeta de "Contratos".</summary>
        public List<string> ProjectsWithoutContratosFolder { get; set; } = new();
    }
}
