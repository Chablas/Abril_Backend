using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces
{
    public interface IReclutamientoRepository
    {
        Task<ReclutamientoFormDataDto> GetFormData(int? userId);

        /// <summary>Área (nombre + area_scope) del worker vinculado al usuario. (null, null) si no resuelve.</summary>
        Task<(string? AreaNombre, int? AreaScopeId, int? WorkerId)> ResolveSolicitante(int userId);

        /// <summary>
        /// Link de la carpeta de SharePoint donde se suben los sustentos (singleton
        /// gth_sustento_folder, primera fila vigente). Se define por BD: dev y prod
        /// apuntan a bibliotecas distintas. null si no está configurada.
        /// </summary>
        Task<string?> GetSustentoFolderUrl();

        /// <summary>
        /// Persiste la solicitud + un requerimiento por vacante, generando el código
        /// REQ-AAAA-NNNN correlativo por año. Devuelve el id de la solicitud y los códigos creados.
        /// </summary>
        Task<SolicitudPersonalCreateResultDto> Create(GthSolicitud solicitud, List<VacanteCreateDto> vacantes, int? userId);

        /// <summary>
        /// Requerimientos (vacantes) registrados por el usuario, más recientes primero.
        /// Alimenta la tabla "Mis solicitudes de vacante".
        /// </summary>
        Task<List<SolicitudVacanteListItemDto>> GetMisSolicitudesVacante(int userId);

        /// <summary>Requerimientos de una solicitud concreta (para armar el correo de notificación).</summary>
        Task<List<SolicitudVacanteListItemDto>> GetRequerimientosBySolicitud(int solicitudId);

        /// <summary>
        /// Bandeja de la vista de GTH: tarjeta "En proceso" + tabla de solicitudes de contratación de
        /// toda la organización (todos los requerimientos vigentes), en 1 roundtrip.
        /// </summary>
        Task<BandejaReclutamientoDto> GetBandeja();

        /// <summary>
        /// Actualiza la prioridad de un requerimiento vigente. Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/>
        /// 400 si la prioridad no es válida y 404 si el requerimiento no existe.
        /// </summary>
        Task UpdatePrioridad(int requerimientoId, int prioridadId, int? userId);

        /// <summary>
        /// Detalle de seguimiento de un requerimiento del usuario (cabecera + fases del pipeline con su
        /// estado ya calculado), en 1 roundtrip. Devuelve null si el requerimiento no existe o no le
        /// pertenece al usuario.
        /// </summary>
        Task<SeguimientoDto?> GetSeguimiento(int requerimientoId, int userId);

        /// <summary>
        /// Detalle de un requerimiento para la vista de GTH (modal del ojo): cabecera + asignación
        /// interna + catálogos con cupos + canales de publicación, en 1 roundtrip. Devuelve null si
        /// el requerimiento no existe.
        /// </summary>
        Task<DetalleRequerimientoGthDto?> GetDetalleGth(int requerimientoId);

        /// <summary>
        /// Guarda la asignación interna de GTH (responsable, tipo de proceso, prioridad y razón
        /// social) reemplazando los 4 campos. Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/>
        /// 400 si algún id no es válido y 404 si el requerimiento no existe.
        /// </summary>
        Task UpdateAsignacionGth(int requerimientoId, AsignacionGthUpdateDto dto, int? userId);

        /// <summary>
        /// Reemplaza el conjunto de canales donde se registró la publicación de la vacante
        /// (reconciliación: agrega los nuevos, da de baja los quitados) y avanza el
        /// requerimiento a la fase PUBLICACION si aún no la alcanzó. Devuelve el estado resultante.
        /// </summary>
        Task<EstadoRequerimientoResultDto> ReplacePublicaciones(int requerimientoId, List<int> canalIds, int? userId);

        /// <summary>
        /// Avanza el requerimiento de la fase PUBLICACION a LONG_LIST (inicio de la revisión de CV).
        /// Idempotente si ya está en Long list o más adelante. Lanza
        /// <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 400 si la vacante aún
        /// no está publicada y 404 si el requerimiento no existe. Devuelve el estado resultante.
        /// </summary>
        Task<EstadoRequerimientoResultDto> IniciarRevisionCv(int requerimientoId, int? userId);

        /// <summary>Destinatarios vigentes del correo de nueva solicitud (principales + copias).</summary>
        Task<CorreoDestinatariosDto> GetCorreoDestinatarios();

        /// <summary>
        /// Reemplaza el conjunto de destinatarios vigentes por el indicado (reconciliación:
        /// conserva los que siguen, da de baja los quitados, agrega los nuevos). Los correos
        /// ya vienen normalizados (minúsculas) y sin duplicados desde el servicio.
        /// </summary>
        Task ReplaceCorreoDestinatarios(List<string> principales, List<string> copias, int? userId);
    }
}
