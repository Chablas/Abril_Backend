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
        /// <summary>Observación vigente de esas salidas. Solo la usan los correos de observado.</summary>
        public string? Observacion { get; set; }
    }

    /// <summary>
    /// Lo que necesitan los correos de una PLANILLA entera y no de un consolidado: el aviso a
    /// Tesorería de que se firmó y el aviso de pago al trabajador.
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
        /// <summary>Nombre de quien firmó la planilla. Lo usa el aviso a Tesorería.</summary>
        public string? FirmadoPor { get; set; }
        /// <summary>Nombre del tesorero que registró el pago. Lo usa el aviso de pago.</summary>
        public string? PagadoPor { get; set; }
    }

    /// <summary>
    /// Los correos que cierran el ciclo de la rendición, todos con el mismo chrome de la intranet
    /// (<see cref="SalidaEmailLayout"/>):
    ///
    /// <list type="bullet">
    ///   <item>A la jefatura: el consolidador adjuntó un Consolidado del S10 y está esperando su
    ///     visto bueno.</item>
    ///   <item>Al consolidador: la jefatura aprobó (y firmó) el consolidado.</item>
    ///   <item>Al consolidador: la jefatura lo observó, con el comentario a subsanar.</item>
    ///   <item>A Tesorería: la jefatura firmó una planilla y su reembolso entró a la bandeja.</item>
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
        private const string IconoAprobado    = "req-aprobada";
        private const string IconoObservado   = "req-decision";
        private const string IconoFranjaOk    = "req-check";
        private const string IconoFranjaNo    = "req-rechazadas";
        private const string IconoFranjaAviso = "req-aviso";

        private const string IconoPago        = "req-aprobada";
        private const string IconoPorPagar    = "req-sustento";

        private const string FilaTrabajador = "req-solicitante";
        private const string FilaArea       = "req-area";
        private const string FilaFecha      = "req-fecha";
        private const string FilaPlanilla   = "req-codigo";
        private const string FilaMonto      = "req-sustento";
        private const string FilaReembolso       = "req-ti";

        /// <summary>
        /// A la jefatura: el consolidador adjuntó un Consolidado del S10 y el reembolso está
        /// esperando su visto bueno. El botón abre ese consolidado en Consolidados, que es donde la
        /// jefatura lo aprueba (firma) u observa.
        /// </summary>
        public static string ConsolidadoPorRevisar(SalidaEmailLayout l, ConsolidadoCorreoDatos d, string urlRevisar)
        {
            var quien = string.IsNullOrWhiteSpace(d.Consolidador)
                ? "El consolidador"
                : $"<b>{AbrilEmailLayout.Esc(d.Consolidador)}</b>";

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoRevisar,
                    "Reembolso por revisar",
                    $"{quien} adjuntó el Consolidado del S10{Numero(d)}."),
                l.Tarjeta(FilasConsolidado(d)),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Info,
                    "Falta tu visto bueno para que el reembolso pase a firma y a Tesorería."),
                l.Boton("Revisar el consolidado", urlRevisar),
                l.EnlaceDirecto(urlRevisar));
        }

        /// <summary>
        /// Al consolidador: la jefatura aprobó el reembolso del consolidado —aprobar ES firmar—, así
        /// que ya pasó a Tesorería. Es informativo: el botón solo abre el consolidado.
        /// </summary>
        public static string ConsolidadoAprobado(SalidaEmailLayout l, ConsolidadoCorreoDatos d, string urlVer) =>
            l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoAprobado,
                    "Reembolso aprobado",
                    $"La jefatura aprobó el Consolidado del S10{Numero(d)}."),
                l.Franja(IconoFranjaOk, AbrilEmailLayout.Tono.Verde,
                    string.IsNullOrWhiteSpace(d.DecididoPor)
                        ? "Firmado por la jefatura: pasa a Tesorería."
                        : $"Firmado por <b>{AbrilEmailLayout.Esc(d.DecididoPor)}</b>: pasa a Tesorería."),
                l.Tarjeta(FilasConsolidado(d)),
                l.Boton("Ver el consolidado", urlVer),
                l.EnlaceDirecto(urlVer));

        /// <summary>
        /// Al consolidador: la jefatura OBSERVÓ el reembolso del consolidado. La observación va en la
        /// franja roja porque es lo único que tiene que leer, y la ámbar nombra los DOS caminos que
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
                    "Reembolso observado",
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
        /// A Tesorería: la jefatura firmó una planilla y su reembolso ya está en la bandeja de
        /// pago (RF-TES-01). El botón abre esa planilla en Reembolsos, que es donde Tesorería
        /// confirma la revisión documental y recién después puede pagar.
        /// </summary>
        public static string PorPagarTesoreria(SalidaEmailLayout l, ReembolsoPlanillaCorreoDatos d, string urlRevisar)
        {
            var firma = string.IsNullOrWhiteSpace(d.FirmadoPor)
                ? "La jefatura ya firmó la planilla y el Consolidado del S10."
                : $"Firmada por <b>{AbrilEmailLayout.Esc(d.FirmadoPor)}</b>.";

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoPorPagar,
                    "Reembolso por pagar",
                    $"El reembolso de <b>{AbrilEmailLayout.Esc(d.Trabajador)}</b> quedó firmado y pasó a Tesorería."),
                l.Franja(IconoFranjaOk, AbrilEmailLayout.Tono.Verde, firma),
                l.Tarjeta(FilasPlanilla(d)),
                l.Boton("Revisar el reembolso", urlRevisar),
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
