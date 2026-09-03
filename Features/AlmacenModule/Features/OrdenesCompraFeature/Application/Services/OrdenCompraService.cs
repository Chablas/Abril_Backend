using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Application.Dtos;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Application.Interfaces;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Application.Services;

public class OrdenCompraService : IOrdenCompraService
{
    private const string Contenedor = "almacen-oc-contratos";

    private readonly IOrdenCompraRepository _repository;
    private readonly IFileStorageService _fileStorageService;

    public OrdenCompraService(IOrdenCompraRepository repository, IFileStorageService fileStorageService)
    {
        _repository = repository;
        _fileStorageService = fileStorageService;
    }

    public Task<AlmacenOrdenCompraListResponseDTO> GetOrdenesCompra(AlmacenOrdenCompraQueryParams query) => _repository.GetOrdenesCompra(query);

    public async Task<AlmacenOrdenCompraListItemDTO> CreateOrdenCompra(CreateAlmacenOrdenCompraDTO body, Stream archivo, string archivoNombre, string? subidoPor)
    {
        if (!TipoDocumentoOrdenCompra.EsValido(body.Tipo))
            throw new AbrilException($"Tipo de documento inválido: {body.Tipo}", 400);
        if (string.IsNullOrWhiteSpace(body.Numero) || string.IsNullOrWhiteSpace(body.Proveedor))
            throw new AbrilException("Número y proveedor son obligatorios.", 400);

        var urls = await _fileStorageService.UploadFilesAsync([(archivo, archivoNombre)], Contenedor);
        var url = urls.FirstOrDefault() ?? throw new AbrilException("No se pudo subir el archivo.", 500);

        var entity = await _repository.CreateOrdenCompra(body, url, archivoNombre, subidoPor);

        return new AlmacenOrdenCompraListItemDTO
        {
            Id = entity.Id,
            ProyectoId = entity.ProyectoId,
            Numero = entity.Numero,
            Tipo = entity.Tipo,
            Proveedor = entity.Proveedor,
            ContratistaId = entity.ContratistaId,
            Monto = entity.Monto,
            Moneda = entity.Moneda,
            Fecha = entity.Fecha,
            ArchivoUrl = entity.ArchivoUrl,
            ArchivoNombre = entity.ArchivoNombre,
            SubidoPor = entity.SubidoPor,
            CreatedAt = entity.CreatedAt
        };
    }
}
