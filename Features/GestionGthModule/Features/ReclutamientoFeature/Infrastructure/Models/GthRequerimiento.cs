namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Requerimiento independiente por vacante (tabla <c>gth_requerimiento</c>). Cada vacante de una
    /// <see cref="GthSolicitud"/> genera uno, con su propio código <c>REQ-AAAA-NNNN</c> y su propio
    /// seguimiento. Guarda el puesto, tipo (Nuevo/Reemplazo), proyecto/obra, fecha requerida de
    /// ingreso y estado actual.
    /// </summary>
    public class GthRequerimiento
    {
        public int GthRequerimientoId { get; set; }

        public int GthSolicitudId { get; set; }
        public GthSolicitud? Solicitud { get; set; }

        /// <summary>Código único del requerimiento en formato <c>REQ-AAAA-NNNN</c>.</summary>
        public string Codigo { get; set; } = null!;

        /// <summary>Año del código (AAAA) — usado para el correlativo anual.</summary>
        public int Anio { get; set; }

        /// <summary>Correlativo (NNNN) dentro del año.</summary>
        public int Numero { get; set; }

        public int PuestoId { get; set; }

        /// <summary>
        /// FK a <c>categoria</c>: la categoría REAL declarada para esta vacante, no la de
        /// <c>puesto.categoria_id</c> (esa es solo una guía derivada de los datos y no tiene por qué
        /// coincidir, igual que en <c>workers</c>, donde <c>puesto_id</c> y <c>categoria_id</c> son
        /// ejes independientes).
        ///
        /// Congelada para auditoría: la llenaba el modo "Puesto personalizado" del formulario de
        /// solicitud, que se dio de baja (los puestos nuevos los da de alta GTH en el catálogo). En
        /// los requerimientos nuevos queda null y quien contrate al seleccionado cae a
        /// <c>puesto.categoria_id</c> del puesto del requerimiento.
        /// </summary>
        public int? CategoriaId { get; set; }

        public int GthTipoRequerimientoId { get; set; }

        /// <summary>
        /// FK a <c>workers</c>: el trabajador al que reemplaza esta vacante. Solo se llena cuando
        /// el tipo de requerimiento es <c>REEMPLAZO</c>; en las vacantes nuevas queda null.
        ///
        /// El solicitante lo elige de los trabajadores de su <c>area_scope</c> o de cualquier área
        /// hija, y puede elegirse a sí mismo (pedir el reemplazo propio por renuncia o promoción
        /// es un caso real).
        /// </summary>
        public int? ReemplazaWorkerId { get; set; }

        /// <summary>FK a <c>project</c> (proyecto/obra destino de la vacante).</summary>
        public int ProjectId { get; set; }

        /// <summary>
        /// Salario bruto mensual declarado para la vacante, en soles. Es un dato por vacante y no
        /// por solicitud: dos vacantes de la misma solicitud pueden ser de puestos distintos y
        /// cobrar distinto.
        ///
        /// El formulario lo exige, pero la columna es nullable: los requerimientos anteriores a
        /// este campo no lo tienen y un NOT NULL dejaría el histórico inconsistente. Null =
        /// registrado antes de que se pidiera el dato.
        /// </summary>
        public decimal? SalarioBrutoMensual { get; set; }

        public int GthEstadoRequerimientoId { get; set; }

        /// <summary>FK a <c>gth_prioridad</c> (Alta/Media/Baja). Null = sin prioridad asignada.</summary>
        public int? GthPrioridadId { get; set; }

        /// <summary>FK a <c>gth_responsable_proceso</c> (miembro GTH responsable). Null = sin asignar.</summary>
        public int? GthResponsableProcesoId { get; set; }

        /// <summary>FK a <c>gth_tipo_proceso</c> (Junior/Semisenior/Senior con su SLA). Null = sin clasificar.</summary>
        public int? GthTipoProcesoId { get; set; }

        /// <summary>FK a <c>contributor</c>: razón social activa asignada por GTH para la contratación.</summary>
        public int? ContributorId { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
