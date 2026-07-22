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
        /// Detalle de seguimiento de un requerimiento del usuario (cabecera + fases del pipeline).
        /// Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si no existe
        /// o no le pertenece al usuario.
        /// </summary>
        Task<SeguimientoDto> GetSeguimiento(int requerimientoId, int? userId);

        /// <summary>Destinatarios configurados del correo de nueva solicitud (principales + copias).</summary>
        Task<CorreoDestinatariosDto> GetCorreoDestinatarios();

        /// <summary>Guarda (valida y normaliza) los destinatarios del correo de nueva solicitud.</summary>
        Task SaveCorreoDestinatarios(CorreoDestinatariosDto dto, int? userId);
    }
}
