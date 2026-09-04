namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos
{
    public class SolicitudSalidaListItemDto
    {
        public int Id { get; set; }
        /// <summary>Código SOL-AAAA-NNNN. Null solo en solicitudes anteriores a la columna.</summary>
        public string? Codigo { get; set; }
        public DateOnly FechaSalida { get; set; }

        // ── Datos agregados del/los trayecto(s) para vista de tabla ─────
        /// <summary>Hora de salida del primer trayecto. Null si el motivo no pide horario.</summary>
        public TimeOnly? HoraSalida { get; set; }
        /// <summary>Hora de retorno del último trayecto.</summary>
        public TimeOnly? HoraRetorno { get; set; }
        /// <summary>Motivo del primer trayecto.</summary>
        public string Motivo { get; set; } = string.Empty;
        /// <summary>Origen del primer trayecto.</summary>
        public string? LugarOrigen { get; set; }
        /// <summary>Destino del último trayecto.</summary>
        public string? LugarDestino { get; set; }
        /// <summary>Cantidad total de trayectos (≥ 1).</summary>
        public int TrayectosCount { get; set; }

        public string EstadoAprobacion { get; set; } = string.Empty;
        public string EstadoRendicion { get; set; } = "No rendido";
        public DateTimeOffset CreatedAt { get; set; }
        /// <summary>True si todos los trayectos están cubiertos (captura por trayecto, o catálogo TI) — habilita la rendición.</summary>
        public bool PuedeRendirse { get; set; }

        /// <summary>
        /// True si al menos un trayecto lleva un motivo marcado como reembolsable en
        /// Configuración → Motivos (<c>ga_motivo_salida.es_reembolsable</c>). Sin eso la salida no
        /// genera gasto de movilidad y no hay nada que rendir. El motivo libre no concede.
        /// </summary>
        public bool EsReembolsable { get; set; }

        /// <summary>
        /// Último día para rendir esta salida: el 7.º día hábil del mes siguiente al de su
        /// <c>fecha_salida</c> (sin sábados, domingos ni los feriados de Configuración → Feriados).
        /// </summary>
        public DateOnly PlazoRendicionHasta { get; set; }

        /// <summary>
        /// True si el plazo ya pasó. La salida deja de poder rendirse, pero su detalle se sigue viendo.
        /// </summary>
        public bool PlazoVencido { get; set; }

        /// <summary>
        /// True si la salida está lista para rendirse: aprobada, no rendida, con los trayectos
        /// cubiertos, con motivo reembolsable y dentro del plazo. Lo calcula el backend para que el
        /// desplegable "Mes a rendir", la selección de filas y las tarjetas usen la misma definición.
        /// </summary>
        public bool AptaParaRendir { get; set; }

        // ── Reembolso ────────────────────────────────────────────────────
        // Solo informativo en esta pantalla: el reembolso se gestiona por PLANILLA, y adjuntar el
        // Consolidado del S10 o avisarle al revisor son acciones de Mis Rendiciones.

        /// <summary>"Pendiente" | "Aprobado" | "Rechazado" | "Firmado" | "Pagado".</summary>
        public string EstadoReembolso { get; set; } = "Pendiente";

        /// <summary>Observación del jefe al rechazar el reembolso: es lo que hay que subsanar.</summary>
        public string? ObservacionReembolso { get; set; }
    }
}
