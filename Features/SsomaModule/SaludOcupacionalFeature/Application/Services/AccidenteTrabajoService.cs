using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.AccidenteTrabajo;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class AccidenteTrabajoService : IAccidenteTrabajoService
    {
        private static readonly HashSet<string> TiposValidos = new()
        {
            "Incidente", "Accidente Leve", "Accidente Moderado", "Accidente Grave", "Accidente Fatal"
        };

        private readonly IAccidenteTrabajoRepository _repo;

        public AccidenteTrabajoService(IAccidenteTrabajoRepository repo)
        {
            _repo = repo;
        }

        public Task<PagedResult<AccidenteTrabajoListItemDto>> ListPaged(AccidenteFilterDto filter) =>
            _repo.ListPaged(filter);

        public Task<AccidenteTrabajoDetalleDto> GetById(int id) => _repo.GetById(id);

        public Task<int> Create(AccidenteTrabajoCreateDto dto, int? userId)
        {
            if (dto.WorkerId <= 0)
                throw new AbrilException("El trabajador es obligatorio.", 400);
            if (string.IsNullOrWhiteSpace(dto.Descripcion))
                throw new AbrilException("La descripción del accidente es obligatoria.", 400);
            if (!string.IsNullOrWhiteSpace(dto.TipoAccidente) && !TiposValidos.Contains(dto.TipoAccidente))
                throw new AbrilException($"Tipo de accidente no válido. Valores permitidos: {string.Join(", ", TiposValidos)}.", 400);
            return _repo.Create(dto, userId ?? 0);
        }

        public Task Update(int id, AccidenteTrabajoUpdateDto dto) => _repo.Update(id, dto);

        public Task Cerrar(int id, AccidenteCerrarDto dto, int? userId) => _repo.Cerrar(id, dto, userId);

        public Task Delete(int id) => _repo.Delete(id);

        public Task<int> CreateSeguimiento(int accidenteId, AccidenteSeguimientoCreateDto dto, int? userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Descripcion))
                throw new AbrilException("La descripción del seguimiento es obligatoria.", 400);
            return _repo.CreateSeguimiento(accidenteId, dto, userId ?? 0);
        }

        public Task DeleteSeguimiento(int seguimientoId) => _repo.DeleteSeguimiento(seguimientoId);
    }
}
