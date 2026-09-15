using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Dtos
{
    /// <summary>
    /// Un Consolidado del S10 visto por la jefatura que lo tiene que firmar. La unidad de esta
    /// pantalla es el CONSOLIDADO y no la planilla: un mismo registro del S10 puede cubrir varias
    /// —de uno o de varios trabajadores, siempre de una misma razón social— y la decisión del
    /// reembolso las alcanza a todas, así que decidirlas por separado nunca tuvo sentido.
    ///
    /// Los agregados (montos, salidas, trabajadores) están acotados a lo que ESE usuario puede ver,
    /// salvo <see cref="MontoTotal"/>, que es el importe declarado en el S10 y es del documento
    /// entero.
    /// </summary>
    public class ConsolidadoListItemDto
    {
        /// <summary>Id de <c>ga_consolidado_s10</c>.</summary>
        public int Id { get; set; }

        /// <summary>Número de reembolso que devolvió el S10. Null en los consolidados viejos.</summary>
        public string? NumeroReembolso { get; set; }

        /// <summary>
        /// Importe con el que el S10 registró las planillas que cubre — el documento ENTERO, sin
        /// recortar por visibilidad. Null en los consolidados subidos antes de que se pidiera.
        /// </summary>
        public decimal? MontoTotal { get; set; }

        /// <summary>Suma de las salidas visibles de sus planillas: lo que este usuario ve del total.</summary>
        public decimal MontoVisible { get; set; }

        // ── Documentos ───────────────────────────────────────────────────
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;
        /// <summary>Copia con la firma de la jefatura. Null mientras no se apruebe el reembolso.</summary>
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoFilename { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }
        public DateTimeOffset UploadedAt { get; set; }
        /// <summary>Quién lo adjuntó (el trabajador o su consolidador). Null si no se pudo resolver.</summary>
        public string? SubidoPor { get; set; }

        // ── Qué cubre ────────────────────────────────────────────────────
        /// <summary>
        /// Planillas cubiertas, TODAS —también las que el usuario no ve—, porque el documento y su
        /// importe son de ese conjunto entero. Las que no ve traen solo el código y el monto; ver
        /// <see cref="ConsolidadoPlanillaDto.Visible"/>.
        /// </summary>
        public List<ConsolidadoPlanillaDto> Rendiciones { get; set; } = new();

        /// <summary>Trabajadores de las salidas visibles, sin repetir.</summary>
        public List<string> Trabajadores { get; set; } = new();
        public int SalidasCount { get; set; }

        /// <summary>Razón social común de sus trabajadores. Null si se mezclan o si falta cargarla.</summary>
        public int? RazonSocialId { get; set; }
        public string? RazonSocial { get; set; }

        /// <summary>"Agosto 2026", o un rango si el consolidado cruza meses.</summary>
        public string Periodo { get; set; } = string.Empty;
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }

        // ── Reembolso ────────────────────────────────────────────────────
        /// <summary>Resumen de las salidas visibles: gana el estado que más atención pide.</summary>
        public string EstadoReembolso { get; set; } = EstadosSalida.Reembolso.NombrePendiente;
        public bool ReembolsoMixto { get; set; }
        public string? ObservacionReembolso { get; set; }
        /// <summary>
        /// Quién escribió esa observación: "Jefatura" o "Tesorería" (RG-49). Vacío si no hay
        /// ninguna. Lo que devolvió Tesorería vuelve acá para que la jefatura lo firme de nuevo,
        /// así que hace falta distinguirlo de la propia observación vieja.
        /// </summary>
        public string ObservacionReembolsoOrigen { get; set; } = string.Empty;

        // ── Qué se puede hacer con este consolidado ──────────────────────
        /// <summary>Salidas visibles con el reembolso listo para decidir (rendidas y sin decidir).</summary>
        public int PorDecidirCount { get; set; }

        /// <summary>
        /// True si el usuario puede decidir el reembolso de este consolidado. Es false cuando cubre
        /// salidas SUYAS y él no es su propio revisor: nadie decide lo suyo, y la única excepción es
        /// tener el <b>jefe personalizado apuntándose a sí mismo</b> (Gestión de Ingresos → ficha
        /// del trabajador). La pantalla lo usa para apagar las acciones antes de que el backend las
        /// rechace.
        /// </summary>
        public bool PuedeDecidir { get; set; } = true;
    }

    /// <summary>Una planilla cubierta por el consolidado, con lo que la pantalla muestra de ella.</summary>
    public class ConsolidadoPlanillaDto
    {
        public int Id { get; set; }
        /// <summary>Código REN-AAAA-NNNN.</summary>
        public string Codigo { get; set; } = string.Empty;

        /// <summary>
        /// false = el consolidado la cubre pero el usuario no ve ninguna de sus salidas. Se lista
        /// igual (con su código y su monto) porque el importe declarado incluye esa planilla y, sin
        /// ella, el total del documento no cuadraría con lo que se muestra.
        /// </summary>
        public bool Visible { get; set; }

        /// <summary>Monto de la planilla COMPLETA. Es lo que suma contra el importe del consolidado.</summary>
        public decimal MontoTotalPlanilla { get; set; }

        // Lo de abajo solo viene en las visibles.
        public string? NumeroPlanilla { get; set; }
        public string? Periodo { get; set; }
        public List<string> Trabajadores { get; set; } = new();
        public int SalidasCount { get; set; }
        /// <summary>Suma de las salidas visibles de esta planilla.</summary>
        public decimal MontoVisible { get; set; }
        public string? EstadoReembolso { get; set; }
        public string? PdfUrl { get; set; }
        public string? PdfFilename { get; set; }
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoFilename { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }
    }

    /// <summary>
    /// Una salida cubierta por el consolidado, para que la jefatura vea qué gasto está firmando.
    /// Es solo lectura: el reembolso se decide por consolidado entero.
    /// </summary>
    public class ConsolidadoSalidaDto
    {
        public int Id { get; set; }
        public string? Codigo { get; set; }
        /// <summary>Planilla a la que pertenece, para agrupar las salidas en el detalle.</summary>
        public int RendicionId { get; set; }
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

    public class ConsolidadoDetalleDto : ConsolidadoListItemDto
    {
        /// <summary>Las salidas visibles de todas sus planillas, en orden de planilla y trabajador.</summary>
        public List<ConsolidadoSalidaDto> Salidas { get; set; } = new();
    }

    public class ConsolidadoFiltersDto
    {
        public int? WorkerId { get; set; }
        /// <summary>"Pendiente" | "Observado" | "Firmado" | "Proceder con el reembolso" | "Pagado" | null.</summary>
        public string? EstadoReembolso { get; set; }
        /// <summary>Búsqueda por número de reembolso del S10 o por código de rendición.</summary>
        public string? Texto { get; set; }
        public int? PeriodoAnio { get; set; }
        public int? PeriodoMes { get; set; }

        /// <summary>Filtro de área elegido en la UI (nodo + descendientes, resueltos en el frontend).</summary>
        public List<int>? FilterAreaScopeIds { get; set; }

        // ── Visibilidad (la resuelve el servicio, ámbito CONSOLIDADOS) ──
        public int? CurrentUserId { get; set; }
        public bool SeesAll { get; set; }
        public bool SeesAllOverride { get; set; }
        public List<int>? VisibleAreaScopeIds { get; set; }
    }

    /// <summary>
    /// Números de las tarjetas del encabezado, contados sobre el conjunto ya filtrado: en qué punto
    /// del reembolso está cada consolidado del alcance.
    /// </summary>
    public class ResumenConsolidadosDto
    {
        /// <summary>Esperando la decisión de la jefatura: es lo que la pantalla viene a resolver.</summary>
        public int PorDecidir { get; set; }
        /// <summary>Devueltos con una observación: la pelota está en el trabajador.</summary>
        public int Observados { get; set; }
        /// <summary>
        /// Ya firmados y en manos de Tesorería (por revisar o por pagar). No cuenta por la firma
        /// estampada sino por el ESTADO: un consolidado que Tesorería devolvió (RG-49) conserva la
        /// copia firmada pero volvió a esperar a la jefatura, y va en la tarjeta de observados.
        /// Lo pagado tampoco: ya no espera a nadie.
        /// </summary>
        public int Firmados { get; set; }

        public static ResumenConsolidadosDto De(IEnumerable<ConsolidadoListItemDto> consolidados)
        {
            var lista = consolidados as ICollection<ConsolidadoListItemDto> ?? consolidados.ToList();
            return new ResumenConsolidadosDto
            {
                PorDecidir = lista.Count(x => x.PorDecidirCount > 0),
                Observados = lista.Count(x => x.EstadoReembolso == EstadosSalida.Reembolso.NombreObservado),
                Firmados   = lista.Count(x => x.EstadoReembolso == EstadosSalida.Reembolso.NombreFirmado
                                           || x.EstadoReembolso == EstadosSalida.Reembolso.NombrePorPagar),
            };
        }
    }

    public class ConsolidadoListResultDto
    {
        public List<ConsolidadoListItemDto> Data { get; set; } = new();
        public ResumenConsolidadosDto Resumen { get; set; } = new();
    }

    public class ConsolidadoFilterDataDto
    {
        public List<TrabajadorOptionDto> Trabajadores { get; set; } = new();
        /// <summary>Árbol area_scope (lista plana) para el filtro de área en cascada.</summary>
        public List<AreaNodeDto> AreaTree { get; set; } = new();
        public List<PeriodoConsolidadoOptionDto> Periodos { get; set; } = new();
    }

    public class PeriodoConsolidadoOptionDto
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// Cuerpo de la decisión del reembolso. Va por CONSOLIDADO: el servidor resuelve las salidas de
    /// todas sus planillas que están dentro del alcance del usuario y con el reembolso por decidir.
    /// </summary>
    public class ConsolidadoAccionDto
    {
        public List<int> ConsolidadoIds { get; set; } = new();
        /// <summary>Obligatoria al observar: es lo único que el trabajador va a leer.</summary>
        public string? Observacion { get; set; }
    }

    /// <summary>
    /// Selección sobre la que se quiere saber qué correos saldrían. Es el mismo conjunto con el que
    /// después se va a escribir, para que la confirmación no prometa un correo que la acción no
    /// vaya a mandar.
    /// </summary>
    public class ConsolidadoCorreoPreviewRequestDto
    {
        public List<int> ConsolidadoIds { get; set; } = new();
        /// <summary>true = la variante que aprueba (y firma); false = la que observa.</summary>
        public bool Aprobar { get; set; }
    }

    /// <summary>Un PDF que hay que firmar al aprobar el reembolso.</summary>
    public class DocumentoParaFirmarDto
    {
        /// <summary>Id de la fila de <c>ga_consolidado_s10</c>. Sin uso en la planilla misma.</summary>
        public int Id { get; set; }

        /// <summary>
        /// PDF sobre el que se estampa: el original, o —si el consolidado es compartido y otro jefe
        /// ya lo firmó al aprobar otra de sus planillas— su copia firmada, para que la firma nueva
        /// se sume a la anterior en vez de borrarla.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Nombre del ORIGINAL: la copia firmada se nombra a partir de él.</summary>
        public string Filename { get; set; } = string.Empty;

        /// <summary>
        /// Lugar de la firma en la hoja: cuántas firmas trae ya el documento (0 = la esquina de
        /// siempre). Ver <c>SignaturePdfStamper.Stamp</c>.
        /// </summary>
        public int Slot { get; set; }
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
    /// El aviso a Tesorería de que una planilla quedó firmada y ya se puede pagar. El destinatario
    /// principal se resuelve por ROL (TESORERO), que es la misma condición que abre la bandeja de
    /// Reembolsos: así el correo le llega exactamente a quien puede actuar sobre él, sin depender
    /// de un área ni de una lista de nombres.
    ///
    /// Los demás —el Coordinador ERP, por ejemplo— NO salen de acá: son destinatarios normales de
    /// Configuración → Correos (tipo ROL) y los agrega el resolver al enviar. Este DTO solo trae
    /// el principal.
    /// </summary>
    public class TesoreriaCorreoInfoDto
    {
        public ReembolsoPlanillaCorreoDatos Datos { get; set; } = new();
        /// <summary>Correos corporativos de quien tiene el rol TESORERO. Vacío si no lo tiene nadie.</summary>
        public List<string> Destinatarios { get; set; } = new();
    }
}
