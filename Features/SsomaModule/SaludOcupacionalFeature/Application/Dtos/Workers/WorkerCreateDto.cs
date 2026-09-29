using Abril_Backend.Shared.Services.Actores.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers
{
    public class WorkerCreateDto
    {
        public string ApellidoNombre { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public string? Celular { get; set; }
        public string? EmailCorporativo { get; set; }
        /// <summary>Correo personal / de contacto. Va a <c>person.email</c> y puede repetirse.</summary>
        public string? EmailPersonal { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        /// <summary>
        /// Checkbox "Mostrar en el boletín" (<c>person.mostrar_en_boletin</c>): true = su
        /// cumpleaños aparece en el calendario del boletín. null = el formulario no gestiona el
        /// campo (contratistas, que no capturan fecha de nacimiento) y se deja lo que ya estuviera
        /// guardado; en una persona nueva queda en true, el valor por defecto de la columna.
        /// </summary>
        public bool? MostrarEnBoletin { get; set; }
        public string? Sexo { get; set; }
        public DateOnly? FechaIngreso { get; set; }
        /// <summary>
        /// FK a <c>puesto</c>: el campo de presentación del trabajador y el único camino a su
        /// categoría (<c>puesto.categoria_id</c>). La categoría no se manda: cambiarla es
        /// cambiar de puesto, o cambiarle la categoría al puesto desde Configuración →
        /// Categorías y Puestos.
        /// </summary>
        public int? PuestoId { get; set; }
        // El área NO se manda: es la de destino del puesto (puesto.area_destino_scope_id), y el
        // backend deriva de ella los campos legacy Area/Subarea/Jefatura que lleguen en null.
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
        /// <summary>
        /// "DNI" o "CE". No se persiste en BD — se usa solo para validación.
        /// Si no se envía, se infiere del formato: 8 dígitos = DNI, resto = CE.
        /// </summary>
        public string? TipoDocumento { get; set; }
        public int? AniosExperiencia { get; set; }
        /// <summary>
        /// true = el formulario gestiona los actores del trabajador (quién aprueba su salida, qué jefe
        /// se entera, quién revisa su planilla, quiénes la consolidan y quiénes firman su consolidado)
        /// y <see cref="ActoresPersonalizados"/> manda: lo que viene se guarda y lo que no viene se
        /// quita, para que ese actor vuelva a salir de su área. false (por defecto) = el formulario no
        /// muestra la sección (contratistas) y no se toca lo que ya estuviera guardado.
        /// </summary>
        public bool GestionaActores { get; set; } = false;

        /// <summary>Lo personalizado en la ficha, por actor y en orden.</summary>
        public List<ActorPersonalizadoInputDto> ActoresPersonalizados { get; set; } = new();
    }
}
