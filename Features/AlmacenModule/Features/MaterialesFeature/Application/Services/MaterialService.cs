using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Dtos;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Models;

namespace Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Services;

public class MaterialService : IMaterialService
{
    private readonly IMaterialRepository _repository;

    public MaterialService(IMaterialRepository repository) => _repository = repository;

    public Task<AlmacenFiltrosDTO> GetFiltros() => _repository.GetFiltros();

    public async Task<AlmacenMaterialDTO> CreateMaterial(CreateAlmacenMaterialDTO body)
    {
        if (string.IsNullOrWhiteSpace(body.Codigo) || string.IsNullOrWhiteSpace(body.Nombre) || string.IsNullOrWhiteSpace(body.UnidadMedida))
            throw new AbrilException("Código, nombre y unidad de medida son obligatorios.", 400);

        if (await _repository.CodigoExiste(body.Codigo))
            throw new AbrilException($"Ya existe un material con el código {body.Codigo}.", 409);

        return await _repository.CreateMaterial(body);
    }

    public Task<AlmacenMovimientoListResponseDTO> GetMovimientos(AlmacenMovimientosQueryParams query) => _repository.GetMovimientos(query);

    public Task<AlmacenMovimientoListItemDTO> CreateMovimiento(CreateAlmacenMovimientoDTO body, string? creadoPor)
    {
        if (!TipoMovimientoAlmacen.EsValido(body.Tipo))
            throw new AbrilException($"Tipo de movimiento inválido: {body.Tipo}", 400);
        if (body.Cantidad <= 0)
            throw new AbrilException("La cantidad debe ser mayor a 0.", 400);

        return _repository.CreateMovimiento(body, creadoPor);
    }

    public Task<AlmacenStockDTO> GetStock(int? proyectoId) => _repository.GetStock(proyectoId);

    public Task<AlmacenDashboardDTO> GetDashboard(int? proyectoId, int diasVentana)
    {
        if (diasVentana < 7 || diasVentana > 365) diasVentana = 90;
        return _repository.GetDashboard(proyectoId, diasVentana);
    }
}
