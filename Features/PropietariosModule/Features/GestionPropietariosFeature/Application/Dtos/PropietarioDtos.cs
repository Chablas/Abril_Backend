using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Dtos
{
    /// <summary>
    /// Estado de la cuenta con la que el propietario entra a la app (columna «Acceso»). Se calcula
    /// en el repositorio, en este orden.
    /// </summary>
    public static class PropietarioAcceso
    {
        /// <summary>La persona no tiene un usuario vigente (lo eliminaron desde Seguridad).</summary>
        public const string SinCuenta = "SIN_CUENTA";
        /// <summary>Tiene usuario pero no el rol PROPIETARIO (se lo quitaron desde Seguridad).</summary>
        public const string SinRol = "SIN_ROL";
        /// <summary>Todavía no creó su contraseña con el enlace de la invitación.</summary>
        public const string Pendiente = "PENDIENTE";
        /// <summary>Desactivado desde Seguridad → Usuarios.</summary>
        public const string Desactivado = "DESACTIVADO";
        public const string Activo = "ACTIVO";
    }

    /// <summary>Carga inicial de la pantalla: proyectos (filtro y formulario) + primera página.</summary>
    public class PropietariosInitDto
    {
        public List<PropietarioProyectoDto> Proyectos { get; set; } = new();
        public PagedResult<PropietarioListItemDto> Propietarios { get; set; } = null!;
    }

    /// <summary>Proyectos que se venden al público (tipo PROYECTO), visibles en el sistema.</summary>
    public class PropietarioProyectoDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = string.Empty;
    }

    /// <summary>Una fila de la tabla: la persona, su cuenta y todos sus inmuebles.</summary>
    public class PropietarioListItemDto
    {
        public int PersonId { get; set; }
        public int? UserId { get; set; }
        public string? Dni { get; set; }
        public string? FirstNames { get; set; }
        public string? FirstLastName { get; set; }
        public string? SecondLastName { get; set; }
        public string? FullName { get; set; }
        /// <summary><c>app_user.email</c>: a donde le llegan la invitación y la recuperación.</summary>
        public string? Email { get; set; }
        public int? PhoneNumber { get; set; }
        /// <summary>Ver <see cref="PropietarioAcceso"/>.</summary>
        public string Acceso { get; set; } = PropietarioAcceso.SinCuenta;
        /// <summary>
        /// El usuario no tiene otro rol que PROPIETARIO. Si tiene otros, también entra a la
        /// intranet con ese correo, y el correo se cambia desde Seguridad → Usuarios.
        /// </summary>
        public bool SoloPropietario { get; set; }
        public List<PropiedadDto> Propiedades { get; set; } = new();
    }

    public class PropiedadDto
    {
        public int PropietarioId { get; set; }
        public int PersonId { get; set; }
        public int ProjectId { get; set; }
        public string Proyecto { get; set; } = string.Empty;
        public string? Torre { get; set; }
        public string Departamento { get; set; } = string.Empty;
    }

    public class PropiedadGuardarDto
    {
        /// <summary>Null en un inmueble nuevo.</summary>
        public int? PropietarioId { get; set; }
        public int ProjectId { get; set; }
        public string? Torre { get; set; }
        public string Departamento { get; set; } = string.Empty;
    }

    public class PropietarioCreateDto
    {
        public string Dni { get; set; } = string.Empty;
        public string? FirstNames { get; set; }
        public string? FirstLastName { get; set; }
        public string? SecondLastName { get; set; }
        public string? Email { get; set; }
        public int? PhoneNumber { get; set; }
        public List<PropiedadGuardarDto> Propiedades { get; set; } = new();
    }

    /// <summary>El DNI no se edita: es con lo que entra a la app.</summary>
    public class PropietarioUpdateDto
    {
        public string? FirstNames { get; set; }
        public string? FirstLastName { get; set; }
        public string? SecondLastName { get; set; }
        public string? Email { get; set; }
        public int? PhoneNumber { get; set; }
        public List<PropiedadGuardarDto> Propiedades { get; set; } = new();
    }

    /// <summary>Resultado de guardar: si se mandó la invitación, a qué correo.</summary>
    public class PropietarioGuardadoDto
    {
        public int PersonId { get; set; }
        public string? InvitacionEnviadaA { get; set; }
        /// <summary>El propietario quedó guardado pero el correo falló: hay que reenviarlo.</summary>
        public bool InvitacionFallida { get; set; }
    }

    /// <summary>
    /// Búsqueda por DNI del modal de crear: primero en el sistema (la persona puede existir por
    /// GTH o por Seguridad → Usuarios) y, si no está, en RENIEC.
    /// </summary>
    public class PropietarioPersonaDto
    {
        /// <summary>SISTEMA (ya existe en person), RENIEC, o NINGUNA (no se encontró).</summary>
        public string Fuente { get; set; } = "NINGUNA";
        public int? PersonId { get; set; }
        public string? FirstNames { get; set; }
        public string? FirstLastName { get; set; }
        public string? SecondLastName { get; set; }
        public string? Email { get; set; }
        public int? PhoneNumber { get; set; }
        /// <summary>Tiene un usuario vigente: se reusa (y su correo no se cambia desde acá).</summary>
        public bool TieneUsuario { get; set; }
        /// <summary>Ya está en la lista de propietarios: se edita desde la tabla.</summary>
        public bool YaEsPropietario { get; set; }
    }

    /// <summary>Lo que devuelve el repositorio al guardar, para decidir si se manda la invitación.</summary>
    public class PropietarioGuardadoRepoDto
    {
        public int PersonId { get; set; }
        public int UserId { get; set; }
        public bool EnviarInvitacion { get; set; }
    }

    /// <summary>Cuenta del propietario, para reenviar la invitación.</summary>
    public class PropietarioCuentaDto
    {
        public int UserId { get; set; }
        public bool TienePassword { get; set; }
    }
}
