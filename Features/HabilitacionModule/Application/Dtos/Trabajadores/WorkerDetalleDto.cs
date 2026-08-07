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
        public string? EmailCorporativo { get; set; }
        /// <summary>Correo personal / de contacto (person.email).</summary>
        public string? EmailPersonal { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public string? Sexo { get; set; }
        public DateOnly? FechaIngreso { get; set; }
        public DateOnly? FechaRetiro { get; set; }
        public string? Categoria { get; set; }
        public string? Ocupacion { get; set; }
        public int? OcupacionId { get; set; }
        public string? Puesto { get; set; }
        /// <summary>
        /// Nodo del árbol de áreas asignado (workers.area_scope_id). Es lo que el formulario usa
        /// para precargar los desplegables de área; Area/Subarea son su equivalencia legacy.
        /// </summary>
        public int? AreaScopeId { get; set; }
        public string? Area { get; set; }
        public string? Subarea { get; set; }
        public string? ContrataCasa { get; set; }
        /// <summary>FK a <c>workers_obra_oficina_staff</c>. Fuente de verdad.</summary>
        public int? ObraOficinaStaffId { get; set; }
        /// <summary>Nombre del catálogo (solo lectura, derivado de <see cref="ObraOficinaStaffId"/>).</summary>
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
        /// <summary>
        /// Jefe elegido a mano para este trabajador (<c>workers_revisores</c>), que se sobrepone al
        /// revisor de su área. Null = no tiene y le corresponde el revisor del área. Es lo que
        /// precarga el checkbox "Jefe personalizado" del formulario.
        /// </summary>
        public int? JefePersonalizadoWorkerId { get; set; }
        public string? JefePersonalizadoNombre { get; set; }
        public string? JefePersonalizadoEmail { get; set; }
    }
}
