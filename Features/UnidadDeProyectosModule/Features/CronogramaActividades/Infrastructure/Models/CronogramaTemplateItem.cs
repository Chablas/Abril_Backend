namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// Ítem editable de una plantilla de cronograma (ANTEPROYECTO/PROYECTO/PROYECTO_ACTUALIZACION),
    /// usado por AplicarPlantillaAsync para generar las actividades reales de un proyecto.
    /// </summary>
    public class CronogramaTemplateItem
    {
        public int Id { get; set; }
        public string TipoCronograma { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int Nivel { get; set; }
        public bool EsPadre { get; set; }
        public string? ParentCodigo { get; set; }
        public string? PredecesoraCodigo { get; set; }
        public int Orden { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
