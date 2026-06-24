using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Services
{
    public class MiSaludService : IMiSaludService
    {
        private readonly IMiSaludRepository _repo;
        private readonly ISharePointHabService _sharePoint;
        private readonly ILogger<MiSaludService> _logger;

        public MiSaludService(
            IMiSaludRepository repo,
            ISharePointHabService sharePoint,
            ILogger<MiSaludService> logger)
        {
            _repo       = repo;
            _sharePoint = sharePoint;
            _logger     = logger;
        }

        public async Task<MiSaludResumenDto> GetResumen(int userId)
        {
            var workerId = await _repo.ResolverWorkerIdAsync(userId);
            return await _repo.GetResumen(workerId);
        }

        public async Task<PagedResult<MiDescansoDto>> GetDescansos(int userId, int page)
        {
            var workerId = await _repo.ResolverWorkerIdAsync(userId);
            return await _repo.GetDescansos(workerId, page);
        }

        public async Task<int> CreateDescanso(int userId, CrearMiDescansoDto dto)
        {
            if (dto.FechaFin < dto.FechaInicio)
                throw new AbrilException("La fecha de fin no puede ser anterior a la fecha de inicio.", 400);

            var workerId = await _repo.ResolverWorkerIdAsync(userId);

            string? urlCertificado = null;
            if (dto.Documento != null && dto.Documento.Length > 0)
            {
                try
                {
                    using var stream = dto.Documento.OpenReadStream();
                    urlCertificado = await _sharePoint.SubirArchivoAsync(
                        stream, dto.Documento.FileName, "descanso-medico");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error subiendo certificado de descanso para worker {WorkerId}", workerId);
                }
            }

            return await _repo.CreateDescanso(workerId, dto, userId, urlCertificado);
        }
    }
}
