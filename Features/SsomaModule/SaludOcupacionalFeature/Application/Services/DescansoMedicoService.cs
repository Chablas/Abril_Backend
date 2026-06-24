using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.DescansoMedico;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class DescansoMedicoService : IDescansoMedicoService
    {
        private static readonly HashSet<string> TiposValidos = new()
        {
            "Particular", "Ocupacional"
        };

        private readonly IDescansoMedicoRepository _repo;

        public DescansoMedicoService(IDescansoMedicoRepository repo)
        {
            _repo = repo;
        }

        public Task<PagedResult<DescansoMedicoListItemDto>> ListPaged(DescansoMedicoFilterDto filter) =>
            _repo.ListPaged(filter);

        public Task<DescansoMedicoDetalleDto> GetById(int id) => _repo.GetById(id);

        public Task<int> Create(DescansoMedicoCreateDto dto, int? userId)
        {
            if (dto.WorkerId <= 0)
                throw new AbrilException("El trabajador es obligatorio.", 400);
            if (string.IsNullOrWhiteSpace(dto.Tipo) || !TiposValidos.Contains(dto.Tipo))
                throw new AbrilException("El tipo debe ser 'Particular' u 'Ocupacional'.", 400);
            if (dto.FechaFin < dto.FechaInicio)
                throw new AbrilException("La fecha de fin no puede ser anterior a la fecha de inicio.", 400);
            return _repo.Create(dto, userId ?? 0);
        }

        public Task Update(int id, DescansoMedicoUpdateDto dto)
        {
            if (dto.FechaFin < dto.FechaInicio)
                throw new AbrilException("La fecha de fin no puede ser anterior a la fecha de inicio.", 400);
            return _repo.Update(id, dto);
        }

        public Task Aprobar(int id, DescansoAprobarDto dto, int? userId) =>
            _repo.Aprobar(id, dto, userId);

        public Task Rechazar(int id, DescansoRechazarDto dto, int? userId)
        {
            if (string.IsNullOrWhiteSpace(dto.MotivoRechazo))
                throw new AbrilException("El motivo de rechazo es obligatorio.", 400);
            return _repo.Rechazar(id, dto, userId);
        }

        public Task Delete(int id) => _repo.Delete(id);
    }
}
