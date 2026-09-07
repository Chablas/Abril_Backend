using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos
{
    /// <summary>
    /// Una planilla de rendición vista por el revisor. Los agregados están acotados a las salidas
    /// que ESE usuario puede ver (misma visibilidad que Gestión de Salidas): una planilla puede
    /// agrupar a trabajadores de varias áreas y no todas le competen.
    /// </summary>
    public class GestionRendicionListItemDto
    {
        public int Id { get; set; }
        /// <summary>Código REN-AAAA-NNNN — es como el trabajador la nombra en los correos.</summary>
        public string Codigo { get; set; } = string.Empty;
        public string? NumeroPlanilla { get; set; }
        public DateTimeOffset RendidoAt { get; set; }

        /// <summary>"Agosto 2026", o un rango si la planilla cruza meses.</summary>
        public string Periodo { get; set; } = string.Empty;
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }

        /// <summary>Trabajadores visibles que aparecen en la planilla, sin repetir.</summary>
        public List<string> Trabajadores { get; set; } = new();
        public int SalidasCount { get; set; }
        public decimal MontoTotal { get; set; }

        // ── Documentos ───────────────────────────────────────────────────
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoFilename { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }
        public ConsolidadoS10Dto? ConsolidadoS10 { get; set; }

        // ── Primera revisión ─────────────────────────────────────────────
        // El paso que va ANTES del Consolidado del S10: el revisor mira tramos, montos y capturas
        // y decide. Es de la PLANILLA, así que no se resume de las salidas como el reembolso.

        /// <summary>"Lista para enviar" | "En primera revisión" | "Aprobada" | "Observada".</summary>
        public string EstadoPrimeraRevision { get; set; } = EstadosSalida.PrimeraRevision.NombreBorrador;

        /// <summary>Cuándo la envió el trabajador. Null si todavía no la envió.</summary>
        public DateTimeOffset? EnviadaRevisionAt { get; set; }

        /// <summary>Cuándo se decidió la primera revisión. Null si todavía no se decidió.</summary>
        public DateTimeOffset? PrimeraRevisionAt { get; set; }

        /// <summary>Comentario con el que se observó. Null si no se observó.</summary>
        public string? PrimeraRevisionObservacion { get; set; }

        /// <summary>
        /// True si esta planilla está esperando la primera revisión: el revisor puede aprobarla u
        /// observarla. Es lo que habilita las acciones de la pantalla.
        /// </summary>
        public bool PorPrimeraRevision { get; set; }

        // ── Reembolso ────────────────────────────────────────────────────
        /// <summary>Resumen de las salidas visibles: gana el estado que más atención pide.</summary>
        public string EstadoReembolso { get; set; } = EstadosSalida.Reembolso.NombrePendiente;
        public bool ReembolsoMixto { get; set; }
        public string? ObservacionReembolso { get; set; }
        public DateTimeOffset? RevisorNotificadoAt { get; set; }

        // ── Qué se puede hacer con esta planilla ─────────────────────────
        /// <summary>Salidas con el reembolso listo para decidir (rendidas, con S10 y sin decidir).</summary>
        public int PorDecidirCount { get; set; }
        /// <summary>Salidas con el reembolso aprobado y todavía sin firmar.</summary>
        public int PorFirmarCount { get; set; }
        /// <summary>
        /// True si alguna de las salidas visibles es del propio revisor. Nadie decide el reembolso
        /// de lo suyo (salvo Gerentes), así que la pantalla lo avisa antes de que el backend lo
        /// rechace.
        /// </summary>
        public bool IncluyePropias { get; set; }
    }

    /// <summary>Una salida de la planilla, para decidir su reembolso una por una desde el detalle.</summary>
    public class GestionRendicionSalidaDto
    {
        public int Id { get; set; }
        public string? Codigo { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        public string? Area { get; set; }
        public DateOnly FechaSalida { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        public int TrayectosCount { get; set; }
        public decimal Monto { get; set; }
        public string EstadoReembolso { get; set; } = EstadosSalida.Reembolso.NombrePendiente;
        public string? ObservacionReembolso { get; set; }
        /// <summary>True si su reembolso está listo para decidir (rendida, con S10 y sin decidir).</summary>
        public bool PorDecidir { get; set; }
        /// <summary>True si es del propio revisor: no puede decidirla salvo que sea Gerente.</summary>
        public bool EsPropia { get; set; }
    }

    public class GestionRendicionDetalleDto : GestionRendicionListItemDto
    {
        public List<GestionRendicionSalidaDto> Salidas { get; set; } = new();
    }

    public class GestionRendicionFiltersDto
    {
        public int? WorkerId { get; set; }

        /// <summary>
        /// "Lista para enviar" | "En primera revisión" | "Aprobada" | "Observada" | null para todas.
        /// </summary>
        public string? EstadoPrimeraRevision { get; set; }
        /// <summary>"Pendiente" | "Aprobado" | "Rechazado" | "Firmado" | "Pagado" | null para todos.</summary>
        public string? EstadoReembolso { get; set; }
        /// <summary>true = solo con consolidado adjunto; false = solo sin él; null = todas.</summary>
        public bool? ConConsolidado { get; set; }
        public int? PeriodoAnio { get; set; }
        public int? PeriodoMes { get; set; }

        /// <summary>Filtro de área elegido en la UI (nodo + descendientes, resueltos en el frontend).</summary>
        public List<int>? FilterAreaScopeIds { get; set; }

        // ── Visibilidad (la resuelve el servicio, igual que en Gestión de Salidas) ──
        public int? CurrentUserId { get; set; }
        public bool SeesAll { get; set; }
        public bool SeesAllOverride { get; set; }
        public List<int>? VisibleAreaScopeIds { get; set; }
    }

    /// <summary>
    /// Números de las tarjetas del encabezado, contados sobre el conjunto ya filtrado. Son las tres
    /// cosas que esperan al revisor, en el orden del flujo.
    /// </summary>
    public class ResumenGestionRendicionesDto
    {
        /// <summary>Planillas esperando la PRIMERA revisión: el primer paso del revisor.</summary>
        public int PrimeraRevision { get; set; }
        /// <summary>
        /// Aprobadas en primera revisión y sin el Consolidado del S10: la pelota está en el
        /// trabajador. Las que no pasaron la primera revisión no cuentan acá — esas están en la
        /// tarjeta anterior y no en una espera del trabajador.
        /// </summary>
        public int SinConsolidado { get; set; }
        /// <summary>Planillas con reembolso por decidir (con S10 adjunto) — la segunda revisión.</summary>
        public int PorRevisar { get; set; }
        /// <summary>Planillas con reembolso aprobado esperando la firma.</summary>
        public int PorFirmar { get; set; }

        public static ResumenGestionRendicionesDto De(IEnumerable<GestionRendicionListItemDto> planillas)
        {
            var lista = planillas as ICollection<GestionRendicionListItemDto> ?? planillas.ToList();
            return new ResumenGestionRendicionesDto
            {
                PrimeraRevision = lista.Count(x => x.PorPrimeraRevision),
                SinConsolidado  = lista.Count(x => x.ConsolidadoS10 == null
                                               && x.EstadoPrimeraRevision == EstadosSalida.PrimeraRevision.NombreAprobada),
                PorRevisar      = lista.Count(x => x.PorDecidirCount > 0),
                PorFirmar       = lista.Count(x => x.PorFirmarCount > 0),
            };
        }
    }

    public class GestionRendicionListResultDto
    {
        public List<GestionRendicionListItemDto> Data { get; set; } = new();
        public ResumenGestionRendicionesDto Resumen { get; set; } = new();
    }

    public class GestionRendicionFilterDataDto
    {
        public List<TrabajadorOptionDto> Trabajadores { get; set; } = new();
        /// <summary>Árbol area_scope (lista plana) para el filtro de área en cascada.</summary>
        public List<AreaNodeDto> AreaTree { get; set; } = new();
        public List<PeriodoRendicionOptionDto> Periodos { get; set; } = new();
    }

    public class PeriodoRendicionOptionDto
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// Cuerpo de las acciones en bloque. Se manda una de las dos cosas: las planillas completas
    /// (lo normal, desde la tabla) o salidas sueltas (desde el detalle, cuando el revisor decide
    /// una por una). Si vienen las dos, se juntan.
    /// </summary>
    /// <summary>
    /// Cuerpo de la decisión de la PRIMERA revisión. Va por planilla y no por salida: lo que se
    /// revisa es el documento entero y la decisión es total (RG-19).
    /// </summary>
    public class PrimeraRevisionAccionDto
    {
        public List<int> RendicionIds { get; set; } = new();

        /// <summary>
        /// Obligatoria al observar (RG-20): es el comentario que el trabajador va a leer para
        /// saber qué corregir antes de volver a generar la rendición.
        /// </summary>
        public string? Observacion { get; set; }
    }

    public class ReembolsoAccionDto
    {
        public List<int> RendicionIds { get; set; } = new();
        public List<int> SolicitudIds { get; set; } = new();
        /// <summary>Obligatoria al rechazar: es lo único que el trabajador va a leer.</summary>
        public string? Observacion { get; set; }
    }
}
