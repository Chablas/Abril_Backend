using Abril_Backend.Features.Habilitacion.Application.Dtos.HabEmpresa;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces
{
    public interface IHabEmpresaRepository
    {
        Task<List<EmpresaEntregableDto>> GetEntregablesEmpresaAsync(
            int empresaId, int proyectoId, int? mes, int? anio);

        Task<SsHabEmpresa> UpdateEntregableEmpresaAsync(
            int id, EmpresaEntregableUpdateDto dto, int? userId, int? empresaId = null);

        Task InicializarEntregablesEmpresaAsync(int empresaId, int proyectoId);
    }
}
