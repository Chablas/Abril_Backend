namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos
{
    public class SolicitudSalidaCreateDto
    {
        public DateOnly FechaSalida { get; set; }
        public TimeOnly HoraSalida { get; set; }
        public TimeOnly? HoraRetorno { get; set; }

        /// <summary>Id de ga_motivo_salida. Nulo cuando el usuario elige "Otro motivo".</summary>
        public int? MotivoId { get; set; }
        /// <summary>Texto libre cuando MotivoId es nulo.</summary>
        public string? MotivoLibre { get; set; }

        /// <summary>Id de ga_lugar. Nulo cuando el usuario elige "Otro lugar".</summary>
        public int? LugarOrigenId { get; set; }
        /// <summary>Texto libre cuando LugarOrigenId es nulo.</summary>
        public string? LugarOrigenLibre { get; set; }

        /// <summary>Id de ga_lugar. Nulo cuando el usuario elige "Otro lugar".</summary>
        public int? LugarDestinoId { get; set; }
        /// <summary>Texto libre cuando LugarDestinoId es nulo.</summary>
        public string? LugarDestinoLibre { get; set; }
    }
}
