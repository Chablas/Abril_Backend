namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos
{
    /// <summary>
    /// Correos SSOMA de un proyecto que se editan en Configuración → Proyectos.
    ///
    /// Ni el residente ni el coordinador administrativo son correos escritos a mano:
    /// son referencias al trabajador (<see cref="ResidenteWorkersId"/> y
    /// <see cref="WorkersCoordAdminId"/>). Su correo se lee de
    /// <c>workers.email_corporativo</c> al enviar, así no queda una segunda copia que se
    /// desactualiza cuando la persona cambia de correo.
    /// </summary>
    public class ProjectEmailsUpdateDto
    {
        /// <summary>
        /// Trabajador que es residente del proyecto, o null para dejarlo sin residente.
        /// A diferencia de los correos de texto, acá null SÍ limpia el valor: el
        /// formulario siempre manda el objeto completo.
        /// </summary>
        public int? ResidenteWorkersId { get; set; }

        /// <summary>
        /// Trabajador que es coordinador administrativo, o null para dejarlo sin
        /// coordinador. Misma semántica que <see cref="ResidenteWorkersId"/>: null limpia.
        /// </summary>
        public int? WorkersCoordAdminId { get; set; }

        public string? EmailResponsable { get; set; }
        public string? EmailRrhh { get; set; }
        public string? EmailCoordSsoma { get; set; }
    }

    /// <summary>
    /// Un trabajador elegible como residente o coordinador administrativo, para los
    /// desplegables del formulario.
    /// </summary>
    public class ResidenteOptionDto
    {
        public int WorkerId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// Respuesta única del formulario de correos del proyecto: los valores actuales, los
    /// correos del residente y del coordinador administrativo ya resueltos (solo lectura)
    /// y los trabajadores elegibles, en una sola petición.
    /// </summary>
    public class ProjectEmailsDto
    {
        public int? ResidenteWorkersId { get; set; }
        /// <summary>Nombre del residente actual, para mostrarlo sin tener que buscarlo en la lista.</summary>
        public string? ResidenteNombre { get; set; }
        /// <summary>Correo corporativo del residente actual — el que realmente se va a usar.</summary>
        public string? ResidenteEmail { get; set; }

        public int? WorkersCoordAdminId { get; set; }
        /// <summary>Nombre del coordinador administrativo actual.</summary>
        public string? CoordAdminNombre { get; set; }
        /// <summary>Correo corporativo del coordinador administrativo actual — el que realmente se va a usar.</summary>
        public string? CoordAdminEmail { get; set; }

        public string? EmailResponsable { get; set; }
        public string? EmailRrhh { get; set; }
        public string? EmailCoordSsoma { get; set; }

        public List<ResidenteOptionDto> Residentes { get; set; } = new();
    }
}
