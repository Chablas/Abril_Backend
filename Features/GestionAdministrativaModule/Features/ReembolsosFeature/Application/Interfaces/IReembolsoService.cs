using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Interfaces
{
    /// <summary>
    /// "Reembolsos": la bandeja de Tesorería. Es el último paso del ciclo — lo que la jefatura ya
    /// firmó se paga acá. Antes vivía como un modo dentro de Gestión de Salidas; se separó porque
    /// esa pantalla ya no llega hasta el reembolso.
    ///
    /// El acceso son DOS condiciones: el rol TESORERO (del token) y que el puesto del trabajador
    /// sea de categoría Tesorero (de la base). Con una sola no alcanza.
    /// </summary>
    public interface IReembolsoService
    {
        /// <summary>Lanza 403 si el usuario no cumple las dos condiciones de Tesorería.</summary>
        Task EnsureTesoreroAsync(int userId);

        Task<ReembolsoListResultDto> GetAll(ReembolsoFiltersDto filters, int userId);
        Task<ReembolsoFilterDataDto> GetFilterData(int userId);
        Task<ReembolsoDetalleDto> GetDetalle(int rendicionId, int userId);

        /// <summary>
        /// Marca como pagadas las salidas firmadas de lo seleccionado (planillas completas o
        /// salidas sueltas desde el detalle).
        /// </summary>
        Task<ReembolsoBulkResultDto> MarcarPagadas(PagarDto dto, int tesoreroUserId);
    }
}
