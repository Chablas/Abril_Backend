namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Application.Dtos
{
    public class LessonAreaSegmentDTO
    {
        public string AreaItemName { get; set; } = string.Empty;
        public string AreaTypeName { get; set; } = string.Empty;
    }

    /// <summary>Una fila por cada rama (hoja) de area_scope, con su path completo.</summary>
    public class LessonAreaConfigItemDTO
    {
        public int? LessonAreaId { get; set; } // null si todavía no se ha togglado
        public int AreaScopeId { get; set; }
        public List<LessonAreaSegmentDTO> Path { get; set; } = new();
        public bool Active { get; set; }
    }

    public class ToggleLessonAreaResultDTO
    {
        public int LessonAreaId { get; set; }
        public bool Active { get; set; }
    }
}
