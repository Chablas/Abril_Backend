using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Services;

public class ResiduoTipoService : IResiduoTipoService
{
    private readonly IResiduoTipoRepository _repo;
    public ResiduoTipoService(IResiduoTipoRepository repo) => _repo = repo;

    public Task<List<ResiduoTipoDto>> ListarAsync(bool? activo) => _repo.ListarAsync(activo);

    public async Task<ResiduoTipoDto?> ObtenerAsync(int id)
    {
        var entidad = await _repo.ObtenerAsync(id);
        if (entidad == null) return null;
        return new ResiduoTipoDto
        {
            Id = entidad.Id,
            CodigoSigersol = entidad.CodigoSigersol,
            Nombre = entidad.Nombre,
            EsPeligroso = entidad.EsPeligroso,
            Activo = entidad.Activo,
        };
    }

    public Task<int> CrearAsync(ResiduoTipoUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new AbrilException("El tipo de residuo necesita un nombre.", 400);
        return _repo.CrearAsync(dto);
    }

    public async Task ActualizarAsync(int id, ResiduoTipoUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new AbrilException("El tipo de residuo necesita un nombre.", 400);
        var ok = await _repo.ActualizarAsync(id, dto);
        if (!ok) throw new AbrilException("Tipo de residuo no encontrado.", 404);
    }

    public async Task DesactivarAsync(int id)
    {
        var ok = await _repo.DesactivarAsync(id);
        if (!ok) throw new AbrilException("Tipo de residuo no encontrado.", 404);
    }

    public Task<List<ResiduoTipoFactorDto>> ListarFactoresAsync(int residuoTipoId) => _repo.ListarFactoresAsync(residuoTipoId);

    public Task<int> CrearFactorAsync(int residuoTipoId, ResiduoTipoFactorUpsertDto dto)
    {
        if (dto.FactorM3aTon <= 0)
            throw new AbrilException("El factor de conversión debe ser mayor a 0.", 400);
        return _repo.CrearFactorAsync(residuoTipoId, dto);
    }

    public async Task ActualizarFactorAsync(int factorId, ResiduoTipoFactorUpsertDto dto)
    {
        if (dto.FactorM3aTon <= 0)
            throw new AbrilException("El factor de conversión debe ser mayor a 0.", 400);
        var ok = await _repo.ActualizarFactorAsync(factorId, dto);
        if (!ok) throw new AbrilException("Factor no encontrado.", 404);
    }

    public async Task EliminarFactorAsync(int factorId)
    {
        var ok = await _repo.EliminarFactorAsync(factorId);
        if (!ok) throw new AbrilException("Factor no encontrado.", 404);
    }
}
