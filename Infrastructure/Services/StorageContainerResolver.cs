using Microsoft.Extensions.Options;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Infrastructure.Services
{
    public class StorageContainerResolver : IStorageContainerResolver
    {
        private readonly StorageOptions _options;

        public StorageContainerResolver(IOptions<StorageOptions> options)
        {
            _options = options.Value;
        }

        public string GetLessonsContainerName()
        {
            return _options.StorageProvider.ToLower() switch
            {
                "azure" => _options.AzureStorage.LessonsContainer,
                "local" => _options.LocalStorage.LessonsContainer,
                _ => throw new InvalidOperationException("Proveedor de storage no válido")
            };
        }

        public string GetIvtContainerName()
        {
            return _options.StorageProvider.ToLower() switch
            {
                "azure" => _options.AzureStorage.IvtContainer,
                "local" => _options.LocalStorage.IvtContainer,
                _ => throw new InvalidOperationException("Proveedor de storage no válido")
            };
        }

        public string GetConstructionSiteLogbookContainerName()
        {
            return _options.StorageProvider.ToLower() switch
            {
                "azure" => _options.AzureStorage.ConstructionSiteLogbookContainer,
                "local" => _options.LocalStorage.ConstructionSiteLogbookContainer,
                _ => throw new InvalidOperationException("Proveedor de storage no válido")
            };
        }

        public string GetResidentIncidentContainerName()
        {
            return _options.StorageProvider.ToLower() switch
            {
                "azure" => _options.AzureStorage.ResidentReportIncidence,
                "local" => _options.LocalStorage.ResidentReportIncidence,
                _ => throw new InvalidOperationException("Proveedor de storage no válido")
            };
        }
    }
}