using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces
{
    public interface IReclutamientoService
    {
        Task<ReclutamientoFormDataDto> GetFormData(int? userId);

        /// <summary>
        /// Crea la solicitud de personal: valida, sube el sustento (opcional) a SharePoint y
        /// persiste 1 requerimiento por vacante con su código REQ-AAAA-NNNN.
        /// </summary>
        Task<SolicitudPersonalCreateResultDto> Create(SolicitudPersonalCreateDto dto, int? userId, IFormFile? sustento);

        /// <summary>Tabla "Mis solicitudes de vacante" del usuario (vacío si no hay usuario).</summary>
        Task<List<SolicitudVacanteListItemDto>> GetMisSolicitudes(int? userId);

        /// <summary>
        /// Bandeja de la vista de GTH: tarjeta "En proceso" + tabla de solicitudes de contratación de
        /// toda la organización, en una sola petición.
        /// </summary>
        Task<BandejaReclutamientoDto> GetBandeja();

        /// <summary>Actualiza la prioridad (Alta/Media/Baja) de un requerimiento desde la bandeja de GTH.</summary>
        Task UpdatePrioridad(int requerimientoId, int prioridadId, int? userId);

        /// <summary>
        /// Detalle de seguimiento de un requerimiento del usuario (cabecera + fases del pipeline).
        /// Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si no existe
        /// o no le pertenece al usuario.
        /// </summary>
        Task<SeguimientoDto> GetSeguimiento(int requerimientoId, int? userId);

        /// <summary>
        /// Detalle de un requerimiento para la vista de GTH (modal del ojo de la bandeja).
        /// Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si no existe.
        /// </summary>
        Task<DetalleRequerimientoGthDto> GetDetalleGth(int requerimientoId);

        /// <summary>Guarda la asignación interna de GTH de un requerimiento (los 4 desplegables del modal).</summary>
        Task UpdateAsignacionGth(int requerimientoId, AsignacionGthUpdateDto dto, int? userId);

        /// <summary>
        /// Registra los canales donde se publicó la vacante (reemplaza el conjunto) y avanza el
        /// requerimiento a la fase PUBLICACION. Devuelve el estado resultante.
        /// </summary>
        Task<EstadoRequerimientoResultDto> ReplacePublicaciones(int requerimientoId, PublicacionesUpdateDto dto, int? userId);

        /// <summary>Inicia la revisión de CV: avanza el requerimiento de PUBLICACION a LONG_LIST.</summary>
        Task<EstadoRequerimientoResultDto> IniciarRevisionCv(int requerimientoId, int? userId);

        /// <summary>Destinatarios configurados del correo de nueva solicitud (principales + copias).</summary>
        Task<CorreoDestinatariosDto> GetCorreoDestinatarios();

        /// <summary>Guarda (valida y normaliza) los destinatarios del correo de nueva solicitud.</summary>
        Task SaveCorreoDestinatarios(CorreoDestinatariosDto dto, int? userId);
    }
}
