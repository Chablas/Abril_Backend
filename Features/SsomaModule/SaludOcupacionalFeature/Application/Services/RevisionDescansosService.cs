using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.RevisionDescansos;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class RevisionDescansosService : IRevisionDescansosService
    {
        private readonly IRevisionDescansosRepository _repo;

        public RevisionDescansosService(IRevisionDescansosRepository repo)
        {
            _repo = repo;
        }

        public Task<RevisionDescansosInitDto> GetInit(RevisionDescansosFiltroDto filtro) =>
            _repo.GetInit(filtro);

        public Task<PagedResult<RevisionDescansoListItemDto>> ListPaged(RevisionDescansosFiltroDto filtro) =>
            _repo.ListPaged(filtro);

        public Task<RevisionDescansoDetalleDto> GetDetalle(int id) =>
            _repo.GetDetalle(id);

        public Task<int> Aprobar(RevisionDescansosAprobarDto dto, int? userId)
        {
            if (dto.Ids is not { Count: > 0 })
                throw new AbrilException("Debes seleccionar al menos una solicitud.", 400);
            return _repo.Aprobar(dto.Ids, userId);
        }

        public Task<int> Rechazar(RevisionDescansosRechazarDto dto, int? userId)
        {
            if (dto.Ids is not { Count: > 0 })
                throw new AbrilException("Debes seleccionar al menos una solicitud.", 400);
            if (string.IsNullOrWhiteSpace(dto.MotivoRechazo))
                throw new AbrilException("El motivo de rechazo es obligatorio.", 400);
            return _repo.Rechazar(dto.Ids, dto.MotivoRechazo.Trim(), userId);
        }
    }
}
