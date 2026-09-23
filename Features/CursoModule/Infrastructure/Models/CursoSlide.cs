using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.CursoModule.Infrastructure.Models
{
    [Table("curso_slide")]
    public class CursoSlide
    {
        public int Id { get; set; }
        public int CursoId { get; set; }
        public int Orden { get; set; }

        /// <summary>Código libre del tipo de slide (no enum): "contenido_texto", "pregunta_vf", "pregunta_arrastrar",
        /// "pregunta_imagen", "pregunta_opcion_multiple", "pregunta_ordenar", "pregunta_completar", etc.</summary>
        public string TipoCodigo { get; set; } = string.Empty;

        public bool EsEvaluable { get; set; }
        public decimal? Puntaje { get; set; }

        /// <summary>"igualdad_exacta" | "sin_calificar".</summary>
        public string? ModoCorreccion { get; set; }

        /// <summary>
        /// Contenido/opciones completos de la slide (jsonb). Por convención, cuando EsEvaluable=true
        /// debe incluir la clave "respuestaCorrecta" para que el servicio de corrección genérica
        /// (ModoCorreccion="igualdad_exacta") pueda compararla contra la respuesta del usuario sin
        /// conocer el tipo de pregunta. Esa clave se elimina antes de enviar la slide al frontend.
        /// </summary>
        public string ConfiguracionJson { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
