namespace Abril_Backend.Features.GestionAdministrativa.Trayectos.Infrastructure.Models
{
    /// <summary>
    /// Catálogo de trayectos preconfigurados — asocia un par (lugar origen, lugar destino) con su
    /// monto referencial en soles. Sirve como fuente de datos para autocompletar montos en las
    /// solicitudes de salida. Distinto de <c>GaSolicitudTrayecto</c>, que representa el tramo
    /// real de una solicitud específica.
    /// </summary>
    public class GaTrayecto
    {
        public int Id { get; set; }
        public int LugarOrigenId { get; set; }
        public int LugarDestinoId { get; set; }
        public decimal Monto { get; set; }
        /// <summary>
        /// Si false, ninguna salida que use este par (origen, destino) genera reembolso de
        /// movilidad, aunque el motivo elegido si lo permita (ej. Oficina Central - Bosque Real,
        /// que la empresa cubre con movilidad propia). El trayecto solo puede QUITAR el
        /// reembolso: nunca lo concede por su cuenta si el motivo no lo da.
        /// </summary>
        public bool EsReembolsable { get; set; } = true;
        public bool Activo { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
