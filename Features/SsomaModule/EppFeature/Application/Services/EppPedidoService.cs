using Abril_Backend.Features.SsomaModule.EppFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.EppFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.EppFeature.Application.Services
{
    public class EppPedidoService : IEppPedidoService
    {
        private readonly IEppPedidoRepository _repository;

        public EppPedidoService(IEppPedidoRepository repository)
        {
            _repository = repository;
        }

        public Task<List<EppPedidoListDto>> GetPedidosAsync() => _repository.GetPedidosAsync();

        public Task<EppPedidoDetalleDto?> GetPedidoDetalleAsync(int pedidoId) => _repository.GetPedidoDetalleAsync(pedidoId);

        public Task<EppPedidoDetalleDto> CreatePedidoAsync(EppPedidoCreateDto dto, int? userId) => _repository.CreatePedidoAsync(dto, userId);
    }
}
