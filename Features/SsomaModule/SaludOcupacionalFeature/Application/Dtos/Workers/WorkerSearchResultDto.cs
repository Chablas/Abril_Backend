namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers
{
    public class WorkerSearchResultDto
    {
        public int Id { get; set; }
        public string? ApellidoNombre { get; set; }
        public string? Dni { get; set; }
        public string? EmailCorporativo { get; set; }
        public string? Ocupacion { get; set; }
        public string? Categoria { get; set; }
        public string? Cargo { get; set; }
        /// <summary>FK a workers_obra_oficina_staff — clasificación de riesgo actual, ya
        /// gestionada exclusivamente desde Habilitación (Cambiar obra / puesto de trabajo).</summary>
        public int? ObraOficinaStaffId { get; set; }
        public string? ObraOficinaStaffNombre { get; set; }
        public int? EmpresaActualId { get; set; }
        public string? EmpresaActual { get; set; }
        public bool Activo { get; set; }
        public int? AniosExperiencia { get; set; }
        public DateOnly? FechaIngreso { get; set; }
        public bool InhabilitadoSsoma { get; set; }
        public bool EsAbril { get; set; }
    }
}
