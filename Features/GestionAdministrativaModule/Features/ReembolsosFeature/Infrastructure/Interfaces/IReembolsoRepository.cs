using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Infrastructure.Interfaces
{
    public interface IReembolsoRepository
    {
        /// <summary>Planillas firmadas, confirmadas y pagadas de toda la organización, ya filtradas.</summary>
        Task<List<ReembolsoListItemDto>> GetAll(ReembolsoFiltersDto filters);

        /// <summary>
        /// Una planilla con el desglose de sus salidas y los tramos de cada una (con sus vouchers).
        /// Null si no está en la bandeja.
        /// </summary>
        Task<ReembolsoDetalleDto?> GetDetalle(int rendicionId);

        /// <summary>Opciones de los filtros (trabajadores, árbol de áreas y periodos) de la bandeja.</summary>
        Task<ReembolsoFilterDataDto> GetFilterData();

        /// <summary>
        /// Traduce una selección de planillas + salidas sueltas a ids de salida que estén en
        /// <paramref name="estadoId"/>. El recorte por estado es lo que impide que mandar un
        /// rendicion_id alcance salidas que todavía no llegaron a ese paso.
        /// </summary>
        Task<List<int>> ResolverSolicitudIds(
            IEnumerable<int> rendicionIds, IEnumerable<int> solicitudIds, int estadoId);

        /// <summary>
        /// Igual, para las acciones que aceptan la salida en más de un estado. Lo usa Observar, que
        /// devuelve tanto lo firmado como lo ya confirmado para pagar (RG-49).
        /// </summary>
        Task<List<int>> ResolverSolicitudIds(
            IEnumerable<int> rendicionIds, IEnumerable<int> solicitudIds, int[] estadoIds);

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
        /// Lo que necesitan los correos de esta pantalla, UNA fila por (planilla, trabajador): los
        /// dos dicen algo de "tu rendición", así que una planilla que agrupe a varias personas
        /// manda un correo a cada una con SUS números, no con el total del documento.
        /// </summary>
        Task<List<ReembolsoPlanillaCorreoDatos>> GetPlanillaCorreoInfo(IEnumerable<int> solicitudIds);

        /// <summary>
        /// Seguimiento de Tesorería (11.4): lo ya abonado, agrupado por colaborador. Mira solo lo
        /// Pagado — no es una bandeja de trabajo sino la consulta del histórico de pagos.
        /// </summary>
        Task<ReembolsoSeguimientoDto> GetSeguimiento(ReembolsoFiltersDto filters);
    }
}
