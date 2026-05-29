using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces
{
    public interface IGestionSalidaRepository
    {
        Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters);
        Task<GestionSalidaFilterDataDto> GetFilterData();
        Task Aprobar(int id, int reviewerUserId);
        Task Rechazar(int id, int reviewerUserId);

        /// <summary>
        /// Crea un registro <c>GaRendicion</c> con la info del PDF subido y marca como rendidas
        /// todas las solicitudes elegibles vinculándolas al rendicion. Todo en una transacción.
        /// </summary>
        Task<List<int>> CrearRendicionYMarcarBulk(
            IEnumerable<int> ids,
            int userId,
            string pdfUrl,
            string? pdfItemId,
            string pdfFilename);

        /// <summary>IDs elegibles (Aprobadas + No rendidas) sin tocar BD. Pre-flight.</summary>
        Task<List<int>> GetEligibleIdsForRendicion(IEnumerable<int> ids);

        /// <summary>
        /// Del set dado, devuelve solicitudes que tienen al menos UN trayecto SIN ninguna captura.
        /// (Una solicitud sin trayectos también se incluye como incompleta).
        /// </summary>
        Task<List<int>> GetIdsConTrayectosSinCapturas(IEnumerable<int> ids);

        /// <summary>Detalle completo (cabecera + trayectos con capturas + rendición si existe).</summary>
        Task<GestionSalidaDetalleDto?> GetDetalle(int id);

        /// <summary>Datos para armar la planilla — una fila por TRAYECTO de las solicitudes dadas.</summary>
        Task<List<RendicionItemDto>> GetRendicionData(List<int> solicitudIds);

        /// <summary>Registra (o limpia) la hora real en la que la persona salió. Solo se actualiza el campo extra; no afecta el flujo principal.</summary>
        Task SetHoraSalidaReal(int solicitudId, TimeOnly? hora, int registradaPorUserId);
    }
}
