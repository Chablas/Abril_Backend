namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>Opción genérica {id, nombre} para desplegables del formulario.</summary>
    public class OpcionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    /// <summary>
    /// Datos que necesita el formulario "Nueva solicitud de personal" en una sola petición:
    /// el área del solicitante (derivada del usuario, no editable) y los catálogos de los
    /// desplegables (puestos, tipos de requerimiento y proyectos/obras).
    /// </summary>
    public class ReclutamientoFormDataDto
    {
        public string? AreaNombre { get; set; }
        public int? AreaScopeId { get; set; }
        public int MaxVacantes { get; set; } = 10;
        public List<OpcionDto> Puestos { get; set; } = new();
        public List<OpcionDto> TiposRequerimiento { get; set; } = new();
        public List<OpcionDto> Proyectos { get; set; } = new();
    }
}
