using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Topico;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class TopicoService : ITopicoService
    {
        private static readonly HashSet<string> TiposValidos = new()
        {
            "Consulta", "Primeros Auxilios", "Curacion", "Inyectable",
            "Control", "Emergencia", "Otro"
        };

        private readonly ITopicoRepository _repo;

        public TopicoService(ITopicoRepository repo)
        {
            _repo = repo;
        }

        public Task<PagedResult<TopicoListItemDto>> ListPaged(TopicoFilterDto filter) =>
            _repo.ListPaged(filter);

        public Task<TopicoDetalleDto> GetById(int id) => _repo.GetById(id);

        public Task<int> Create(TopicoCreateDto dto, int? userId)
        {
            if (dto.WorkerId <= 0)
                throw new AbrilException("El trabajador es obligatorio.", 400);
            if (string.IsNullOrWhiteSpace(dto.TipoAtencion) || !TiposValidos.Contains(dto.TipoAtencion))
                throw new AbrilException($"Tipo de atención no válido. Valores permitidos: {string.Join(", ", TiposValidos)}.", 400);
            return _repo.Create(dto, userId ?? 0);
        }

        public Task Update(int id, TopicoUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TipoAtencion) || !TiposValidos.Contains(dto.TipoAtencion))
                throw new AbrilException($"Tipo de atención no válido. Valores permitidos: {string.Join(", ", TiposValidos)}.", 400);
            return _repo.Update(id, dto);
        }

        public Task Delete(int id) => _repo.Delete(id);
    }
}
