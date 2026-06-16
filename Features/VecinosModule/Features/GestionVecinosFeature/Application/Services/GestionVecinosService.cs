using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Dtos;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Services
{
    public class GestionVecinosService : IGestionVecinosService
    {
        private readonly IGestionVecinosRepository _repository;

        public GestionVecinosService(IGestionVecinosRepository repository)
        {
            _repository = repository;
        }

        public async Task<VecinosPageDto> GetPageData(VecinoFilterDto filter)
        {
            var options = await _repository.GetOptions();
            var vecinos = await _repository.GetPaged(filter);
            return new VecinosPageDto { Options = options, Vecinos = vecinos };
        }

        public Task<PagedResult<VecinoListItemDto>> GetList(VecinoFilterDto filter)
            => _repository.GetPaged(filter);

        public Task<int> Create(VecinoCreateDto dto, int userId)
        {
            if (dto.ProjectId <= 0)
                throw new AbrilException("Debe seleccionar un proyecto.", 400);
            if (string.IsNullOrWhiteSpace(dto.Direccion))
                throw new AbrilException("La dirección es obligatoria.", 400);
            if (string.IsNullOrWhiteSpace(dto.NombrePropietario))
                throw new AbrilException("El nombre del propietario o representante es obligatorio.", 400);

            var dni = dto.Dni?.Trim() ?? string.Empty;
            if (dni.Length != 8 || !dni.All(char.IsDigit))
                throw new AbrilException("El DNI debe tener 8 dígitos.", 400);

            if (dto.VecinoColindanciaId <= 0)
                throw new AbrilException("Debe seleccionar si es colindante o no colindante.", 400);
            if (dto.VecinoTipoConstruccionId <= 0)
                throw new AbrilException("Debe seleccionar el tipo de construcción.", 400);

            return _repository.Create(dto, userId);
        }

        // ── Solicitudes ─────────────────────────────────────────────────────
        public async Task<VecinoSolicitudesResponseDto> GetSolicitudes(int vecinoId)
        {
            if (!await _repository.VecinoExists(vecinoId))
                throw new AbrilException("El vecino no existe.", 404);

            return await _repository.GetSolicitudes(vecinoId);
        }

        public async Task<int> CreateSolicitud(int vecinoId, VecinoSolicitudCreateDto dto, int userId)
        {
            if (!await _repository.VecinoExists(vecinoId))
                throw new AbrilException("El vecino no existe.", 404);
            if (string.IsNullOrWhiteSpace(dto.Descripcion))
                throw new AbrilException("La descripción de la solicitud es obligatoria.", 400);

            return await _repository.CreateSolicitud(vecinoId, dto, userId);
        }

        public async Task UpdateSolicitudEstado(int solicitudId, int estadoId, int userId)
        {
            if (estadoId <= 0)
                throw new AbrilException("Debe seleccionar un estado válido.", 400);

            var ok = await _repository.UpdateSolicitudEstado(solicitudId, estadoId, userId);
            if (!ok)
                throw new AbrilException("No se pudo actualizar el estado de la solicitud.", 404);
        }

        // ── Compromisos ─────────────────────────────────────────────────────
        public async Task<List<VecinoCompromisoItemDto>> GetCompromisos(int solicitudId)
        {
            if (!await _repository.SolicitudExists(solicitudId))
                throw new AbrilException("La solicitud no existe.", 404);

            return await _repository.GetCompromisos(solicitudId);
        }

        public async Task<int> CreateCompromiso(int solicitudId, VecinoCompromisoCreateDto dto, int userId)
        {
            if (!await _repository.SolicitudExists(solicitudId))
                throw new AbrilException("La solicitud no existe.", 404);
            if (string.IsNullOrWhiteSpace(dto.Descripcion))
                throw new AbrilException("La descripción del compromiso es obligatoria.", 400);
            if (dto.FechaInicio.HasValue && dto.FechaFin.HasValue && dto.FechaFin.Value < dto.FechaInicio.Value)
                throw new AbrilException("La fecha fin no puede ser anterior a la fecha de inicio.", 400);

            return await _repository.CreateCompromiso(solicitudId, dto, userId);
        }

        public async Task UpdateCompromisoEstado(int compromisoId, int estadoId, int userId)
        {
            if (estadoId <= 0)
                throw new AbrilException("Debe seleccionar un estado válido.", 400);

            var ok = await _repository.UpdateCompromisoEstado(compromisoId, estadoId, userId);
            if (!ok)
                throw new AbrilException("No se pudo actualizar el estado del compromiso.", 404);
        }

        public async Task UpdateEntregableEstado(int entregableId, int estadoId, int userId)
        {
            if (estadoId <= 0)
                throw new AbrilException("Debe seleccionar un estado válido.", 400);

            var ok = await _repository.UpdateEntregableEstado(entregableId, estadoId, userId);
            if (!ok)
                throw new AbrilException("No se pudo actualizar el estado del entregable.", 404);
        }

        // ── Dashboard ────────────────────────────────────────────────────────
        public Task<VecinosDashboardDto> GetDashboard() => _repository.GetDashboard();
    }
}
