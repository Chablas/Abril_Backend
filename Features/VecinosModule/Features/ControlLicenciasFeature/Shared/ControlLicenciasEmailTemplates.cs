using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Shared
{
    /// <summary>
    /// Los dos correos automáticos de Control de Licencias: vencimiento de un documento y visita
    /// programada de Anexo H. Comparten el mismo chrome (<see cref="ControlLicenciasEmailLayout"/>)
    /// que el resto de la intranet — acá solo queda qué datos van y en qué orden.
    ///
    /// Ambos llevan siempre el proyecto y la razón social del proyecto: sin ese dato, alguien que
    /// coordina varias obras no puede saber a cuál corresponde el aviso solo por el nombre del tipo
    /// de licencia.
    /// </summary>
    public static class ControlLicenciasEmailTemplates
    {
        private const string IconoCabecera    = "req-solicitud";
        private const string IconoAlerta      = "req-solicitud";
        private const string IconoProyecto    = "req-proyecto";
        private const string IconoRazonSocial = "req-solicitante";
        private const string IconoLicencia    = "req-codigo";
        private const string IconoVencimiento = "req-fecha";
        private const string IconoEstado      = "req-plazo";
        private const string IconoVisita      = "req-fecha";

        private const string IconoFranjaUrgente = "req-rechazadas";

        /// <summary>Datos comunes del proyecto, los mismos para vencimiento y visita.</summary>
        public sealed record Proyecto(
            string Nombre, string? Codigo, string? RazonSocial, string? Ruc);

        /// <summary>Correo de vencimiento (recordatorio previo o alerta ya vencida).</summary>
        public static string Vencimiento(
            ControlLicenciasEmailLayout l, Proyecto proyecto, string tipoDescripcion,
            DateOnly fechaVencimiento, int diasRestantes, bool esUrgente, string urlControlLicencias)
        {
            static string Esc(string? valor) => AbrilEmailLayout.Esc(valor);

            var vencido = diasRestantes < 0;
            var detalleDias = diasRestantes > 1 ? $"Faltan <b>{diasRestantes} días</b> para su vencimiento."
                : diasRestantes == 1 ? "Vence <b>mañana</b>."
                : diasRestantes == 0 ? "Vence <b>hoy</b>."
                : $"Venció hace <b>{-diasRestantes} día(s)</b>.";

            var titulo = vencido ? "Licencia vencida" : "Vencimiento de licencia próximo";
            var bajada = vencido
                ? $"La licencia <b>{Esc(tipoDescripcion)}</b> venció el <b>{fechaVencimiento:dd/MM/yyyy}</b>."
                : $"La licencia <b>{Esc(tipoDescripcion)}</b> vence el <b>{fechaVencimiento:dd/MM/yyyy}</b>.";

            return l.Documento(
                new AbrilEmailLayout.Cabecera(IconoCabecera, titulo, bajada),
                esUrgente
                    ? l.Franja(IconoFranjaUrgente, AbrilEmailLayout.Tono.Rojo,
                        "<b>URGENTE — Interferencia de vías.</b> Este trámite requiere gestión inmediata ante la municipalidad.")
                    : "",
                l.Tarjeta(DatosProyectoYLicencia(proyecto, tipoDescripcion, fechaVencimiento, detalleDias, vencido)),
                l.Boton("Ver en Control de Licencias", urlControlLicencias),
                l.EnlaceDirecto(urlControlLicencias));
        }

        /// <summary>Correo de recordatorio de visita programada (Anexo H).</summary>
        public static string Visita(
            ControlLicenciasEmailLayout l, Proyecto proyecto, string tipoDescripcion,
            DateOnly fechaVisita, string urlControlLicencias)
        {
            static string Esc(string? valor) => AbrilEmailLayout.Esc(valor);

            var filas = new List<AbrilEmailLayout.Fila>
            {
                new(IconoProyecto, "Proyecto", FilaProyecto(proyecto)),
            };
            AgregarRazonSocial(filas, proyecto);
            filas.Add(new(IconoLicencia, "Documento", Esc(tipoDescripcion)));
            filas.Add(new(IconoVisita, "Visita programada", $"<b>{fechaVisita:dd/MM/yyyy}</b>"));

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoCabecera, "Visita programada",
                    $"Hay una visita de <b>{Esc(tipoDescripcion)}</b> programada para el <b>{fechaVisita:dd/MM/yyyy}</b>."),
                l.Tarjeta(filas),
                l.Boton("Ver en Control de Licencias", urlControlLicencias),
                l.EnlaceDirecto(urlControlLicencias));
        }

        // ── Bloques compartidos ───────────────────────────────────────────────

        private static List<AbrilEmailLayout.Fila> DatosProyectoYLicencia(
            Proyecto proyecto, string tipoDescripcion, DateOnly fechaVencimiento,
            string detalleDias, bool vencido)
        {
            static string Esc(string? valor) => AbrilEmailLayout.Esc(valor);

            var filas = new List<AbrilEmailLayout.Fila>
            {
                new(IconoProyecto, "Proyecto", FilaProyecto(proyecto)),
            };
            AgregarRazonSocial(filas, proyecto);
            filas.Add(new(IconoLicencia, "Licencia", Esc(tipoDescripcion)));
            filas.Add(new(IconoVencimiento, vencido ? "Venció" : "Vence", $"<b>{fechaVencimiento:dd/MM/yyyy}</b>"));
            filas.Add(new(IconoEstado, "Estado", detalleDias));
            return filas;
        }

        private static string FilaProyecto(Proyecto proyecto)
        {
            static string Esc(string? valor) => AbrilEmailLayout.Esc(valor);
            return string.IsNullOrWhiteSpace(proyecto.Codigo)
                ? $"<b>{Esc(proyecto.Nombre)}</b>"
                : $"<b>{Esc(proyecto.Nombre)}</b><br />Código: {Esc(proyecto.Codigo)}";
        }

        private static void AgregarRazonSocial(List<AbrilEmailLayout.Fila> filas, Proyecto proyecto)
        {
            if (string.IsNullOrWhiteSpace(proyecto.RazonSocial)) return;

            static string Esc(string? valor) => AbrilEmailLayout.Esc(valor);
            var valor = string.IsNullOrWhiteSpace(proyecto.Ruc)
                ? Esc(proyecto.RazonSocial)
                : $"{Esc(proyecto.RazonSocial)}<br />RUC: {Esc(proyecto.Ruc)}";
            filas.Add(new(IconoRazonSocial, "Razón social", valor));
        }
    }
}
