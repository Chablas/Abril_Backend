namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Infrastructure.Models
{
    /// <summary>
    /// Tabla filtro que decide qué area_item puede usarse en Lecciones Aprendidas.
    /// area_item_id apunta a la fuente global (area_item); active = true significa
    /// que esa área aparece como opción en /mejora-continua/lessons-learned.
    /// </summary>
    public class LessonArea
    {
        public int LessonAreaId { get; set; }
        public int AreaItemId { get; set; }
        public bool Active { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
