namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos
{
    /// <summary>
    /// Cronograma de Hitos → Configuración, en una sola llamada: las secciones (Correos y
    /// Recordatorios) con sus correos y destinatarios, y las opciones del modal de destinatario.
    /// </summary>
    public class CronogramaConfiguracionDto
    {
        public List<CronogramaCorreoGrupoDto> Grupos { get; set; } = new();
        public List<CronogramaCorreoTrabajadorOpcionDto> Trabajadores { get; set; } = new();
        /// <summary>Con cuánta gente alcanza hoy cada uno: un rol en 0 no le llega a nadie.</summary>
        public List<CronogramaCorreoRolOpcionDto> Roles { get; set; } = new();
        /// <summary>Tipos de destinatario (trabajador, rol, correo escrito a mano).</summary>
        public List<CronogramaCorreoOpcionDto> Tipos { get; set; } = new();
        /// <summary>Cómo lo recibe (Para, CC, CCO).</summary>
        public List<CronogramaCorreoOpcionDto> Recepciones { get; set; } = new();
    }

    /// <summary>Una sección de la pantalla: Correos (los dispara una acción) o Recordatorios (el calendario).</summary>
    public class CronogramaCorreoGrupoDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public List<CronogramaCorreoDto> Correos { get; set; } = new();
    }

    /// <summary>Un correo (o recordatorio) con sus dos interruptores y su lista de destinatarios.</summary>
    public class CronogramaCorreoDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        /// <summary>Interruptor del correo: false = no se envía a nadie.</summary>
        public bool Active { get; set; }
        /// <summary>El destinatario que pone el sistema en cada envío (el residente). Null = no tiene.</summary>
        public string? PrincipalNombre { get; set; }
        public bool PrincipalActive { get; set; }
        /// <summary>Primero los Para, después los CC y los CCO.</summary>
        public List<CronogramaCorreoDestinatarioDto> Destinatarios { get; set; } = new();
    }

    public class CronogramaCorreoDestinatarioDto
    {
        public int Id { get; set; }
        /// <summary>TRABAJADOR, ROL o CORREO (<c>CronogramaCorreoDestinatarioTipos</c>).</summary>
        public string TipoCodigo { get; set; } = string.Empty;
        /// <summary>PARA, CC o CCO (<c>CronogramaCorreoRecepciones</c>).</summary>
        public string RecepcionCodigo { get; set; } = string.Empty;
        /// <summary>El nombre del trabajador, el del rol o la dirección escrita a mano.</summary>
        public string Nombre { get; set; } = string.Empty;
        /// <summary>El correo corporativo del trabajador o la dirección escrita a mano. Null en un rol.</summary>
        public string? Email { get; set; }
        /// <summary>Solo en un rol: a cuántos correos se expande hoy.</summary>
        public int? Miembros { get; set; }
        public int? WorkerId { get; set; }
        public int? RoleId { get; set; }
        public bool Active { get; set; }
        /// <summary>Está en la lista pero hoy no resuelve a ningún correo.</summary>
        public bool SinCorreo { get; set; }
    }

    public class CronogramaCorreoTrabajadorOpcionDto
    {
        public int WorkerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class CronogramaCorreoRolOpcionDto
    {
        public int RoleId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Miembros { get; set; }
    }

    public class CronogramaCorreoOpcionDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
    }

    public class CronogramaCorreoActiveDto
    {
        public bool Active { get; set; }
    }

    /// <summary>Alta o edición de un destinatario: el tipo decide cuál de los tres campos se usa.</summary>
    public class CronogramaCorreoDestinatarioInputDto
    {
        public string? TipoCodigo { get; set; }
        public string? RecepcionCodigo { get; set; }
        public int? WorkerId { get; set; }
        public int? RoleId { get; set; }
        public string? Correo { get; set; }
    }

    /// <summary>Lo que devuelve el repositorio al agregar o editar un destinatario.</summary>
    public class CronogramaDestinatarioGuardadoDto
    {
        /// <summary>false = no existe el correo (alta) o el destinatario (edición).</summary>
        public bool Encontrado { get; set; }
        /// <summary>false = el trabajador o el rol elegido ya no existe.</summary>
        public bool DestinoExiste { get; set; }
        /// <summary>false = falta el tipo o la recepción en sus catálogos.</summary>
        public bool CatalogoOk { get; set; }
        public bool Guardado { get; set; }
        /// <summary>El correo con su lista ya actualizada.</summary>
        public CronogramaCorreoDto? Correo { get; set; }
    }
}
