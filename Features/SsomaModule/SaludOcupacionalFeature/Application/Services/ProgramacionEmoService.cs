using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class ProgramacionEmoService : IProgramacionEmoService
    {
        private static readonly HashSet<string> EstadosValidos = new()
        {
            "Programado", "Confirmado", "Realizado", "No se presentó", "Cancelado", "Reprogramado"
        };

        private readonly IProgramacionEmoRepository _repo;

        public ProgramacionEmoService(IProgramacionEmoRepository repo)
        {
            _repo = repo;
        }

        public Task<List<ProgramacionListDto>> List(ProgramacionFilterDto filter) => _repo.List(filter);

        public Task<int> Create(ProgramacionCreateDto dto, int? userId)
        {
            if (dto.WorkerId <= 0) throw new AbrilException("El trabajador es obligatorio.", 400);
            if (dto.TipoEmoId <= 0) throw new AbrilException("El tipo de EMO es obligatorio.", 400);
            return _repo.Create(dto, userId);
        }

        public Task Update(int id, ProgramacionUpdateDto dto, int? userId)
        {
            if (dto.TipoEmoId <= 0) throw new AbrilException("El tipo de EMO es obligatorio.", 400);
            return _repo.Update(id, dto, userId);
        }

        public Task UpdateEstado(int id, string estado, int? emoResultadoId, int? userId)
        {
            if (string.IsNullOrWhiteSpace(estado) || !EstadosValidos.Contains(estado))
                throw new AbrilException("El estado de la programación no es válido.", 400);
            return _repo.UpdateEstado(id, estado, emoResultadoId, userId);
        }
    }
}
