using Abril_Backend.Features.AuthModule.Shared.Dtos;

namespace Abril_Backend.Features.AuthModule.UserFeature.Application.Dtos
{
    /// <summary>Detalle de un usuario: sus roles y las funcionalidades a las que accede por ellos.</summary>
    public class UserDetailDto
    {
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? DocumentIdentityCode { get; set; }

        /// <summary>CONTRATISTA, PERSONA o COLABORADOR: el mismo criterio que la tabla de Usuarios.</summary>
        public string UserType { get; set; } = null!;

        public bool Active { get; set; }
        public List<AccessRoleDto> Roles { get; set; } = new();

        /// <summary>Cada funcionalidad una vez, con todos los roles que se la dan.</summary>
        public List<AccessFeatureDto> Features { get; set; } = new();
    }
}
