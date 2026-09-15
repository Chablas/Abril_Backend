using Abril_Backend.Features.AuthModule.Shared.Dtos;

namespace Abril_Backend.Features.AuthModule.Role.Application.Dtos
{
    /// <summary>Detalle de un rol: los usuarios que lo tienen y las funcionalidades que da.</summary>
    public class RoleDetailDto
    {
        public int RoleId { get; set; }
        public string RoleDescription { get; set; } = null!;
        public List<AccessUserDto> Users { get; set; } = new();
        public List<AccessFeatureDto> Features { get; set; } = new();
    }
}
