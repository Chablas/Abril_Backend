using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

public interface IResiduoTipoRepository
{
    Task<List<ResiduoTipoDto>> ListarAsync(bool? activo);
    Task<SsResiduoTipo?> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoTipoUpsertDto dto);
    Task<bool> ActualizarAsync(int id, ResiduoTipoUpsertDto dto);
    Task<bool> DesactivarAsync(int id);

    Task<List<ResiduoTipoFactorDto>> ListarFactoresAsync(int residuoTipoId);
    Task<int> CrearFactorAsync(int residuoTipoId, ResiduoTipoFactorUpsertDto dto);
    Task<bool> ActualizarFactorAsync(int factorId, ResiduoTipoFactorUpsertDto dto);
    Task<bool> EliminarFactorAsync(int factorId);

    /// <summary>Factor vigente de un tipo de residuo para una fecha dada, o null si no hay ninguno.</summary>
    Task<decimal?> ObtenerFactorVigenteAsync(int residuoTipoId, DateOnly fecha);
}
