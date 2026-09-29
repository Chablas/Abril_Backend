using System.Security.Claims;

namespace Abril_Backend.Shared.Constants
{
    /// <summary>
    /// Quién crea y edita proyectos, y quién asigna su residente. El resto de los roles ve los
    /// proyectos en solo lectura. Se decide por rol, sin una feature aparte.
    ///
    /// El residente va por separado porque da permisos: el Cronograma de Hitos deja subir
    /// versiones solo al residente de la obra. Por eso el RESIDENTE edita proyectos pero no cambia
    /// quién es el residente, ni en Configuración → Proyectos ni en Gestión de responsables.
    ///
    /// Espejo del frontend <c>src/app/core/constants/proyecto-roles.ts</c>: mantener ambos alineados.
    /// </summary>
    public static class ProyectoRoles
    {
        /// <summary>Crear, editar y eliminar proyectos. Para <c>[Authorize(Roles = ...)]</c>.</summary>
        public const string EditanProyectos =
            Roles.AdministradorSistema + "," +
            Roles.JefeProyectos + "," +
            Roles.CoordinadorProyectos + "," +
            Roles.GerenteInmobiliario + "," +
            Roles.Residente;

        private static readonly string[] AsignanResidente =
        {
            Roles.AdministradorSistema,
            Roles.JefeProyectos,
            Roles.CoordinadorProyectos,
            Roles.GerenteInmobiliario,
        };

        /// <summary>Asignar o cambiar el residente del proyecto, desde cualquier pantalla.</summary>
        public static bool PuedeAsignarResidente(ClaimsPrincipal user) => AsignanResidente.Any(user.IsInRole);
    }
}
