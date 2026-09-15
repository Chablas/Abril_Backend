using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Email
{
    /// <summary>Un tramo de la solicitud tal como se imprime en el correo, ya resuelto a texto.</summary>
    /// <param name="Orden">Número visible del trayecto (1-based).</param>
    /// <param name="HoraSalida">"HH:mm", o vacío cuando el motivo no pide horario.</param>
    /// <param name="HoraRetorno">"HH:mm", "Sin retorno", o vacío cuando el motivo no pide horario.</param>
    /// <param name="Origen">Vacío cuando el motivo no pide lugares.</param>
    /// <param name="Destino">Vacío cuando el motivo no pide lugares.</param>
    public sealed record SalidaCorreoTrayecto(
        int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino);

    /// <summary>
    /// Datos que necesitan los cuatro correos del ciclo de la solicitud. Los arma el servicio con
    /// lo que ya tiene en memoria: el correo no vuelve a la base a buscar nada.
    /// </summary>
    public sealed class SalidaCorreoDatos
    {
        public int SolicitudId { get; set; }
        /// <summary>Código SOL-AAAA-NNNN (o "#N" en las anteriores al código).</summary>
        public string Codigo { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public DateOnly FechaSalida { get; set; }
        public IReadOnlyList<SalidaCorreoTrayecto> Trayectos { get; set; } = new List<SalidaCorreoTrayecto>();
        /// <summary>
        /// true = la solicitud tiene al menos un trayecto de hora exacta, así que hay horas que
        /// recuperar. Con todos los motivos de hora estimada el recordatorio no va.
        /// </summary>
        public bool MostrarRecordatorio { get; set; }
    }

    /// <summary>
    /// Los cuatro correos del ciclo de la solicitud de salida, con el mismo chrome de la intranet
    /// (<see cref="SalidaEmailLayout"/>) que ya usan los tres correos del reembolso y los de
    /// Gestión GTH:
    ///
    /// <list type="bullet">
    ///   <item>Al revisor: hay una solicitud esperando su decisión.</item>
    ///   <item>Al solicitante: se recibió su solicitud y está en revisión.</item>
    ///   <item>Al solicitante: su solicitud quedó aprobada.</item>
    ///   <item>Al solicitante: su solicitud quedó rechazada, con el motivo.</item>
    /// </list>
    ///
    /// El del revisor es el único con DOS botones que ejecutan la acción desde el propio correo
    /// (llevan un token firmado y aprueban o rechazan sin pasar por la intranet); su enlace
    /// directo apunta igual a Gestión de Salidas para quien prefiera entrar. Los otros tres
    /// llevan un botón que abre la pantalla exacta en la intranet.
    ///
    /// Criterio editorial heredado de <see cref="AbrilEmailLayout"/>: el correo lleva datos y un
    /// acceso, no explicaciones. La bajada es UNA línea y los avisos son de estado.
    /// </summary>
    public static class SolicitudSalidaEmailTemplates
    {
        // Íconos del catálogo de public/images/emails/icons (los genera
        // Abril-Frontend/scripts/generate-email-icons.js). Se reutilizan los que ya existen, igual
        // que en ReembolsoEmailTemplates: no hace falta un juego propio para cuatro correos.
        private const string IconoPorAprobar  = "req-solicitud";
        private const string IconoEnRevision  = "req-estado";
        private const string IconoAprobada    = "req-aprobada";
        private const string IconoRechazada   = "req-decision";

        private const string IconoFranjaOk     = "req-check";
        private const string IconoFranjaNo     = "req-rechazadas";
        private const string IconoFranjaAviso  = "req-aviso";
        private const string IconoFranjaHoras  = "req-recordatorio";

        private const string FilaCodigo      = "req-codigo";
        private const string FilaSolicitante = "req-solicitante";
        private const string FilaFecha       = "req-fecha";
        private const string FilaHora        = "req-hora";
        private const string FilaMotivo      = "req-justificacion";
        private const string FilaLugar       = "req-lugar";

        private const string SeccionTrayectos = "req-lugar";

        private const string TextoRecordatorio =
            "No olvides coordinar la recuperación de las horas dentro del mes calendario.";

        /// <summary>
        /// Al revisor: una solicitud espera su decisión. Los dos botones ejecutan la acción desde
        /// el correo (el token vale 30 días); el enlace directo lleva a Gestión de Salidas.
        /// </summary>
        public static string PorAprobar(
            SalidaEmailLayout l, SalidaCorreoDatos d,
            string urlAprobar, string urlRechazar, string urlGestion) =>
            l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoPorAprobar,
                    "Solicitud de salida por aprobar",
                    $"<b>{AbrilEmailLayout.Esc(d.Solicitante)}</b> registró la solicitud "
                    + $"<b>{AbrilEmailLayout.Esc(d.Codigo)}</b> del <b>{d.FechaSalida:dd/MM/yyyy}</b>."),
                Detalle(l, d, conSolicitante: true),
                Recordatorio(l, d),
                l.BotonesRespuesta("Aprobar", urlAprobar, "Rechazar", urlRechazar),
                l.EnlaceDirecto(urlGestion));

        /// <summary>
        /// Al solicitante: su solicitud quedó registrada y está en revisión.
        /// <paramref name="enviadoRevisorA"/> son los correos a los que realmente salió (vacío si
        /// ese correo está apagado) y <paramref name="aprobadorAsignado"/> el revisor que la tiene
        /// asignada aunque no le haya llegado el correo: sin esa distinción el aviso diría que se
        /// envió a alguien que nunca lo recibió.
        /// </summary>
        public static string EnRevision(
            SalidaEmailLayout l, SalidaCorreoDatos d, string urlVer,
            IReadOnlyList<string> enviadoRevisorA, string? aprobadorAsignado)
        {
            var (tono, icono, aviso) =
                enviadoRevisorA.Count > 0
                    ? (AbrilEmailLayout.Tono.Info, IconoFranjaAviso,
                       $"Enviada a <b>{AbrilEmailLayout.Esc(string.Join(", ", enviadoRevisorA))}</b> para su revisión.")
                : !string.IsNullOrWhiteSpace(aprobadorAsignado)
                    ? (AbrilEmailLayout.Tono.Info, IconoFranjaAviso,
                       $"Asignada a <b>{AbrilEmailLayout.Esc(aprobadorAsignado)}</b> para su revisión.")
                    : (AbrilEmailLayout.Tono.Ambar, IconoFranjaAviso,
                       "Sin jefatura inmediata identificada. El equipo administrativo será notificado para asignarla.");

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoEnRevision,
                    "Tu solicitud está en revisión",
                    $"Recibimos tu solicitud <b>{AbrilEmailLayout.Esc(d.Codigo)}</b> del "
                    + $"<b>{d.FechaSalida:dd/MM/yyyy}</b>."),
                l.Franja(icono, tono, aviso),
                Detalle(l, d, conSolicitante: false),
                Recordatorio(l, d),
                l.Boton("Ver mi solicitud", urlVer),
                l.EnlaceDirecto(urlVer));
        }

        /// <summary>Al solicitante: su solicitud quedó aprobada.</summary>
        public static string Aprobada(SalidaEmailLayout l, SalidaCorreoDatos d, string urlVer) =>
            l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoAprobada,
                    "Tu solicitud fue aprobada",
                    $"Tu solicitud <b>{AbrilEmailLayout.Esc(d.Codigo)}</b> del "
                    + $"<b>{d.FechaSalida:dd/MM/yyyy}</b> quedó aprobada."),
                l.Franja(IconoFranjaOk, AbrilEmailLayout.Tono.Verde, "Aprobada por tu jefatura."),
                Detalle(l, d, conSolicitante: false),
                Recordatorio(l, d),
                l.Boton("Ver mi solicitud", urlVer),
                l.EnlaceDirecto(urlVer));

        /// <summary>
        /// Al solicitante: su solicitud quedó rechazada. El motivo va en la franja roja porque es
        /// lo único que tiene que leer.
        /// </summary>
        public static string Rechazada(
            SalidaEmailLayout l, SalidaCorreoDatos d, string? motivoRechazo, string urlVer)
        {
            var motivo = string.IsNullOrWhiteSpace(motivoRechazo)
                ? "Sin motivo registrado. Coordina con tu jefatura."
                : AbrilEmailLayout.EscMultilinea(motivoRechazo.Trim());

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoRechazada,
                    "Tu solicitud fue rechazada",
                    $"Tu solicitud <b>{AbrilEmailLayout.Esc(d.Codigo)}</b> del "
                    + $"<b>{d.FechaSalida:dd/MM/yyyy}</b> fue rechazada."),
                l.Franja(IconoFranjaNo, AbrilEmailLayout.Tono.Rojo, $"<b>Motivo:</b> {motivo}"),
                Detalle(l, d, conSolicitante: false),
                l.Boton("Ver el detalle", urlVer),
                l.EnlaceDirecto(urlVer));
        }

        // ── Bloques compartidos ───────────────────────────────────────────────

        /// <summary>
        /// El detalle de la solicitud. Con un solo trayecto (el caso normal) todo entra en una
        /// tarjeta; con varios, la tarjeta se queda con la cabecera y los tramos pasan a una tabla,
        /// que es donde se leen comparados. Los dos bloques se devuelven juntos porque
        /// <c>Documento</c> los concatena igual.
        /// </summary>
        private static string Detalle(SalidaEmailLayout l, SalidaCorreoDatos d, bool conSolicitante)
        {
            var filas = new List<AbrilEmailLayout.Fila>();

            if (!string.IsNullOrWhiteSpace(d.Codigo))
                filas.Add(new(FilaCodigo, "Código", AbrilEmailLayout.Esc(d.Codigo)));
            if (conSolicitante)
                filas.Add(new(FilaSolicitante, "Colaborador", AbrilEmailLayout.Esc(d.Solicitante)));

            var varios = d.Trayectos.Count > 1;
            filas.Add(new(FilaFecha, "Fecha de salida",
                $"{d.FechaSalida:dd/MM/yyyy}{(varios ? $" · {d.Trayectos.Count} trayectos" : "")}"));

            if (!varios)
            {
                // Las filas sin dato no se agregan: un motivo sin horario ni lugares (ej. licencia
                // sin goce de haber) mostraría tres guiones que no informan nada.
                var t = d.Trayectos.Count == 1 ? d.Trayectos[0] : null;
                if (t != null)
                {
                    Agregar(filas, FilaHora,   "Hora de salida",  t.HoraSalida);
                    Agregar(filas, FilaHora,   "Hora de retorno", t.HoraRetorno);
                    Agregar(filas, FilaMotivo, "Motivo",          t.Motivo);
                    Agregar(filas, FilaLugar,  "Origen",          t.Origen);
                    Agregar(filas, FilaLugar,  "Destino",         t.Destino);
                }
                return l.Tarjeta(filas);
            }

            // Los anchos suman el ancho interno de la tarjeta (580) para que las columnas no se
            // aprieten — ver la nota de Columna en AbrilEmailLayout.
            var columnas = new List<AbrilEmailLayout.Columna>
            {
                new("#", 28, AbrilEmailLayout.Alineacion.Centro),
                new("Salida", 54, AbrilEmailLayout.Alineacion.Centro),
                new("Retorno", 54, AbrilEmailLayout.Alineacion.Centro),
                new("Motivo", 148),
                new("Origen", 148),
                new("Destino", 148),
            };

            var cuerpo = d.Trayectos
                .Select(t => (IReadOnlyList<AbrilEmailLayout.Celda>)new List<AbrilEmailLayout.Celda>
                {
                    new(t.Orden.ToString(), Negrita: true),
                    new(Guion(t.HoraSalida), NoWrap: true),
                    new(Guion(t.HoraRetorno), NoWrap: true),
                    new(Guion(t.Motivo)),
                    new(Guion(t.Origen)),
                    new(Guion(t.Destino)),
                })
                .ToList();

            return l.Tarjeta(filas)
                 + l.Seccion(SeccionTrayectos, "Trayectos")
                 + l.Tabla(columnas, cuerpo);
        }

        /// <summary>
        /// Recordatorio de recuperación de horas. Va en los tres correos donde la salida sigue en
        /// pie (revisor, en revisión, aprobada); en el de rechazo no hay horas que recuperar.
        /// </summary>
        private static string Recordatorio(SalidaEmailLayout l, SalidaCorreoDatos d) =>
            d.MostrarRecordatorio
                ? l.Franja(IconoFranjaHoras, AbrilEmailLayout.Tono.Ambar, TextoRecordatorio)
                : "";

        private static void Agregar(
            List<AbrilEmailLayout.Fila> filas, string icono, string etiqueta, string valor)
        {
            if (!string.IsNullOrWhiteSpace(valor))
                filas.Add(new(icono, etiqueta, AbrilEmailLayout.Esc(valor)));
        }

        /// <summary>En la tabla sí va un guión: una celda vacía se lee como un error de armado.</summary>
        private static string Guion(string valor) =>
            string.IsNullOrWhiteSpace(valor) ? "—" : AbrilEmailLayout.Esc(valor);
    }
}
