namespace Abril_Backend.Features.AuthModule.Shared.Dtos
{
    // Piezas con las que los detalles de Seguridad (funcionalidad, rol y usuario) cuentan quién
    // tiene acceso a qué. El acceso se lee igual que al iniciar sesión
    // (AuthRepository.GetAllowedFeaturesAsync): usuario → user_role vivo → rol vivo → role_feature
    // → funcionalidad. Además se descartan los usuarios eliminados (app_user.state = false), que
    // la pantalla de Usuarios tampoco muestra.

    /// <summary>Rol por el que llega un acceso: el «vía» de un usuario o de una funcionalidad.</summary>
    public class RoleRefDto
    {
        public int RoleId { get; set; }
        public string RoleDescription { get; set; } = null!;
    }

    /// <summary>Rol con lo que reparte: cuántos usuarios lo tienen y cuántas funcionalidades da.</summary>
    public class AccessRoleDto
    {
        public int RoleId { get; set; }
        public string RoleDescription { get; set; } = null!;
        public int UsersCount { get; set; }
        public int FeaturesCount { get; set; }
    }

    /// <summary>
    /// Usuario con acceso. Un usuario desactivado (<see cref="Active"/> false) conserva sus roles,
    /// así que se lista igual: no puede iniciar sesión, pero recupera el acceso al reactivarlo.
    /// </summary>
    public class AccessUserDto
    {
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public string? DisplayName { get; set; }
        public bool Active { get; set; }

        /// <summary>Roles que le dan el acceso. Null cuando el rol ya es el contexto (detalle de un rol).</summary>
        public List<RoleRefDto>? ViaRoles { get; set; }
    }

    /// <summary>Funcionalidad a la que se tiene acceso.</summary>
    public class AccessFeatureDto
    {
        public int FeatureId { get; set; }
        public string FeatureKey { get; set; } = null!;
        public int? ModuleId { get; set; }
        public string? ModuleName { get; set; }

        /// <summary>Roles que la dan. Null cuando el rol ya es el contexto (detalle de un rol).</summary>
        public List<RoleRefDto>? ViaRoles { get; set; }
    }
}
