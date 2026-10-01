using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Interfaces
{
    /// <summary>Guarda los documentos de un contrato en la carpeta de SharePoint configurada para
    /// su proyecto (Configuración → Carpeta de Contratos), siguiendo la estructura:
    /// {CarpetaConfigurada}/{Especialidad}/{RUC - Razón social}/{CONTRATO N° X}/{Subcarpeta}.
    /// Simplificado respecto a IAdjudicacionOneDriveStorage: sin el split 04_OBRAS/07_OT (no tiene
    /// equivalente claro acá) ni el nivel de "partida" (no aplica a contratos de diseño).</summary>
    public interface IProjectContractStorage
    {
        Task<SharePointUploadResultDto> UploadContractAsync(
            ProjectContractGenerationDataDTO data, string fileName, Stream content, string contentType);

        /// <summary>Paso 7: sube el contrato firmado escaneado a la subcarpeta "Escaneados" de la
        /// misma carpeta del contrato (mismo árbol que UploadContractAsync).</summary>
        Task<SharePointUploadResultDto> UploadScannedDocAsync(
            ProjectContractGenerationDataDTO data, string fileName, Stream content, string contentType);
    }
}
