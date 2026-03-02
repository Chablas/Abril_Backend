using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Infrastructure.InternalServices
{
    public class AzureBlobStorageService : IFileStorageService
    {
        private readonly BlobServiceClient _serviceClient;

        public AzureBlobStorageService(IConfiguration config)
        {
            var connectionString = config["Storage:AzureStorage:ConnectionString"];
            _serviceClient = new BlobServiceClient(connectionString);
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string containerName)
        {
            var container = _serviceClient.GetBlobContainerClient(containerName);

            await container.CreateIfNotExistsAsync();

            var blobClient = container.GetBlobClient(fileName);

            var headers = new BlobHttpHeaders
            {
                ContentType = GetContentType(fileName)
            };

            await blobClient.UploadAsync(fileStream, new BlobUploadOptions
            {
                HttpHeaders = headers
            });

            return blobClient.Uri.ToString();
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };
        }
    }
}