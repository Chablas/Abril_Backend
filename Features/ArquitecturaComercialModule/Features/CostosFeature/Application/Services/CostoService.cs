using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Application.Dtos;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Application.Interfaces;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Application.Services;

public class CostoService : ICostoService
{
    private readonly ICostoRepository _repository;

    public CostoService(ICostoRepository repository) => _repository = repository;

    private static void ValidarPartida(string partida)
    {
        if (!PartidaCosto.EsValido(partida))
            throw new AbrilException($"Partida inválida: {partida}", 400);
    }

    public Task<CostoFiltrosDTO> GetFiltros() => _repository.GetFiltros();

    public async Task<CostoMatrizDTO> GetMatriz(int proyectoId, int anio, int mes)
    {
        var matriz = await _repository.GetMatriz(proyectoId, anio, mes);
        if (matriz == null) throw new AbrilException("No se encontró el proyecto.", 404);
        return matriz;
    }

    public Task UpsertRegistro(UpsertCostoRegistroDTO body, string? creadoPor)
    {
        ValidarPartida(body.Partida);
        if (body.Semana < 1 || body.Semana > 6) throw new AbrilException("Semana inválida.", 400);
        if (body.Monto < 0) throw new AbrilException("El monto no puede ser negativo.", 400);
        return _repository.UpsertRegistro(body, creadoPor);
    }

    public Task UpsertProyeccion(UpsertCostoProyeccionDTO body, string? creadoPor)
    {
        ValidarPartida(body.Partida);
        if (body.Monto < 0) throw new AbrilException("El monto no puede ser negativo.", 400);
        return _repository.UpsertProyeccion(body, creadoPor);
    }

    public Task<CostoDashboardDTO> GetDashboard(int anio, int mes) => _repository.GetDashboard(anio, mes);

    public Task<CostoEvolucionDTO> GetEvolucion(int anioDesde, int mesDesde, int cantidadMeses)
    {
        if (cantidadMeses < 1 || cantidadMeses > 24) cantidadMeses = 12;
        return _repository.GetEvolucion(anioDesde, mesDesde, cantidadMeses);
    }

    public Task UpsertMeta(UpsertCostoMetaDTO body, string? creadoPor)
    {
        if (body.Monto < 0) throw new AbrilException("El monto no puede ser negativo.", 400);
        return _repository.UpsertMeta(body, creadoPor);
    }
}
