using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Services
{
    public class SolicitudSalidaService : ISolicitudSalidaService
    {
        private readonly ISolicitudSalidaRepository _repo;

        public SolicitudSalidaService(ISolicitudSalidaRepository repo)
        {
            _repo = repo;
        }

        public Task<SolicitudSalidaFormDataDto> GetFormData() => _repo.GetFormData();

        public Task<List<SolicitudSalidaListItemDto>> GetByUserId(int userId) => _repo.GetByUserId(userId);

        public Task<int> Create(SolicitudSalidaCreateDto dto, int? userId)
        {
            if (dto.HoraRetorno.HasValue && dto.HoraRetorno.Value <= dto.HoraSalida)
                throw new AbrilException("La hora de retorno debe ser posterior a la hora de salida.", 400);

            var tieneOrigenId    = dto.LugarOrigenId.HasValue;
            var tieneOrigenLibre = !string.IsNullOrWhiteSpace(dto.LugarOrigenLibre);
            if (!tieneOrigenId && !tieneOrigenLibre)
                throw new AbrilException("Debe indicar un lugar de origen.", 400);

            var tieneDestinoId    = dto.LugarDestinoId.HasValue;
            var tieneDestinoLibre = !string.IsNullOrWhiteSpace(dto.LugarDestinoLibre);
            if (!tieneDestinoId && !tieneDestinoLibre)
                throw new AbrilException("Debe indicar un lugar de destino.", 400);

            if (tieneOrigenId && tieneDestinoId && dto.LugarOrigenId == dto.LugarDestinoId)
                throw new AbrilException("El lugar de origen y el lugar de destino no pueden ser iguales.", 400);

            return _repo.Create(dto, userId);
        }
    }
}
