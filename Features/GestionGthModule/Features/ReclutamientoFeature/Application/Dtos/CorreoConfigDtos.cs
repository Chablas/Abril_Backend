namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    // ────────────────────────── Pantalla de configuración ──────────────────────────

    /// <summary>Una fila de la sección de un correo: un destinatario con su interruptor.</summary>
    public class CorreoDestinatarioFilaDto
    {
        public int DestinatarioId { get; set; }

        /// <summary>Código del destinatario dinámico; null en los correos adicionales.</summary>
        public string? Codigo { get; set; }

        /// <summary>Nombre para mostrar. Los adicionales pueden no tenerlo (se muestra el correo).</summary>
        public string? Nombre { get; set; }

        /// <summary>Texto de ayuda: de dónde sale el correo.</summary>
        public string? Descripcion { get; set; }

        /// <summary>Correo guardado; null en los dinámicos (se resuelve al enviar).</summary>
        public string? Email { get; set; }

        /// <summary>true = va en copia (CC); false = destinatario principal (Para).</summary>
        public bool EsCopia { get; set; }

        /// <summary>Interruptor de la fila.</summary>
        public bool Active { get; set; }

        /// <summary>true = se puede editar el correo desde la pantalla (solo los adicionales).</summary>
        public bool Editable { get; set; }

        /// <summary>true = se puede eliminar la fila (solo los adicionales).</summary>
        public bool Eliminable { get; set; }

        /// <summary>
        /// Correo al que se resuelve hoy un destinatario dinámico, para que la pantalla muestre
        /// a quién le va a llegar de verdad. Null si todavía no se puede resolver.
        /// </summary>
        public string? EmailResuelto { get; set; }

        /// <summary>Nombre de la persona detrás de <see cref="EmailResuelto"/>, cuando se conoce.</summary>
        public string? NombreResuelto { get; set; }

        /// <summary>
        /// true = es dinámico y hoy no resuelve a ningún correo (no hay Gerente General cargado,
        /// el área de GTH no tiene correo, …), así que aunque esté activo no le llega a nadie.
        /// </summary>
        public bool SinCorreo { get; set; }

        /// <summary>
        /// true = se resuelve recién al enviar porque depende de la solicitud (el gerente del
        /// área del solicitante cambia según quién pida). La pantalla no puede previsualizarlo.
        /// </summary>
        public bool DependeDeLaSolicitud { get; set; }

        public int Orden { get; set; }
    }

    /// <summary>Una sección de la pantalla: un correo con su interruptor maestro y sus destinatarios.</summary>
    public class CorreoConfigEventoDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }

        /// <summary>Interruptor maestro: false = este correo no se envía, sin importar los destinatarios.</summary>
        public bool Active { get; set; }

        /// <summary>
        /// true = el destinatario principal lo agrega el backend solo (ej. la long list siempre va
        /// al solicitante), así que la pantalla no debe advertir que falta uno.
        /// </summary>
        public bool PrincipalAutomatico { get; set; }

        public int Orden { get; set; }
        public List<CorreoDestinatarioFilaDto> Destinatarios { get; set; } = new();
    }

    /// <summary>Respuesta única de la pantalla de Configuración de correos, en una sola petición.</summary>
    public class CorreoConfigDto
    {
        public List<CorreoConfigEventoDto> Eventos { get; set; } = new();
    }

    // ────────────────────────────── Envío ──────────────────────────────

    /// <summary>Persona o buzón al que resuelve hoy un destinatario dinámico.</summary>
    public class CorreoDestinatarioResueltoDto
    {
        public string Email { get; set; } = string.Empty;
        public string? Nombre { get; set; }
    }

    /// <summary>
    /// Fila de configuración lista para el envío: ya filtrada por correo activo y destinatario
    /// activo. El resolver la expande a un correo concreto según su <see cref="Codigo"/>.
    /// </summary>
    public class CorreoDestinatarioEnvioDto
    {
        /// <summary>Código del destinatario dinámico; null en los correos adicionales.</summary>
        public string? Codigo { get; set; }
        /// <summary>Correo guardado; null en los dinámicos.</summary>
        public string? Email { get; set; }
        public string? Nombre { get; set; }
        public bool EsCopia { get; set; }
        public int Orden { get; set; }
    }

    // ────────────────────────────── Escritura ──────────────────────────────

    /// <summary>Alta de un correo adicional dentro de la sección de un correo.</summary>
    public class CorreoAdicionalCreateDto
    {
        /// <summary>Código del correo (APROBACION_GG, SOLICITUD, …) desde cuya sección se agrega.</summary>
        public string EventoCodigo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Nombre { get; set; }
        /// <summary>true = copia (CC); false = destinatario principal (Para).</summary>
        public bool EsCopia { get; set; }
    }

    /// <summary>Edición de un correo adicional.</summary>
    public class CorreoAdicionalUpdateDto
    {
        public string Email { get; set; } = string.Empty;
        public string? Nombre { get; set; }
        public bool EsCopia { get; set; }
    }

    /// <summary>Prender o apagar un destinatario, o el correo completo.</summary>
    public class CorreoActivePatchDto
    {
        public bool Active { get; set; }
    }
}
