using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Dtos
{
    /// <summary>
    /// Una planilla de rendición del trabajador. Es la unidad de esta pantalla: una planilla = un
    /// PDF = un registro en el S10, y puede agrupar varias salidas.
    ///
    /// Los conteos y el monto están acotados a las salidas DEL TRABAJADOR: una planilla generada
    /// por el revisor desde Gestión de Salidas puede mezclar a varias personas, y en "Mis
    /// Rendiciones" contar las de otros sería mentir. Los documentos (planilla, copia firmada,
    /// consolidado), en cambio, son de la planilla entera: el papel es uno solo.
    /// </summary>
    public class RendicionListItemDto
    {
        public int Id { get; set; }

        /// <summary>Correlativo impreso en la planilla ("TI: 000123"). Null en las que no lo tienen.</summary>
        public string? NumeroPlanilla { get; set; }

        /// <summary>Cuándo se rindió (es lo que fija el orden por defecto de la tabla).</summary>
        public DateTimeOffset RendidoAt { get; set; }

        /// <summary>Periodo que cubren las salidas propias ("Agosto 2026", o "Jul — Ago 2026" si cruza meses).</summary>
        public string Periodo { get; set; } = string.Empty;

        /// <summary>Año/mes de la salida más antigua de la planilla — es la clave del filtro de periodo.</summary>
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }

        /// <summary>Cuántas salidas propias entran en esta planilla.</summary>
        public int SalidasCount { get; set; }

        /// <summary>Suma de lo rendido en las salidas propias de esta planilla.</summary>
        public decimal MontoTotal { get; set; }

        // ── Documentos de la planilla ────────────────────────────────────
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;

        /// <summary>Copia firmada por la jefatura. Null mientras nadie la firme.</summary>
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoFilename { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }

        /// <summary>Consolidado del S10 vigente de la planilla. Null si todavía no se adjuntó.</summary>
        public ConsolidadoS10Dto? ConsolidadoS10 { get; set; }

        // ── Reembolso ────────────────────────────────────────────────────
        /// <summary>
        /// Estado del reembolso de la planilla, resumido a partir de las salidas propias: gana el
        /// que más atención pide (Rechazado > Pendiente > Aprobado > Firmado > Pagado), porque
        /// mientras una salida siga atrás la planilla no está cerrada.
        /// </summary>
        public string EstadoReembolso { get; set; } = EstadosSalida.Reembolso.NombrePendiente;

        /// <summary>True si las salidas propias no están todas en el mismo estado de reembolso.</summary>
        public bool ReembolsoMixto { get; set; }

        /// <summary>Observación del rechazo: es lo que hay que subsanar. Null si no hay rechazo.</summary>
        public string? ObservacionReembolso { get; set; }

        /// <summary>Última vez que se le avisó al revisor por esta planilla. Null si nunca.</summary>
        public DateTimeOffset? RevisorNotificadoAt { get; set; }

        /// <summary>
        /// True mientras el reembolso siga abierto (Pendiente o Rechazado): adjuntar o reemplazar
        /// el consolidado después de aprobado no tendría a quién avisarle ni qué reabrir.
        /// </summary>
        public bool PuedeAdjuntarConsolidado { get; set; }

        /// <summary>True cuando ya hay consolidado adjunto y el reembolso sigue abierto.</summary>
        public bool PuedeNotificarRevisor { get; set; }
    }

    /// <summary>Una salida dentro de la planilla, para el detalle.</summary>
    public class RendicionSalidaDto
    {
        public int Id { get; set; }
        public string? Codigo { get; set; }
        public DateOnly FechaSalida { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        public int TrayectosCount { get; set; }
        public decimal Monto { get; set; }
        public string EstadoReembolso { get; set; } = EstadosSalida.Reembolso.NombrePendiente;
        public string? ObservacionReembolso { get; set; }
    }

    /// <summary>La planilla con el desglose de sus salidas propias.</summary>
    public class RendicionDetalleDto : RendicionListItemDto
    {
        public List<RendicionSalidaDto> Salidas { get; set; } = new();
    }

    public class RendicionFiltersDto
    {
        /// <summary>"Pendiente" | "Aprobado" | "Rechazado" | "Firmado" | "Pagado" | null para todos.</summary>
        public string? EstadoReembolso { get; set; }

        /// <summary>true = solo con consolidado adjunto; false = solo sin él; null = todas.</summary>
        public bool? ConConsolidado { get; set; }

        /// <summary>Periodo (mes de la salida más antigua de la planilla). Los dos o ninguno.</summary>
        public int? PeriodoAnio { get; set; }
        public int? PeriodoMes { get; set; }
    }

    /// <summary>
    /// Números de las tarjetas del encabezado. Se cuentan sobre el MISMO conjunto que muestra la
    /// tabla (con los filtros ya aplicados), así que acompañan a la búsqueda; por eso viajan en la
    /// respuesta del listado y no en <c>filter-data</c>. Son las tres cosas que le pueden faltar al
    /// trabajador, en el orden del flujo.
    /// </summary>
    public class ResumenRendicionesDto
    {
        /// <summary>Planillas sin el Consolidado del S10 adjunto: el paso que sigue a rendir.</summary>
        public int SinConsolidado { get; set; }
        /// <summary>Con consolidado y reembolso abierto, pero sin avisarle todavía al revisor.</summary>
        public int PorAvisar { get; set; }
        /// <summary>Con el reembolso rechazado: esperan que el trabajador subsane.</summary>
        public int Observadas { get; set; }

        /// <summary>Cuenta las tres bandejas sobre las planillas recibidas (el conjunto ya filtrado).</summary>
        public static ResumenRendicionesDto De(IEnumerable<RendicionListItemDto> rendiciones)
        {
            var lista = rendiciones as ICollection<RendicionListItemDto> ?? rendiciones.ToList();
            return new ResumenRendicionesDto
            {
                SinConsolidado = lista.Count(x => x.ConsolidadoS10 == null),
                PorAvisar      = lista.Count(x => x.PuedeNotificarRevisor && x.RevisorNotificadoAt == null),
                Observadas     = lista.Count(x => x.EstadoReembolso == EstadosSalida.Reembolso.NombreRechazado),
            };
        }
    }

    /// <summary>Respuesta del listado: las planillas y las tarjetas de ese mismo conjunto.</summary>
    public class RendicionListResultDto
    {
        public List<RendicionListItemDto> Data { get; set; } = new();
        public ResumenRendicionesDto Resumen { get; set; } = new();
    }

    /// <summary>Un periodo ofrecido por el filtro (mes con al menos una planilla del trabajador).</summary>
    public class PeriodoOptionDto
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        /// <summary>"Agosto 2026" — ya capitalizado, la pantalla lo imprime tal cual.</summary>
        public string Label { get; set; } = string.Empty;
    }

    public class RendicionFilterDataDto
    {
        public List<PeriodoOptionDto> Periodos { get; set; } = new();
    }
}
