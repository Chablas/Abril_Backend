using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces
{
    public interface IGestionSalidaService
    {
        Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters);

        /// <summary>Tabla ordenada y paginada (la vista principal de gestión de salidas).</summary>
        Task<PagedResult<GestionSalidaListItemDto>> GetPaged(GestionSalidaFiltersDto filters);
        /// <summary>
        /// Datos de los filtros. El árbol de áreas se recorta al alcance de visibilidad del
        /// usuario: quien ve todo (GTH / recepción) recibe el árbol completo; un gerente recibe
        /// su gerencia + descendientes; un jefe recibe su área + subáreas. Así el desplegable en
        /// cascada arranca en el nodo tope que cada usuario controla, no siempre en la gerencia.
        /// </summary>
        Task<GestionSalidaFilterDataDto> GetFilterData(int? currentUserId, bool seesAllOverride);
        Task<byte[]> GetExcel(GestionSalidaFiltersDto filters);
        Task Aprobar(int id, int reviewerUserId);
        Task Rechazar(int id, int reviewerUserId);
        /// <summary>
        /// Marca solicitudes elegibles como Rendidas y genera la planilla de gasto por movilidad (PDF).
        /// Devuelve los bytes del PDF + cuántas se procesaron.
        /// </summary>
        /// <param name="ownerUserId">
        /// Si se indica, actúa como guard: todas las solicitudes deben pertenecer al trabajador de ese
        /// usuario (rendición desde el autoservicio del trabajador). Null = sin restricción (Gestión de Salidas).
        /// </param>
        Task<(byte[] Pdf, int Count)> RendirYGenerarPlanilla(IEnumerable<int> ids, int userId, int? ownerUserId = null);

        /// <summary>Detalle de una solicitud para el modal — devuelve null si no existe.</summary>
        Task<GestionSalidaDetalleDto?> GetDetalle(int id);

        /// <summary>Registra (o limpia) la hora real de salida. Para uso del rol USUARIO DE RECEPCIÓN.</summary>
        Task SetHoraSalidaReal(int id, TimeOnly? hora, int registradaPorUserId);
    }
}
