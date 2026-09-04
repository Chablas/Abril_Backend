using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Infrastructure.Interfaces
{
    public interface IReembolsoRepository
    {
        /// <summary>Planillas firmadas y pagadas de toda la organización, ya filtradas.</summary>
        Task<List<ReembolsoListItemDto>> GetAll(ReembolsoFiltersDto filters);

        /// <summary>Una planilla con el desglose de sus salidas. Null si no está en la bandeja.</summary>
        Task<ReembolsoDetalleDto?> GetDetalle(int rendicionId);

        /// <summary>Opciones de los filtros (trabajadores, árbol de áreas y periodos) de la bandeja.</summary>
        Task<ReembolsoFilterDataDto> GetFilterData();

        /// <summary>
        /// Traduce una selección de planillas + salidas sueltas a ids de salida, recortados a la
        /// bandeja de Tesorería: mandar un rendicion_id no puede alcanzar salidas que todavía no
        /// están firmadas.
        /// </summary>
        Task<List<int>> ResolverSolicitudIds(IEnumerable<int> rendicionIds, IEnumerable<int> solicitudIds);

        /// <summary>
        /// Marca como Pagadas las salidas indicadas que estén Firmadas. Las demás se ignoran.
        /// Devuelve los ids que efectivamente cambiaron.
        /// </summary>
        Task<List<int>> MarcarPagadas(IEnumerable<int> ids, int tesoreroUserId);
    }
}
