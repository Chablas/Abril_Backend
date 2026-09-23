using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.CursoModule.Infrastructure.Models
{
    [Table("curso_intento_respuesta")]
    public class CursoIntentoRespuesta
    {
        public int Id { get; set; }
        public int CursoIntentoId { get; set; }
        public int CursoSlideId { get; set; }

        /// <summary>Respuesta del usuario (jsonb), en la misma forma que "respuestaCorrecta" de la slide.</summary>
        public string RespuestaJson { get; set; } = "{}";

        public bool? EsCorrecta { get; set; }
        public decimal? PuntajeObtenido { get; set; }
        public int? TiempoRespuestaSeg { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
