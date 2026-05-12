namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos
{
    public class SolicitudSalidaListItemDto
    {
        public int Id { get; set; }
        public DateOnly FechaSalida { get; set; }
        public TimeOnly HoraSalida { get; set; }
        public TimeOnly? HoraRetorno { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
