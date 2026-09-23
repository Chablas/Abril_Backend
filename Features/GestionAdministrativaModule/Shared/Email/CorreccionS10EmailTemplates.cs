using System.Globalization;
using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Email
{
    /// <summary>
    /// Datos de los dos correos de la subsanación con el ERP. Los dos hablan de la misma unidad —la
    /// corrección del Consolidado del S10 que pidió el consolidador, que puede cubrir una o varias
    /// planillas— así que comparten un solo shape; lo arma el servicio o el repositorio en un número
    /// fijo de consultas y el correo no vuelve a la base.
    /// </summary>
    public sealed class CorreccionS10CorreoDatos
    {
        /// <summary>La corrección a la que lleva el botón (la primera, si el pedido cubre varias planillas).</summary>
        public int CorreccionId { get; set; }
        public int RendicionId { get; set; }

        /// <summary>
        /// Consolidado observado sobre el que se pidió la corrección. Lo usa el aviso de atención:
        /// su botón abre Consolidados en ese documento, que es donde se reemplaza. Null en las
        /// correcciones anteriores a la columna.
        /// </summary>
        public int? ConsolidadoS10Id { get; set; }

        /// <summary>
        /// Código CONS-SIGLA-AAAA-NNN del consolidado observado. Los dos correos nombran el pedido
        /// por su consolidado, no por las planillas que cubre. Null en los anteriores al código.
        /// </summary>
        public string? ConsolidadoCodigo { get; set; }

        /// <summary>Trabajador(es) dueños de las salidas, separados por coma.</summary>
        public string Trabajador { get; set; } = string.Empty;
        public string? Area { get; set; }

        /// <summary>Quién pidió la corrección: el consolidador.</summary>
        public string? SolicitadaPor { get; set; }
        /// <summary>Correo de quien la pidió. Lo usa el aviso de atención, que va dirigido a él.</summary>
        public string? SolicitadaPorEmail { get; set; }

        /// <summary>Periodo de las salidas observadas ("Agosto 2026", o un rango si cruza meses).</summary>
        public string? Periodo { get; set; }

        /// <summary>Número de reembolso del Consolidado del S10 observado: con esto el ERP lo ubica.</summary>
        public string? NumeroReembolso { get; set; }

        /// <summary>Monto declarado en el S10 para el consolidado, en soles. 0 en los viejos, que no lo tienen.</summary>
        public decimal MontoTotal { get; set; }

        /// <summary>El «MOTIVO *» del consolidador: qué le pide al ERP.</summary>
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
    }

    /// <summary>
    /// Los dos correos de la gestión con el Coordinador ERP, con el mismo chrome de la intranet
    /// (<see cref="SalidaEmailLayout"/>) que el resto de los correos de salidas:
    ///
    /// <list type="bullet">
    ///   <item>Al Coordinador ERP: hay una corrección del S10 esperándolo, con el código del
    ///     consolidado, el número de reembolso, la observación que devolvió el reembolso —de la
    ///     jefatura o de Tesorería— y el MOTIVO del consolidador (§10.5 / RF-OBS-06).</item>
    ///   <item>Al consolidador: el ERP ya atendió — puede recargar el Consolidado (RF-OBS-08).</item>
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
        private const string FilaReembolso       = "req-ti";
        private const string FilaMonto      = "req-sustento";
        private const string FilaMotivo     = "req-comentario";
        private const string FilaAtendida   = "req-vistobueno";

        /// <summary>
        /// Al Coordinador ERP: hay una corrección del Consolidado del S10 esperándolo. Las dos
        /// franjas llevan los dos textos que el requerimiento manda enviarle (§10.5): con qué se
        /// observó y qué pide el consolidador. Van en franjas y no en la tarjeta porque son lo único
        /// que tiene que leer para saber qué hacer en el S10.
        /// </summary>
        public static string Solicitada(
            SalidaEmailLayout l, CorreccionS10CorreoDatos d, string urlBandeja)
        {
            var bloques = new List<string?>
            {
                l.Franja(IconoFranjaNo, AbrilEmailLayout.Tono.Rojo,
                    $"<b>Lo que pide el consolidador:</b> {AbrilEmailLayout.EscMultilinea(d.Motivo.Trim())}"),
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

            bloques.Add(l.Tarjeta(Filas(d)));
            bloques.Add(l.Boton("Atender la corrección", urlBandeja));
            bloques.Add(l.EnlaceDirecto(urlBandeja));

            var quien = string.IsNullOrWhiteSpace(d.SolicitadaPor) ? "El consolidador" : AbrilEmailLayout.Esc(d.SolicitadaPor);

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoSolicitada,
                    "Corrección del S10 pendiente",
                    $"<b>{quien}</b> solicita una corrección en el S10 para {ElConsolidado(d)}."),
                bloques.ToArray());
        }

        /// <summary>
        /// Al consolidador: el ERP ya hizo la corrección en el S10. El botón lo deja en Consolidados,
        /// que es donde recarga el Consolidado — el paso que esta confirmación acaba de habilitar.
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

            if (!string.IsNullOrWhiteSpace(d.ComentarioAtencion))
                bloques.Add(l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Info,
                    "<b>Comentario del ERP:</b> "
                    + AbrilEmailLayout.EscMultilinea(d.ComentarioAtencion.Trim())));

            bloques.Add(l.Tarjeta(Filas(d)));
            bloques.Add(l.Boton("Recargar el Consolidado del S10", urlRecargar));
            bloques.Add(l.EnlaceDirecto(urlRecargar));

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoAtendida,
                    "Ya puedes recargar el Consolidado del S10",
                    $"La corrección que pediste para {ElConsolidado(d)} fue atendida en el S10."),
                bloques.ToArray());
        }

        /// <summary>
        /// Cómo se nombra el pedido en el asunto de los dos correos: " - CONS-…", o " - Consolidado
        /// del S10 N.° …" en los anteriores al código. Lo usan los dos servicios que los envían.
        /// </summary>
        public static string NombreEnAsunto(CorreccionS10CorreoDatos d) =>
            !string.IsNullOrWhiteSpace(d.ConsolidadoCodigo) ? $" - {d.ConsolidadoCodigo}"
            : !string.IsNullOrWhiteSpace(d.NumeroReembolso) ? $" - Consolidado del S10 N.° {d.NumeroReembolso}"
            : string.Empty;

        // ── Bloques compartidos ───────────────────────────────────────────────

        /// <summary>
        /// "el consolidado <b>CONS-…</b> (N.º de reembolso <b>…</b>)": el pedido es sobre el
        /// consolidado, así que se nombra por su código y por el número con el que lo registró el
        /// S10, no por las rendiciones que cubre.
        /// </summary>
        private static string ElConsolidado(CorreccionS10CorreoDatos d)
        {
            var codigo = string.IsNullOrWhiteSpace(d.ConsolidadoCodigo)
                ? null
                : $"<b>{AbrilEmailLayout.Esc(d.ConsolidadoCodigo)}</b>";
            var numero = string.IsNullOrWhiteSpace(d.NumeroReembolso)
                ? null
                : $"N.º de reembolso <b>{AbrilEmailLayout.Esc(d.NumeroReembolso)}</b>";

            return (codigo, numero) switch
            {
                (not null, not null) => $"el consolidado {codigo} ({numero})",
                (not null, null)     => $"el consolidado {codigo}",
                (null, not null)     => $"el consolidado con {numero}",
                _                    => "el consolidado",
            };
        }

        /// <summary>
        /// La tarjeta del pedido. Las filas sin dato no se agregan: una tarjeta con "—" repetidos no
        /// informa nada. El consolidador lleva las rendiciones de toda su área, así que la tarjeta
        /// siempre nombra a los colaboradores.
        /// </summary>
        private static List<AbrilEmailLayout.Fila> Filas(CorreccionS10CorreoDatos d)
        {
            var filas = new List<AbrilEmailLayout.Fila>();

            if (!string.IsNullOrWhiteSpace(d.ConsolidadoCodigo))
                filas.Add(new(FilaCodigo, "Consolidado", AbrilEmailLayout.Esc(d.ConsolidadoCodigo)));

            // El número de reembolso es EL dato con el que el ERP ubica el registro en el S10: si falta, se dice
            // en vez de omitir la fila, porque su ausencia es en sí un problema a resolver.
            filas.Add(new(FilaReembolso, "N.º de reembolso del S10",
                string.IsNullOrWhiteSpace(d.NumeroReembolso)
                    ? "sin número de reembolso registrado"
                    : AbrilEmailLayout.Esc(d.NumeroReembolso)));

            if (!string.IsNullOrWhiteSpace(d.Trabajador))
                filas.Add(new(FilaTrabajador, "Colaborador", AbrilEmailLayout.Esc(d.Trabajador)));
            if (!string.IsNullOrWhiteSpace(d.Area))
                filas.Add(new(FilaArea, "Área", AbrilEmailLayout.Esc(d.Area)));

            if (!string.IsNullOrWhiteSpace(d.Periodo))
                filas.Add(new(FilaPeriodo, "Periodo", AbrilEmailLayout.Esc(d.Periodo)));

            if (d.MontoTotal > 0m)
                filas.Add(new(FilaMonto, "Monto del consolidado",
                    $"S/ {d.MontoTotal.ToString("N2", CultureInfo.GetCultureInfo("es-PE"))}"));

            if (!string.IsNullOrWhiteSpace(d.AtendidaPor))
            {
                filas.Add(new(FilaAtendida, "Atendida por", AbrilEmailLayout.Esc(d.AtendidaPor)));
                if (!string.IsNullOrWhiteSpace(d.Motivo))
                    filas.Add(new(FilaMotivo, "Lo que pediste", AbrilEmailLayout.Esc(d.Motivo)));
            }

            return filas;
        }
    }
}
