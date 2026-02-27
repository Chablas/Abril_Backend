using Azure.Storage.Blobs;

namespace Abril_Backend.Infrastructure.InternalServices
{
    public class AzureBlobService
    {
        private readonly BlobContainerClient _container;

        public AzureBlobService(IConfiguration config)
        {
            var connectionString = config["AzureStorage:ConnectionString"];
            var containerName = config["AzureStorage:ContainerName"];

            var serviceClient = new BlobServiceClient(connectionString);
            _container = serviceClient.GetBlobContainerClient(containerName);
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
        {
            var blobClient = _container.GetBlobClient(fileName);

            await blobClient.UploadAsync(fileStream, overwrite: true);

            return blobClient.Uri.ToString();
        }
    }

}