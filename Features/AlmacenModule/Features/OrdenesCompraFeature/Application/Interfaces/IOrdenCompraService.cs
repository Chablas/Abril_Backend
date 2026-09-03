using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Application.Dtos;

namespace Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Application.Interfaces;

public interface IOrdenCompraService
{
    Task<AlmacenOrdenCompraListResponseDTO> GetOrdenesCompra(AlmacenOrdenCompraQueryParams query);
    Task<AlmacenOrdenCompraListItemDTO> CreateOrdenCompra(CreateAlmacenOrdenCompraDTO body, Stream archivo, string archivoNombre, string? subidoPor);
}
