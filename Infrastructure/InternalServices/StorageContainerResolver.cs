using Microsoft.Extensions.Options;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Infrastructure.InternalServices
{
    public class StorageContainerResolver : IStorageContainerResolver
    {
        private readonly StorageOptions _options;

        public StorageContainerResolver(IOptions<StorageOptions> options)
        {
            _options = options.Value;
        }

        public string GetContainerName()
        {
            return _options.StorageProvider switch
            {
                "Azure" => _options.AzureStorage.ContainerName,
                "Local" => _options.LocalStorage.ContainerName,
                _ => throw new InvalidOperationException("Proveedor de storage no válido")
            };
        }
    }
}