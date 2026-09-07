using System.Globalization;
using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Email
{
    /// <summary>
    /// Datos que necesitan los cuatro correos de la PRIMERA revisión. Todos hablan de la misma
    /// unidad —la planilla— así que comparten un solo shape; lo arma el repositorio con una
    /// consulta por rendición y el correo no vuelve a la base a buscar nada.
    /// </summary>
    public sealed class RendicionRevisionCorreoDatos
    {
        public int RendicionId { get; set; }

        /// <summary>Código REN-AAAA-NNNN. Es lo que el trabajador reconoce; nunca el id.</summary>
        public string Codigo { get; set; } = string.Empty;

        public string Trabajador { get; set; } = string.Empty;
        public string? Area { get; set; }

        /// <summary>Periodo que cubre la planilla ("Agosto 2026", o un rango si cruza meses).</summary>
        public string? Periodo { get; set; }

        /// <summary>Cuántas salidas agrupa la planilla.</summary>
        public int SalidasCount { get; set; }

        /// <summary>Cuántos tramos (trayectos) suman esas salidas — es lo que el jefe revisa.</summary>
        public int TramosCount { get; set; }

        /// <summary>Suma de lo rendido en la planilla, en soles.</summary>
        public decimal MontoTotal { get; set; }

        /// <summary>Número impreso en el PDF ("TI: 000123"), o null si la planilla no lo tiene.</summary>
        public string? NumeroPlanilla { get; set; }

        /// <summary>Nombre del jefe que aprobó u observó. Solo en los dos correos de decisión.</summary>
        public string? DecididoPor { get; set; }

        /// <summary>Comentario de la observación. Solo en el correo de observada.</summary>
        public string? Observacion { get; set; }
    }

    /// <summary>
    /// Los cuatro correos de la PRIMERA revisión de una rendición, con el mismo chrome de la
    /// intranet (<see cref="SalidaEmailLayout"/>) que el resto de los correos de salidas:
    ///
    /// <list type="bullet">
    ///   <item>Al solicitante: su rendición quedó registrada y se envió a primera revisión.</item>
    ///   <item>Al jefe/revisor: hay una rendición esperando su decisión (aprobar u observar).</item>
    ///   <item>Al solicitante: aprobada — ya puede cargar el Consolidado del S10.</item>
    ///   <item>Al solicitante: observada, con el comentario de qué corregir.</item>
    /// </list>
    ///
    /// El del revisor es el único con DOS botones. A diferencia del correo de aprobación de la
    /// SALIDA (que resuelve desde el propio correo con un token firmado), acá los dos botones
    /// llevan a la pantalla: observar exige escribir un comentario, y aprobar a ciegas sin ver los
    /// tramos ni las capturas sería justamente saltarse la revisión que este paso agrega.
    ///
    /// Criterio editorial heredado de <see cref="AbrilEmailLayout"/>: el correo lleva datos y un
    /// acceso, no explicaciones. La bajada es UNA línea y las franjas son de estado.
    /// </summary>
    public static class RendicionRevisionEmailTemplates
    {
        // Íconos del catálogo de public/images/emails/icons (los genera
        // Abril-Frontend/scripts/generate-email-icons.js). Se reutilizan los que ya existen, igual
        // que en los otros correos de salidas.
        private const string IconoEnRevision = "req-estado";
        private const string IconoPorRevisar = "req-solicitud";
        private const string IconoAprobada   = "req-aprobada";
        private const string IconoObservada  = "req-observaciones";

        private const string IconoFranjaOk    = "req-check";
        private const string IconoFranjaNo    = "req-rechazadas";
        private const string IconoFranjaAviso = "req-aviso";

        private const string FilaCodigo     = "req-codigo";
        private const string FilaTrabajador = "req-solicitante";
        private const string FilaArea       = "req-area";
        private const string FilaPeriodo    = "req-fecha";
        private const string FilaTramos     = "req-lugar";
        private const string FilaMonto      = "req-sustento";
        private const string FilaPlanilla   = "req-ti";
        private const string FilaDecision   = "req-vistobueno";
        private const string FilaObs        = "req-comentario";

        /// <summary>
        /// Al solicitante: su rendición quedó registrada y salió a primera revisión. No lleva botón
        /// a propósito —es informativo y no le pide nada—, solo el enlace por si quiere entrar.
        /// <paramref name="enviadoRevisorA"/> son los correos a los que realmente salió el aviso al
        /// jefe (vacío si ese correo está apagado en Configuración) y
        /// <paramref name="revisorAsignado"/> el jefe que la tiene aunque no le haya llegado el
        /// correo: sin esa distinción el aviso diría que se envió a alguien que nunca lo recibió.
        /// </summary>
        public static string EnRevision(
            SalidaEmailLayout l, RendicionRevisionCorreoDatos d, string urlVer,
            IReadOnlyList<string> enviadoRevisorA, string? revisorAsignado)
        {
            var (tono, aviso) =
                enviadoRevisorA.Count > 0
                    ? (AbrilEmailLayout.Tono.Info,
                       $"Enviada a <b>{AbrilEmailLayout.Esc(string.Join(", ", enviadoRevisorA))}</b> para su revisión.")
                : !string.IsNullOrWhiteSpace(revisorAsignado)
                    ? (AbrilEmailLayout.Tono.Info,
                       $"Asignada a <b>{AbrilEmailLayout.Esc(revisorAsignado)}</b> para su revisión.")
                    : (AbrilEmailLayout.Tono.Ambar,
                       "Sin jefatura inmediata identificada. Coordina con Gestión del Talento Humano.");

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoEnRevision,
                    "Tu rendición está en revisión",
                    $"Recibimos tu rendición <b>{AbrilEmailLayout.Esc(d.Codigo)}</b> y pasó a la primera "
                    + "revisión de tu jefatura."),
                l.Franja(IconoFranjaAviso, tono, aviso),
                l.Tarjeta(Filas(d, conTrabajador: false, estado: "Primera revisión")),
                l.EnlaceDirecto(urlVer));
        }

        /// <summary>
        /// Al jefe/revisor: hay una rendición esperando su primera revisión. Los dos botones abren
        /// la planilla en Gestión de Rendiciones con la acción planteada; el enlace directo lleva a
        /// la misma pantalla sin acción, para quien prefiera mirar antes de decidir.
        /// </summary>
        public static string PorRevisar(
            SalidaEmailLayout l, RendicionRevisionCorreoDatos d,
            string urlAprobar, string urlObservar, string urlGestion) =>
            l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoPorRevisar,
                    "Tienes una rendición pendiente de revisión",
                    $"<b>{AbrilEmailLayout.Esc(d.Trabajador)}</b> envió la rendición "
                    + $"<b>{AbrilEmailLayout.Esc(d.Codigo)}</b> para tu primera revisión."),
                l.Tarjeta(Filas(d, conTrabajador: true)),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Info,
                    "Aprobarla habilita al trabajador a cargar el Consolidado del S10; observarla le "
                    + "pide corregir capturas y montos."),
                l.BotonesRespuesta("Aprobar", urlAprobar, "Observar", urlObservar),
                l.EnlaceDirecto(urlGestion));

        /// <summary>
        /// Al solicitante: aprobada en primera revisión. El botón lo deja en Mis Rendiciones, que es
        /// donde carga el Consolidado del S10 — el paso que esta aprobación acaba de habilitar.
        /// </summary>
        public static string Aprobada(SalidaEmailLayout l, RendicionRevisionCorreoDatos d, string urlCargarS10) =>
            l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoAprobada,
                    "Tu rendición fue aprobada en primera revisión",
                    $"Tu jefatura aprobó la primera revisión de la rendición "
                    + $"<b>{AbrilEmailLayout.Esc(d.Codigo)}</b>."),
                l.Franja(IconoFranjaOk, AbrilEmailLayout.Tono.Verde,
                    string.IsNullOrWhiteSpace(d.DecididoPor)
                        ? "Ya puedes registrar la información en el S10 y cargar el Consolidado con su número de guía."
                        : $"Aprobada por <b>{AbrilEmailLayout.Esc(d.DecididoPor)}</b>. Ya puedes cargar el "
                          + "Consolidado del S10 con su número de guía."),
                l.Tarjeta(Filas(d, conTrabajador: false)),
                l.Boton("Cargar el Consolidado del S10", urlCargarS10),
                l.EnlaceDirecto(urlCargarS10));

        /// <summary>
        /// Al solicitante: observada en primera revisión. El comentario del jefe va en la franja
        /// roja porque es lo único que tiene que leer, y el botón lo deja en la planilla exacta que
        /// tiene que corregir y volver a generar.
        /// </summary>
        public static string Observada(SalidaEmailLayout l, RendicionRevisionCorreoDatos d, string urlSubsanar)
        {
            var observacion = string.IsNullOrWhiteSpace(d.Observacion)
                ? "Sin observación registrada. Coordina con tu jefatura antes de volver a generarla."
                : AbrilEmailLayout.EscMultilinea(d.Observacion.Trim());

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoObservada,
                    "Tu rendición tiene una observación",
                    $"Tu rendición <b>{AbrilEmailLayout.Esc(d.Codigo)}</b> volvió observada de la "
                    + "primera revisión."),
                l.Franja(IconoFranjaNo, AbrilEmailLayout.Tono.Rojo, $"<b>Observación:</b> {observacion}"),
                l.Tarjeta(Filas(d, conTrabajador: false, estado: "Observada")),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Ambar,
                    "Corrige las capturas y los montos de las salidas de esta rendición y vuelve a "
                    + $"generarla: conserva el código <b>{AbrilEmailLayout.Esc(d.Codigo)}</b>."),
                l.Boton("Resolver la observación", urlSubsanar),
                l.EnlaceDirecto(urlSubsanar));
        }

        // ── Bloques compartidos ───────────────────────────────────────────────

        /// <summary>
        /// La tarjeta de la planilla. Las filas sin dato no se agregan: una tarjeta con "—"
        /// repetidos no informa nada.
        /// </summary>
        /// <param name="conTrabajador">
        /// true solo en el correo al revisor: al trabajador no hay que decirle su propio nombre.
        /// </param>
        /// <param name="estado">
        /// Etiqueta de estado para cerrar la tarjeta. Se omite en los correos donde la franja ya
        /// dice en qué quedó (aprobada), para no repetirlo dos veces.
        /// </param>
        private static List<AbrilEmailLayout.Fila> Filas(
            RendicionRevisionCorreoDatos d, bool conTrabajador, string? estado = null)
        {
            var filas = new List<AbrilEmailLayout.Fila>();

            if (!string.IsNullOrWhiteSpace(d.Codigo))
                filas.Add(new(FilaCodigo, "Código", AbrilEmailLayout.Esc(d.Codigo)));

            if (conTrabajador)
            {
                filas.Add(new(FilaTrabajador, "Colaborador", AbrilEmailLayout.Esc(d.Trabajador)));
                if (!string.IsNullOrWhiteSpace(d.Area))
                    filas.Add(new(FilaArea, "Área", AbrilEmailLayout.Esc(d.Area)));
            }

            if (!string.IsNullOrWhiteSpace(d.Periodo))
                filas.Add(new(FilaPeriodo, "Periodo", AbrilEmailLayout.Esc(d.Periodo)));

            // Los tramos son lo que se revisa; las salidas, cómo vienen agrupados. Van juntos
            // porque por separado ninguno de los dos números se entiende solo.
            var tramos  = d.TramosCount == 1 ? "1 tramo" : $"{d.TramosCount} tramos";
            var salidas = d.SalidasCount == 1 ? "1 salida" : $"{d.SalidasCount} salidas";
            filas.Add(new(FilaTramos, "Tramos", $"{tramos} · {salidas}"));

            if (d.MontoTotal > 0m)
                filas.Add(new(FilaMonto, "Monto total",
                    $"S/ {d.MontoTotal.ToString("N2", CultureInfo.GetCultureInfo("es-PE"))}"));

            if (!string.IsNullOrWhiteSpace(d.NumeroPlanilla))
                filas.Add(new(FilaPlanilla, "Planilla", AbrilEmailLayout.Esc(d.NumeroPlanilla)));

            if (!string.IsNullOrWhiteSpace(d.DecididoPor))
                filas.Add(new(FilaDecision, "Revisado por", AbrilEmailLayout.Esc(d.DecididoPor)));

            if (!string.IsNullOrWhiteSpace(estado))
                filas.Add(new(FilaObs, "Estado", AbrilEmailLayout.Esc(estado)));

            return filas;
        }
    }
}
