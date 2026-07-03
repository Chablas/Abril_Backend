using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Convalidacion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class ConvalidacionService : IConvalidacionService
    {
        private static readonly HashSet<string> ResultadosValidos = new()
        {
            "Pendiente", "Aprobada", "Aprobada con Observaciones", "Rechazada"
        };

        private readonly IConvalidacionRepository _repo;

        public ConvalidacionService(IConvalidacionRepository repo)
        {
            _repo = repo;
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
    }
}
