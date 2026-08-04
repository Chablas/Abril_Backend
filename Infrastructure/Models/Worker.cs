using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Models
{
    [Table("workers")]
    public class Worker
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("id_trabajador")]
        public int? IdTrabajador { get; set; }

        [Column("person_id")]
        public int? PersonId { get; set; }

        [Column("contributor_id")]
        public int? ContributorId { get; set; }

        /*[Column("celular")]
        public string? Celular { get; set; }*/

        [Column("apellido_nombre")]
        public string? ApellidoNombre { get; set; }

        /// <summary>Correo corporativo @abril.pe del trabajador (antes llamado email_personal).</summary>
        [Column("email_corporativo")]
        public string? EmailCorporativo { get; set; }

        /// <summary>Celular corporativo del trabajador (el personal vive en <c>person.phone_number</c>).</summary>
        [Column("celular_corporativo")]
        public string? CelularCorporativo { get; set; }

        // La fecha de nacimiento dejó de vivir aquí: ahora es única en person.fecha_nacimiento.

        [Column("fecha_ingreso")]
        public DateOnly? FechaIngreso { get; set; }

        [Column("fecha_retiro")]
        public DateOnly? FechaRetiro { get; set; }

        [Column("categoria")]
        public string? Categoria { get; set; }

        /// <summary>
        /// FK al catálogo <c>workers_category</c>. Usado SOLO por Lecciones Aprendidas
        /// y Solicitud de Salidas (normaliza el texto de <see cref="Categoria"/>, que se
        /// conserva para el resto de funcionalidades).
        /// </summary>
        [Column("worker_category_id")]
        public int? WorkerCategoryId { get; set; }

        [Column("ocupacion")]
        public string? Ocupacion { get; set; }

        /// <summary>
        /// FK a <c>cat_ocupacion</c> (puesto de trabajo normalizado). Complementa a
        /// <see cref="Ocupacion"/> (texto libre, se conserva por compatibilidad) para
        /// permitir tabular accidentes/enfermedades por puesto en reportes como el
        /// informe anual DIGESA.
        /// </summary>
        [Column("ocupacion_id")]
        public int? OcupacionId { get; set; }

        [ForeignKey(nameof(OcupacionId))]
        public CatOcupacion? OcupacionCatalogo { get; set; }

        /// <summary>
        /// Nombre del puesto final del trabajador. Se autocompleta en el frontend
        /// concatenando <see cref="Categoria"/> y <see cref="Ocupacion"/>
        /// (ej. "Operario Abogado"), pero es editable.
        /// </summary>
        [Column("puesto")]
        public string? Puesto { get; set; }

        /// <summary>
        /// FK al catálogo <c>categoria_maestra</c> (EMPLEADO / PRACTICANTE PRE-PRO / RCC),
        /// tomado de la columna CATEGORÍA de la Data Maestra de GTH.
        /// </summary>
        [Column("categoria_maestra_id")]
        public int? CategoriaMaestraId { get; set; }

        /// <summary>Nombre del contacto de emergencia del trabajador.</summary>
        [Column("contacto_emergencia")]
        public string? ContactoEmergencia { get; set; }

        /// <summary>Celular del contacto de emergencia.</summary>
        [Column("celular_emergencia")]
        public string? CelularEmergencia { get; set; }

        [Column("area")]
        public string? Area { get; set; }

        [Column("subarea")]
        public string? Subarea { get; set; }

        /// <summary>
        /// FK a <c>area_scope</c>. Reemplaza el uso de <see cref="Area"/>/<see cref="Subarea"/>
        /// (texto plano) para resolver jefaturas vía el árbol jerárquico.
        /// </summary>
        [Column("area_scope_id")]
        public int? AreaScopeId { get; set; }

        [Column("contrata_casa")]
        public string? ContrataCasa { get; set; }

        /// <summary>
        /// FK a <c>workers_obra_oficina_staff</c> (Obra / Staff / Oficina Central).
        /// Reemplaza al texto plano <c>obra_oficina</c> y al nodo hoja de tipo
        /// "Área Obra_Oficina" del árbol <c>area_scope</c>: la ubicación del
        /// trabajador ya NO se deduce del área. Ver <c>ObraOficinaStaffIds</c>.
        /// </summary>
        [Column("obra_oficina_staff_id")]
        public int? ObraOficinaStaffId { get; set; }

        [ForeignKey(nameof(ObraOficinaStaffId))]
        public WorkersObraOficinaStaff? ObraOficinaStaff { get; set; }

        [Column("jefatura")]
        public string? Jefatura { get; set; }

        [Column("estado")]
        public string? Estado { get; set; }

        [Column("habilitado_obra")]
        public bool? HabilitadoObra { get; set; }

        [Column("sctr")]
        public bool? Sctr { get; set; }

        [Column("condicion_medica")]
        public string? CondicionMedica { get; set; }

        [Column("procedencia")]
        public string? Procedencia { get; set; }

        [Column("notas")]
        public string? Notas { get; set; }

        [Column("anios_experiencia")]
        public int? AniosExperiencia { get; set; }

        [Column("puntos_infraccion")]
        public int? PuntosInfraccion { get; set; }

        /// <summary>
        /// Jefe directo (worker) encargado de revisar las lecciones aprendidas de este
        /// trabajador. Autoreferencia a <c>workers.id</c>; null si aún no se asigna.
        /// </summary>
        [Column("worker_lesson_jefe_id")]
        public int? WorkerLessonJefeId { get; set; }

        /// <summary>
        /// Jefe directo (worker) encargado de aprobar/rechazar las solicitudes de salida
        /// de este trabajador. Autoreferencia a <c>workers.id</c>; null si aún no se asigna,
        /// en cuyo caso el aprobador se resuelve por el árbol de áreas (ApproverResolver).
        /// </summary>
        [Column("worker_salida_jefe_id")]
        public int? WorkerSalidaJefeId { get; set; }

        /// <summary>
        /// Si true, las lecciones aprendidas creadas por este trabajador se auto-aprueban
        /// al momento de crear o editar (solo en sus propias lecciones). Sin notificación.
        /// </summary>
        [Column("auto_approve_lesson")]
        public bool AutoApproveLesson { get; set; }

        [Column("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        public Person? Person { get; set; }
        public Contributor? Contributor { get; set; }
    }
}
