namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models
{
    /// <summary>
    /// Un tramo individual de una solicitud de salida — una solicitud puede tener N trayectos
    /// (encadenados: el origen del trayecto N+1 suele ser el destino del N, pero se almacena
    /// independientemente para flexibilidad).
    /// </summary>
    public class GaSolicitudTrayecto
    {
        public int Id { get; set; }
        public int SolicitudId { get; set; }
        /// <summary>Orden secuencial dentro de la solicitud (0-based).</summary>
        public int Orden { get; set; }
        public TimeOnly HoraSalida { get; set; }
        public TimeOnly? HoraRetorno { get; set; }
        public int? MotivoId { get; set; }
        public string? MotivoLibre { get; set; }
        public int? LugarOrigenId { get; set; }
        public string? LugarOrigenLibre { get; set; }
        public int? LugarDestinoId { get; set; }
        public string? LugarDestinoLibre { get; set; }
    }
}
