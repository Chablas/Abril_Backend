namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Infrastructure.Models
{
    /// <summary>
    /// Tabla filtro que decide qué rama de area_scope puede usarse en Lecciones Aprendidas.
    /// area_scope_id apunta a un nodo del árbol (típicamente una hoja); active = true
    /// significa que esa rama aparece como opción en /mejora-continua/lessons-learned.
    /// </summary>
    public class LessonArea
    {
        public int LessonAreaId { get; set; }
        public int AreaScopeId { get; set; }
        public bool Active { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
