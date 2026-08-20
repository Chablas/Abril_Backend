namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared
{
    /// <summary>
    /// Correo "Solicitud de personal por aprobar" que va a los gerentes.
    ///
    /// Es el correo del que salió el brandeo del resto: todo el chrome (logo, aros lima, tarjetas,
    /// tabla azul, botón verde) vive ahora en <see cref="ReclutamientoEmailLayout"/> y lo comparten
    /// los diez correos del módulo. Acá solo queda qué datos van y en qué orden.
    ///
    /// El correo NO explica el flujo de aprobación ni qué hace el botón: eso se ve en la pantalla
    /// «Aprobaciones». Acá solo van los datos de la solicitud y el acceso. No volver a agregar esos
    /// párrafos.
    /// </summary>
    public static class AprobacionGgEmailTemplate
    {
        /// <summary>Una vacante de la solicitud, con los importes y textos ya formateados.</summary>
        public sealed record Vacante(
            string Codigo,
            string Puesto,
            string Tipo,
            string? Reemplazado,
            string ProyectoObra,
            string Salario);

        /// <summary>Datos que se muestran en el correo.</summary>
        /// <param name="EsRecordatorio">true en el reenvío: agrega la franja ámbar de recordatorio.</param>
        public sealed record Datos(
            string Area,
            string? Solicitante,
            IReadOnlyList<Vacante> Vacantes,
            string? Justificacion,
            string? SustentoUrl,
            string? SustentoNombre,
            string Link,
            bool EsRecordatorio);

        /// <param name="assetsUrl">
        /// Base pública desde donde se sirven las imágenes (App:EmailAssetsUrl). Tiene que ser
        /// alcanzable desde internet: la resuelve el proxy de imágenes de Outlook, no el cliente.
        /// </param>
        public static string Construir(Datos datos, string assetsUrl)
        {
            var l = new ReclutamientoEmailLayout(assetsUrl);
            static string Esc(string? valor) => ReclutamientoEmailLayout.Esc(valor);

            // ── Tarjeta de cabecera: quién pide ───────────────────────────────
            var datosSolicitud = new List<ReclutamientoEmailLayout.Fila>
            {
                new("req-area", "Área solicitante", Esc(datos.Area)),
            };
            if (!string.IsNullOrWhiteSpace(datos.Solicitante))
                datosSolicitud.Add(new("req-solicitante", "Solicitante", Esc(datos.Solicitante)));

            // ── Tarjeta de cierre: por qué lo pide y con qué respaldo ─────────
            var datosSustento = new List<ReclutamientoEmailLayout.Fila>();
            if (!string.IsNullOrWhiteSpace(datos.Justificacion))
                datosSustento.Add(new("req-justificacion", "Justificación", Esc(datos.Justificacion)));
            if (!string.IsNullOrWhiteSpace(datos.SustentoUrl))
                datosSustento.Add(new(
                    "req-sustento",
                    "Sustento adjunto",
                    ReclutamientoEmailTextos.Enlace(datos.SustentoUrl!, datos.SustentoNombre ?? "Ver documento")));

            return l.Documento(
                new ReclutamientoEmailLayout.Cabecera(
                    "req-solicitud", "Solicitud de Personal", "Pendiente de tu aprobación:"),
                datos.EsRecordatorio
                    ? l.Franja("req-recordatorio", ReclutamientoEmailLayout.Tono.Ambar,
                        "<b>Recordatorio:</b> sigue pendiente de tu aprobación.")
                    : "",
                l.Tarjeta(datosSolicitud),
                l.Seccion("req-vacantes", $"Vacantes solicitadas ({datos.Vacantes.Count})"),
                l.Tabla(ReclutamientoEmailTextos.ColumnasVacantesConSalario, FilasVacantes(datos.Vacantes)),
                l.Tarjeta(datosSustento),
                l.Boton("Revisar y aprobar", datos.Link),
                l.EnlaceDirecto(datos.Link));
        }

        /// <summary>
        /// Filas de la tabla de vacantes. La línea "Reemplaza a {trabajador}" va en la misma celda
        /// del tipo (vacía en las vacantes nuevas y en los requerimientos anteriores a que se
        /// pidiera ese dato) para no agregar una columna a una tabla que ya tiene cinco.
        /// </summary>
        private static List<IReadOnlyList<ReclutamientoEmailLayout.Celda>> FilasVacantes(
            IReadOnlyList<Vacante> vacantes) =>
            vacantes
                .Select(v => (IReadOnlyList<ReclutamientoEmailLayout.Celda>)new List<ReclutamientoEmailLayout.Celda>
                {
                    new(ReclutamientoEmailLayout.Esc(v.Codigo), Negrita: true, Color: ReclutamientoEmailLayout.Azul, NoWrap: true),
                    new(ReclutamientoEmailLayout.Esc(v.Puesto)),
                    new(ReclutamientoEmailLayout.Esc(v.Tipo) + ReclutamientoEmailTextos.Reemplaza(v.Reemplazado)),
                    new(ReclutamientoEmailLayout.Esc(v.ProyectoObra)),
                    new(ReclutamientoEmailLayout.Esc(v.Salario), Negrita: true, NoWrap: true),
                })
                .ToList();
    }
}
