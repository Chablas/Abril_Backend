using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
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
        /// <summary>
        /// Monto de la planilla COMPLETA (todas sus salidas, de todos sus trabajadores). Es el
        /// importe que se registró en el S10, así que es contra este —y no contra
        /// <see cref="MontoTotal"/>, que está recortado— que tiene que cuadrar el monto del
        /// Consolidado del S10. Coinciden salvo en las planillas que agrupan a varias personas.
        /// </summary>
        public decimal MontoTotalPlanilla { get; set; }

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
        /// <summary>
        /// True si el usuario puede decidir sobre esta planilla (su primera revisión y el reembolso
        /// de sus salidas). Es false cuando la planilla incluye salidas SUYAS y él no es su propio
        /// revisor: nadie decide lo suyo, y la única excepción es tener el <b>jefe personalizado
        /// apuntándose a sí mismo</b> (Gestión de Ingresos → ficha del trabajador). La pantalla lo
        /// usa para apagar las acciones antes de que el backend las rechace.
        /// </summary>
        public bool PuedeDecidir { get; set; } = true;
    }

    /// <summary>Un PDF de la planilla que hay que firmar al aprobar su reembolso.</summary>
    public class DocumentoParaFirmarDto
    {
        /// <summary>Id de la fila de <c>ga_consolidado_s10</c>. Sin uso en la planilla misma.</summary>
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
    }

    /// <summary>
    /// Una planilla cuyo reembolso se puede aprobar, con TODO lo que hay que firmar: su PDF y el
    /// Consolidado del S10 que respalda a sus salidas.
    /// </summary>
    public class PlanillaParaFirmarDto
    {
        public int RendicionId { get; set; }
        /// <summary>Salidas de la planilla que entran en esta aprobación.</summary>
        public List<int> SolicitudIds { get; set; } = new();
        public string PlanillaUrl { get; set; } = string.Empty;
        public string PlanillaFilename { get; set; } = string.Empty;
        /// <summary>
        /// Consolidados vigentes que cubren esas salidas. Normalmente uno —el de la planilla, que
        /// es como se adjunta hoy—; en registros antiguos puede haber uno por salida suelta, y por
        /// eso es una lista y no un solo documento.
        /// </summary>
        public List<DocumentoParaFirmarDto> Consolidados { get; set; } = new();
    }

    /// <summary>Dónde quedó en SharePoint la copia firmada de un documento.</summary>
    public class ArchivoFirmadoDto
    {
        public string Url { get; set; } = string.Empty;
        public string? ItemId { get; set; }
        public string Filename { get; set; } = string.Empty;
    }

    /// <summary>Una planilla ya firmada: qué salidas cubre y dónde quedaron sus copias firmadas.</summary>
    public class PlanillaFirmadaDto
    {
        public int RendicionId { get; set; }
        public List<int> SolicitudIds { get; set; } = new();
        public ArchivoFirmadoDto Planilla { get; set; } = new();
        /// <summary>consolidadoId → su copia firmada.</summary>
        public Dictionary<int, ArchivoFirmadoDto> Consolidados { get; set; } = new();
    }

    /// <summary>
    /// Una salida de la planilla, para que el revisor vea qué agrupa el documento que está
    /// decidiendo. Es solo lectura: el reembolso se decide por planilla entera, no salida por
    /// salida — ver <see cref="GestionRendicionListItemDto.PuedeDecidir"/> y
    /// <see cref="GestionRendicionListItemDto.PorDecidirCount"/>.
    /// </summary>
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
    }

    public class GestionRendicionDetalleDto : GestionRendicionListItemDto
    {
        public List<GestionRendicionSalidaDto> Salidas { get; set; } = new();

        // Los destinatarios de los correos de las decisiones NO viajan acá: se piden aparte con
        // GetCorreoPreview cuando el revisor aprieta el botón. Antes había un
        // CorreoReembolsoAprobado en este DTO que nadie llenaba, así que el modal decía siempre
        // "nadie recibirá el aviso" aunque el correo estuviera activo. Un preview por acción
        // también evita resolver cinco listas de correos cada vez que se abre el detalle.
    }

    /// <summary>
    /// El aviso a Tesorería de que una planilla quedó firmada y ya se puede pagar. Los
    /// destinatarios se resuelven por PUESTO (categoría Tesorero) y no por área ni por una lista
    /// escrita a mano: es la misma condición que abre la bandeja de Reembolsos, así que el correo
    /// le llega exactamente a quien puede actuar sobre él.
    /// </summary>
    public class TesoreriaCorreoInfoDto
    {
        public ReembolsoPlanillaCorreoDatos Datos { get; set; } = new();
        /// <summary>Correos corporativos de Tesorería. Vacío si no hay ningún puesto asignado.</summary>
        public List<string> Destinatarios { get; set; } = new();
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

        public static ResumenGestionRendicionesDto De(IEnumerable<GestionRendicionListItemDto> planillas)
        {
            var lista = planillas as ICollection<GestionRendicionListItemDto> ?? planillas.ToList();
            return new ResumenGestionRendicionesDto
            {
                PrimeraRevision = lista.Count(x => x.PorPrimeraRevision),
                SinConsolidado  = lista.Count(x => x.ConsolidadoS10 == null
                                               && x.EstadoPrimeraRevision == EstadosSalida.PrimeraRevision.NombreAprobada),
                PorRevisar      = lista.Count(x => x.PorDecidirCount > 0),
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
