using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Adjudicaciones.Application.Interfaces;
using Abril_Backend.Features.Adjudicaciones.Infrastructure.Interfaces;
using Abril_Backend.Features.Adjudicaciones.Application.Dtos;

namespace Abril_Backend.Features.Adjudicaciones.Application.Services
{
    public class ProjectSubContractorService : IProjectSubContractorService
    {
        private readonly IProjectSubContractorRepository _projectSubContractorRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;
        private readonly IProjectRepository _projectRepository;

        public ProjectSubContractorService(
            IProjectSubContractorRepository projectSubContractorRepository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver,
            IProjectRepository projectRepository
            )
        {
            _projectSubContractorRepository = projectSubContractorRepository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
            _projectRepository = projectRepository;
        }

        /*public async Task GetPaged()
        {
            var respuesta = await _projectSubContractorRepository.GetPaged(page);
        }*/

        public async Task Create(ProjectSubContractorCreateDTO dto, int userId)
        {
            var container = _containerResolver.GetProjectSubContractorContainerName();

            var quotationUrls = await UploadFiles(dto.QuotationFiles, container);
            var comparativeUrls = await UploadFiles(dto.ComparativeFiles, container);

            await _projectSubContractorRepository.Create(dto, quotationUrls, comparativeUrls, userId);
        }

        public async Task<ProjectSubContractorFormDataDTO> GetFormData()
        {
            var projectsTask = _projectRepository.GetAllFactory();
            var contractsTask = _projectSubContractorRepository.GetContractsFactory();
            var contractTypesTask = _projectSubContractorRepository.GetContractTypeFactory();
            var contractOriginsTask = _projectSubContractorRepository.GetContractOriginFactory();
            var paymentMethodsTask = _projectSubContractorRepository.GetPaymentMethodFactory();
            var currenciesTask = _projectSubContractorRepository.GetCurrencyFactory();

            var workItemsTask = _projectSubContractorRepository.GetWorkItemFactory();
            var companiesTask = _projectSubContractorRepository.GetCompanyFactory();

            await Task.WhenAll(projectsTask, contractsTask, contractTypesTask, contractOriginsTask, paymentMethodsTask, currenciesTask, workItemsTask, companiesTask);

            return new ProjectSubContractorFormDataDTO
            {
                Projects = await projectsTask,
                Contracts = await contractsTask,
                ContractTypes = await contractTypesTask,
                ContractOrigins = await contractOriginsTask,
                PaymentMethods = await paymentMethodsTask,
                Currencies = await currenciesTask,
                WorkItems = await workItemsTask,
                Companies = await companiesTask
            };
        }

        private async Task<List<string>> UploadFiles(List<IFormFile>? files, string container)
        {
            if (files == null || !files.Any())
                return new List<string>();

            var filesToUpload = new List<(Stream Stream, string FileName)>();
            var streams = new List<Stream>();

            foreach (var file in files)
            {
                if (file.Length == 0)
                    throw new AbrilException("Se detectó un archivo vacío.");

                var extension = Path.GetExtension(file.FileName);
                var fileName = $"{Guid.NewGuid()}{extension}";
                var stream = file.OpenReadStream();

                streams.Add(stream);
                filesToUpload.Add((stream, fileName));
            }

            try
            {
                return await _fileStorageService.UploadFilesAsync(filesToUpload, container);
            }
            finally
            {
                foreach (var stream in streams)
                    stream.Dispose();
            }
        }
    }
}