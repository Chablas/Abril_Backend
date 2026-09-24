using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Infrastructure.Interfaces
{
    public interface IReembolsoRepository
    {
        /// <summary>
        /// Consolidados del S10 firmados, confirmados y pagados de toda la organización, ya
        /// filtrados. La unidad es el DOCUMENTO: uno puede cubrir varias planillas.
        /// </summary>
        Task<List<ReembolsoListItemDto>> GetAll(ReembolsoFiltersDto filters);

        /// <summary>
        /// Un consolidado con las planillas que cubre y el desglose de sus salidas. Null si no está
        /// en la bandeja.
        /// </summary>
        Task<ReembolsoDetalleDto?> GetDetalle(int consolidadoId);

        /// <summary>
        /// El detalle de UNA salida de la bandeja —trayectos, vouchers, montos y adjuntos—, en
        /// consulta: es lo que Tesorería revisa antes de confirmar (RF-TES-05). Null si la salida no
        /// está en la bandeja.
        /// </summary>
        Task<SolicitudSalidaDetalleDto?> GetSalidaDetalle(int solicitudId);

        /// <summary>Opciones de los filtros (trabajadores, árbol de áreas y periodos) de la bandeja.</summary>
        Task<ReembolsoFilterDataDto> GetFilterData();

        /// <summary>
        /// Traduce una selección de consolidados a ids de salida que estén en
        /// <paramref name="estadoId"/>. El recorte por estado es lo que impide que mandar un
        /// consolidado alcance salidas que todavía no llegaron a ese paso.
        /// </summary>
        Task<List<int>> ResolverSolicitudIds(IEnumerable<int> consolidadoIds, int estadoId);

        /// <summary>
        /// Igual, para las acciones que aceptan la salida en más de un estado. Lo usa Observar, que
        /// devuelve tanto lo firmado como lo ya confirmado para pagar (RG-49).
        /// </summary>
        Task<List<int>> ResolverSolicitudIds(IEnumerable<int> consolidadoIds, int[] estadoIds);

        /// <summary>
        /// Marca como "Proceder con el reembolso" las salidas indicadas que estén Firmadas (RG-26).
        /// Las demás se ignoran. Devuelve los ids que efectivamente cambiaron.
        /// </summary>
        Task<List<int>> ConfirmarRevision(IEnumerable<int> ids, int tesoreroUserId);

        /// <summary>
        /// Devuelve las salidas indicadas con el motivo de Tesorería (RG-49): las deja Observadas
        /// —el mismo estado que usa la jefatura, con el origen marcado— y borra la confirmación de
        /// la revisión. Solo aplica sobre lo firmado o listo para pagar; lo pagado no vuelve.
        /// </summary>
        Task<List<int>> Observar(IEnumerable<int> ids, string observacion, int tesoreroUserId);

        /// <summary>
        /// Marca como Pagadas las salidas indicadas que estén en "Proceder con el reembolso".
        /// Las demás se ignoran. Devuelve los ids que efectivamente cambiaron.
        /// </summary>
        Task<List<int>> MarcarPagadas(IEnumerable<int> ids, int tesoreroUserId);

        /// <summary>
        /// Lo que necesita el aviso de pago, UNA fila por (planilla, trabajador): dice algo de "tu
        /// rendición", así que una planilla que agrupe a varias personas manda un correo a cada una
        /// con SUS números, no con el total del documento.
        /// </summary>
        Task<List<ReembolsoPlanillaCorreoDatos>> GetPlanillaCorreoInfo(IEnumerable<int> solicitudIds);

        /// <summary>
        /// Lo que necesita el aviso de que Tesorería devolvió el reembolso: UNO por consolidado,
        /// dirigido a su consolidador (ver <c>ConsolidadoCorreoLoader</c>).
        /// </summary>
        Task<List<ConsolidadoCorreoDatos>> GetConsolidadoCorreoDatos(IReadOnlyCollection<int> solicitudIds);

        /// <summary>
        /// Destinatario principal de los avisos a Tesorería: quien tiene el rol TESORERO (ver
        /// <c>CorreosTesoreriaLoader</c>). Lo usa el preview de la confirmación de la revisión.
        /// </summary>
        Task<List<string>> GetCorreosTesoreria();

        /// <summary>
        /// Lo que necesita el aviso a Tesorería de que confirmó la revisión: UNO por consolidado de
        /// las salidas indicadas, con lo que ese consolidado tiene hoy por pagar, y el rol TESORERO
        /// como destinatario principal.
        /// </summary>
        Task<ReembolsoPorPagarCorreoInfoDto> GetPorPagarCorreoInfo(IReadOnlyCollection<int> solicitudIds);

        /// <summary>
        /// Seguimiento de Tesorería (11.4): lo ya abonado, agrupado por colaborador. Mira solo lo
        /// Pagado — no es una bandeja de trabajo sino la consulta del histórico de pagos.
        /// </summary>
        Task<ReembolsoSeguimientoDto> GetSeguimiento(ReembolsoFiltersDto filters);
    }
}
