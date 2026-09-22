using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.CursoModule.Infrastructure.Models
{
    /// <summary>
    /// Evidencia de auditoría legal (SUNAFIL) de un intento de curso: 1:1 con CursoIntento.
    /// No simplificar — cada campo respalda que el intento fue realizado por la persona,
    /// en el lugar/momento declarado, con su consentimiento explícito.
    /// </summary>
    [Table("curso_intento_evidencia")]
    public class CursoIntentoEvidencia
    {
        public int Id { get; set; }
        public int CursoIntentoId { get; set; }

        /// <summary>Siempre capturada en backend vía HttpContext.Connection.RemoteIpAddress, nunca enviada por el cliente.</summary>
        public string IpAddress { get; set; } = string.Empty;

        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }
        public decimal? PrecisionGpsMetros { get; set; }

        /// <summary>Tomado de los headers de la request (User-Agent), no del body.</summary>
        public string UserAgent { get; set; } = string.Empty;

        public string? DeviceFingerprint { get; set; }

        public bool DeclaracionJuradaAceptada { get; set; }
        public string DeclaracionTexto { get; set; } = string.Empty;

        /// <summary>SHA-256 de respuestas+resultado+timestamps+userId, calculado en backend al finalizar (sella el intento).</summary>
        public string? HashSha256 { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SelladoAt { get; set; }
    }
}
