namespace Abril_Backend.Application.DTOs.ArquitecturaComercial
{
    public class ActividadListItemDTO
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string? ProjectNombre { get; set; }
        public int? Indice { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Tipo { get; set; }
        public int? EtapaId { get; set; }
        public string? EtapaNombre { get; set; }
        public int? UserId { get; set; }
        public string? ResponsableNombre { get; set; }
        public string? Encargado1 { get; set; }
        public DateOnly? InicioProgramado { get; set; }
        public DateOnly? FinProgramado { get; set; }
        public DateOnly? InicioEfectivo { get; set; }
        public DateOnly? FinEfectivo { get; set; }
        public string? Observaciones { get; set; }
        public bool Activo { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int? Retraso { get; set; }
    }
}
