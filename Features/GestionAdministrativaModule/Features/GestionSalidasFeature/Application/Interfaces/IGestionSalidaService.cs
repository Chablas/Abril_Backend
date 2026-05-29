using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces
{
    public interface IGestionSalidaService
    {
        Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters);
        Task<GestionSalidaFilterDataDto> GetFilterData();
        Task<byte[]> GetExcel(GestionSalidaFiltersDto filters);
        Task Aprobar(int id, int reviewerUserId);
        Task Rechazar(int id, int reviewerUserId);
        /// <summary>
        /// Marca solicitudes elegibles como Rendidas y genera la planilla de gasto por movilidad (PDF).
        /// Devuelve los bytes del PDF + cuántas se procesaron.
        /// </summary>
        Task<(byte[] Pdf, int Count)> RendirYGenerarPlanilla(IEnumerable<int> ids, int userId);

        /// <summary>Detalle de una solicitud para el modal — devuelve null si no existe.</summary>
        Task<GestionSalidaDetalleDto?> GetDetalle(int id);

        /// <summary>Registra (o limpia) la hora real de salida. Para uso del rol USUARIO DE RECEPCIÓN.</summary>
        Task SetHoraSalidaReal(int id, TimeOnly? hora, int registradaPorUserId);
    }
}
