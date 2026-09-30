using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Dtos;

namespace Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Interfaces
{
    public interface IGestionPropietariosService
    {
        Task<PropietariosInitDto> GetInit(int pageSize);
        Task<PagedResult<PropietarioListItemDto>> GetPaged(int page, int pageSize, string? search, int? projectId);
        Task<PropietarioPersonaDto> BuscarPersona(string dni);
        Task<PropietarioGuardadoDto> Crear(PropietarioCreateDto dto, int userId);
        Task<PropietarioGuardadoDto> Actualizar(int personId, PropietarioUpdateDto dto, int userId);
        /// <summary>Devuelve el correo al que se mandó.</summary>
        Task<string> ReenviarInvitacion(int personId);
        Task Eliminar(int personId, int userId);
    }
}
