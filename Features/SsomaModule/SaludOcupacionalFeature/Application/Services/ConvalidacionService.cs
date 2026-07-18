using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Convalidacion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Shared.Models;
using Microsoft.AspNetCore.Hosting;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class ConvalidacionService : IConvalidacionService
    {
        private static readonly HashSet<string> ResultadosValidos = new()
        {
            "Pendiente", "Aprobada", "Aprobada con Observaciones", "Rechazada"
        };

        private readonly IConvalidacionRepository _repo;
        private readonly string[] _logoPaths;

        public ConvalidacionService(IConvalidacionRepository repo, IWebHostEnvironment env)
        {
            _repo = repo;
            _logoPaths = new[]
            {
                Path.Combine(env.WebRootPath, "images", "abril-logo.png"),
                Path.Combine(env.WebRootPath, "images", "logo-abril.jpg"),
                Path.Combine(env.ContentRootPath, "Templates", "logo-abril.jpg"),
            };
        }

        public Task<PagedResponseDto<ConvalidacionListDto>> List(ConvalidacionFilterDto filter) => _repo.List(filter);

        public Task<int> Create(ConvalidacionCreateDto dto, int? userId)
        {
            if (dto.EmoOrigenId <= 0) throw new AbrilException("El EMO es obligatorio.", 400);
            if (!ResultadosValidos.Contains(dto.Resultado))
                throw new AbrilException("El resultado de la convalidación no es válido.", 400);
            return _repo.Create(dto, userId);
        }

        public Task Update(int id, ConvalidacionUpdateDto dto, int? userId)
        {
            if (!ResultadosValidos.Contains(dto.Resultado))
                throw new AbrilException("El resultado de la convalidación no es válido.", 400);
            return _repo.Update(id, dto, userId);
        }

        public async Task<byte[]> GenerarPdfAsync(int id)
        {
            var detalle = await _repo.GetDetalleAsync(id)
                ?? throw new AbrilException("Convalidación no encontrada.", 404);

            byte[]? logoBytes = null;
            var logoPath = _logoPaths.FirstOrDefault(File.Exists);
            if (logoPath != null)
                logoBytes = await File.ReadAllBytesAsync(logoPath);

            return ConvalidacionPdfService.GenerarPdf(detalle, logoBytes);
        }
    }
}
