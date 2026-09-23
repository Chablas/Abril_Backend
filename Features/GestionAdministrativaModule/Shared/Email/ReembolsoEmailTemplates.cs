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
    }

    /// <summary>
    /// Lo que necesita el aviso al trabajador de que su rendición quedó incluida en un Consolidado
    /// del S10. Va UNO por (planilla, trabajador), igual que el de pago: una planilla puede agrupar
    /// a varios trabajadores y a cada uno le importa lo suyo.
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
    /// Lo que necesitan los correos de una PLANILLA entera y no de un consolidado: el aviso de
    /// pago al trabajador.
    /// </summary>
    public sealed class ReembolsoPlanillaCorreoDatos
    {
        public int RendicionId { get; set; }
        /// <summary>Código REN-AAAA-NNNN de la planilla. Vacío en las anteriores a la columna.</summary>
        public string Codigo { get; set; } = string.Empty;
        public string Trabajador { get; set; } = string.Empty;
        /// <summary>Correo del trabajador. Lo usa el aviso de pago, que va dirigido a él.</summary>
        public string? TrabajadorEmail { get; set; }
        public string? Area { get; set; }
        /// <summary>Número de la planilla ("TI: 000123"), o null si la planilla no lo tiene.</summary>
        public string? NumeroPlanilla { get; set; }
        /// <summary>Periodo que cubre la planilla ("Agosto 2026", o un rango si cruza meses).</summary>
        public string? Periodo { get; set; }
        /// <summary>Cuántas salidas del trabajador entran en la planilla.</summary>
        public int SalidasCount { get; set; }
        /// <summary>Suma de lo rendido por el trabajador en esa planilla, en soles.</summary>
        public decimal MontoTotal { get; set; }
        /// <summary>Número de reembolso del Consolidado del S10. Null si la planilla no lo tiene.</summary>
        public string? NumeroReembolso { get; set; }
        /// <summary>Nombre del tesorero que registró el pago. Lo usa el aviso de pago.</summary>
        public string? PagadoPor { get; set; }
    }

    /// <summary>
    /// Los correos que cierran el ciclo de la rendición, todos con el mismo chrome de la intranet
    /// (<see cref="SalidaEmailLayout"/>):
    ///
    /// <list type="bullet">
    ///   <item>Al trabajador: su rendición quedó incluida en el Consolidado del S10 que adjuntó el
    ///     consolidador.</item>
    ///   <item>A la jefatura: el consolidador adjuntó un Consolidado del S10 y está esperando su
    ///     visto bueno.</item>
    ///   <item>Al consolidador: la jefatura aprobó (y firmó) el consolidado.</item>
    ///   <item>Al consolidador: la jefatura lo observó, con el comentario a subsanar.</item>
    ///   <item>A Tesorería: la jefatura terminó de firmar el consolidado y su reembolso entró a la
    ///     bandeja. Uno por consolidado, no uno por planilla.</item>
    ///   <item>Al consolidador: Tesorería devolvió el consolidado antes de pagarlo (RG-49).</item>
    ///   <item>Al trabajador: Tesorería ya pagó — el cierre del ciclo.</item>
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

        private const string FilaTrabajador = "req-solicitante";
        private const string FilaArea       = "req-area";
        private const string FilaFecha      = "req-fecha";
        private const string FilaPlanilla   = "req-codigo";
        private const string FilaMonto      = "req-sustento";
        private const string FilaReembolso       = "req-ti";
        private const string FilaRendiciones = "req-formulario";
        private const string FilaFirma       = "req-vistobueno";

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
                    $"Tesorería revisó el Consolidado del S10{Numero(d)} y lo devolvió antes de pagarlo."),
                l.Franja(IconoFranjaNo, AbrilEmailLayout.Tono.Rojo,
                    $"<b>Motivo:</b> {motivo}"),
                l.Tarjeta(FilasConsolidado(d)),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Ambar,
                    "Vuelve a adjuntar el Consolidado del S10 corregido, o solicita la corrección al "
                    + "Coordinador ERP si el arreglo tiene que hacerse dentro del S10. Al recargarlo, "
                    + "vuelve a la jefatura para la firma y de ahí a Tesorería."),
                l.Boton("Ver el consolidado", urlVer),
                l.EnlaceDirecto(urlVer));
        }

        /// <summary>
        /// Al solicitante: Tesorería ya pagó su reembolso (RG-28). Es el cierre del ciclo, así que
        /// no pide nada — el botón solo lo lleva a su planilla en Mis Rendiciones.
        /// </summary>
        public static string Pagado(SalidaEmailLayout l, ReembolsoPlanillaCorreoDatos d, string urlVer)
        {
            var monto = d.MontoTotal.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"));

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoPago,
                    "Reembolso realizado",
                    $"Tesorería registró el pago de tu rendición <b>{AbrilEmailLayout.Esc(d.Codigo)}</b>."),
                l.Franja(IconoFranjaOk, AbrilEmailLayout.Tono.Verde,
                    string.IsNullOrWhiteSpace(d.PagadoPor)
                        ? $"Monto reembolsado: <b>S/ {monto}</b>."
                        : $"Monto reembolsado: <b>S/ {monto}</b> · registrado por "
                          + $"<b>{AbrilEmailLayout.Esc(d.PagadoPor)}</b>."),
                l.Tarjeta(FilasPlanilla(d)),
                l.Boton("Ver mi rendición", urlVer),
                l.EnlaceDirecto(urlVer));
        }

        /// <summary>" N.° 00123" si el consolidado tiene número de reembolso; vacío si es de los viejos.</summary>
        private static string Numero(ConsolidadoCorreoDatos d) =>
            string.IsNullOrWhiteSpace(d.NumeroReembolso)
                ? string.Empty
                : $" <b>N.° {AbrilEmailLayout.Esc(d.NumeroReembolso)}</b>";

        /// <summary>
        /// Filas de los correos de un CONSOLIDADO: qué planillas y trabajadores cubre, el periodo y
        /// el importe declarado en el S10. Las que no tienen dato no se agregan.
        /// </summary>
        private static List<AbrilEmailLayout.Fila> FilasConsolidado(ConsolidadoCorreoDatos d)
        {
            var filas = new List<AbrilEmailLayout.Fila>();

            if (d.Rendiciones.Count > 0)
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

        /// <summary>"Ana, Luis y Rosa" o "Ana, Luis, Rosa +4": un consolidado puede cubrir a muchos.</summary>
        private static string ResumirNombres(List<string> nombres)
        {
            const int Maximo = 3;
            if (nombres.Count <= Maximo) return string.Join(", ", nombres);
            return $"{string.Join(", ", nombres.Take(Maximo))} +{nombres.Count - Maximo}";
        }

        /// <summary>
        /// Filas de los correos de una PLANILLA entera: en vez de una fecha de salida suelta muestra
        /// el periodo que cubre y cuántas salidas trae.
        /// </summary>
        private static List<AbrilEmailLayout.Fila> FilasPlanilla(ReembolsoPlanillaCorreoDatos d)
        {
            var filas = new List<AbrilEmailLayout.Fila>
            {
                new(FilaTrabajador, "Trabajador", AbrilEmailLayout.Esc(d.Trabajador)),
            };

            if (!string.IsNullOrWhiteSpace(d.Area))
                filas.Add(new(FilaArea, "Área", AbrilEmailLayout.Esc(d.Area)));

            var salidas = d.SalidasCount == 1 ? "1 salida" : $"{d.SalidasCount} salidas";
            filas.Add(new(FilaFecha, "Periodo",
                string.IsNullOrWhiteSpace(d.Periodo) ? salidas : $"{AbrilEmailLayout.Esc(d.Periodo)} · {salidas}"));

            if (!string.IsNullOrWhiteSpace(d.NumeroPlanilla))
                filas.Add(new(FilaPlanilla, "Planilla", AbrilEmailLayout.Esc(d.NumeroPlanilla)));

            if (!string.IsNullOrWhiteSpace(d.NumeroReembolso))
                filas.Add(new(FilaReembolso, "N.º de reembolso del S10", AbrilEmailLayout.Esc(d.NumeroReembolso)));

            if (d.MontoTotal > 0m)
                filas.Add(new(FilaMonto, "Monto rendido",
                    $"S/ {d.MontoTotal.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))}"));

            return filas;
        }
    }
}
