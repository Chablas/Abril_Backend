namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Payload del formulario "Nueva solicitud de personal". La justificación y el sustento
    /// (adjunto multipart, aparte del JSON) son de la solicitud completa; cada elemento de
    /// <see cref="Vacantes"/> genera un requerimiento independiente.
    /// </summary>
    public class SolicitudPersonalCreateDto
    {
        public string? Justificacion { get; set; }
        public List<VacanteCreateDto> Vacantes { get; set; } = new();
    }

    public class VacanteCreateDto
    {
        /// <summary>
        /// Puesto elegido del catálogo. Null cuando <see cref="PuestoPersonalizado"/> es true: en
        /// ese caso el puesto lo resuelve el backend a partir de <see cref="PuestoNombre"/>.
        /// </summary>
        public int? PuestoId { get; set; }

        /// <summary>
        /// true = el solicitante marcó "Puesto personalizado": escribió un puesto que no está en el
        /// desplegable y eligió a mano su categoría. El backend lo da de alta en el catálogo
        /// <c>puesto</c> (o reutiliza el existente si el nombre ya está) antes de crear la vacante.
        /// </summary>
        public bool PuestoPersonalizado { get; set; }

        /// <summary>Nombre del puesto personalizado. Se guarda normalizado en MAYÚSCULAS.</summary>
        public string? PuestoNombre { get; set; }

        /// <summary>
        /// Categoría real de la vacante. Obligatoria con <see cref="PuestoPersonalizado"/>: es la
        /// mitad del par (puesto, categoría) que queda guardado en el requerimiento para cuando el
        /// seleccionado entre a <c>workers</c>. Ignorada cuando el puesto viene del desplegable.
        /// </summary>
        public int? CategoriaId { get; set; }

        public int TipoRequerimientoId { get; set; }

        /// <summary>
        /// Trabajador al que reemplaza la vacante. Obligatorio cuando el tipo de requerimiento es
        /// <c>REEMPLAZO</c> (salvo que el solicitante no tenga <c>area_scope_id</c> y por lo tanto
        /// no haya lista de dónde elegir); ignorado en las vacantes nuevas, donde se guarda null.
        /// Debe pertenecer al área del solicitante o a un área hija — el backend lo revalida, no se
        /// confía en lo que mande el cliente.
        /// </summary>
        public int? ReemplazaWorkerId { get; set; }

        public int ProjectId { get; set; }
        public DateOnly FechaRequeridaIngreso { get; set; }
    }

    /// <summary>Resultado de crear la solicitud: los códigos REQ-AAAA-NNNN generados.</summary>
    public class SolicitudPersonalCreateResultDto
    {
        public int SolicitudId { get; set; }
        public List<string> Codigos { get; set; } = new();

        /// <summary>
        /// ¿Salió el correo de aprobación a Gerencia General? false cuando no hay destinatarios
        /// configurados o el envío falló: la solicitud queda registrada esperando un reenvío, así
        /// que hay que avisárselo al solicitante.
        /// </summary>
        public bool CorreoGerenciaEnviado { get; set; }
    }
}
