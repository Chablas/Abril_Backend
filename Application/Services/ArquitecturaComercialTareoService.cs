using System.Security.Cryptography;
using Abril_Backend.Application.DTOs.ArquitecturaComercial;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Application.Services
{
    public class ArquitecturaComercialTareoService : IArquitecturaComercialTareoService
    {
        private readonly IArquitecturaComercialTareoRepository _repository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;

        public ArquitecturaComercialTareoService(
            IArquitecturaComercialTareoRepository repository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver)
        {
            _repository = repository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
        }

        public Task<TareoEnrolamientoEstadoDTO> GetEnrolamientoEstado(int workerId)
            => _repository.GetEnrolamientoEstado(workerId);

        public async Task EnrolarWorker(int workerId, TareoEnrolamientoRequestDTO body)
        {
            if (body.Embedding is not { Length: 128 })
                throw new AbrilException("El embedding facial debe tener 128 valores.", 400);

            var fotoUrl = await SubirFoto(workerId, "enrolamiento", body.FotoBase64);
            await _repository.EnrolarWorker(workerId, fotoUrl, body.Embedding);
        }

        public async Task<TareoRegistroDTO> Marcar(int workerId, Guid idempotencyKey, TareoMarcarRequestDTO body, string? ipOrigen)
        {
            if (string.IsNullOrWhiteSpace(body.FotoBase64))
                throw new AbrilException("La foto es obligatoria para marcar el tareo.", 400);

            var (fotoUrl, fotoHash) = await SubirFotoConHash(workerId, body.Tipo, body.FotoBase64);
            return await _repository.Marcar(workerId, idempotencyKey, body, fotoUrl, fotoHash, ipOrigen);
        }

        public Task<TareoMiTareoHoyDTO> GetMiTareoHoy(int workerId)
            => _repository.GetMiTareoHoy(workerId);

        public Task<TareoRegistroListResponseDTO> GetRegistros(TareoFiltroDTO filtro)
            => _repository.GetRegistros(filtro);

        public Task<bool> Revisar(int id, int revisorUserId, TareoRevisarRequestDTO body)
            => _repository.Revisar(id, revisorUserId, body);

        public Task<List<TareoReporteSemanalDTO>> GetReporteSemanal(int? proyectoId, DateOnly semanaLunes)
            => _repository.GetReporteSemanal(proyectoId, semanaLunes);

        private async Task<string> SubirFoto(int workerId, string prefijo, string fotoBase64)
        {
            var (url, _) = await SubirFotoConHash(workerId, prefijo, fotoBase64);
            return url;
        }

        private async Task<(string Url, string Hash)> SubirFotoConHash(int workerId, string prefijo, string fotoBase64)
        {
            byte[] bytes;
            try
            {
                var payload = fotoBase64.Contains(',') ? fotoBase64[(fotoBase64.IndexOf(',') + 1)..] : fotoBase64;
                bytes = Convert.FromBase64String(payload);
            }
            catch (FormatException)
            {
                throw new AbrilException("La foto enviada no es una imagen válida.", 400);
            }

            if (bytes.Length == 0)
                throw new AbrilException("La foto enviada está vacía.", 400);

            var hash = Convert.ToHexString(SHA256.HashData(bytes));

            var container = _containerResolver.GetTareosContainerName();
            using var stream = new MemoryStream(bytes);
            var fileName = $"{prefijo}_{workerId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.jpg";
            var urls = await _fileStorageService.UploadFilesAsync([(stream, fileName)], container);

            return (urls[0], hash);
        }
    }
}
