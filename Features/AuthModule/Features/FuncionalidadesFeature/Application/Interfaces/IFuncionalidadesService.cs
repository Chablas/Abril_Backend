using Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Application.Dtos;

namespace Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Application.Interfaces
{
    public interface IFuncionalidadesService
    {
        Task<List<FuncionalidadListItemDto>> List();
        Task<FuncionalidadDetalleDto> GetDetalle(int featureId);
    }
}
