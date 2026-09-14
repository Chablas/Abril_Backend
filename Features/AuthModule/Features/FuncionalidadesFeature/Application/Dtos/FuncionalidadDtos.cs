using Abril_Backend.Features.AuthModule.Shared.Dtos;

namespace Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Application.Dtos
{
    /// <summary>Una fila de Seguridad → Funcionalidades.</summary>
    public class FuncionalidadListItemDto
    {
        public int FeatureId { get; set; }
        public string FeatureKey { get; set; } = null!;
        public int? ModuleId { get; set; }
        public string? ModuleName { get; set; }

        /// <summary>Roles vivos que la tienen asignada.</summary>
        public int RolesCount { get; set; }

        /// <summary>Usuarios que la reciben por alguno de esos roles, cada uno contado una sola vez.</summary>
        public int UsersCount { get; set; }
    }

    /// <summary>Detalle de una funcionalidad: los roles que la tienen y quiénes acceden por ellos.</summary>
    public class FuncionalidadDetalleDto
    {
        public int FeatureId { get; set; }
        public string FeatureKey { get; set; } = null!;
        public int? ModuleId { get; set; }
        public string? ModuleName { get; set; }
        public List<AccessRoleDto> Roles { get; set; } = new();
        public List<AccessUserDto> Users { get; set; } = new();
    }
}
