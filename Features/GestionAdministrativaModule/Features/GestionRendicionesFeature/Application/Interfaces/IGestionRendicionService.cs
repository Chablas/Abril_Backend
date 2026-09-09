using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Interfaces
{
    /// <summary>
    /// "Gestión de Rendiciones": el revisor sobre las planillas de su alcance. Acá vive todo lo que
    /// va DESDE el Consolidado del S10 en adelante —adjuntarlo, decidir el reembolso y firmar la
    /// planilla—; Gestión de Salidas llega hasta rendir. El pago es de Tesorería y vive en
    /// Reembolsos.
    ///
    /// La visibilidad es exactamente la de Gestión de Salidas: mismas salidas, agrupadas por
    /// planilla.
    /// </summary>
    public interface IGestionRendicionService
    {
        Task<GestionRendicionListResultDto> GetAll(GestionRendicionFiltersDto filters);
        Task<GestionRendicionFilterDataDto> GetFilterData(GestionRendicionFiltersDto scope);
        Task<GestionRendicionDetalleDto> GetDetalle(int rendicionId, GestionRendicionFiltersDto scope);

        /// <summary>
        /// Qué correos saldrían si se toma una de las decisiones de la pantalla sobre la selección
        /// indicada, y a quién. Lo piden las confirmaciones —las de los botones masivos y las del
        /// modal de detalle— para nombrar las direcciones reales en vez de prometer "se le avisará
        /// por correo". Lista vacía = esa decisión hoy no manda ningún correo.
        /// </summary>
        Task<List<CorreoAvisoPreviewDto>> GetCorreoPreview(
            CorreoPreviewRequestDto request, GestionRendicionFiltersDto scope);

        /// <summary>
        /// Aprueba u observa la PRIMERA revisión de las planillas seleccionadas (RG-30). Aprobar
        /// habilita al trabajador a cargar el Consolidado del S10; observar le pide corregir las
        /// capturas y los montos y volver a generar la rendición con el mismo código.
        ///
        /// La decisión es por planilla y total (RG-19): no se aprueban tramos por separado.
        /// Avisa a los solicitantes por correo (best-effort).
        /// </summary>
        Task<ReembolsoBulkResultDto> DecidirPrimeraRevision(
            PrimeraRevisionAccionDto accion, bool aprobar, GestionRendicionFiltersDto scope, int reviewerUserId);

        /// <summary>
        /// Adjunta (o reemplaza) el PDF Consolidado del S10 de una planilla. El revisor lo sube en
        /// nombre del trabajador cuando este no puede; el archivo cubre la planilla entera.
        ///
        /// Solo con la primera revisión APROBADA (RG-35): lo valida el servicio compartido.
        /// </summary>
        /// <param name="montoTotal">
        /// Importe total del consolidado. Tiene que coincidir con el monto de la planilla completa
        /// o se rechaza con 400.
        /// </param>
        /// <param name="numeroGuia">Número de guía del S10 (texto, obligatorio).</param>
        Task<ConsolidadoS10Dto> UploadConsolidadoS10(
            int rendicionId, IFormFile file, decimal montoTotal, string numeroGuia, int userId);

        /// <summary>
        /// Aprueba o rechaza el reembolso de lo seleccionado. La selección puede venir por planilla
        /// (lo normal) o por salidas sueltas (desde el detalle); en los dos casos se recorta a lo
        /// que el usuario puede ver. Avisa al solicitante por correo (best-effort).
        /// </summary>
        /// <remarks>
        /// Aprobar ES firmar: estampa la firma del revisor en todas las hojas de los documentos de
        /// la planilla (su PDF y el Consolidado del S10) y deja las salidas en "Firmado", que es lo
        /// que Tesorería ve como pagable. Lanza 409 si el revisor todavía no registró su firma: la
        /// pantalla usa ese código para abrir el modal donde la dibuja y reintentar.
        /// </remarks>
        Task<ReembolsoBulkResultDto> DecidirReembolso(
            ReembolsoAccionDto accion, bool aprobar, GestionRendicionFiltersDto scope, int reviewerUserId);
    }
}
