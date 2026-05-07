namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos
{
    public class ProjectDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public string? Codigo { get; set; }
        public string? Abbreviation { get; set; }
        public string? LevelDescription { get; set; }
        public string? Estado { get; set; }

        // Contribuyente
        public int? ContributorId { get; set; }
        public string? ContributorRuc { get; set; }
        public string? ContributorName { get; set; }
        public string? ContributorAddress { get; set; }
        public string? ContributorDistrict { get; set; }
        public string? ContributorProvince { get; set; }
        public string? ContributorDepartment { get; set; }
        public string? ContributorLegalEntityRegistryNumber { get; set; }

        // Ubicación del proyecto
        public string? ProjectDistrict { get; set; }
        public string? ProjectProvince { get; set; }
        public string? ProjectDepartment { get; set; }
        public string? ProjectLocation { get; set; }

        // Responsable
        public string? ResponsableArqCom { get; set; }
        public int? ResponsableArqComId { get; set; }

        // Fechas
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaFin { get; set; }
        public DateOnly? InicioObra { get; set; }
        public DateOnly? FinObra { get; set; }

        // Métricas físicas
        public string? NumNiveles { get; set; }
        public string? NumSotanos { get; set; }
        public string? Pisos { get; set; }
        public int? TiempoConstruccion { get; set; }
        public decimal? AreaM2 { get; set; }
        public decimal? AreaTechadaM2 { get; set; }
        public decimal? HhTotalCasa { get; set; }
        public string? CantTrabajadoresCasa { get; set; }

        // Flags
        public bool? TieneArquitecturaComercial { get; set; }

        public bool Active { get; set; }
    }
}
