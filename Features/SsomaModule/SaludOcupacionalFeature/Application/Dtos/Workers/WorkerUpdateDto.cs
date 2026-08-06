namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers
{
    public class WorkerUpdateDto
    {
        public string ApellidoNombre { get; set; } = string.Empty;
        public string? Celular { get; set; }
        public string? EmailCorporativo { get; set; }
        /// <summary>Correo personal / de contacto. Va a <c>person.email</c> y puede repetirse.</summary>
        public string? EmailPersonal { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public string? Sexo { get; set; }
        public DateOnly? FechaIngreso { get; set; }
        public string? Categoria { get; set; }
        public string? Ocupacion { get; set; }
        public int? OcupacionId { get; set; }
        /// <summary>Nombre del puesto final (autocompletado de Categoría + Ocupación, editable).</summary>
        public string? Puesto { get; set; }
        /// <summary>
        /// Nodo del árbol de áreas elegido en el formulario (workers.area_scope_id). Cuando viene,
        /// es la fuente de verdad del área: el backend deriva de él los campos legacy
        /// Area/Subarea/Jefatura y se ignora lo que llegue en esos tres.
        /// </summary>
        public int? AreaScopeId { get; set; }
        public string? Area { get; set; }
        public string? Subarea { get; set; }
        public string? ContrataCasa { get; set; }
        /// <summary>FK a <c>workers_obra_oficina_staff</c> (Obra / Staff / Oficina Central).</summary>
        public int? ObraOficinaStaffId { get; set; }
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
