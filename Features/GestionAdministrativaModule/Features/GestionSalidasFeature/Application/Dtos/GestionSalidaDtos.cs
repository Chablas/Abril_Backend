namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos
{
    public class GestionSalidaListItemDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        public DateOnly FechaSalida { get; set; }
        public TimeOnly HoraSalida { get; set; }
        public TimeOnly? HoraRetorno { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class GestionSalidaFiltersDto
    {
        public int? WorkerId { get; set; }
        public int? LugarProyectoId { get; set; }
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
}
