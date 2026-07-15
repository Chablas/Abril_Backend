using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces
{
    public interface IGestionSalidaRepository
    {
        Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters);

        /// <summary>Igual que <see cref="GetAll"/> pero ordenado por la columna indicada y paginado.</summary>
        Task<PagedResult<GestionSalidaListItemDto>> GetPaged(GestionSalidaFiltersDto filters);
        /// <summary>
        /// Datos de los filtros (trabajadores, lugares y árbol de áreas). Cuando
        /// <paramref name="seesAll"/> es false, tanto los trabajadores como el árbol de áreas se
        /// recortan a <paramref name="visibleAreaScopeIds"/> (área del usuario hacia abajo). El
        /// propio trabajador del usuario siempre se incluye en la lista de trabajadores.
        /// </summary>
        Task<GestionSalidaFilterDataDto> GetFilterData(bool seesAll, List<int> visibleAreaScopeIds, int? currentUserId);
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
            string pdfFilename,
            int numeroPlanilla);

        /// <summary>Consume el siguiente número de la secuencia <c>seq_planilla_numero</c>.</summary>
        Task<int> GetNextNumeroPlanillaAsync();

        /// <summary>IDs elegibles (Aprobadas + No rendidas) sin tocar BD. Pre-flight.</summary>
        Task<List<int>> GetEligibleIdsForRendicion(IEnumerable<int> ids);

        /// <summary>
        /// Del set dado, devuelve los IDs que NO pertenecen al trabajador del usuario indicado
        /// (worker → person → user). Se usa como guard cuando el propio trabajador rinde sus salidas
        /// desde el autoservicio: solo puede rendir lo suyo.
        /// </summary>
        Task<List<int>> GetIdsNotOwnedByUser(IEnumerable<int> ids, int userId);

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
