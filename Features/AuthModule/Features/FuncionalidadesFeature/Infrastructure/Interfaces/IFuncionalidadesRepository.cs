using Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Application.Dtos;

namespace Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Infrastructure.Interfaces
{
    public interface IFuncionalidadesRepository
    {
        Task<List<FuncionalidadListItemDto>> List();

        /// <summary>Null si la funcionalidad no existe.</summary>
        Task<FuncionalidadDetalleDto?> GetDetalle(int featureId);
    }
}
