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
        /// <summary>
        /// Correo corporativo, solo para mostrarlo bajo el desplegable. El que se guarda
        /// es el id: el correo se vuelve a leer de la ficha del trabajador al enviar.
        /// </summary>
        public string? Email { get; set; }
    }

    /// <summary>
    /// Todos los desplegables del modal crear/editar proyecto en una sola respuesta —
    /// antes eran dos peticiones (una por tipo de responsable) y el coordinador
    /// administrativo habría sido una tercera.
    /// </summary>
    public class ProjectLookupsDto
    {
        /// <summary>Trabajadores activos de la subárea Arquitectura Comercial.</summary>
        public List<ResponsableLookupDto> ArqCom { get; set; } = new();
        /// <summary>Trabajadores activos de la subárea Unidad de Proyectos.</summary>
        public List<ResponsableLookupDto> Udp { get; set; } = new();
        /// <summary>
        /// Elegibles como coordinador administrativo: personal Casa no retirado con correo
        /// corporativo — mismo criterio que Gestión de Responsables.
        /// </summary>
        public List<ResponsableLookupDto> CoordAdmins { get; set; } = new();
    }

    /// <summary>Worker del usuario logueado, resuelto vía User→Person.UserId→Worker.PersonId
    /// (mismo cruce que ProjectRepository.GetMyProjectIds). Null si no tiene ficha vinculada.</summary>
    public class MyWorkerDto
    {
        public int WorkerId { get; set; }
        public string ApellidoNombre { get; set; } = string.Empty;
    }
}
