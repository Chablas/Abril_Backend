namespace Abril_Backend.Infrastructure.InternalServices
{
    public class StorageOptions
    {
        public string StorageProvider { get; set; }
        public AzureStorageOptions AzureStorage { get; set; }
        public LocalStorageOptions LocalStorage { get; set; }
    }

    public class AzureStorageOptions
    {
        public string ConnectionString { get; set; }
        public string ContainerName { get; set; }
    }

    public class LocalStorageOptions
    {
        public string ContainerName { get; set; }
    }
}
