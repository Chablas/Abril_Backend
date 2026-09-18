using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Dtos
{
    /// <summary>
    /// Un Consolidado del S10 visto por la jefatura que lo tiene que firmar y por el consolidador que
    /// lo adjuntó. La unidad de esta pantalla es el CONSOLIDADO y no la planilla: un mismo registro
    /// del S10 puede cubrir varias —de uno o de varios trabajadores, de las razones sociales que
    /// sean— y la decisión del reembolso las alcanza a todas.
    ///
    /// Los agregados (montos, salidas, trabajadores) están acotados a lo que ESE usuario puede ver,
    /// salvo <see cref="MontoTotal"/>, que es el importe declarado en el S10 y es del documento
    /// entero.
    /// </summary>
    public class ConsolidadoListItemDto
    {
        /// <summary>Id de <c>ga_consolidado_s10</c>.</summary>
        public int Id { get; set; }

        /// <summary>
        /// Código de la rendición grupal, <c>CON-AAAA-NNNN</c>: el nombre del conjunto de planillas
        /// que se consolidaron juntas. Sobrevive al reemplazo del archivo. Null en los consolidados
        /// anteriores a la columna.
        /// </summary>
        public string? Codigo { get; set; }

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
        /// <summary>
        /// La PLANILLA GRUPAL: el PDF que junta en un solo documento las planillas de gasto de todo
        /// lo que cubre el consolidado. La genera Abril One al adjuntarse el S10, no se sube. Null
        /// en los consolidados anteriores a la columna.
        /// </summary>
        public string? PlanillaGrupalUrl { get; set; }
        public string? PlanillaGrupalFilename { get; set; }
        /// <summary>Copia con la firma de la jefatura. Null mientras no se apruebe el reembolso.</summary>
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoFilename { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }
        public DateTimeOffset UploadedAt { get; set; }
        /// <summary>Quién lo adjuntó: el consolidador. Null si no se pudo resolver.</summary>
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

        /// <summary>
        /// Razón social bajo la que quedó el registro del S10: la del consolidador que lo adjuntó
        /// (ver <c>RazonSocialConsolidador</c>), no la de los trabajadores, que pueden ser de varias.
        /// Null si no la tiene cargada.
        /// </summary>
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

        // ── Qué puede hacer la jefatura ──────────────────────────────────
        /// <summary>
        /// Salidas del consolidado con el reembolso listo para decidir que le toca decidir a ESTE
        /// usuario: las de los trabajadores de los que es la jefatura (el revisor que resuelve
        /// <c>IJefeRevisorResolver</c>, ver <c>RevisorDeLaSalida</c>). 0 = no tiene nada que decidir
        /// acá, aunque vea el consolidado: el consolidador, un gerente o GTH lo ven pero no lo
        /// aprueban.
        /// </summary>
        public int PorDecidirCount { get; set; }

        // ── Qué puede hacer el consolidador ──────────────────────────────
        /// <summary>
        /// True si el usuario es consolidador de TODOS los trabajadores de las planillas que cubre
        /// (Consolidados → Configuración → Consolidadores): es el dueño del trámite del S10 y las
        /// acciones de abajo son suyas.
        /// </summary>
        public bool PuedeConsolidar { get; set; }

        /// <summary>
        /// True si el consolidador puede avisarle a la jefatura que el consolidado la está esperando:
        /// tiene salidas con el reembolso Pendiente que decide OTRO (si el consolidador es también su
        /// jefe, las decide él). Se puede repetir a propósito (un correo se pierde);
        /// <see cref="JefaturaAvisadaAt"/> dice cuándo fue el último aviso.
        /// </summary>
        public bool PuedeAvisarJefatura { get; set; }

        /// <summary>Último aviso a la jefatura por las salidas de este consolidado. Null si nunca.</summary>
        public DateTimeOffset? JefaturaAvisadaAt { get; set; }

        /// <summary>
        /// True si el consolidador puede pedirle la corrección al Coordinador ERP: el reembolso está
        /// observado (por la jefatura o por Tesorería) y no hay otra corrección en curso. Es un
        /// camino ALTERNATIVO a volver a adjuntar el consolidado corregido, no un paso obligatorio.
        /// </summary>
        public bool PuedeSolicitarCorreccion { get; set; }

        /// <summary>
        /// La corrección con el Coordinador ERP que está viva en alguna de sus planillas. Null en el
        /// caso normal: casi ningún consolidado pasa por el ERP. Su estado dice de quién es la pelota.
        /// </summary>
        public CorreccionS10Dto? CorreccionS10 { get; set; }
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
        /// <summary>Devueltos con una observación: la pelota está en el consolidador.</summary>
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
        /// <summary>Obligatoria al observar: es lo que el consolidador va a leer para subsanar.</summary>
        public string? Observacion { get; set; }
    }

    /// <summary>
    /// Cuerpo de "Solicitar corrección al ERP". Un solo campo, que es el «MOTIVO *» del
    /// requerimiento: viaja en el cuerpo y no en la query porque es texto libre y largo.
    /// </summary>
    public class SolicitarCorreccionS10Dto
    {
        /// <summary>Qué corrección se necesita en el S10. Obligatorio (RG-21 / CA-17).</summary>
        public string Motivo { get; set; } = string.Empty;
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
        /// <summary>
        /// Qué acción se está por confirmar. Null (o <see cref="ConsolidadoCorreoAcciones.Reembolso"/>)
        /// = la decisión de la jefatura; las otras dos son del consolidador y van sobre UN
        /// consolidado. Ver <see cref="ConsolidadoCorreoAcciones"/>.
        /// </summary>
        public string? Accion { get; set; }
    }

    /// <summary>Valores de <see cref="ConsolidadoCorreoPreviewRequestDto.Accion"/>.</summary>
    public static class ConsolidadoCorreoAcciones
    {
        /// <summary>La jefatura aprueba (firma) u observa el reembolso: avisa al consolidador.</summary>
        public const string Reembolso = "REEMBOLSO";

        /// <summary>El consolidador le avisa a la jefatura que el consolidado la está esperando.</summary>
        public const string AvisoJefatura = "AVISO_JEFATURA";

        /// <summary>El consolidador le pide la corrección al Coordinador ERP.</summary>
        public const string CorreccionErp = "CORRECCION_ERP";
    }

    /// <summary>
    /// Lo que necesita "Avisar a la jefatura" sobre un consolidado: si el usuario es su
    /// consolidador, qué salidas están esperando a la jefatura y a quién hay que escribirle.
    /// </summary>
    public class AvisoJefaturaInfoDto
    {
        public bool PuedeConsolidar { get; set; }
        /// <summary>Salidas visibles del consolidado con el reembolso Pendiente.</summary>
        public List<int> SolicitudIds { get; set; } = new();
        /// <summary>Correos de la jefatura de esas salidas (sus revisores), sin repetir.</summary>
        public List<string> JefaturaEmails { get; set; } = new();
        /// <summary>Nombres de esa jefatura, para el mensaje de la pantalla.</summary>
        public List<string> JefaturaNombres { get; set; } = new();
        /// <summary>Datos del correo. Null si no hay nada pendiente.</summary>
        public ConsolidadoCorreoDatos? Datos { get; set; }
    }

    /// <summary>
    /// Lo que necesita "Solicitar corrección al ERP" sobre un consolidado: si el usuario es su
    /// consolidador, qué planillas quedaron observadas y si ya hay una corrección en curso.
    /// </summary>
    public class CorreccionConsolidadoPlanDto
    {
        public bool PuedeConsolidar { get; set; }
        /// <summary>Planillas del consolidado con alguna salida Observada: se pide una corrección por cada una.</summary>
        public List<int> RendicionIdsObservadas { get; set; } = new();
        public bool HayCorreccionEnCurso { get; set; }
        public string? NumeroReembolso { get; set; }
        /// <summary>Nombre de quien pide la corrección (el usuario), para el correo al ERP.</summary>
        public string? Solicitante { get; set; }
        /// <summary>Datos del consolidado para el correo. Null si no hay nada observado.</summary>
        public ConsolidadoCorreoDatos? Datos { get; set; }
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
        /// <summary>
        /// Consolidado del S10 que respalda a la planilla: es lo que abre el botón del correo,
        /// porque la bandeja de Tesorería lista por documento. Null solo si la planilla no tiene
        /// consolidado vigente, y ahí el botón lleva a la bandeja sin abrir nada.
        /// </summary>
        public int? ConsolidadoId { get; set; }
    }
}
