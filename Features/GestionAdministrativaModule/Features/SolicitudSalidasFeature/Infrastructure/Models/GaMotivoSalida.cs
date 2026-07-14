namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models
{
    public class GaMotivoSalida
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
        /// <summary>Si true, al solicitar una salida con este motivo se exige un documento
        /// adjunto (a modo de prueba, ej. constancia de capacitación o cita médica).</summary>
        public bool RequiereAdjunto { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
