using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Restringidos;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.DescansoMedico;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class DescansoMedicoService : IDescansoMedicoService
    {
        private static readonly HashSet<string> TiposValidos = new() { "Particular", "Ocupacional" };

        private readonly IDescansoMedicoRepository _repo;
        private readonly ITrabajadorRestringidoService _restringido;

        public DescansoMedicoService(IDescansoMedicoRepository repo, ITrabajadorRestringidoService restringido)
        {
            _repo = repo;
            _restringido = restringido;
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

        public async Task Aprobar(int id, DescansoAprobarDto dto, int? userId)
        {
            var descanso = await _repo.GetById(id);
            await _repo.Aprobar(id, dto, userId);

            if (descanso?.WorkerId > 0)
            {
                await _restringido.CreateAsync(new TrabajadorRestringidoCreateDto
                {
                    WorkerId       = descanso.WorkerId,
                    Dni            = descanso.WorkerDni,
                    ApellidoNombre = descanso.WorkerNombre,
                    Motivo         = "Descanso médico aprobado",
                    FechaRestriccion = DateOnly.FromDateTime(DateTime.Today),
                    RestringidoPor = "SSOMA"
                });
            }
        }

        public Task Rechazar(int id, DescansoRechazarDto dto, int? userId)
        {
            if (string.IsNullOrWhiteSpace(dto.MotivoRechazo))
                throw new AbrilException("El motivo de rechazo es obligatorio.", 400);
            return _repo.Rechazar(id, dto, userId);
        }

        public Task DarAlta(int id, DarAltaDto dto, int? userId) =>
            _repo.DarAlta(id, dto, userId);

        public Task<List<DescansoSeguimientoDto>> GetSeguimientos(int descansoId) =>
            _repo.GetSeguimientos(descansoId);

        public Task<int> CreateSeguimiento(int descansoId, DescansoSeguimientoCreateDto dto, int? userId, string? rolUsuario)
        {
            if (string.IsNullOrWhiteSpace(dto.Tipo))
                throw new AbrilException("El tipo de seguimiento es obligatorio.", 400);
            return _repo.CreateSeguimiento(descansoId, dto, userId ?? 0, rolUsuario);
        }

        public Task Delete(int id) => _repo.Delete(id);
    }
}
