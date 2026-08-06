using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Configuracion;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces
{
    public interface IEmoCorreoConfigService
    {
        Task<EmoCorreosConfigDto> GetConfig();
        Task<int> Create(EmoCorreoDestinatarioCreateDto dto);
        Task Update(int id, EmoCorreoDestinatarioUpdateDto dto);
        Task SetActive(int id, bool active);
        Task Delete(int id);
    }
}
