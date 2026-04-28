using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Alerta;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces
{
    public interface IEmoAlertaService
    {
        Task<EmoAlertaResultDto> ProcesarAlertas();
    }
}
