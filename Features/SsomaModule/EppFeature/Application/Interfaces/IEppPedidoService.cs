using Abril_Backend.Features.SsomaModule.EppFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.EppFeature.Application.Interfaces
{
    public interface IEppPedidoService
    {
        Task<List<EppPedidoListDto>> GetPedidosAsync();
        Task<EppPedidoDetalleDto?> GetPedidoDetalleAsync(int pedidoId);
        Task<EppPedidoDetalleDto> CreatePedidoAsync(EppPedidoCreateDto dto, int? userId);
    }
}
