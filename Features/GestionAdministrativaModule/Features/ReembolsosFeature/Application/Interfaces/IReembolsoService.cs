using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Interfaces
{
    /// <summary>
    /// "Reembolsos": la bandeja de Tesorería. Es el último paso del ciclo — lo que la jefatura ya
    /// firmó se revisa y se paga acá. Antes vivía como un modo dentro de Gestión de Salidas; se
    /// separó porque esa pantalla ya no llega hasta el reembolso.
    ///
    /// El flujo son DOS pasos y no uno (RG-26): primero Tesorería confirma que la documentación
    /// está completa —planilla, Consolidado del S10, firma de la jefatura y tramos con sus
    /// vouchers— y recién entonces la planilla queda habilitada para el pago.
    ///
    /// El acceso lo decide el rol TESORERO y nada más (lo exige el controller contra el token).
    /// Antes pedía además un puesto de categoría Tesorero y se quitó: la categoría del puesto ya
    /// no niega la entrada a la pantalla.
    /// </summary>
    public interface IReembolsoService
    {
        Task<ReembolsoListResultDto> GetAll(ReembolsoFiltersDto filters);
        Task<ReembolsoFilterDataDto> GetFilterData();
        Task<ReembolsoDetalleDto> GetDetalle(int rendicionId);

        /// <summary>
        /// Confirma la revisión documental de lo seleccionado: sus salidas firmadas pasan a
        /// "Proceder con el reembolso", que es el único estado desde el que se puede pagar.
        /// </summary>
        Task<ReembolsoBulkResultDto> ConfirmarRevision(ReembolsoSeleccionDto dto, int tesoreroUserId);

        /// <summary>
        /// Marca como pagadas las salidas ya confirmadas de lo seleccionado (planillas completas o
        /// salidas sueltas desde el detalle) y avisa a cada colaborador.
        /// </summary>
        Task<ReembolsoBulkResultDto> MarcarPagadas(ReembolsoSeleccionDto dto, int tesoreroUserId);

        /// <summary>
        /// A quién le llegaría el aviso de pago de lo seleccionado. Lo piden las confirmaciones
        /// (el botón masivo y el del modal de detalle) para nombrar las direcciones reales en vez
        /// de prometer un aviso genérico. Lista vacía = hoy no sale ningún correo.
        ///
        /// Solo existe para el pago: confirmar la revisión es un paso interno y no avisa a nadie.
        /// </summary>
        Task<List<CorreoAvisoPreviewDto>> GetCorreoPreviewPago(ReembolsoSeleccionDto dto);

        /// <summary>Seguimiento de pagos por colaborador (11.4 del requerimiento).</summary>
        Task<ReembolsoSeguimientoDto> GetSeguimiento(ReembolsoFiltersDto filters);
    }
}
