using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Services
{
    public class ManagerSignatureService : IManagerSignatureService
    {
        private const int MaxBytes = 2 * 1024 * 1024; // 2 MB
        private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private readonly IManagerSignatureRepository _repository;

        public ManagerSignatureService(IManagerSignatureRepository repository)
        {
            _repository = repository;
        }

        public Task<ManagerSignatureDto?> GetSingleton() => _repository.GetSingleton();

        public async Task<ManagerSignatureDto> Save(ManagerSignatureSaveDto dto, int userId)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.ImageBase64))
                throw new AbrilException("Debe dibujar una firma antes de guardar.");

            var raw = dto.ImageBase64.Trim();

            // Aceptar tanto el data URL completo ("data:image/png;base64,XXXX") como solo el base64.
            var commaIdx = raw.IndexOf(',');
            if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIdx >= 0)
                raw = raw[(commaIdx + 1)..];

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(raw);
            }
            catch (FormatException)
            {
                throw new AbrilException("La firma no tiene un formato de imagen válido.");
            }

            if (bytes.Length == 0)
                throw new AbrilException("La firma está vacía.");
            if (bytes.Length > MaxBytes)
                throw new AbrilException("La firma es demasiado grande (máximo 2 MB).");
            if (!bytes.Take(PngMagic.Length).SequenceEqual(PngMagic))
                throw new AbrilException("La firma debe ser una imagen PNG.");

            await _repository.Upsert(bytes, "image/png", userId);

            return await _repository.GetSingleton()
                ?? throw new AbrilException("No se pudo guardar la firma.", 500);
        }
    }
}
