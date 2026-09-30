using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Shared.Services.SharePoint.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Services
{
    public class ProjectContractStorage : IProjectContractStorage
    {
        private readonly IGraphSharePointService _graph;
        private readonly IProjectContractFolderRepository _folderRepository;
        private readonly IProjectContractRepository _contractRepository;

        // Reconoce carpetas "CONTRATO N° 1", "CONTRATO N°2", "CONTRATO Nº 3", etc. — mismo
        // criterio que _adjudicacionFolderRegex en AdjudicacionOneDriveStorage.
        private static readonly Regex _contratoFolderRegex =
            new(@"^\s*CONTRATO\s*N[°º]?\s*(\d+)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public ProjectContractStorage(
            IGraphSharePointService graph,
            IProjectContractFolderRepository folderRepository,
            IProjectContractRepository contractRepository)
        {
            _graph = graph;
            _folderRepository = folderRepository;
            _contractRepository = contractRepository;
        }

        public async Task<SharePointUploadResultDto> UploadContractAsync(
            ProjectContractGenerationDataDTO data, string fileName, Stream content, string contentType)
        {
            if (string.IsNullOrWhiteSpace(data.WorkSpecialtyDescription))
                throw new AbrilException(
                    "El contrato no tiene una especialidad asignada. Asígnela antes de guardar documentos.", 400);

            var raiz = await _folderRepository.GetResolvedFolderAsync(data.ProjectId)
                ?? throw new AbrilException(
                    "Este proyecto no tiene configurada una carpeta de Contratos. " +
                    "Configúrela en Configuración → Carpeta de Contratos y vuelva a intentarlo.", 422);

            var especialidadId = await FindOrCreateExactAsync(
                raiz.DriveId, raiz.FolderId, Sanitize(data.WorkSpecialtyDescription));

            var contratistaName = Sanitize($"{data.ContratistaRuc} - {data.ContratistaRazonSocial}");
            var contratistaId = await FindOrCreateContratistaAsync(
                raiz.DriveId, especialidadId, data.ContratistaRuc, contratistaName);

            var contratoFolderId = await EnsureContratoFolderAsync(raiz.DriveId, contratistaId, data);

            var subfolderId = await FindOrCreateExactAsync(raiz.DriveId, contratoFolderId, "Contrato");

            var result = await _graph.UploadToOneDriveFolderAsync(
                raiz.DriveId, subfolderId, fileName, content, contentType, autoRenameOnLock: true)
                ?? throw new AbrilException("No se pudo subir el archivo a SharePoint.");

            if (string.IsNullOrEmpty(result.WebUrl))
                throw new AbrilException("No se pudo obtener la URL del archivo subido a SharePoint.");

            return result;
        }

        /// <summary>Devuelve (o crea) la carpeta "CONTRATO N° X" del contrato, autoincremental
        /// dentro de la carpeta del contratista — mismo mecanismo que AdjudicacionFolderName.</summary>
        private async Task<string> EnsureContratoFolderAsync(
            string driveId, string contratistaId, ProjectContractGenerationDataDTO data)
        {
            if (!string.IsNullOrWhiteSpace(data.FolderName))
                return await FindOrCreateExactAsync(driveId, contratistaId, data.FolderName!);

            var children = await _graph.GetChildFoldersByItemIdAsync(driveId, contratistaId);
            var maxNumber = 0;
            foreach (var child in children)
            {
                var m = _contratoFolderRegex.Match(child.Name ?? "");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n > maxNumber)
                    maxNumber = n;
            }

            var folderName = $"CONTRATO N° {maxNumber + 1}";
            await _contractRepository.SetFolderNameAsync(data.ProjectContractId, folderName);
            data.FolderName = folderName;

            return await _graph.EnsureChildFolderAsync(driveId, contratistaId, folderName);
        }

        private async Task<string> FindOrCreateContratistaAsync(
            string driveId, string parentId, string ruc, string fullName)
        {
            var children = await _graph.GetChildFoldersByItemIdAsync(driveId, parentId);
            var prefix = ruc + " - ";
            var match = children.FirstOrDefault(f =>
                (f.Name ?? "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.Name, fullName, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match.ItemId;

            return await _graph.EnsureChildFolderAsync(driveId, parentId, fullName);
        }

        private async Task<string> FindOrCreateExactAsync(string driveId, string parentId, string name)
        {
            var children = await _graph.GetChildFoldersByItemIdAsync(driveId, parentId);
            var match = children.FirstOrDefault(f =>
                string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match.ItemId;

            return await _graph.EnsureChildFolderAsync(driveId, parentId, name);
        }

        /// <summary>Elimina caracteres que OneDrive/SharePoint no aceptan en nombres de carpeta —
        /// mismo criterio que Sanitize en AdjudicacionOneDriveStorage.</summary>
        private static string Sanitize(string name)
        {
            var invalid = new HashSet<char> { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '#', '%' };
            var result = string.Concat(name.Select(c => invalid.Contains(c) ? '-' : c)).Trim();
            if (result.Length > 60) result = result[..60];
            return result.TrimEnd(' ', '.');
        }
    }
}
