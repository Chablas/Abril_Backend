namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Payload del formulario "Nueva solicitud de personal". La justificación (obligatoria) y el
    /// sustento (adjunto multipart, aparte del JSON) son de la solicitud completa; cada elemento de
    /// <see cref="Vacantes"/> genera un requerimiento independiente.
    /// </summary>
    public class SolicitudPersonalCreateDto
    {
        /// <summary>
        /// Justificación general de la solicitud. Obligatoria: es el sustento que leen el gerente
        /// del área y Gerencia General para aprobar, y va en el cuerpo de sus correos.
        /// </summary>
        public string? Justificacion { get; set; }

        public List<VacanteCreateDto> Vacantes { get; set; } = new();
    }

    public class VacanteCreateDto
    {
        /// <summary>
        /// Puesto elegido del catálogo. Es el único origen posible: el solicitante no puede dar de
        /// alta puestos nuevos desde este formulario — eso lo hace GTH en el catálogo de puestos.
        /// </summary>
        public int? PuestoId { get; set; }

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

        /// <summary>
        /// Salario bruto mensual de la vacante, en soles. Obligatorio: es parte de lo que el
        /// gerente del área y Gerencia General aprueban. Se guarda redondeado a 2 decimales.
        /// </summary>
        public decimal? SalarioBrutoMensual { get; set; }
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
