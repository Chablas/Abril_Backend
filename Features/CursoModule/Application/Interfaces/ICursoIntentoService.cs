using Abril_Backend.Features.CursoModule.Application.Dtos;

namespace Abril_Backend.Features.CursoModule.Application.Interfaces
{
    public interface ICursoIntentoService
    {
        Task<IniciarIntentoResultDto> IniciarAsync(int userId, IniciarIntentoDto dto, string ipAddress, string userAgent);
        Task<ResponderSlideResultDto> ResponderAsync(int intentoId, ResponderSlideDto dto);
        Task<FinalizarIntentoResultDto> FinalizarAsync(int intentoId, FinalizarIntentoDto dto);
        Task<CursoIntentoDetalleDto> GetDetalleAsync(int intentoId);
    }
}
