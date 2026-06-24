using Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Dtos;

namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Interfaces
{
    /// <summary>
    /// Guarda/lee los documentos de una adjudicación en la carpeta de OneDrive del proyecto
    /// (Configuración → Carpeta de adjudicaciones), siguiendo la estructura:
    /// {Proyecto}/Contratos/{Especialidad}/{Partida}/{RUC - Razón social}/{Id - Partida}/{Subcarpeta}.
    /// Las carpetas de especialidad/partida/contratista/adjudicación/subcarpeta se crean si no existen.
    /// </summary>
    public interface IAdjudicacionOneDriveStorage
    {
        /// <summary>
        /// Asegura la cadena de carpetas para la adjudicación y sube el archivo en la subcarpeta
        /// correspondiente al tipo de documento. Devuelve WebUrl + ItemId (+ nombre final usado).
        /// </summary>
        Task<SharePointUploadResultDto> UploadAsync(
            int projectSubContractorId,
            AdjudicacionDocumentType documentType,
            string fileName,
            Stream content,
            string contentType,
            bool autoRenameOnLock = false);

        /// <summary>Igual que <see cref="UploadAsync(int, AdjudicacionDocumentType, string, Stream, string, bool)"/>
        /// pero recibiendo el pathData ya cargado (evita una consulta extra a BD).</summary>
        Task<SharePointUploadResultDto> UploadAsync(
            AdjudicacionPathDataDto pathData,
            AdjudicacionDocumentType documentType,
            string fileName,
            Stream content,
            string contentType,
            bool autoRenameOnLock = false);

        /// <summary>Descarga un documento de la adjudicación a partir de su webUrl de OneDrive.</summary>
        Task<byte[]> DownloadByWebUrlAsync(string webUrl);

        /// <summary>Descarga múltiples documentos de la adjudicación como PDF (driveId del proyecto).</summary>
        Task<Dictionary<string, byte[]>> DownloadMultipleAsPdfAsync(
            int projectSubContractorId,
            IReadOnlyList<(string ItemId, bool AlreadyPdf)> items);
    }
}
