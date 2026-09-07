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
        Task<ConsolidadoS10Dto> UploadConsolidadoS10(int rendicionId, IFormFile file, int userId);

        /// <summary>
        /// Aprueba o rechaza el reembolso de lo seleccionado. La selección puede venir por planilla
        /// (lo normal) o por salidas sueltas (desde el detalle); en los dos casos se recorta a lo
        /// que el usuario puede ver. Avisa al solicitante por correo (best-effort).
        /// </summary>
        Task<ReembolsoBulkResultDto> DecidirReembolso(
            ReembolsoAccionDto accion, bool aprobar, GestionRendicionFiltersDto scope, int reviewerUserId);

        /// <summary>
        /// Estampa la firma del usuario en las planillas de lo seleccionado y pasa a Firmado sus
        /// salidas con reembolso aprobado. Lanza 409 si el usuario no registró su firma todavía:
        /// la pantalla usa ese código para abrir el modal donde la dibuja y reintentar.
        /// </summary>
        Task<ReembolsoBulkResultDto> Firmar(
            ReembolsoAccionDto accion, GestionRendicionFiltersDto scope, int userId);
    }
}
