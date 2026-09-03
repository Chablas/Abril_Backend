using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Application.Dtos;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Infrastructure.Models;

namespace Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Infrastructure.Interfaces;

public interface IOrdenCompraRepository
{
    Task<AlmacenOrdenCompraListResponseDTO> GetOrdenesCompra(AlmacenOrdenCompraQueryParams query);
    Task<AlmacenOrdenCompra> CreateOrdenCompra(CreateAlmacenOrdenCompraDTO body, string archivoUrl, string archivoNombre, string? subidoPor);
}
