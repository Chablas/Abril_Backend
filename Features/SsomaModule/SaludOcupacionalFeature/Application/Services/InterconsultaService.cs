using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Interconsulta;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class InterconsultaService : IInterconsultaService
    {
        private static readonly HashSet<string> EstadosValidos = new() { "Pendiente", "Atendida", "Cancelada" };

        private readonly IInterconsultaRepository _repo;

        public InterconsultaService(IInterconsultaRepository repo)
        {
            _repo = repo;
        }

        public Task<PagedResult<InterconsultaListDto>> List(InterconsultaFilterDto filter) => _repo.List(filter);

        public Task<InterconsultaDetalleDto> GetById(int id) => _repo.GetById(id);

        public Task<int> Create(InterconsultaCreateDto dto, int? userId)
        {
            if (dto.EmoId <= 0) throw new AbrilException("El EMO es obligatorio.", 400);
            if (dto.WorkerId <= 0) throw new AbrilException("El trabajador es obligatorio.", 400);
            if (string.IsNullOrWhiteSpace(dto.Especialidad))
                throw new AbrilException("La especialidad es obligatoria.", 400);
            return _repo.Create(dto, userId);
        }

        public Task Update(int id, InterconsultaUpdateDto dto, int? userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Especialidad))
                throw new AbrilException("La especialidad es obligatoria.", 400);
            return _repo.Update(id, dto, userId);
        }

        public Task UpdateResultado(int id, InterconsultaResultadoPatchDto dto, int? userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Estado) || !EstadosValidos.Contains(dto.Estado))
                throw new AbrilException("El estado de la interconsulta no es válido.", 400);
            return _repo.UpdateResultado(id, dto, userId);
        }

        public Task UpdateDerivacion(int id, InterconsultaDerivacionPatchDto dto, int? userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Especialidad))
                throw new AbrilException("La especialidad es obligatoria.", 400);
            return _repo.UpdateDerivacion(id, dto, userId);
        }
    }
}
