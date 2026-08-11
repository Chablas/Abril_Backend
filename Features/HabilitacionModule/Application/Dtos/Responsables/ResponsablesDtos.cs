namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Responsables
{
    /// <summary>Trabajador elegible como responsable (con correo corporativo), para el picker.</summary>
    public class ResponsableWorkerOptionDto
    {
        public int WorkerId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// Una razón social (Contributor) de Casa con su administrador responsable actual.
    /// El correo se guarda como texto en <c>contributor.email_administrador</c> — no hay FK a
    /// worker, el picker del frontend solo lo usa para autocompletar el valor al seleccionar.
    /// </summary>
    public class ResponsableRazonSocialDto
    {
        public int ContributorId { get; set; }
        public string ContributorName { get; set; } = string.Empty;
        public string? EmailAdministrador { get; set; }
    }

    public class ResponsableRazonSocialUpdateDto
    {
        public string? EmailAdministrador { get; set; }
    }

    /// <summary>Un proyecto con su coordinador de administración actual (project.email_coord_admin).</summary>
    public class ResponsableProyectoDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = string.Empty;
        public string? EmailCoordAdmin { get; set; }
    }

    public class ResponsableProyectoUpdateDto
    {
        public string? EmailCoordAdmin { get; set; }
    }

    /// <summary>Payload único de la pantalla "Gestión de Responsables": todo en una petición.</summary>
    public class ResponsablesDto
    {
        public List<ResponsableRazonSocialDto> RazonesSociales { get; set; } = new();
        public List<ResponsableProyectoDto> Proyectos { get; set; } = new();
        public List<ResponsableWorkerOptionDto> Trabajadores { get; set; } = new();
    }
}
