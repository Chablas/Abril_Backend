using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Infrastructure.Interfaces
{
    public interface IGestionRendicionRepository
    {
        /// <summary>Planillas visibles para el usuario, ya filtradas.</summary>
        Task<List<GestionRendicionListItemDto>> GetAll(GestionRendicionFiltersDto filters);

        /// <summary>Una planilla con el desglose de sus salidas visibles. Null si no ve ninguna.</summary>
        Task<GestionRendicionDetalleDto?> GetDetalle(int rendicionId, GestionRendicionFiltersDto scope);

        /// <summary>Opciones de los filtros (trabajadores, árbol de áreas y periodos) del alcance.</summary>
        Task<GestionRendicionFilterDataDto> GetFilterData(GestionRendicionFiltersDto scope);

        /// <summary>
        /// Traduce una selección de planillas + salidas sueltas a los ids de salida sobre los que
        /// se va a actuar, recortados a lo que el usuario puede ver. Sin esto, mandar un
        /// rendicion_id permitiría tocar salidas de áreas ajenas que cuelgan de la misma planilla.
        /// </summary>
        Task<List<int>> ResolverSolicitudIds(
            IEnumerable<int> rendicionIds, IEnumerable<int> solicitudIds, GestionRendicionFiltersDto scope);

        // ── Reembolso (movido desde Gestión de Salidas: es un paso posterior al consolidado) ──

        /// <summary>
        /// Aprueba o rechaza el reembolso de las salidas indicadas. Solo pasan las que están
        /// Rendidas, tienen Consolidado del S10 y su reembolso sigue Pendiente o Rechazado — el
        /// resto se ignora en silencio (la selección de la pantalla puede traer de todo).
        ///
        /// Un usuario no decide el reembolso de sus propias salidas, misma regla que la aprobación
        /// de la salida: la excepción son los Gerentes.
        /// </summary>
        /// <param name="aprobar">true = Aprobado; false = Rechazado (exige observación).</param>
        /// <returns>Ids de las salidas que efectivamente cambiaron de estado.</returns>
        Task<List<int>> DecidirReembolso(IEnumerable<int> ids, bool aprobar, string? observacion, int reviewerUserId);

        /// <summary>
        /// Las salidas listas para firmar de la selección: reembolso Aprobado, ya rendidas y con
        /// planilla. Devuelve, por planilla, el id de la rendición y el PDF que hay que estampar.
        /// </summary>
        Task<List<RendicionPorFirmarDto>> GetRendicionesPorFirmar(IEnumerable<int> ids);

        /// <summary>
        /// Guarda la copia firmada de una planilla y pasa a Firmado las salidas indicadas de esa
        /// planilla. Si la planilla ya estaba firmada se conserva el archivo anterior y solo se
        /// mueven los estados (dos jefes pueden firmar salidas distintas de la misma planilla).
        /// </summary>
        Task MarcarFirmadas(int rendicionId, IEnumerable<int> solicitudIds, int userId,
                            string? pdfUrl, string? pdfItemId, string? pdfFilename);

        /// <summary>Carpeta de SharePoint donde se guardan las planillas (y sus copias firmadas).</summary>
        Task<string?> GetRendicionFolderUrl();

        /// <summary>
        /// Datos de una salida para armar los correos del reembolso (trabajador, área, planilla,
        /// monto rendido y a quién avisar). Null si la salida no existe.
        /// </summary>
        Task<ReembolsoCorreoInfoDto?> GetReembolsoCorreoInfo(int solicitudId);
    }
}
