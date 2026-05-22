namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos
{
    public class GestionSalidaListItemDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        public DateOnly FechaSalida { get; set; }
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
        public int TrayectosCount { get; set; }
        public string EstadoAprobacion { get; set; } = string.Empty;
        public string EstadoRendicion { get; set; } = "No rendido";
        public DateTimeOffset CreatedAt { get; set; }
        /// <summary>True si todos los trayectos tienen al menos una captura — habilita la rendición.</summary>
        public bool PuedeRendirse { get; set; }
    }

    public class GestionSalidaFiltersDto
    {
        public int? WorkerId { get; set; }
        public int? LugarProyectoId { get; set; }
        public string? EstadoRendicion { get; set; }
    }

    public class MarcarRendidasBulkDto
    {
        public List<int> Ids { get; set; } = new();
    }

    public class GestionSalidaFilterDataDto
    {
        public List<TrabajadorOptionDto> Trabajadores { get; set; } = new();
        public List<LugarProyectoOptionDto> LugaresProyecto { get; set; } = new();
    }

    public class TrabajadorOptionDto
    {
        public int WorkerId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
    }

    public class LugarProyectoOptionDto
    {
        public int GaLugarId { get; set; }
        public string NombreDisplay { get; set; } = string.Empty;
    }

    public class AprobarRechazarDto { }

    public class GestionSalidaCapturaDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public DateTimeOffset UploadedAt { get; set; }
    }

    public class GestionSalidaTrayectoDto
    {
        public int Id { get; set; }
        public int Orden { get; set; }
        public TimeOnly HoraSalida { get; set; }
        public TimeOnly? HoraRetorno { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        public List<GestionSalidaCapturaDto> Capturas { get; set; } = new();
    }

    public class GestionSalidaDetalleDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        public DateOnly FechaSalida { get; set; }
        public string EstadoAprobacion { get; set; } = string.Empty;
        public string EstadoRendicion { get; set; } = "No rendido";
        public DateTimeOffset CreatedAt { get; set; }
        public string? MotivoRechazo { get; set; }
        public GestionSalidaRendicionDto? Rendicion { get; set; }
        public List<GestionSalidaTrayectoDto> Trayectos { get; set; } = new();
    }

    public class GestionSalidaRendicionDto
    {
        public int Id { get; set; }
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;
        public DateTimeOffset RendidoAt { get; set; }
    }

    /// <summary>Una fila del PDF de planilla — un registro = UN TRAYECTO (no una solicitud).</summary>
    public class RendicionItemDto
    {
        /// <summary>Trayecto ID.</summary>
        public int Id { get; set; }
        /// <summary>Solicitud a la que pertenece este trayecto — para agrupar el TOTAL al final.</summary>
        public int SolicitudId { get; set; }
        public int WorkerId { get; set; }
        public string TrabajadorNombre { get; set; } = string.Empty;
        public string? TrabajadorDni { get; set; }
        public string? Area { get; set; }
        public DateOnly FechaSalida { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        /// <summary>Razón social de la empresa a la que está afiliado el trabajador.</summary>
        public string? RazonSocial { get; set; }
        public string? Ruc { get; set; }
        /// <summary>Suma de los montos de las capturas de este trayecto (columna IMPORTE).</summary>
        public decimal Importe { get; set; }
    }
}
