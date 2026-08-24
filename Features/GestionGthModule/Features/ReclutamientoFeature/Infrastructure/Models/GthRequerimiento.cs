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
        /// FK a <c>categoria</c>: la categoría declarada para esta vacante aparte de la de
        /// <c>puesto.categoria_id</c>.
        ///
        /// Congelada para auditoría: la llenaba el modo "Puesto personalizado" del formulario de
        /// solicitud, que se dio de baja (los puestos nuevos los da de alta GTH en el catálogo). En
        /// los requerimientos nuevos queda null, y la ficha del contratado siempre sale de
        /// <c>puesto.categoria_id</c> del puesto del requerimiento — que desde
        /// <c>Migrations_Manual/2026-08-21_workers_categoria_desde_puesto.sql</c> es el único
        /// camino a la categoría de un trabajador.
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

        /// <summary>
        /// true = la vacante es un ingreso directo <b>FFT</b>: el solicitante ya sabe a quién
        /// quiere, así que el proceso omite publicación, revisión de CV, long list, entrevistas y
        /// envío de finalistas. Del pedido se salta al formulario de información del postulante y,
        /// cuando GTH lo aprueba, a la programación de su EMO de ingreso (regla 8.8 del
        /// requerimiento funcional).
        ///
        /// El flujo se decide por esta columna y nunca por el proyecto: "FFT" era una fuente de
        /// reclutamiento del catálogo, pero atar el salto de fases a un proyecto/obra dejaría la
        /// regla a merced de un renombre.
        /// </summary>
        public bool EsFft { get; set; }

        /// <summary>
        /// Nombre completo del candidato que nombró el solicitante. Obligatorio en las vacantes
        /// <see cref="EsFft"/> (la base lo exige con <c>ck_gth_requerimiento_fft_candidato</c>) y
        /// null en el resto. Es el nombre con el que se abre su ficha de candidato.
        /// </summary>
        public string? FftCandidatoNombre { get; set; }

        /// <summary>
        /// Correo personal del candidato FFT: el buzón al que GTH le manda su formulario. Se guarda
        /// aunque después GTH lo corrija en el envío — este es el que declaró el solicitante y es
        /// lo que Gerencia General aprueba.
        /// </summary>
        public string? FftCandidatoCorreo { get; set; }

        public int GthEstadoRequerimientoId { get; set; }

        /// <summary>FK a <c>gth_prioridad</c> (Alta/Media/Baja). Null = sin prioridad asignada.</summary>
        public int? GthPrioridadId { get; set; }

        /// <summary>
        /// FK a <c>gth_responsable_proceso</c> (el reclutador a cargo). Null = sin asignar.
        /// Quiénes pueden estar acá lo decide Configuración → Reclutadores; la FK apunta a la
        /// fila, no al <c>worker</c>, así que desactivar a un reclutador no borra el histórico.
        /// </summary>
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
