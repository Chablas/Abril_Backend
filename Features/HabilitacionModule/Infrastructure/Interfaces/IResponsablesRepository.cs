using Abril_Backend.Features.Habilitacion.Application.Dtos.Responsables;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces
{
    public interface IResponsablesRepository
    {
        Task<ResponsablesDto> GetAll();
        Task UpdateRazonSocial(int contributorId, ResponsableRazonSocialUpdateDto dto, int? userId);
        Task UpdateProyecto(int projectId, ResponsableProyectoUpdateDto dto, int? userId, bool puedeAsignarResidente);
    }
}
