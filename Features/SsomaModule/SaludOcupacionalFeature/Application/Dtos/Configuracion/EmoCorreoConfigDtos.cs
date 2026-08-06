namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Configuracion
{
    /// <summary>Códigos del catálogo <c>ss_emo_correo_tipo</c>.</summary>
    public static class EmoCorreoTipoCodigo
    {
        public const string Principal = "PRINCIPAL";
        public const string Copia     = "COPIA";
    }

    /// <summary>Códigos de los destinatarios fijos/dinámicos (filas con <c>editable = false</c>).</summary>
    public static class EmoCorreoDestinatarioCodigo
    {
        /// <summary>Expande a los emails de contacto de la clínica de la programación.</summary>
        public const string Clinica = "CLINICA";
    }

    /// <summary>Una fila de la pantalla de configuración de correos de EMO.</summary>
    public class EmoCorreoDestinatarioDto
    {
        public int Id { get; set; }
        /// <summary>PRINCIPAL o COPIA.</summary>
        public string Tipo { get; set; } = string.Empty;
        public string? Codigo { get; set; }
        public string? Email { get; set; }
        public string? Nombre { get; set; }
        public string? Descripcion { get; set; }
        /// <summary>false = fila fija: solo se puede prender/apagar.</summary>
        public bool Editable { get; set; }
        public bool Active { get; set; }
        public int Orden { get; set; }
    }

    /// <summary>
    /// Respuesta única de la pantalla: ambas listas en una sola petición
    /// (destinatarios de "Para" y de "CC").
    /// </summary>
    public class EmoCorreosConfigDto
    {
        public List<EmoCorreoDestinatarioDto> Principales { get; set; } = new();
        public List<EmoCorreoDestinatarioDto> Copias { get; set; } = new();
    }

    public class EmoCorreoDestinatarioCreateDto
    {
        /// <summary>PRINCIPAL o COPIA.</summary>
        public string Tipo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Nombre { get; set; }
    }

    public class EmoCorreoDestinatarioUpdateDto
    {
        public string Email { get; set; } = string.Empty;
        public string? Nombre { get; set; }
    }

    public class EmoCorreoActiveDto
    {
        public bool Active { get; set; }
    }

    /// <summary>
    /// Configuración ya resuelta para el envío: la usan la programación manual
    /// (ProgramacionEmoRepository) y la automática (EmoAutoProgramacionService).
    /// </summary>
    public class EmoCorreoEnvioConfigDto
    {
        /// <summary>true = agregar los emails de contacto de la clínica al "Para".</summary>
        public bool IncluirClinica { get; set; } = true;
        /// <summary>Correos fijos configurados que van en "Para".</summary>
        public List<string> Principales { get; set; } = new();
        /// <summary>Correos configurados que van en "CC".</summary>
        public List<string> Copias { get; set; } = new();
    }
}
