using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Application.Dtos;
using Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Application.Interfaces;
using Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Application.Services
{
    /// <summary>
    /// Seguridad → Funcionalidades: el catálogo de la tabla <c>feature</c> y quién accede a cada una.
    /// Es solo lectura a propósito: las funcionalidades se dan de alta por base de datos, junto con
    /// la pantalla que protegen, y se asignan a los roles desde Seguridad → Roles.
    /// </summary>
    public class FuncionalidadesService : IFuncionalidadesService
    {
        private readonly IFuncionalidadesRepository _repo;

        public FuncionalidadesService(IFuncionalidadesRepository repo) => _repo = repo;

        public Task<List<FuncionalidadListItemDto>> List() => _repo.List();

        public async Task<FuncionalidadDetalleDto> GetDetalle(int featureId) =>
            await _repo.GetDetalle(featureId)
            ?? throw new AbrilException("La funcionalidad no existe.", 404);
    }
}
