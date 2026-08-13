namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>Opción genérica {id, nombre} para desplegables del formulario.</summary>
    public class OpcionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    /// <summary>
    /// Opción del desplegable "Tipo de requerimiento". Lleva además el código estable del catálogo
    /// (<c>NUEVO</c> / <c>REEMPLAZO</c>) porque el formulario cambia de forma según el tipo: al
    /// elegir Reemplazo aparece el desplegable del trabajador reemplazado. El frontend decide por
    /// el código y no por el nombre, que es solo presentación.
    /// </summary>
    public class TipoRequerimientoOpcionDto : OpcionDto
    {
        public string Codigo { get; set; } = string.Empty;
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

        public List<TipoRequerimientoOpcionDto> TiposRequerimiento { get; set; } = new();
        public List<OpcionDto> Proyectos { get; set; } = new();

        /// <summary>
        /// Trabajadores entre los que se elige al reemplazado cuando el tipo de requerimiento es
        /// <c>REEMPLAZO</c>: los del <c>area_scope</c> del solicitante y los de cualquier área hija
        /// (mismo subárbol que el filtro de Área del resto del sistema), incluido el propio
        /// solicitante — pedir el reemplazo de uno mismo por renuncia o promoción es un caso real.
        ///
        /// Se sirve acá y no en un endpoint aparte porque es una lista chica (los trabajadores de
        /// un área) y así abrir el modal sigue siendo una sola petición. Vacía cuando el
        /// solicitante no tiene <see cref="AreaScopeId"/>: en ese caso no hay de dónde elegir y el
        /// campo deja de ser obligatorio.
        /// </summary>
        public List<OpcionDto> TrabajadoresArea { get; set; } = new();

        /// <summary>
        /// A quién le llegará el correo si se envía la solicitud en este momento. Lo resuelve el
        /// mismo servicio que hace el envío real, así que el aviso del modal no puede divergir de
        /// lo que sale. Listas vacías = no hay a quién notificar (hay que configurarlo).
        /// </summary>
        public SolicitudDestinatariosDto Destinatarios { get; set; } = new();
    }
}
