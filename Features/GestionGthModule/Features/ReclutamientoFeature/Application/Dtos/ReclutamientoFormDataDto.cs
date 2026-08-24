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
        /// <summary>
        /// Área a mostrar en el campo "Área del solicitante": el nombre del nodo de
        /// <see cref="AreaScopeId"/>, o de su primer ancestro que no sea una gerencia. Se resuelve
        /// desde el árbol de áreas y no desde <c>workers.area</c>, que es texto plano congelado.
        /// Null solo cuando el usuario no tiene ficha de trabajador o su ficha no tiene área.
        /// </summary>
        public string? AreaNombre { get; set; }

        /// <summary>Nodo de <c>area_scope</c> del solicitante; es el que define el subárbol de <see cref="TrabajadoresArea"/> y a qué gerente se le notifica.</summary>
        public int? AreaScopeId { get; set; }
        public int MaxVacantes { get; set; } = 10;
        public List<OpcionDto> Puestos { get; set; } = new();
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

        /// <summary>
        /// true si la ficha del solicitante es de Gerencia General (categoría del puesto). El
        /// formulario lo usa para avisar que una solicitud <b>FFT</b> suya no pasa por la aprobación
        /// de Gerencia General: se estaría aprobando a sí mismo, así que el pedido va directo a GTH.
        /// Sale de la misma regla con la que la pantalla «Aprobaciones» decide el nivel del usuario,
        /// nunca del rol.
        /// </summary>
        public bool EsGerenteGeneral { get; set; }

        /// <summary>
        /// A quién le llegaría el aviso a GTH de un pedido FFT registrado por el propio Gerente
        /// General (correo <c>FFT_SOLICITUD_GG</c>). Solo se resuelve cuando
        /// <see cref="EsGerenteGeneral"/> es true — en el resto de los casos ese correo no sale y el
        /// aviso del modal es el de <see cref="Destinatarios"/>.
        /// </summary>
        public SolicitudDestinatariosDto? DestinatariosFft { get; set; }
    }
}
