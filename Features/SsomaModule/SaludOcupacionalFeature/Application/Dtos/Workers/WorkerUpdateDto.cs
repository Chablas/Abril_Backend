namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers
{
    public class WorkerUpdateDto
    {
        public string ApellidoNombre { get; set; } = string.Empty;
        public string? Celular { get; set; }
        public string? EmailPersonal { get; set; }
        public string? EmailCorporativo { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public string? Sexo { get; set; }
        public DateOnly? FechaIngreso { get; set; }
        public string? Categoria { get; set; }
        public string? Ocupacion { get; set; }
        public int? OcupacionId { get; set; }
        public string? Area { get; set; }
        public string? Subarea { get; set; }
        public string? ContrataCasa { get; set; }
        public string? ObraOficina { get; set; }
        public string? Jefatura { get; set; }
        public string? Ruc { get; set; }
        public string? Procedencia { get; set; }
        public string? CondicionMedica { get; set; }
        public string? Notas { get; set; }
        public bool Sctr { get; set; } = false;
        public bool HabilitadoObra { get; set; } = false;
        public int? EmpresaId { get; set; }
        public int? ProyectoId { get; set; }
        public int? AniosExperiencia { get; set; }
    }
}
