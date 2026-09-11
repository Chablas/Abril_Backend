using System.Globalization;
using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Email
{
    /// <summary>
    /// Datos de los dos correos de la subsanación con el ERP. Los dos hablan de la misma unidad
    /// —la solicitud de corrección de una planilla— así que comparten un solo shape; lo arma el
    /// repositorio en una consulta y el correo no vuelve a la base.
    /// </summary>
    public sealed class CorreccionS10CorreoDatos
    {
        public int CorreccionId { get; set; }
        public int RendicionId { get; set; }

        /// <summary>Código REN-AAAA-NNNN de la planilla. Es lo que el trabajador reconoce.</summary>
        public string Codigo { get; set; } = string.Empty;

        public string Trabajador { get; set; } = string.Empty;
        /// <summary>Correo del trabajador. Lo usa el aviso de atención, que va dirigido a él.</summary>
        public string? TrabajadorEmail { get; set; }
        public string? Area { get; set; }

        /// <summary>Periodo que cubre la planilla ("Agosto 2026", o un rango si cruza meses).</summary>
        public string? Periodo { get; set; }

        /// <summary>Número impreso en el PDF ("TI: 000123"), o null si la planilla no lo tiene.</summary>
        public string? NumeroPlanilla { get; set; }

        /// <summary>Guía del Consolidado del S10 observado: con esto el ERP lo ubica.</summary>
        public string? NumeroGuia { get; set; }

        /// <summary>Monto de la planilla completa, en soles.</summary>
        public decimal MontoTotal { get; set; }

        /// <summary>El «MOTIVO *» del trabajador: qué le pide al ERP.</summary>
        public string Motivo { get; set; } = string.Empty;

        /// <summary>Con qué se observó el reembolso.</summary>
        public string? MotivoJefatura { get; set; }

        /// <summary>
        /// Quién escribió esa observación: "Jefatura" o "Tesorería" (RG-49). Vacío en las
        /// correcciones anteriores a la columna, que son todas de jefatura.
        /// </summary>
        public string MotivoOrigen { get; set; } = string.Empty;

        /// <summary>Nombre del Coordinador ERP que atendió. Solo en el correo de atención.</summary>
        public string? AtendidaPor { get; set; }

        /// <summary>Comentario del ERP al confirmar. Solo en el correo de atención.</summary>
        public string? ComentarioAtencion { get; set; }

        /// <summary>True si el ERP anuló el registro y hace falta una guía nueva (CA-19).</summary>
        public bool GuiaAnulada { get; set; }
    }

    /// <summary>
    /// Los dos correos de la gestión con el Coordinador ERP, con el mismo chrome de la intranet
    /// (<see cref="SalidaEmailLayout"/>) que el resto de los correos de salidas:
    ///
    /// <list type="bullet">
    ///   <item>Al Coordinador ERP: hay una corrección del S10 esperándolo, con la guía, la
    ///     observación que devolvió el reembolso —de la jefatura o de Tesorería— y el MOTIVO del
    ///     trabajador (§10.5 / RF-OBS-06).</item>
    ///   <item>Al trabajador: el ERP ya atendió — puede recargar el Consolidado (RF-OBS-08).</item>
    /// </list>
    ///
    /// Ninguno de los dos resuelve nada desde el correo: la confirmación del ERP es un check en su
    /// bandeja y la recarga es un archivo, así que los botones llevan a la pantalla.
    ///
    /// Criterio editorial heredado de <see cref="AbrilEmailLayout"/>: el correo lleva datos y un
    /// acceso, no explicaciones. La bajada es UNA línea y las franjas son de estado.
    /// </summary>
    public static class CorreccionS10EmailTemplates
    {
        // Íconos del catálogo de public/images/emails/icons (los genera
        // Abril-Frontend/scripts/generate-email-icons.js). Se reutilizan los que ya existen, igual
        // que en los otros correos de salidas.
        private const string IconoSolicitada = "req-observaciones";
        private const string IconoAtendida   = "req-aprobada";

        private const string IconoFranjaOk    = "req-check";
        private const string IconoFranjaNo    = "req-rechazadas";
        private const string IconoFranjaAviso = "req-aviso";

        private const string FilaCodigo     = "req-codigo";
        private const string FilaTrabajador = "req-solicitante";
        private const string FilaArea       = "req-area";
        private const string FilaPeriodo    = "req-fecha";
        private const string FilaGuia       = "req-ti";
        private const string FilaMonto      = "req-sustento";
        private const string FilaMotivo     = "req-comentario";
        private const string FilaAtendida   = "req-vistobueno";

        /// <summary>
        /// Al Coordinador ERP: hay una corrección del Consolidado del S10 esperándolo. Las dos
        /// franjas llevan los dos textos que el requerimiento manda enviarle (§10.5): con qué
        /// observó la jefatura y qué pide el trabajador. Van en franjas y no en la tarjeta porque
        /// son lo único que tiene que leer para saber qué hacer en el S10.
        /// </summary>
        public static string Solicitada(
            SalidaEmailLayout l, CorreccionS10CorreoDatos d, string urlBandeja)
        {
            var bloques = new List<string?>
            {
                l.Franja(IconoFranjaNo, AbrilEmailLayout.Tono.Rojo,
                    $"<b>Lo que pide el colaborador:</b> {AbrilEmailLayout.EscMultilinea(d.Motivo.Trim())}"),
            };

            if (!string.IsNullOrWhiteSpace(d.MotivoJefatura))
            {
                // Quién devolvió el consolidado cambia con quién hay que coordinar después, así que
                // el rótulo lo nombra. Sin origen (correcciones viejas) se dice "de la jefatura",
                // que es de donde venían todas.
                var deQuien = string.IsNullOrWhiteSpace(d.MotivoOrigen)
                    ? "de la jefatura"
                    : $"de {d.MotivoOrigen.ToLowerInvariant()}";

                bloques.Add(l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Ambar,
                    $"<b>Observación {AbrilEmailLayout.Esc(deQuien)}:</b> "
                    + AbrilEmailLayout.EscMultilinea(d.MotivoJefatura.Trim())));
            }

            bloques.Add(l.Tarjeta(Filas(d, conTrabajador: true)));
            bloques.Add(l.Boton("Atender la corrección", urlBandeja));
            bloques.Add(l.EnlaceDirecto(urlBandeja));

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoSolicitada,
                    "Corrección del S10 pendiente",
                    $"<b>{AbrilEmailLayout.Esc(d.Trabajador)}</b> solicita una corrección en el S10 para la "
                    + $"rendición <b>{AbrilEmailLayout.Esc(d.Codigo)}</b>."),
                bloques.ToArray());
        }

        /// <summary>
        /// Al trabajador: el ERP ya hizo la corrección en el S10. El botón lo deja en Mis
        /// Rendiciones, que es donde recarga el Consolidado — el paso que esta confirmación acaba
        /// de habilitar.
        ///
        /// Cuando el consolidado se anuló, la franja lo dice en rojo: no alcanza con volver a
        /// subir el mismo archivo, hay que sacar una guía nueva (CA-19).
        /// </summary>
        public static string Atendida(
            SalidaEmailLayout l, CorreccionS10CorreoDatos d, string urlRecargar)
        {
            var quien = string.IsNullOrWhiteSpace(d.AtendidaPor)
                ? "El Coordinador ERP"
                : $"<b>{AbrilEmailLayout.Esc(d.AtendidaPor)}</b>";

            var bloques = new List<string?>
            {
                l.Franja(IconoFranjaOk, AbrilEmailLayout.Tono.Verde,
                    $"{quien} confirmó que la corrección ya se hizo en el S10."),
            };

            if (d.GuiaAnulada)
                bloques.Add(l.Franja(IconoFranjaNo, AbrilEmailLayout.Tono.Rojo,
                    "El registro anterior se <b>anuló</b>: genera una guía nueva en el S10 — la guía "
                    + (string.IsNullOrWhiteSpace(d.NumeroGuia)
                        ? "anterior"
                        : $"<b>{AbrilEmailLayout.Esc(d.NumeroGuia)}</b>")
                    + " ya no se puede volver a usar."));

            if (!string.IsNullOrWhiteSpace(d.ComentarioAtencion))
                bloques.Add(l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Info,
                    "<b>Comentario del ERP:</b> "
                    + AbrilEmailLayout.EscMultilinea(d.ComentarioAtencion.Trim())));

            bloques.Add(l.Tarjeta(Filas(d, conTrabajador: false)));
            bloques.Add(l.Boton("Recargar el Consolidado del S10", urlRecargar));
            bloques.Add(l.EnlaceDirecto(urlRecargar));

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoAtendida,
                    "Ya puedes recargar el Consolidado del S10",
                    $"La corrección que pediste para la rendición <b>{AbrilEmailLayout.Esc(d.Codigo)}</b> "
                    + "fue atendida en el S10."),
                bloques.ToArray());
        }

        // ── Bloques compartidos ───────────────────────────────────────────────

        /// <summary>
        /// La tarjeta de la planilla. Las filas sin dato no se agregan: una tarjeta con "—"
        /// repetidos no informa nada.
        /// </summary>
        /// <param name="conTrabajador">
        /// true solo en el correo al ERP: al trabajador no hay que decirle su propio nombre.
        /// </param>
        private static List<AbrilEmailLayout.Fila> Filas(
            CorreccionS10CorreoDatos d, bool conTrabajador)
        {
            var filas = new List<AbrilEmailLayout.Fila>();

            if (!string.IsNullOrWhiteSpace(d.Codigo))
                filas.Add(new(FilaCodigo, "Rendición", AbrilEmailLayout.Esc(d.Codigo)));

            if (conTrabajador)
            {
                filas.Add(new(FilaTrabajador, "Colaborador", AbrilEmailLayout.Esc(d.Trabajador)));
                if (!string.IsNullOrWhiteSpace(d.Area))
                    filas.Add(new(FilaArea, "Área", AbrilEmailLayout.Esc(d.Area)));
            }

            if (!string.IsNullOrWhiteSpace(d.Periodo))
                filas.Add(new(FilaPeriodo, "Periodo", AbrilEmailLayout.Esc(d.Periodo)));

            if (!string.IsNullOrWhiteSpace(d.NumeroPlanilla))
                filas.Add(new(FilaCodigo, "Planilla", AbrilEmailLayout.Esc(d.NumeroPlanilla)));

            // La guía es EL dato con el que el ERP ubica el registro en el S10: si falta, se dice
            // en vez de omitir la fila, porque su ausencia es en sí un problema a resolver.
            filas.Add(new(FilaGuia, "N.º de guía S10",
                string.IsNullOrWhiteSpace(d.NumeroGuia)
                    ? "sin guía registrada"
                    : AbrilEmailLayout.Esc(d.NumeroGuia)));

            if (d.MontoTotal > 0m)
                filas.Add(new(FilaMonto, "Monto de la planilla",
                    $"S/ {d.MontoTotal.ToString("N2", CultureInfo.GetCultureInfo("es-PE"))}"));

            if (!string.IsNullOrWhiteSpace(d.AtendidaPor))
                filas.Add(new(FilaAtendida, "Atendida por", AbrilEmailLayout.Esc(d.AtendidaPor)));
            else if (!conTrabajador && !string.IsNullOrWhiteSpace(d.Motivo))
                filas.Add(new(FilaMotivo, "Motivo", AbrilEmailLayout.Esc(d.Motivo)));

            return filas;
        }
    }
}
