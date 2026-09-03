namespace Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Dtos
{
    /// <summary>
    /// Carga inicial de la pantalla "Correos" (1 sola petición): los correos configurables con
    /// sus destinatarios ya resueltos + las opciones de los desplegables del modal de alta
    /// (trabajadores y áreas).
    /// </summary>
    public class CorreoConfigInicialDto
    {
        public List<CorreoEventoDto> Eventos { get; set; } = new();
        public List<CorreoWorkerOptionDto> Trabajadores { get; set; } = new();
        public List<CorreoAreaOptionDto> Areas { get; set; } = new();
    }

    /// <summary>Un correo configurable (ga_correo_evento) con su lista de destinatarios.</summary>
    public class CorreoEventoDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int Orden { get; set; }

        /// <summary>Interruptor maestro: false = este correo no se envía a nadie.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Etiqueta del destinatario principal que calcula el backend (el revisor, el solicitante).</summary>
        public string? DestinatarioPrincipalNombre { get; set; }

        /// <summary>false = el correo no se manda a su destinatario principal, solo a los configurados.</summary>
        public bool DestinatarioPrincipalActivo { get; set; } = true;

        /// <summary>true = la pantalla muestra el interruptor maestro de este correo.</summary>
        public bool PermiteDesactivarEnvio { get; set; }

        /// <summary>true = la pantalla muestra el interruptor del destinatario principal.</summary>
        public bool PermiteDesactivarPrincipal { get; set; }

        /// <summary>
        /// Destinatarios configurados (ga_correo_regla). NO incluye al principal: ese no es una
        /// fila de la tabla sino una propiedad del propio correo, y la pantalla lo dibuja aparte
        /// con <see cref="DestinatarioPrincipalActivo"/>.
        /// </summary>
        public List<CorreoDestinatarioDto> Destinatarios { get; set; } = new();
    }

    /// <summary>
    /// Un destinatario configurado, ya resuelto para mostrarlo: además de lo que está guardado
    /// trae a quién le llega hoy, para que la pantalla no tenga que cruzar catálogos.
    /// </summary>
    public class CorreoDestinatarioDto
    {
        /// <summary>ga_correo_regla.id.</summary>
        public int Id { get; set; }
        /// <summary>TRABAJADOR / AREA / CORREO.</summary>
        public string TipoCodigo { get; set; } = string.Empty;

        /// <summary>Nombre para mostrar: el del trabajador, el del área, o el propio correo.</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Dirección literal (tipo CORREO) o el correo corporativo del trabajador. Null en AREA.</summary>
        public string? Email { get; set; }

        /// <summary>Solo en AREA: a cuántos correos se expande hoy. Null en los otros tipos.</summary>
        public int? Miembros { get; set; }

        public int? WorkerId { get; set; }
        public int? AreaScopeId { get; set; }
        public bool IncluirDescendientes { get; set; } = true;
        public bool Active { get; set; } = true;

        /// <summary>true = está activo pero hoy no resuelve a ningún correo, así que no le llega a nadie.</summary>
        public bool SinCorreo { get; set; }
    }

    public class CorreoWorkerOptionDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    public class CorreoAreaOptionDto
    {
        public int AreaScopeId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        /// <summary>Correo de grupo del área (area_scope.email), informativo. Puede ser null.</summary>
        public string? Email { get; set; }
    }

    // ── Escritura ────────────────────────────────────────────────────────────
    // Las operaciones son granulares (una por acción de la pantalla) y no un
    // reemplazo completo de la lista: los interruptores guardan al momento de
    // tocarlos, así que un PUT con la lista entera pisaría lo que otro editor
    // acabara de cambiar en otra fila.

    /// <summary>Cuerpo de los endpoints que solo prenden o apagan algo.</summary>
    public class CorreoActiveUpdateDto
    {
        public bool Active { get; set; }
    }

    /// <summary>Alta o edición de un destinatario configurado.</summary>
    public class CorreoDestinatarioInputDto
    {
        /// <summary>TRABAJADOR / AREA / CORREO.</summary>
        public string TipoCodigo { get; set; } = string.Empty;
        public int? WorkerId { get; set; }
        public int? AreaScopeId { get; set; }
        public string? Correo { get; set; }
        /// <summary>Solo aplica a AREA: si true, también los trabajadores de sus sub-áreas.</summary>
        public bool IncluirDescendientes { get; set; } = true;
    }
}
