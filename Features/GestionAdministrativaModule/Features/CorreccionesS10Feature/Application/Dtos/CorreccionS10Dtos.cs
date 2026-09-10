using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Application.Dtos
{
    /// <summary>
    /// Una solicitud de corrección en la bandeja del Coordinador ERP. Trae todo lo que necesita
    /// para hacer su trabajo sin abrir nada más: la guía con la que ubica el registro en el S10,
    /// qué observó la jefatura y qué le pide el colaborador.
    ///
    /// No trae la planilla ni los tramos: el ERP no revisa el gasto —eso ya lo hizo la jefatura—,
    /// solo corrige el documento del S10. Sí trae los dos PDF por si necesita contrastarlos.
    /// </summary>
    public class CorreccionS10ListItemDto
    {
        public int Id { get; set; }

        /// <summary>Planilla a la que pertenece el consolidado por corregir.</summary>
        public int RendicionId { get; set; }

        /// <summary>Código REN-AAAA-NNNN: es como el colaborador nombra su rendición.</summary>
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Número impreso en el PDF ("TI: 000123"). Null en las planillas que no lo tienen.</summary>
        public string? NumeroPlanilla { get; set; }

        /// <summary>"Pendiente de corrección S10" | "Pendiente de recarga S10".</summary>
        public string Estado { get; set; } = EstadosSalida.CorreccionS10.NombreSolicitada;

        /// <summary>True mientras el ERP no la haya atendido: es lo que le queda por hacer.</summary>
        public bool PorAtender { get; set; }

        // ── Quién la pide ────────────────────────────────────────────────────
        /// <summary>Nombre del colaborador dueño de la rendición.</summary>
        public string Trabajador { get; set; } = string.Empty;
        /// <summary>Área a la que entra por su puesto. Null si no se resuelve.</summary>
        public string? Area { get; set; }
        /// <summary>Quién apretó "Solicitar" — normalmente el propio colaborador.</summary>
        public string SolicitadaPor { get; set; } = string.Empty;
        public DateTimeOffset SolicitadaAt { get; set; }

        // ── Qué hay que corregir ─────────────────────────────────────────────
        /// <summary>El «MOTIVO *» del colaborador: la corrección que necesita (RG-21).</summary>
        public string Motivo { get; set; } = string.Empty;

        /// <summary>Con qué observó la jefatura el reembolso, copiada al solicitar.</summary>
        public string? MotivoJefatura { get; set; }

        /// <summary>
        /// Guía del Consolidado del S10 observado. Es EL dato con el que el ERP encuentra el
        /// registro en el S10, así que la tabla lo muestra en columna propia.
        /// </summary>
        public string? NumeroGuia { get; set; }

        /// <summary>Periodo que cubre la planilla ("Agosto 2026", o un rango si cruza meses).</summary>
        public string Periodo { get; set; } = string.Empty;
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }

        /// <summary>Monto de la planilla completa — el importe que debería tener el registro del S10.</summary>
        public decimal MontoTotalPlanilla { get; set; }

        // ── Documentos, para contrastar ───────────────────────────────────────
        /// <summary>Planilla de gasto por movilidad (el PDF que genera Abril One).</summary>
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;

        /// <summary>
        /// El Consolidado del S10 OBSERVADO: el archivo exacto que la jefatura miró. Null solo si
        /// la planilla llegó sin consolidado vigente, que no pasa en el flujo normal.
        /// </summary>
        public ConsolidadoS10Dto? ConsolidadoS10 { get; set; }

        // ── Atención ──────────────────────────────────────────────────────────
        /// <summary>Coordinador ERP que confirmó. Null mientras esté por atender.</summary>
        public string? AtendidaPor { get; set; }
        public DateTimeOffset? AtendidaAt { get; set; }
        public string? ComentarioAtencion { get; set; }

        /// <summary>True si el ERP anuló el registro y hace falta una guía nueva (CA-19).</summary>
        public bool GuiaAnulada { get; set; }
    }

    /// <summary>
    /// Filtros de la bandeja. No hay filtro por área ni recorte de visibilidad: el Coordinador ERP
    /// es uno para toda la organización (§6.1: "Responsable ERP, parametrizable"), igual que
    /// Tesorería — ve todas las correcciones, no las de un árbol de áreas.
    /// </summary>
    public class CorreccionS10FiltersDto
    {
        /// <summary>"Pendiente de corrección S10" | "Pendiente de recarga S10" | null para todas.</summary>
        public string? Estado { get; set; }

        /// <summary>Ficha del colaborador (<c>workers.id</c>), del desplegable. Null para todos.</summary>
        public int? WorkerId { get; set; }

        /// <summary>Busca en el código de la rendición y en el número de guía.</summary>
        public string? Q { get; set; }

        /// <summary>Periodo de la planilla. Los dos o ninguno.</summary>
        public int? PeriodoAnio { get; set; }
        public int? PeriodoMes { get; set; }
    }

    /// <summary>
    /// Las dos tarjetas del encabezado, contadas sobre el MISMO conjunto que muestra la tabla (con
    /// los filtros ya aplicados), así que viajan con el listado y no en <c>filter-data</c>. Son los
    /// dos lados del paso: lo que espera al ERP y lo que ya devolvió al colaborador.
    /// </summary>
    public class ResumenCorreccionesS10Dto
    {
        /// <summary>Solicitudes sin atender: es la bandeja de trabajo del Coordinador.</summary>
        public int PorAtender { get; set; }

        /// <summary>Ya atendidas y esperando que el colaborador recargue el Consolidado.</summary>
        public int PorRecargar { get; set; }

        public static ResumenCorreccionesS10Dto De(IEnumerable<CorreccionS10ListItemDto> items)
        {
            var lista = items as ICollection<CorreccionS10ListItemDto> ?? items.ToList();
            return new ResumenCorreccionesS10Dto
            {
                PorAtender  = lista.Count(x => x.PorAtender),
                PorRecargar = lista.Count(x => !x.PorAtender),
            };
        }
    }

    /// <summary>Respuesta del listado: las correcciones y las tarjetas de ese mismo conjunto.</summary>
    public class CorreccionS10ListResultDto
    {
        public List<CorreccionS10ListItemDto> Data { get; set; } = new();
        public ResumenCorreccionesS10Dto Resumen { get; set; } = new();
    }

    /// <summary>
    /// Datos de arranque de la bandeja: lo que NO cambia al mover los filtros. Incluye a quién le
    /// llega el aviso de atención, resuelto con la misma llamada que hace el envío, para que la
    /// confirmación no prometa un correo que Configuración dejó apagado.
    /// </summary>
    public class CorreccionS10FilterDataDto
    {
        /// <summary>Colaboradores con al menos una corrección registrada.</summary>
        public List<TrabajadorOptionDto> Trabajadores { get; set; } = new();

        /// <summary>Periodos (meses) con al menos una corrección.</summary>
        public List<PeriodoCorreccionOptionDto> Periodos { get; set; } = new();
    }

    /// <summary>
    /// Un periodo ofrecido por el filtro. Cada pantalla de planillas declara el suyo (hay uno
    /// equivalente en Mis Rendiciones, Gestión de Rendiciones y Reembolsos): comparten shape pero
    /// no el tipo, para que el contrato de una pantalla no arrastre a las otras.
    /// </summary>
    public class PeriodoCorreccionOptionDto
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        /// <summary>"Agosto 2026" — ya capitalizado, la pantalla lo imprime tal cual.</summary>
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// El check de confirmación del Coordinador ERP (RG-22 / RF-OBS-07). El comentario es opcional
    /// —el requerimiento solo exige el check—, pero <see cref="GuiaAnulada"/> cambia lo que el
    /// colaborador tiene que hacer después, así que se pregunta explícitamente.
    /// </summary>
    public class AtenderCorreccionS10Dto
    {
        /// <summary>Qué se hizo en el S10. Opcional.</summary>
        public string? ComentarioAtencion { get; set; }

        /// <summary>
        /// true = el registro del S10 se ANULÓ y el colaborador tiene que sacar una guía nueva; la
        /// anterior queda bloqueada (HU-ERP-03 / CA-19). false = se corrigió conservando la guía.
        /// </summary>
        public bool GuiaAnulada { get; set; }
    }

    /// <summary>Resultado de atender una o varias correcciones en bloque.</summary>
    public class CorreccionS10BulkResultDto
    {
        public int Procesadas { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Cuerpo del check masivo desde la tabla: las mismas dos respuestas, para N filas.</summary>
    public class AtenderCorreccionS10BulkDto : AtenderCorreccionS10Dto
    {
        public List<int> CorreccionIds { get; set; } = new();
    }

    /// <summary>
    /// El colaborador dueño de una corrección, con lo que necesitan sus dos correos. Se resuelve en
    /// una consulta: el correo no vuelve a la base a buscar nada.
    /// </summary>
    public class CorreccionS10SolicitanteDto
    {
        /// <summary>Ficha del colaborador (<c>workers.id</c>).</summary>
        public int WorkerId { get; set; }
        public string Trabajador { get; set; } = "Colaborador";
        /// <summary>Correo del usuario del colaborador. Null si su persona no tiene usuario.</summary>
        public string? Email { get; set; }
        /// <summary>Nombre del área a la que entra por su puesto. Null si no se resuelve.</summary>
        public string? Area { get; set; }
    }
}
