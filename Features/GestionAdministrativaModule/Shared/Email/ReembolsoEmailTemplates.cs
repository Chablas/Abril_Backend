using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Email
{
    /// <summary>
    /// Lo que necesitan los correos que hablan de un CONSOLIDADO del S10: el aviso a la jefatura de
    /// que hay uno por revisar, y los que le vuelven al consolidador con la decisión de la jefatura o
    /// con la observación de Tesorería. Es por consolidado y no por salida porque el consolidado es
    /// el documento que se decide y el que el consolidador tiene que subsanar: un correo por salida
    /// le llegaría repetido tantas veces como salidas cubra. Lo arma
    /// <c>ConsolidadoCorreoLoader</c> en un número fijo de consultas.
    /// </summary>
    public sealed class ConsolidadoCorreoDatos
    {
        public int ConsolidadoId { get; set; }
        /// <summary>Código CONS-SIGLA-AAAA-NNN. Null en los consolidados anteriores a la columna.</summary>
        public string? Codigo { get; set; }
        /// <summary>Número de reembolso que devolvió el S10. Null en los consolidados viejos.</summary>
        public string? NumeroReembolso { get; set; }
        /// <summary>Importe declarado en el S10 (el documento entero). Null en los consolidados viejos.</summary>
        public decimal? MontoTotal { get; set; }
        /// <summary>Códigos REN-AAAA-NNNN de las planillas que cubre.</summary>
        public List<string> Rendiciones { get; set; } = new();
        /// <summary>Trabajadores de las salidas de las que habla el correo, sin repetir.</summary>
        public List<string> Trabajadores { get; set; } = new();
        /// <summary>Periodo de esas salidas ("Agosto 2026", o un rango si cruza meses).</summary>
        public string? Periodo { get; set; }
        /// <summary>Cuántas salidas del consolidado entran en el aviso.</summary>
        public int SalidasCount { get; set; }
        /// <summary>Nombre de quien adjuntó el consolidado: el consolidador.</summary>
        public string? Consolidador { get; set; }
        /// <summary>Correo del consolidador (app_user.email): el destinatario de los correos de vuelta.</summary>
        public string? ConsolidadorEmail { get; set; }
        /// <summary>Nombre de quien decidió u observó (jefatura o Tesorería). Null si no aplica.</summary>
        public string? DecididoPor { get; set; }

        /// <summary>
        /// Quién acaba de firmar, cuando el aviso sale porque ALGUIEN FIRMÓ y el documento todavía
        /// debe otra firma (en obra: el administrador de obra, y detrás el residente). Null cuando
        /// el aviso es el del consolidador al adjuntar, que es el otro camino al mismo correo.
        /// </summary>
        public string? FirmoAntes { get; set; }
        /// <summary>Observación vigente de esas salidas. Solo la usan los correos de observado.</summary>
        public string? Observacion { get; set; }
    }

    /// <summary>
    /// Lo que necesita el aviso a Tesorería de que la jefatura terminó de firmar un Consolidado del
    /// S10. Describe el DOCUMENTO entero —lo que Tesorería abre, revisa y paga—, no las planillas
    /// sueltas: con un aviso por planilla, un consolidado de tres le llegaba tres veces.
    /// </summary>
    public sealed class ConsolidadoTesoreriaCorreoDatos
    {
        public int ConsolidadoId { get; set; }
        /// <summary>Código CONS-SIGLA-AAAA-NNN. Null en los consolidados anteriores a la columna.</summary>
        public string? Codigo { get; set; }
        /// <summary>Área con la que se armó el código (la del consolidador). Null en los antiguos.</summary>
        public string? Area { get; set; }
        /// <summary>Número de reembolso que devolvió el S10. Null en los consolidados viejos.</summary>
        public string? NumeroReembolso { get; set; }
        /// <summary>Cuántas planillas cubre el documento.</summary>
        public int RendicionesCount { get; set; }
        /// <summary>
        /// Suma de las planillas COMPLETAS que cubre: el «Total Abril One» que Tesorería ve al
        /// abrirlo, contra el que se declaró el importe del S10.
        /// </summary>
        public decimal MontoRendido { get; set; }
        /// <summary>
        /// Quiénes lo firmaron, en el orden de la cadena (en obra, el administrador y después el
        /// residente). El último es quien lo terminó de firmar y disparó el aviso.
        /// </summary>
        public List<string> Firmantes { get; set; } = new();

        /// <summary>
        /// Lo que Tesorería había observado, cuando el consolidado VUELVE a su bandeja con esa
        /// observación subsanada. Solo lo usa ese aviso (plantilla 20); null en el de siempre.
        /// </summary>
        public string? ObservacionTesoreria { get; set; }
    }

    /// <summary>
    /// Lo que necesita el aviso a Tesorería de que confirmó la revisión de un Consolidado del S10 y
    /// quedó listo para programar el pago (plantilla 21). Es el resumen de lo que va a desembolsar,
    /// uno por consolidado, igual que el aviso de consolidado firmado.
    /// </summary>
    public sealed class ConsolidadoPorPagarCorreoDatos
    {
        public int ConsolidadoId { get; set; }
        /// <summary>Código CONS-SIGLA-AAAA-NNN. Null en los consolidados anteriores a la columna.</summary>
        public string? Codigo { get; set; }
        /// <summary>Área con la que se armó el código (la del consolidador). Null en los antiguos.</summary>
        public string? Area { get; set; }
        /// <summary>Número de reembolso que devolvió el S10. Null en los consolidados viejos.</summary>
        public string? NumeroReembolso { get; set; }
        /// <summary>
        /// Lo que quedó listo para pagar: la suma de sus salidas en «Proceder con el reembolso», que
        /// es exactamente lo que desembolsa «Marcar como pagado». Casi siempre es el consolidado
        /// entero; es menos cuando alguna de sus planillas todavía espera a su jefatura o ya se pagó.
        /// </summary>
        public decimal MontoTotal { get; set; }
    }

    /// <summary>
    /// Lo que necesita el aviso al trabajador de que su rendición quedó incluida en un Consolidado
    /// del S10. Va UNO por (planilla, trabajador): una planilla puede agrupar a varios trabajadores
    /// y a cada uno le importa lo suyo.
    /// </summary>
    public sealed class RendicionConsolidadaCorreoDatos
    {
        public int RendicionId { get; set; }
        /// <summary>Código REN-AAAA-NNNN de la planilla.</summary>
        public string Codigo { get; set; } = string.Empty;
        /// <summary>Correo del trabajador (app_user.email): es el destinatario.</summary>
        public string? TrabajadorEmail { get; set; }
        /// <summary>Lo que rindió ESTE trabajador en la planilla, en soles.</summary>
        public decimal MontoTrabajador { get; set; }
        /// <summary>Código CONS-SIGLA-AAAA-NNN. Null solo en consolidados anteriores a la columna.</summary>
        public string? ConsolidadoCodigo { get; set; }
        /// <summary>Área del consolidado (la del consolidador). Null si no se pudo resolver.</summary>
        public string? Area { get; set; }
        /// <summary>Número de reembolso que devolvió el S10.</summary>
        public string? NumeroReembolso { get; set; }
        /// <summary>Nombre de quien adjuntó el consolidado.</summary>
        public string? Consolidador { get; set; }
    }

    /// <summary>
    /// Lo que necesita el aviso al trabajador de que su rendición quedó incluida en una planilla
    /// grupal: el paso anterior al Consolidado del S10. Va UNO por (planilla, trabajador), igual que
    /// el de rendición consolidada.
    /// </summary>
    public sealed class RendicionEnPlanillaGrupalCorreoDatos
    {
        public int RendicionId { get; set; }
        /// <summary>Código REN-AAAA-NNNN de la planilla.</summary>
        public string Codigo { get; set; } = string.Empty;
        /// <summary>Correo del trabajador (app_user.email): es el destinatario.</summary>
        public string? TrabajadorEmail { get; set; }
        /// <summary>Lo que rindió ESTE trabajador en la planilla, en soles.</summary>
        public decimal MontoTrabajador { get; set; }
        /// <summary>Código CONS-SIGLA-AAAA-NNN de la planilla grupal (el que después hereda el consolidado).</summary>
        public string PlanillaGrupalCodigo { get; set; } = string.Empty;
        /// <summary>Área de la planilla grupal (la del consolidador). Null si no se pudo resolver.</summary>
        public string? Area { get; set; }
        /// <summary>Nombre de quien preparó la planilla grupal: el consolidador.</summary>
        public string? Consolidador { get; set; }
    }

    /// <summary>
    /// Lo que necesita el aviso de pago a UNA persona: todo lo que Tesorería le acaba de pagar, con
    /// el monto sumado y sus rendiciones una por una. Va UNO por persona y por pago —aunque el pago
    /// cubra varias de sus planillas, o varios consolidados—: con uno por planilla, quien tenía
    /// tres rendiciones en el consolidado recibía tres correos del mismo pago.
    /// </summary>
    public sealed class ReembolsoPagadoCorreoDatos
    {
        public string Trabajador { get; set; } = string.Empty;
        /// <summary>Correo del trabajador (app_user.email): es el destinatario.</summary>
        public string? TrabajadorEmail { get; set; }
        public string? Area { get; set; }
        /// <summary>
        /// Quienes adjuntaron los Consolidados del S10 que cubren esas planillas, sin repetir: los
        /// consolidadores. Casi siempre uno; más solo si Tesorería pagó varios consolidados juntos.
        /// </summary>
        public List<string> Consolidadores { get; set; } = new();
        /// <summary>Números de reembolso de esos consolidados, sin repetir. Casi siempre uno.</summary>
        public List<string> NumerosReembolso { get; set; } = new();
        /// <summary>Periodo de todas las salidas pagadas ("Agosto 2026", o un rango si cruza meses).</summary>
        public string? Periodo { get; set; }
        /// <summary>Cuántas salidas suyas entran en el pago.</summary>
        public int SalidasCount { get; set; }
        /// <summary>Lo que se le pagó: la suma de sus rendiciones, en soles.</summary>
        public decimal MontoTotal { get; set; }
        /// <summary>Nombre del tesorero que registró el pago.</summary>
        public string? PagadoPor { get; set; }
        /// <summary>Sus planillas pagadas, ordenadas por código. Nunca vacía.</summary>
        public List<RendicionPagadaCorreoDatos> Rendiciones { get; set; } = new();
    }

    /// <summary>Una planilla del aviso de pago: lo que se le pagó a la persona por ella.</summary>
    public sealed class RendicionPagadaCorreoDatos
    {
        public int RendicionId { get; set; }
        /// <summary>Código REN-AAAA-NNNN de la planilla, o <c>#id</c> en las anteriores a la columna.</summary>
        public string Codigo { get; set; } = string.Empty;
        /// <summary>Número de la planilla ("TI: 000123"), o null si la planilla no lo tiene.</summary>
        public string? NumeroPlanilla { get; set; }
        /// <summary>Periodo que cubre la planilla ("Agosto 2026", o un rango si cruza meses).</summary>
        public string? Periodo { get; set; }
        /// <summary>Cuántas salidas de la persona entran en la planilla.</summary>
        public int SalidasCount { get; set; }
        /// <summary>Lo rendido por la persona en esa planilla, en soles.</summary>
        public decimal Monto { get; set; }
    }

    /// <summary>
    /// Lo que necesita el aviso al consolidador de que Tesorería pagó su consolidado (plantilla 22).
    /// Tesorería le abona el total a él y él le reembolsa a cada trabajador, así que lleva el monto
    /// pagado y el reparto por persona. Va UNO por consolidado.
    /// </summary>
    public sealed class ConsolidadoPagadoCorreoDatos
    {
        public int ConsolidadoId { get; set; }
        /// <summary>Código CONS-SIGLA-AAAA-NNN. Null en los consolidados anteriores a la columna.</summary>
        public string? Codigo { get; set; }
        /// <summary>
        /// Código de la planilla grupal con la que se registró en el S10. Null en los consolidados
        /// anteriores a la planilla preparada.
        /// </summary>
        public string? PlanillaGrupalCodigo { get; set; }
        /// <summary>Área del consolidado (la del consolidador). Null si no se pudo resolver.</summary>
        public string? Area { get; set; }
        /// <summary>Número de reembolso que devolvió el S10. Null en los consolidados viejos.</summary>
        public string? NumeroReembolso { get; set; }
        /// <summary>Correo de quien adjuntó el consolidado (app_user.email): es el destinatario.</summary>
        public string? ConsolidadorEmail { get; set; }
        /// <summary>Nombre del tesorero que registró el pago.</summary>
        public string? PagadoPor { get; set; }
        /// <summary>
        /// Lo que se pagó: la suma de lo que le toca a cada trabajador. Casi siempre es el
        /// consolidado entero; es menos cuando alguna de sus planillas todavía no estaba por pagar.
        /// </summary>
        public decimal MontoTotal { get; set; }
        /// <summary>Cuántas planillas entran en el pago.</summary>
        public int RendicionesCount { get; set; }
        /// <summary>Lo que le toca a cada persona, ordenado por nombre. Nunca vacía.</summary>
        public List<ReembolsoPorTrabajadorCorreoDatos> Trabajadores { get; set; } = new();
    }

    /// <summary>Una fila del reparto del aviso al consolidador: una persona, sus planillas y su monto.</summary>
    public sealed class ReembolsoPorTrabajadorCorreoDatos
    {
        public string Trabajador { get; set; } = string.Empty;
        /// <summary>Códigos de sus planillas pagadas en el consolidado, ordenados.</summary>
        public List<string> Rendiciones { get; set; } = new();
        /// <summary>Lo que le toca: la suma de esas planillas, en soles.</summary>
        public decimal Monto { get; set; }
    }

    /// <summary>
    /// Los correos que cierran el ciclo de la rendición, todos con el mismo chrome de la intranet
    /// (<see cref="SalidaEmailLayout"/>):
    ///
    /// <list type="bullet">
    ///   <item>Al trabajador: su rendición quedó incluida en la planilla grupal que preparó el
    ///     consolidador.</item>
    ///   <item>Al trabajador: su rendición quedó incluida en el Consolidado del S10 que adjuntó el
    ///     consolidador.</item>
    ///   <item>A la jefatura: el consolidador adjuntó un Consolidado del S10 y está esperando su
    ///     visto bueno.</item>
    ///   <item>Al consolidador: la jefatura aprobó (y firmó) el consolidado.</item>
    ///   <item>Al consolidador: la jefatura lo observó, con el comentario a subsanar.</item>
    ///   <item>A Tesorería: la jefatura terminó de firmar el consolidado y su reembolso entró a la
    ///     bandeja. Uno por consolidado, no uno por planilla.</item>
    ///   <item>A Tesorería, en lugar del anterior: el consolidado que ella había observado vuelve
    ///     firmado, con la observación subsanada.</item>
    ///   <item>A Tesorería: confirmó la revisión del consolidado y quedó listo para programar el
    ///     pago.</item>
    ///   <item>Al consolidador: Tesorería devolvió el consolidado antes de pagarlo (RG-49).</item>
    ///   <item>Al consolidador: Tesorería le pagó el consolidado y cuánto le toca reembolsar a cada
    ///     trabajador. Uno por consolidado.</item>
    ///   <item>Al trabajador: Tesorería ya pagó — el cierre del ciclo. Uno por persona con todas
    ///     sus rendiciones pagadas, no uno por planilla.</item>
    /// </list>
    ///
    /// Desde la primera revisión aprobada el trámite del S10 es del consolidador, así que todo lo
    /// que hay que subsanar le vuelve a él; al trabajador solo le llega el pago.
    ///
    /// Todos llevan UN botón que abre la pantalla exacta en la intranet: el correo avisa y lleva,
    /// no explica el flujo (ver el criterio editorial en <see cref="AbrilEmailLayout"/>).
    /// </summary>
    public static class ReembolsoEmailTemplates
    {
        // Íconos del catálogo de public/images/emails/icons (los genera
        // Abril-Frontend/scripts/generate-email-icons.js). Se reutilizan los que ya existen: no
        // hace falta un juego propio de salidas para tres correos.
        private const string IconoRevisar     = "req-solicitud";
        private const string IconoIncluida    = "req-formulario";
        private const string IconoAprobado    = "req-aprobada";
        private const string IconoObservado   = "req-decision";
        private const string IconoFranjaOk    = "req-check";
        private const string IconoFranjaNo    = "req-rechazadas";
        private const string IconoFranjaAviso = "req-aviso";

        private const string IconoPago        = "req-aprobada";
        private const string IconoSubsanada   = "req-aprobada";
        private const string IconoPorPagar    = "req-aprobada";

        private const string FilaTrabajador = "req-solicitante";
        private const string FilaArea       = "req-area";
        private const string FilaFecha      = "req-fecha";
        private const string FilaPlanilla   = "req-codigo";
        private const string FilaMonto      = "req-sustento";
        private const string FilaReembolso       = "req-ti";
        private const string FilaRendiciones = "req-formulario";
        private const string FilaFirma       = "req-vistobueno";
        private const string FilaObservacion = "req-comentario";
        private const string FilaEstado      = "req-estado";
        private const string FilaPlanillaGrupal = "req-formulario";

        private const string SeccionRendiciones  = "req-formulario";
        private const string SeccionTrabajadores = "req-candidatos";

        /// <summary>
        /// Al trabajador: su rendición quedó incluida en la planilla grupal que acaba de preparar el
        /// consolidador —el papel con el que la registra en el S10—. Es el paso anterior al de
        /// <see cref="RendicionConsolidada"/> y tiene su mismo molde: informativo, con el botón a su
        /// rendición en Mis Rendiciones. La cabecera es «Rendición incluida en una planilla grupal»:
        /// la frase en segunda persona no entra en una línea.
        /// </summary>
        public static string RendicionEnPlanillaGrupal(
            SalidaEmailLayout l, RendicionEnPlanillaGrupalCorreoDatos d, string urlVer)
        {
            var area = string.IsNullOrWhiteSpace(d.Area)
                ? string.Empty
                : $" del área <b>{AbrilEmailLayout.Esc(d.Area)}</b>";

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoIncluida,
                    "Rendición incluida en una planilla grupal",
                    $"Tu rendición <b>{AbrilEmailLayout.Esc(d.Codigo)}</b> fue incluida en la planilla "
                    + $"grupal <b>{AbrilEmailLayout.Esc(d.PlanillaGrupalCodigo)}</b>{area}."),
                l.Tarjeta(FilasRendicionEnPlanillaGrupal(d)),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Info,
                    "La planilla grupal está pendiente de su registro en el S10 y de la firma de la jefatura."),
                l.Boton("Ver mi rendición", urlVer),
                l.EnlaceDirecto(urlVer));
        }

        /// <summary>
        /// Al trabajador: su rendición quedó incluida en el Consolidado del S10 que acaba de adjuntar
        /// el consolidador (plantilla 11 del área usuaria). Es informativo —lo que sigue es la firma
        /// de la jefatura—, así que el botón solo lo lleva a su rendición en Mis Rendiciones.
        /// </summary>
        public static string RendicionConsolidada(
            SalidaEmailLayout l, RendicionConsolidadaCorreoDatos d, string urlVer)
        {
            var consolidado = string.IsNullOrWhiteSpace(d.ConsolidadoCodigo)
                ? string.Empty
                : $" <b>{AbrilEmailLayout.Esc(d.ConsolidadoCodigo)}</b>";
            var area = string.IsNullOrWhiteSpace(d.Area)
                ? string.Empty
                : $" del área <b>{AbrilEmailLayout.Esc(d.Area)}</b>";

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoIncluida,
                    "Tu rendición fue incluida en un consolidado",
                    $"Tu rendición <b>{AbrilEmailLayout.Esc(d.Codigo)}</b> fue incluida en el Consolidado "
                    + $"del S10{consolidado}{area}."),
                l.Tarjeta(FilasRendicionConsolidada(d)),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Info,
                    "El consolidado está pendiente de la revisión y firma de la jefatura antes de pasar a Tesorería."),
                l.Boton("Ver mi rendición", urlVer),
                l.EnlaceDirecto(urlVer));
        }

        /// <summary>
        /// A la jefatura que tiene que firmar AHORA: un Consolidado del S10 está esperando su visto
        /// bueno. El botón abre ese consolidado en Consolidados, que es donde la jefatura lo aprueba
        /// (firma) u observa.
        ///
        /// Sale por dos caminos y lo dice en la primera línea, porque a quien lo recibe le cambia lo
        /// que tiene delante: el consolidador que acaba de adjuntar el documento, o —en obra— la
        /// firma anterior, que es la que le pasa el turno (<see cref="ConsolidadoCorreoDatos.FirmoAntes"/>).
        /// </summary>
        public static string ConsolidadoPorRevisar(SalidaEmailLayout l, ConsolidadoCorreoDatos d, string urlRevisar)
        {
            var enCadena = !string.IsNullOrWhiteSpace(d.FirmoAntes);

            var bajada = enCadena
                ? $"<b>{AbrilEmailLayout.Esc(d.FirmoAntes!)}</b> ya firmó el Consolidado del S10{Numero(d)}."
                : $"{(string.IsNullOrWhiteSpace(d.Consolidador)
                        ? "El consolidador"
                        : $"<b>{AbrilEmailLayout.Esc(d.Consolidador)}</b>")}"
                  + $" adjuntó el Consolidado del S10{Numero(d)}.";

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoRevisar,
                    enCadena ? "Falta tu firma" : "Consolidado por revisar",
                    bajada),
                l.Tarjeta(FilasConsolidado(d)),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Info,
                    enCadena
                        ? "Con tu firma el consolidado queda aprobado y pasa a Tesorería."
                        : "Falta tu visto bueno para que el consolidado pase a firma y a Tesorería."),
                l.Boton("Revisar el consolidado", urlRevisar),
                l.EnlaceDirecto(urlRevisar));
        }

        /// <summary>
        /// Al consolidador: la jefatura aprobó el consolidado —aprobar ES firmar—, así que ya pasó a
        /// Tesorería. Es informativo: el botón solo abre el consolidado.
        /// </summary>
        public static string ConsolidadoAprobado(SalidaEmailLayout l, ConsolidadoCorreoDatos d, string urlVer) =>
            l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoAprobado,
                    "Consolidado aprobado",
                    $"La jefatura aprobó el Consolidado del S10{Numero(d)}."),
                l.Franja(IconoFranjaOk, AbrilEmailLayout.Tono.Verde,
                    string.IsNullOrWhiteSpace(d.DecididoPor)
                        ? "Firmado por la jefatura: pasa a Tesorería."
                        : $"Firmado por <b>{AbrilEmailLayout.Esc(d.DecididoPor)}</b>: pasa a Tesorería."),
                l.Tarjeta(FilasConsolidado(d)),
                l.Boton("Ver el consolidado", urlVer),
                l.EnlaceDirecto(urlVer));

        /// <summary>
        /// Al consolidador: la jefatura OBSERVÓ el consolidado. La observación va en la franja roja
        /// porque es lo único que tiene que leer, y la ámbar nombra los DOS caminos que
        /// tiene: volver a adjuntar el Consolidado del S10 corregido, o pedirle la corrección al
        /// Coordinador ERP cuando el arreglo tiene que hacerse dentro del S10 (§10.5).
        /// </summary>
        public static string ConsolidadoObservado(SalidaEmailLayout l, ConsolidadoCorreoDatos d, string urlVer)
        {
            var observacion = string.IsNullOrWhiteSpace(d.Observacion)
                ? "Sin observación registrada. Coordina con la jefatura antes de volver a adjuntarlo."
                : AbrilEmailLayout.EscMultilinea(d.Observacion.Trim());

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoObservado,
                    "Consolidado observado",
                    $"La jefatura observó el Consolidado del S10{Numero(d)}."),
                l.Franja(IconoFranjaNo, AbrilEmailLayout.Tono.Rojo,
                    $"<b>Observación:</b> {observacion}"),
                l.Tarjeta(FilasConsolidado(d)),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Ambar,
                    "Vuelve a adjuntar el Consolidado del S10 corregido, o solicita la corrección al "
                    + "Coordinador ERP si el arreglo tiene que hacerse dentro del S10."),
                l.Boton("Ver el consolidado", urlVer),
                l.EnlaceDirecto(urlVer));
        }

        /// <summary>
        /// A Tesorería: la jefatura terminó de firmar un Consolidado del S10 y su reembolso ya está
        /// en la bandeja de pago (RF-TES-01). Va UNO por consolidado —lo que Tesorería revisa y paga
        /// es el documento—, con el resumen de lo que cubre. El botón lo abre en Reembolsos, que es
        /// donde Tesorería confirma la revisión documental y recién después puede pagar.
        ///
        /// La franja nombra lo que va a encontrar al abrirlo, en el orden en que lo muestra el
        /// detalle de Reembolsos: si ese detalle cambia de orden o de documentos, cambia esta línea.
        /// </summary>
        public static string ConsolidadoParaTesoreria(
            SalidaEmailLayout l, ConsolidadoTesoreriaCorreoDatos d, string urlRevisar)
        {
            var quien = d.Firmantes.Count == 0
                ? "La jefatura"
                : $"<b>{AbrilEmailLayout.Esc(d.Firmantes[^1])}</b>";
            var codigo = string.IsNullOrWhiteSpace(d.Codigo)
                ? string.Empty
                : $" <b>{AbrilEmailLayout.Esc(d.Codigo)}</b>";
            var area = string.IsNullOrWhiteSpace(d.Area)
                ? string.Empty
                : $" del área <b>{AbrilEmailLayout.Esc(d.Area)}</b>";

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoRevisar,
                    "Consolidado pendiente de revisión",
                    $"{quien} firmó digitalmente el Consolidado del S10{codigo}{area}."),
                l.Tarjeta(FilasTesoreria(d)),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Info,
                    "En <b>Reembolsos</b> encontrarás la planilla grupal, el Consolidado del S10 y las "
                    + "rendiciones incluidas, con sus salidas y vouchers."),
                l.Boton("Revisar el consolidado", urlRevisar),
                l.EnlaceDirecto(urlRevisar));
        }

        /// <summary>
        /// A Tesorería, en lugar de <see cref="ConsolidadoParaTesoreria"/>: el consolidado que ella
        /// había observado vuelve firmado (plantilla 20). No entra por primera vez, así que le dice
        /// qué había observado para que lo revise contra eso.
        ///
        /// La plantilla del área usuaria supone que la jefatura revisa la observación y escribe un
        /// «motivo de subsanación»; acá la observación vuelve al consolidador, que recarga el
        /// consolidado, y la jefatura lo firma. Por eso la tarjeta lleva la observación de Tesorería
        /// y quién firmó, en vez de un motivo que nadie escribe. El título completo de la plantilla
        /// no entra en la cabecera (va en una sola línea): lo lleva el asunto.
        /// </summary>
        public static string ConsolidadoSubsanadoParaTesoreria(
            SalidaEmailLayout l, ConsolidadoTesoreriaCorreoDatos d, string urlRevisar)
        {
            var quien = d.Firmantes.Count == 0
                ? "La jefatura"
                : $"<b>{AbrilEmailLayout.Esc(d.Firmantes[^1])}</b>";
            var codigo = string.IsNullOrWhiteSpace(d.Codigo)
                ? string.Empty
                : $" <b>{AbrilEmailLayout.Esc(d.Codigo)}</b>";
            var area = string.IsNullOrWhiteSpace(d.Area)
                ? string.Empty
                : $" del área <b>{AbrilEmailLayout.Esc(d.Area)}</b>";

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoSubsanada,
                    "Observación subsanada",
                    $"{quien} firmó digitalmente el Consolidado del S10{codigo}{area}, que vuelve a "
                    + "Tesorería con la observación subsanada."),
                l.Tarjeta(FilasTesoreria(d)),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Info,
                    "Revisa nuevamente el consolidado. Si todo está conforme, confirma la revisión para "
                    + "continuar con la <b>programación del pago</b>."),
                l.Boton("Revisar el consolidado", urlRevisar),
                l.EnlaceDirecto(urlRevisar));
        }

        /// <summary>
        /// A Tesorería: confirmó la revisión documental de un Consolidado del S10 y quedó en
        /// «Proceder con el reembolso», listo para programar el pago (plantilla 21). Va UNO por
        /// consolidado, con el resumen de lo que se va a pagar. El botón lo abre en Reembolsos, que
        /// es donde se marca como pagado.
        ///
        /// El título completo de la plantilla no entra en la cabecera (va en una sola línea): lo
        /// lleva el asunto. La franja se adaptó al flujo real: la app no registra montos pagados
        /// —marca el consolidado como pagado—. Al pagar se avisa al consolidador y a los
        /// colaboradores (<see cref="ConsolidadoPagado"/> y <see cref="Pagado"/>): si eso cambia,
        /// cambia esta línea.
        /// </summary>
        public static string ConsolidadoListoParaPago(
            SalidaEmailLayout l, ConsolidadoPorPagarCorreoDatos d, string urlPagar) =>
            l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoPorPagar,
                    "Consolidado listo para programación de pago",
                    $"La revisión del Consolidado del S10{NombreEnBajada(d.Codigo, d.NumeroReembolso)} fue "
                    + "confirmada y ahora puede continuar con la programación del pago."),
                l.Tarjeta(FilasPorPagar(d)),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Info,
                    "Registra el pago en <b>Reembolsos</b>. Al marcarlo como pagado, Abril One notificará "
                    + "al <b>consolidador</b> y a <b>todos los colaboradores incluidos en el consolidado</b>."),
                l.Boton("Programar pago", urlPagar),
                l.EnlaceDirecto(urlPagar));

        /// <summary>
        /// Al consolidador: Tesorería devolvió el consolidado antes de pagarlo (RG-49). Es el mismo
        /// mensaje de fondo que el de la jefatura —qué corregir y por dónde— pero dice claramente de
        /// dónde viene: el consolidado ya estaba firmado, así que si no se nombra a Tesorería se va a
        /// ir a preguntarle a la jefatura.
        /// </summary>
        public static string ConsolidadoObservadoPorTesoreria(
            SalidaEmailLayout l, ConsolidadoCorreoDatos d, string urlVer)
        {
            var motivo = string.IsNullOrWhiteSpace(d.Observacion)
                ? "Sin motivo registrado. Coordina con Tesorería antes de volver a enviarlo."
                : AbrilEmailLayout.EscMultilinea(d.Observacion.Trim());

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoObservado,
                    "Reembolso observado por Tesorería",
                    $"Tesorería revisó el Consolidado del S10{CodigoYNumero(d)} y lo devolvió antes de pagarlo."),
                l.Franja(IconoFranjaNo, AbrilEmailLayout.Tono.Rojo,
                    $"<b>Motivo:</b> {motivo}"),
                l.Tarjeta(FilasConsolidado(d, porCodigo: true)),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Ambar,
                    "Vuelve a adjuntar el Consolidado del S10 corregido, o solicita la corrección al "
                    + "Coordinador ERP si el arreglo tiene que hacerse dentro del S10. Al recargarlo, "
                    + "vuelve a la jefatura para la firma y de ahí a Tesorería."),
                l.Boton("Ver el consolidado", urlVer),
                l.EnlaceDirecto(urlVer));
        }

        /// <summary>
        /// Al consolidador: Tesorería pagó el consolidado (plantilla 22, «El consolidado fue
        /// pagado»). Tesorería le abona el total a él y él le reembolsa a cada trabajador, así que la
        /// franja lleva el monto pagado y la tabla lo que le toca a cada persona, con sus planillas.
        /// El botón abre el consolidado en Consolidados, que es donde el consolidador sigue lo que
        /// adjuntó.
        ///
        /// De la plantilla no van «Fecha de pago» ni «Estado»: la fecha es la del correo y el
        /// estado lo dice la cabecera.
        /// </summary>
        public static string ConsolidadoPagado(SalidaEmailLayout l, ConsolidadoPagadoCorreoDatos d, string urlVer)
        {
            var monto = d.MontoTotal.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"));

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoPago,
                    "El consolidado fue pagado",
                    $"Tesorería registró el pago del Consolidado del S10{NombreEnBajada(d.Codigo, d.NumeroReembolso)}."),
                l.Franja(IconoFranjaOk, AbrilEmailLayout.Tono.Verde,
                    string.IsNullOrWhiteSpace(d.PagadoPor)
                        ? $"Monto reembolsado: <b>S/ {monto}</b>."
                        : $"Monto reembolsado: <b>S/ {monto}</b> · registrado por "
                          + $"<b>{AbrilEmailLayout.Esc(d.PagadoPor)}</b>."),
                l.Tarjeta(FilasConsolidadoPagado(d)),
                l.Seccion(SeccionTrabajadores, "Reembolso por trabajador"),
                TablaReembolsoPorTrabajador(l, d.Trabajadores),
                l.Boton("Ver el consolidado", urlVer),
                l.EnlaceDirecto(urlVer));
        }

        /// <summary>
        /// Al solicitante: Tesorería ya pagó su reembolso (RG-28). Es el cierre del ciclo, así que
        /// no pide nada — el botón solo lo lleva a Mis Rendiciones.
        ///
        /// Es UNO por persona con todo lo que se le pagó, y la franja lleva el monto sumado. Con
        /// una sola rendición va la tarjeta de siempre; con varias, la tarjeta lleva lo que tienen
        /// en común y debajo va una fila por rendición con lo que le tocó por cada una.
        /// </summary>
        public static string Pagado(SalidaEmailLayout l, ReembolsoPagadoCorreoDatos d, string urlVer)
        {
            var monto = d.MontoTotal.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"));
            var una   = d.Rendiciones.Count == 1;

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoPago,
                    "Reembolso realizado",
                    una
                        ? $"Tesorería registró el pago de tu rendición <b>{AbrilEmailLayout.Esc(d.Rendiciones[0].Codigo)}</b>."
                        : $"Tesorería registró el pago de <b>{d.Rendiciones.Count}</b> rendiciones tuyas."),
                l.Franja(IconoFranjaOk, AbrilEmailLayout.Tono.Verde,
                    string.IsNullOrWhiteSpace(d.PagadoPor)
                        ? $"Monto reembolsado: <b>S/ {monto}</b>."
                        : $"Monto reembolsado: <b>S/ {monto}</b> · registrado por "
                          + $"<b>{AbrilEmailLayout.Esc(d.PagadoPor)}</b>."),
                l.Tarjeta(FilasPagado(d)),
                una ? "" : l.Seccion(SeccionRendiciones, "Rendiciones pagadas"),
                una ? "" : TablaRendicionesPagadas(l, d.Rendiciones),
                l.Boton(una ? "Ver mi rendición" : "Ver mis rendiciones", urlVer),
                l.EnlaceDirecto(urlVer));
        }

        /// <summary>
        /// Cómo se nombra el consolidado en un asunto: " - CONS-…", o " - Consolidado del S10 N.° …"
        /// en los anteriores al código. Lo comparten los servicios que mandan estos correos.
        /// </summary>
        public static string NombreEnAsunto(string? codigo, string? numeroReembolso) =>
            !string.IsNullOrWhiteSpace(codigo) ? $" - {codigo}"
            : !string.IsNullOrWhiteSpace(numeroReembolso) ? $" - Consolidado del S10 N.° {numeroReembolso}"
            : string.Empty;

        /// <summary>
        /// Lo mismo para la bajada, después de «Consolidado del S10»: " <b>CONS-…</b>", o
        /// " <b>N.° …</b>" en los anteriores al código.
        /// </summary>
        private static string NombreEnBajada(string? codigo, string? numeroReembolso) =>
            !string.IsNullOrWhiteSpace(codigo) ? $" <b>{AbrilEmailLayout.Esc(codigo)}</b>"
            : !string.IsNullOrWhiteSpace(numeroReembolso) ? $" <b>N.° {AbrilEmailLayout.Esc(numeroReembolso)}</b>"
            : string.Empty;

        /// <summary>" N.° 00123" si el consolidado tiene número de reembolso; vacío si es de los viejos.</summary>
        private static string Numero(ConsolidadoCorreoDatos d) =>
            string.IsNullOrWhiteSpace(d.NumeroReembolso)
                ? string.Empty
                : $" <b>N.° {AbrilEmailLayout.Esc(d.NumeroReembolso)}</b>";

        /// <summary>
        /// " <b>CONS-…</b> (N.º de reembolso <b>…</b>)": el consolidado por su código y por el número
        /// con el que lo registró el S10. Sin código cae en <see cref="Numero"/>.
        /// </summary>
        private static string CodigoYNumero(ConsolidadoCorreoDatos d)
        {
            if (string.IsNullOrWhiteSpace(d.Codigo)) return Numero(d);

            var codigo = $" <b>{AbrilEmailLayout.Esc(d.Codigo)}</b>";
            return string.IsNullOrWhiteSpace(d.NumeroReembolso)
                ? codigo
                : $"{codigo} (N.º de reembolso <b>{AbrilEmailLayout.Esc(d.NumeroReembolso)}</b>)";
        }

        /// <summary>
        /// Filas de los correos de un CONSOLIDADO: qué planillas y trabajadores cubre, el periodo y
        /// el importe declarado en el S10. Las que no tienen dato no se agregan.
        /// </summary>
        /// <param name="porCodigo">
        /// El correo nombra el consolidado por su código: la primera fila es el código y no se
        /// listan las rendiciones que cubre.
        /// </param>
        private static List<AbrilEmailLayout.Fila> FilasConsolidado(ConsolidadoCorreoDatos d, bool porCodigo = false)
        {
            var filas = new List<AbrilEmailLayout.Fila>();

            if (porCodigo)
            {
                if (!string.IsNullOrWhiteSpace(d.Codigo))
                    filas.Add(new(FilaPlanilla, "Consolidado", AbrilEmailLayout.Esc(d.Codigo)));
            }
            else if (d.Rendiciones.Count > 0)
                filas.Add(new(FilaPlanilla, d.Rendiciones.Count == 1 ? "Rendición" : "Rendiciones",
                    AbrilEmailLayout.Esc(string.Join(", ", d.Rendiciones))));

            if (d.Trabajadores.Count > 0)
                filas.Add(new(FilaTrabajador, d.Trabajadores.Count == 1 ? "Trabajador" : "Trabajadores",
                    AbrilEmailLayout.Esc(ResumirNombres(d.Trabajadores))));

            var salidas = d.SalidasCount == 1 ? "1 salida" : $"{d.SalidasCount} salidas";
            filas.Add(new(FilaFecha, "Periodo",
                string.IsNullOrWhiteSpace(d.Periodo) ? salidas : $"{AbrilEmailLayout.Esc(d.Periodo)} · {salidas}"));

            if (!string.IsNullOrWhiteSpace(d.NumeroReembolso))
                filas.Add(new(FilaReembolso, "N.º de reembolso del S10", AbrilEmailLayout.Esc(d.NumeroReembolso)));

            if (d.MontoTotal is > 0m)
                filas.Add(new(FilaMonto, "Monto del consolidado",
                    $"S/ {d.MontoTotal.Value.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))}"));

            return filas;
        }

        /// <summary>
        /// Filas del aviso a Tesorería: el resumen del documento que va a revisar. Las que no tienen
        /// dato (consolidados anteriores al código o al área) no se agregan.
        /// </summary>
        private static List<AbrilEmailLayout.Fila> FilasTesoreria(ConsolidadoTesoreriaCorreoDatos d)
        {
            var filas = new List<AbrilEmailLayout.Fila>();

            if (!string.IsNullOrWhiteSpace(d.Codigo))
                filas.Add(new(FilaPlanilla, "Consolidado", AbrilEmailLayout.Esc(d.Codigo)));

            if (!string.IsNullOrWhiteSpace(d.Area))
                filas.Add(new(FilaArea, "Área", AbrilEmailLayout.Esc(d.Area)));

            if (!string.IsNullOrWhiteSpace(d.NumeroReembolso))
                filas.Add(new(FilaReembolso, "N.º de reembolso", AbrilEmailLayout.Esc(d.NumeroReembolso)));

            // Solo en el aviso de observación subsanada: lo que Tesorería había pedido corregir.
            if (!string.IsNullOrWhiteSpace(d.ObservacionTesoreria))
                filas.Add(new(FilaObservacion, "Observación de Tesorería",
                    AbrilEmailLayout.EscMultilinea(d.ObservacionTesoreria.Trim())));

            if (d.RendicionesCount > 0)
                filas.Add(new(FilaRendiciones, "Rendiciones incluidas", d.RendicionesCount.ToString()));

            // Siempre, aunque sea cero: es el dato que Tesorería va a pagar.
            filas.Add(new(FilaMonto, "Monto rendido",
                $"S/ {d.MontoRendido.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))}"));

            if (d.Firmantes.Count > 0)
                filas.Add(new(FilaFirma, "Firmado por",
                    string.Join("<br />", d.Firmantes.Select(AbrilEmailLayout.Esc))));

            return filas;
        }

        /// <summary>
        /// Filas del aviso de revisión confirmada, en el orden de la plantilla 21: el resumen del
        /// pago. Las que no tienen dato (consolidados anteriores al código o al área) no se agregan.
        /// </summary>
        private static List<AbrilEmailLayout.Fila> FilasPorPagar(ConsolidadoPorPagarCorreoDatos d)
        {
            var filas = new List<AbrilEmailLayout.Fila>();

            if (!string.IsNullOrWhiteSpace(d.Codigo))
                filas.Add(new(FilaPlanilla, "Consolidado", AbrilEmailLayout.Esc(d.Codigo)));

            if (!string.IsNullOrWhiteSpace(d.Area))
                filas.Add(new(FilaArea, "Área", AbrilEmailLayout.Esc(d.Area)));

            if (!string.IsNullOrWhiteSpace(d.NumeroReembolso))
                filas.Add(new(FilaReembolso, "N.º de reembolso", AbrilEmailLayout.Esc(d.NumeroReembolso)));

            // Siempre, aunque sea cero: es lo que se va a pagar.
            filas.Add(new(FilaMonto, "Monto total",
                $"S/ {d.MontoTotal.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))}"));

            // El nombre literal del estado (RG-26), no el «Por pagar» corto de las tablas.
            filas.Add(new(FilaEstado, "Estado",
                AbrilEmailLayout.Esc(EstadosSalida.Reembolso.NombrePorPagar)));

            return filas;
        }

        /// <summary>
        /// Filas del aviso de rendición consolidada, en el orden de la plantilla del área usuaria. Las
        /// que no tienen dato no se agregan; el monto tampoco cuando es cero (una planilla sin nada
        /// reembolsable para este trabajador).
        /// </summary>
        private static List<AbrilEmailLayout.Fila> FilasRendicionConsolidada(RendicionConsolidadaCorreoDatos d)
        {
            var filas = new List<AbrilEmailLayout.Fila>();

            if (!string.IsNullOrWhiteSpace(d.ConsolidadoCodigo))
                filas.Add(new(FilaPlanilla, "Consolidado", AbrilEmailLayout.Esc(d.ConsolidadoCodigo)));

            filas.Add(new(FilaRendiciones, "Rendición", AbrilEmailLayout.Esc(d.Codigo)));

            if (!string.IsNullOrWhiteSpace(d.NumeroReembolso))
                filas.Add(new(FilaReembolso, "N.º de reembolso", AbrilEmailLayout.Esc(d.NumeroReembolso)));

            if (d.MontoTrabajador > 0m)
                filas.Add(new(FilaMonto, "Monto de tu rendición",
                    $"S/ {d.MontoTrabajador.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))}"));

            if (!string.IsNullOrWhiteSpace(d.Consolidador))
                filas.Add(new(FilaTrabajador, "Consolidador", AbrilEmailLayout.Esc(d.Consolidador)));

            return filas;
        }

        /// <summary>
        /// Filas del aviso de rendición incluida en una planilla grupal: las mismas del de rendición
        /// consolidada, con la planilla grupal en lugar del consolidado y sin el número de reembolso,
        /// que todavía no existe.
        /// </summary>
        private static List<AbrilEmailLayout.Fila> FilasRendicionEnPlanillaGrupal(RendicionEnPlanillaGrupalCorreoDatos d)
        {
            var filas = new List<AbrilEmailLayout.Fila>
            {
                new(FilaPlanilla, "Planilla grupal", AbrilEmailLayout.Esc(d.PlanillaGrupalCodigo)),
                new(FilaRendiciones, "Rendición", AbrilEmailLayout.Esc(d.Codigo)),
            };

            if (d.MontoTrabajador > 0m)
                filas.Add(new(FilaMonto, "Monto de tu rendición",
                    $"S/ {d.MontoTrabajador.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))}"));

            if (!string.IsNullOrWhiteSpace(d.Consolidador))
                filas.Add(new(FilaTrabajador, "Consolidador", AbrilEmailLayout.Esc(d.Consolidador)));

            return filas;
        }

        /// <summary>"Ana, Luis y Rosa" o "Ana, Luis, Rosa +4": un consolidado puede cubrir a muchos.</summary>
        private static string ResumirNombres(List<string> nombres)
        {
            const int Maximo = 3;
            if (nombres.Count <= Maximo) return string.Join(", ", nombres);
            return $"{string.Join(", ", nombres.Take(Maximo))} +{nombres.Count - Maximo}";
        }

        /// <summary>
        /// Filas del aviso de pago: en vez de una fecha de salida suelta muestra el periodo que
        /// cubre y cuántas salidas trae. Con varias rendiciones el periodo, las salidas y el monto
        /// son los de todas juntas, y el número de planilla —que es de cada una— pasa a la tabla.
        /// </summary>
        private static List<AbrilEmailLayout.Fila> FilasPagado(ReembolsoPagadoCorreoDatos d)
        {
            var filas = new List<AbrilEmailLayout.Fila>
            {
                new(FilaTrabajador, "Trabajador", AbrilEmailLayout.Esc(d.Trabajador)),
            };

            // Mismo ícono que en el aviso de rendición consolidada: es una persona, como el trabajador.
            if (d.Consolidadores.Count > 0)
                filas.Add(new(FilaTrabajador, d.Consolidadores.Count == 1 ? "Consolidador" : "Consolidadores",
                    AbrilEmailLayout.Esc(string.Join(", ", d.Consolidadores))));

            if (!string.IsNullOrWhiteSpace(d.Area))
                filas.Add(new(FilaArea, "Área", AbrilEmailLayout.Esc(d.Area)));

            var salidas = d.SalidasCount == 1 ? "1 salida" : $"{d.SalidasCount} salidas";
            filas.Add(new(FilaFecha, "Periodo",
                string.IsNullOrWhiteSpace(d.Periodo) ? salidas : $"{AbrilEmailLayout.Esc(d.Periodo)} · {salidas}"));

            if (d.Rendiciones.Count == 1 && !string.IsNullOrWhiteSpace(d.Rendiciones[0].NumeroPlanilla))
                filas.Add(new(FilaPlanilla, "Planilla", AbrilEmailLayout.Esc(d.Rendiciones[0].NumeroPlanilla)));

            if (d.NumerosReembolso.Count > 0)
                filas.Add(new(FilaReembolso, "N.º de reembolso del S10",
                    AbrilEmailLayout.Esc(string.Join(", ", d.NumerosReembolso))));

            if (d.MontoTotal > 0m)
                filas.Add(new(FilaMonto, "Monto rendido",
                    $"S/ {d.MontoTotal.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))}"));

            return filas;
        }

        /// <summary>
        /// Una fila por rendición pagada, para el aviso que junta varias: cuánto le tocó por cada
        /// una. El número de planilla va debajo del código en gris, como en el aviso de rendiciones
        /// disponibles para consolidar.
        /// </summary>
        private static string TablaRendicionesPagadas(
            SalidaEmailLayout l, IReadOnlyList<RendicionPagadaCorreoDatos> rendiciones)
        {
            // Los anchos suman el ancho interno de la tarjeta (580) para que las columnas no se
            // aprieten — ver la nota de Columna en AbrilEmailLayout.
            var columnas = new List<AbrilEmailLayout.Columna>
            {
                new("Rendición", 170),
                new("Periodo", 200),
                new("Salidas", 90, AbrilEmailLayout.Alineacion.Centro),
                new("Monto", 120, AbrilEmailLayout.Alineacion.Derecha),
            };

            static string Secundario(string? valor) =>
                string.IsNullOrWhiteSpace(valor)
                    ? string.Empty
                    : $"<br /><span style=\"color:#64748b;font-weight:400\">{AbrilEmailLayout.Esc(valor)}</span>";

            var cuerpo = rendiciones
                .Select(r => (IReadOnlyList<AbrilEmailLayout.Celda>)new List<AbrilEmailLayout.Celda>
                {
                    new(AbrilEmailLayout.Esc(r.Codigo) + Secundario(r.NumeroPlanilla), Negrita: true, NoWrap: true),
                    new(string.IsNullOrWhiteSpace(r.Periodo) ? "—" : AbrilEmailLayout.Esc(r.Periodo)),
                    new(r.SalidasCount.ToString()),
                    new($"S/ {r.Monto.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))}",
                        NoWrap: true),
                })
                .ToList();

            return l.Tabla(columnas, cuerpo);
        }

        /// <summary>
        /// Filas del aviso de pago al consolidador: con qué códigos conoce el documento —el del
        /// consolidado y el de la planilla grupal con la que lo registró en el S10— y lo que se pagó.
        /// Las que no tienen dato (consolidados anteriores al código o a la planilla preparada) no se
        /// agregan.
        /// </summary>
        private static List<AbrilEmailLayout.Fila> FilasConsolidadoPagado(ConsolidadoPagadoCorreoDatos d)
        {
            var filas = new List<AbrilEmailLayout.Fila>();

            if (!string.IsNullOrWhiteSpace(d.Codigo))
                filas.Add(new(FilaPlanilla, "Consolidado", AbrilEmailLayout.Esc(d.Codigo)));

            if (!string.IsNullOrWhiteSpace(d.PlanillaGrupalCodigo))
                filas.Add(new(FilaPlanillaGrupal, "Planilla grupal", AbrilEmailLayout.Esc(d.PlanillaGrupalCodigo)));

            if (!string.IsNullOrWhiteSpace(d.Area))
                filas.Add(new(FilaArea, "Área", AbrilEmailLayout.Esc(d.Area)));

            if (!string.IsNullOrWhiteSpace(d.NumeroReembolso))
                filas.Add(new(FilaReembolso, "N.º de reembolso del S10", AbrilEmailLayout.Esc(d.NumeroReembolso)));

            filas.Add(new(FilaRendiciones, "Rendiciones pagadas", d.RendicionesCount.ToString()));

            // Siempre, aunque sea cero: es lo que Tesorería le abonó.
            filas.Add(new(FilaMonto, "Monto total",
                $"S/ {d.MontoTotal.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))}"));

            return filas;
        }

        /// <summary>
        /// Una fila por persona, para el aviso al consolidador: a quién le reembolsa y cuánto. Las
        /// planillas van una debajo de otra, que es como las busca en la planilla grupal.
        /// </summary>
        private static string TablaReembolsoPorTrabajador(
            SalidaEmailLayout l, IReadOnlyList<ReembolsoPorTrabajadorCorreoDatos> trabajadores)
        {
            // Los anchos suman el ancho interno de la tarjeta (580) para que las columnas no se
            // aprieten — ver la nota de Columna en AbrilEmailLayout.
            var columnas = new List<AbrilEmailLayout.Columna>
            {
                new("Trabajador", 270),
                new("Rendiciones", 170),
                new("Monto", 140, AbrilEmailLayout.Alineacion.Derecha),
            };

            var cuerpo = trabajadores
                .Select(t => (IReadOnlyList<AbrilEmailLayout.Celda>)new List<AbrilEmailLayout.Celda>
                {
                    new(AbrilEmailLayout.Esc(t.Trabajador)),
                    new(string.Join("<br />", t.Rendiciones.Select(AbrilEmailLayout.Esc)), NoWrap: true),
                    new($"S/ {t.Monto.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))}",
                        Negrita: true, NoWrap: true),
                })
                .ToList();

            return l.Tabla(columnas, cuerpo);
        }
    }
}
