using Abril_Backend.Features.Habilitacion.Application.Dtos.Proyectos;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces
{
    public interface IProyectoHabRepository
    {
        Task<List<ProyectoSimpleDto>> GetActivosAsync();
    }
}
