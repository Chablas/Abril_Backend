namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos
{
    public class ContributorLookupDto
    {
        public int ContributorId { get; set; }
        public string ContributorRuc { get; set; } = null!;
        public string ContributorName { get; set; } = null!;
        public string ContributorAddress { get; set; } = null!;
        public string? ContributorDistrict { get; set; }
        public string? ContributorProvince { get; set; }
        public string? ContributorDepartment { get; set; }
        public string? LegalEntityRegistryNumber { get; set; }
    }

    public class ResponsableLookupDto
    {
        public int Id { get; set; }
        public string ApellidoNombre { get; set; } = string.Empty;
    }

    /// <summary>Worker del usuario logueado, resuelto vía User→Person.UserId→Worker.PersonId
    /// (mismo cruce que ProjectRepository.GetMyProjectIds). Null si no tiene ficha vinculada.</summary>
    public class MyWorkerDto
    {
        public int WorkerId { get; set; }
        public string ApellidoNombre { get; set; } = string.Empty;
    }
}
