using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Infrastructure.InternalServices
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;

        public LocalFileStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }
        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string containerName)
        {
            var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";

            var relativePath = $"/images/{containerName}/{uniqueName}";
            var physicalPath = Path.Combine(_env.WebRootPath, "images", containerName, uniqueName);

            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

            using var stream = new FileStream(physicalPath, FileMode.Create);
            await fileStream.CopyToAsync(stream);

            return relativePath;
        }
    }
}