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
        public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
        {
            var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";

            var relativePath = $"/images/lessons/{uniqueName}";
            var physicalPath = Path.Combine(_env.WebRootPath, "images", "lessons", uniqueName);

            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

            using var stream = new FileStream(physicalPath, FileMode.Create);
            await fileStream.CopyToAsync(stream);

            return relativePath;
        }
    }
}