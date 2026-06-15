using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Interfaces;
using Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Application.Helpers;
using Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Application.Interfaces;
using Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Options;
using ClosedXML.Excel;

namespace Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Application.Services
{
    public class CostosCronogramaService : ICostosCronogramaService
    {
        private readonly ICostosCronogramaRepository _repository;
        private readonly IProjectSubContractorRepository _adjudicacionRepository;
        private readonly IGraphSharePointService _sharePointService;
        private readonly SharePointSiteRef _site;

        private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public CostosCronogramaService(
            ICostosCronogramaRepository repository,
            IProjectSubContractorRepository adjudicacionRepository,
            IGraphSharePointService sharePointService,
            IConfiguration configuration)
        {
            _repository = repository;
            _adjudicacionRepository = adjudicacionRepository;
            _sharePointService = sharePointService;
            _site = SharePointSiteRef.FromConfig(configuration, "CostosYPresupuestos");
        }

        public async Task<CronogramaFormDataDto> GetFormData(int projectSubContractorId)
        {
            return new CronogramaFormDataDto
            {
                Actividades = await _repository.GetActividadesAsync(),
                Nodos = await _repository.GetNodosAsync(projectSubContractorId),
            };
        }

        public async Task<CronogramaActividadDto> CreateActividad(CronogramaActividadCreateDto dto, int userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new AbrilException("El nombre de la actividad es obligatorio.");
            return await _repository.CreateActividadAsync(dto.Nombre, userId);
        }

        public async Task Save(int projectSubContractorId, CronogramaSaveDto dto, int userId)
        {
            if (dto.Nodos == null || dto.Nodos.Count == 0)
                throw new AbrilException("El cronograma debe tener al menos una actividad.");

            // 1) Persistir el árbol del cronograma.
            await _repository.SaveAsync(projectSubContractorId, dto.Nodos, userId);

            // 2) Generar el Excel del cronograma y registrarlo como documento "Cronograma".
            await GenerateExcelAsync(projectSubContractorId, userId);
        }

        private async Task GenerateExcelAsync(int projectSubContractorId, int userId)
        {
            var header = await _adjudicacionRepository.GetSummarySheetDataAsync(projectSubContractorId)
                ?? throw new AbrilException("No se encontraron los datos de la adjudicación.");
            var nodos = await _repository.GetNodosDetalleAsync(projectSubContractorId);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("CRONOGRAMA");
            CronogramaExcelBuilder.Build(ws, header, nodos);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;

            var pathData = await _adjudicacionRepository.GetPathDataAsync(projectSubContractorId);
            var folderPath = BuildCronogramaFolderPath(pathData);

            var abrev = !string.IsNullOrWhiteSpace(header.Abbreviation)
                ? header.Abbreviation
                : (header.ProjectDescription.Length >= 3
                    ? header.ProjectDescription[..3].ToUpperInvariant()
                    : header.ProjectDescription.ToUpperInvariant());
            var fileName = $"CRONOGRAMA N°{header.ContractNumber?.ToString("D3") ?? "000"}{abrev} – {DateTime.UtcNow.Year}.xlsx";

            // autoRenameOnLock: si ya existe un cronograma con el mismo nombre abierto/en uso
            // (HTTP 423), se sube con un nombre alterno ("… (2).xlsx") en lugar de fallar.
            var spResult = await _sharePointService.UploadToSharePointLibraryAsync(
                site:             _site,
                libraryName:      "Adjudicaciones",
                folderPath:       folderPath,
                fileName:         fileName,
                fileStream:       ms,
                contentType:      XlsxMime,
                autoRenameOnLock: true)
                ?? throw new AbrilException("No se pudo subir el cronograma a SharePoint.");

            var fileUrl = spResult.WebUrl ?? throw new AbrilException("No se pudo obtener la URL del cronograma generado.");
            var finalFileName = spResult.FileName ?? fileName; // puede diferir si hubo renombrado por conflicto

            // Registrar como documento "Cronograma" (aparece el enlace en el paso 3)
            // y guardar también la referencia en costos_cronograma.
            await _adjudicacionRepository.SaveDocumentAsync(
                projectSubContractorId, AdjudicacionDocumentType.Schedule, fileUrl, finalFileName, userId, spResult.ItemId);
            await _repository.SaveFileInfoAsync(projectSubContractorId, fileUrl, finalFileName, userId);
        }

        /// <summary>Ruta {proyecto}/{empresa}/{partida}/Cronograma en la biblioteca de Adjudicaciones.</summary>
        private static string BuildCronogramaFolderPath(AdjudicacionPathDataDto data)
        {
            var project  = Sanitize(data.ProjectDescription);
            var company  = Sanitize($"{data.ContributorRuc} - {data.ContributorName}");
            var workItem = Sanitize($"{data.ProjectSubContractorId} - {data.WorkItemDescription}");
            return $"{project}/{company}/{workItem}/Cronograma";
        }

        private static string Sanitize(string name)
        {
            var invalid = new HashSet<char> { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '#', '%' };
            var result = string.Concat(name.Select(c => invalid.Contains(c) ? '-' : c)).Trim();
            return result.Length > 60 ? result[..60].TrimEnd() : result;
        }
    }
}
