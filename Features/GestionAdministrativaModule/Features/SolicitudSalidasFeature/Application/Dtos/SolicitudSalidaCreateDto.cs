namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos
{
    public class SolicitudSalidaCapturaDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public DateTimeOffset UploadedAt { get; set; }
    }

    public class TrayectoDetalleDto
    {
        public int Id { get; set; }
        public int Orden { get; set; }
        public TimeOnly HoraSalida { get; set; }
        public TimeOnly? HoraRetorno { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        public List<SolicitudSalidaCapturaDto> Capturas { get; set; } = new();
    }

    public class SolicitudSalidaDetalleDto
    {
        public int Id { get; set; }
        public DateOnly FechaSalida { get; set; }
        public string EstadoAprobacion { get; set; } = string.Empty;
        public string EstadoRendicion { get; set; } = "No rendido";
        public DateTimeOffset CreatedAt { get; set; }
        public string? MotivoRechazo { get; set; }
        public List<TrayectoDetalleDto> Trayectos { get; set; } = new();
    }

    public class TrayectoCreateDto
    {
        public TimeOnly HoraSalida { get; set; }
        public TimeOnly? HoraRetorno { get; set; }

        /// <summary>Id de ga_motivo_salida. Nulo cuando el usuario elige "Otro motivo".</summary>
        public int? MotivoId { get; set; }
        /// <summary>Texto libre cuando MotivoId es nulo.</summary>
        public string? MotivoLibre { get; set; }

        /// <summary>Id de ga_lugar. Nulo cuando el usuario elige "Otro lugar".</summary>
        public int? LugarOrigenId { get; set; }
        public string? LugarOrigenLibre { get; set; }

        /// <summary>Id de ga_lugar. Nulo cuando el usuario elige "Otro lugar".</summary>
        public int? LugarDestinoId { get; set; }
        public string? LugarDestinoLibre { get; set; }
    }

    public class SolicitudSalidaCreateDto
    {
        public DateOnly FechaSalida { get; set; }
        public List<TrayectoCreateDto> Trayectos { get; set; } = new();
    }
}
