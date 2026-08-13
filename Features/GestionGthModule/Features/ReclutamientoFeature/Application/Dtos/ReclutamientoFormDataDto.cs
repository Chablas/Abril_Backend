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
    /// el área del solicitante (derivada del usuario, no editable), los catálogos de los
    /// desplegables (puestos, tipos de requerimiento y proyectos/obras) y los destinatarios a
    /// los que le llegará la solicitud.
    /// </summary>
    public class ReclutamientoFormDataDto
    {
        public string? AreaNombre { get; set; }
        public int? AreaScopeId { get; set; }
        public int MaxVacantes { get; set; } = 10;
        public List<OpcionDto> Puestos { get; set; } = new();

        /// <summary>
        /// Categorías vigentes para el modo "Puesto personalizado": el solicitante escribe el puesto
        /// y elige de aquí su categoría (la real del trabajador, ver <c>gth_requerimiento.categoria_id</c>).
        /// </summary>
        public List<OpcionDto> Categorias { get; set; } = new();

        public List<OpcionDto> TiposRequerimiento { get; set; } = new();
        public List<OpcionDto> Proyectos { get; set; } = new();

        /// <summary>
        /// A quién le llegará el correo si se envía la solicitud en este momento. Lo resuelve el
        /// mismo servicio que hace el envío real, así que el aviso del modal no puede divergir de
        /// lo que sale. Listas vacías = no hay a quién notificar (hay que configurarlo).
        /// </summary>
        public SolicitudDestinatariosDto Destinatarios { get; set; } = new();
    }
}
