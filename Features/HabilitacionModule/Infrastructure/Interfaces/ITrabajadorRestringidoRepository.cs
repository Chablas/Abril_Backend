using Abril_Backend.Features.Habilitacion.Application.Dtos.Restringidos;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces
{
    public interface ITrabajadorRestringidoRepository
    {
        Task<bool> EstaRestringidoPorDniAsync(string? dni);
        Task<List<TrabajadorRestringidoListDto>> GetAllAsync(bool soloActivos = true, string? dni = null);
        Task<TrabajadorRestringidoListDto> CreateAsync(TrabajadorRestringidoCreateDto dto);
        Task DesactivarAsync(int id);
    }
}
