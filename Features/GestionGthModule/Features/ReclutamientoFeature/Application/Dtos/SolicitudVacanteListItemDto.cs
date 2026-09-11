namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Fila de la tabla "Mis solicitudes de vacante": un requerimiento (vacante) del usuario
    /// logueado. Cada vacante de una solicitud es una fila con su propio código y estado.
    /// </summary>
    public class SolicitudVacanteListItemDto
    {
        public int RequerimientoId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        /// <summary>Puesto solicitado (columna "Puesto").</summary>
        public string Puesto { get; set; } = string.Empty;
        /// <summary>Justificación general de la solicitud (subtítulo bajo el puesto).</summary>
        public string? Justificacion { get; set; }
        /// <summary>Área del solicitante (snapshot al registrar).</summary>
        public string? Area { get; set; }
        /// <summary>Proyecto/obra destino de la vacante.</summary>
        public string? ProyectoObra { get; set; }
        /// <summary>Fecha de envío (created) en hora Perú (UTC-5).</summary>
        public DateTime Enviado { get; set; }
        /// <summary>Código estable del estado (p.ej. NUEVO) — para lógica de front.</summary>
        public string EstadoCodigo { get; set; } = string.Empty;
        /// <summary>Nombre legible del estado (para mostrar en el badge).</summary>
        public string EstadoNombre { get; set; } = string.Empty;
        /// <summary>
        /// Quién registró la solicitud. La tabla ya no muestra solo lo propio sino lo del área
        /// completa, así que la fila tiene que decir de quién es el pedido. Null si el usuario que
        /// la registró no tiene ficha de trabajador.
        /// </summary>
        public string? Solicitante { get; set; }

        /// <summary>
        /// Tipo de requerimiento tal como se muestra (Nuevo / Reemplazo): la columna «Tipo». Es el
        /// nombre del catálogo, que se puede renombrar desde Configuración.
        /// </summary>
        public string TipoRequerimiento { get; set; } = string.Empty;

        /// <summary>
        /// Código estable del tipo (<c>NUEVO</c> / <c>REEMPLAZO</c>): es el que decide cómo se pinta
        /// el tipo en la tabla. El nombre de al lado es presentación.
        /// </summary>
        public string TipoRequerimientoCodigo { get; set; } = string.Empty;

        /// <summary>
        /// true = ingreso directo <b>FFT</b>. Va junto al tipo porque es lo otro que cambia el camino
        /// de la vacante: no la firma nadie y pasa derecho al EMO de ingreso.
        /// </summary>
        public bool EsFft { get; set; }
    }
}
