using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;

public interface IResiduoTipoService
{
    Task<List<ResiduoTipoDto>> ListarAsync(bool? activo);
    Task<ResiduoTipoDto?> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoTipoUpsertDto dto);
    Task ActualizarAsync(int id, ResiduoTipoUpsertDto dto);
    Task DesactivarAsync(int id);

    Task<List<ResiduoTipoFactorDto>> ListarFactoresAsync(int residuoTipoId);
    Task<int> CrearFactorAsync(int residuoTipoId, ResiduoTipoFactorUpsertDto dto);
    Task ActualizarFactorAsync(int factorId, ResiduoTipoFactorUpsertDto dto);
    Task EliminarFactorAsync(int factorId);
}
