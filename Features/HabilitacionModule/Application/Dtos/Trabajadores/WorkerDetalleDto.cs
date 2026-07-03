namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores
{
    public class WorkerDetalleDto
    {
        public int Id { get; set; }
        public int? IdTrabajador { get; set; }
        public string? ApellidoNombre { get; set; }
        public string? Dni { get; set; }
        public string? Ruc { get; set; }
        public string? Celular { get; set; }
        public string? EmailPersonal { get; set; }
        public string? EmailCorporativo { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public string? Sexo { get; set; }
        public DateOnly? FechaIngreso { get; set; }
        public DateOnly? FechaRetiro { get; set; }
        public string? Categoria { get; set; }
        public string? Ocupacion { get; set; }
        public int? OcupacionId { get; set; }
        public string? Puesto { get; set; }
        public string? Area { get; set; }
        public string? Subarea { get; set; }
        public string? ContrataCasa { get; set; }
        public string? ObraOficina { get; set; }
        public string? Jefatura { get; set; }
        public string? Estado { get; set; }
        public bool? HabilitadoObra { get; set; }
        public bool? Sctr { get; set; }
        public string? CondicionMedica { get; set; }
        public string? Procedencia { get; set; }
        public string? Notas { get; set; }
        public int? PuntosInfraccion { get; set; }
        public int? AniosExperiencia { get; set; }
    }
}
