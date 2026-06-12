namespace Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Application.Dtos
{
    public class CronogramaActividadDto
    {
        public int CostosCronogramaActividadId { get; set; }
        public string Nombre { get; set; } = null!;
    }

    public class CronogramaActividadCreateDto
    {
        public string Nombre { get; set; } = null!;
    }

    /// <summary>
    /// Nodo del árbol identificado por la actividad (una actividad aparece a lo sumo
    /// una vez por cronograma; el padre se referencia por su actividad).
    /// </summary>
    public class CronogramaNodoDto
    {
        public int ActividadId { get; set; }
        public int? ParentActividadId { get; set; }
        public int Orden { get; set; }
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaFin { get; set; }
    }

    public class CronogramaSaveDto
    {
        public List<CronogramaNodoDto> Nodos { get; set; } = new();
    }

    public class CronogramaFormDataDto
    {
        public List<CronogramaActividadDto> Actividades { get; set; } = new();
        /// <summary>Nodos del cronograma existente de la adjudicación (vacío si aún no hay).</summary>
        public List<CronogramaNodoDto> Nodos { get; set; } = new();
    }
}
