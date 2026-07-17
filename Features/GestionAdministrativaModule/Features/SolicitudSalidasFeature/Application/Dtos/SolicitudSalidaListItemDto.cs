namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos
{
    public class SolicitudSalidaListItemDto
    {
        public int Id { get; set; }
        public DateOnly FechaSalida { get; set; }

        // ── Datos agregados del/los trayecto(s) para vista de tabla ─────
        /// <summary>Hora de salida del primer trayecto.</summary>
        public TimeOnly HoraSalida { get; set; }
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
    }
}
