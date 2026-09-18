using Abril_Backend.Features.SsomaModule.EppFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Interfaces
{
    public interface IEppPedidoRepository
    {
        Task<List<EppPedidoListDto>> GetPedidosAsync();
        Task<EppPedidoDetalleDto?> GetPedidoDetalleAsync(int pedidoId);
        Task<EppPedidoDetalleDto> CreatePedidoAsync(EppPedidoCreateDto dto, int? userId);
    }
}
