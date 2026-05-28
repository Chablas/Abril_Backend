namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Application.Dtos
{
    /// <summary>Fila mostrada en la pantalla de configuración de áreas para Lecciones.</summary>
    public class LessonAreaConfigItemDTO
    {
        public int? LessonAreaId { get; set; } // null si todavía no se ha togglado
        public int AreaItemId { get; set; }
        public string AreaItemName { get; set; } = string.Empty;
        public string AreaTypeName { get; set; } = string.Empty;
        public string? ParentName { get; set; }
        public bool Active { get; set; }
    }

    public class ToggleLessonAreaResultDTO
    {
        public int LessonAreaId { get; set; }
        public bool Active { get; set; }
    }
}
