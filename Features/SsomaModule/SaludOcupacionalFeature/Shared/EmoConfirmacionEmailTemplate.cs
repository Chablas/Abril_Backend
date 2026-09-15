using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Shared
{
    /// <summary>
    /// Correo "EMO Confirmado" (programación aceptada por la clínica), tanto la versión del
    /// trabajador como la del postulante: es el mismo correo y solo cambia cómo se le llama a la
    /// persona citada (ver <see cref="EmoExaminadoTexto"/>). Quién lo recibe en cada caso NO se
    /// decide acá, sale de la matriz de Configuración de EMOs — son dos secciones distintas,
    /// <c>ACEPTADA</c> y <c>ACEPTADA_POSTULANTE</c>.
    ///
    /// Armaba su propio HTML de punta a punta —es anterior a que el layout subiera a Shared— y por
    /// eso se fue quedando atrás del resto de la familia: el título salía pegado al logo en vez de
    /// centrado, y su constante de fuente traía las comillas de "Segoe UI" literales, que cierran
    /// el atributo <c>style</c> a media declaración y hacían que el correo se viera en serif y sin
    /// los colores de marca (ver la nota de <see cref="AbrilEmailLayout.Fuente"/>). Ahora usa el
    /// mismo <see cref="SaludOcupacionalEmailLayout"/> que el correo de resultado, así que eso lo
    /// resuelve la clase base y no se puede volver a desincronizar.
    ///
    /// El afiche de recomendaciones de la clínica va al final y es parte del correo: lo pidió
    /// Salud Ocupacional para que quien va al examen lea las indicaciones (ayuno, ropa, lentes)
    /// sin abrir un adjunto. No quitarlo.
    /// </summary>
    public static class EmoConfirmacionEmailTemplate
    {
        // Íconos del catálogo de public/images/emails/icons (los genera
        // Abril-Frontend/scripts/generate-email-icons.js).
        private const string CabeceraConfirmado = "emo-check"; // 96x96, aro lima
        private const string FranjaPresentarse  = "emo-aviso"; // 88x88, círculo azul → Tono.Azul

        private const string FilaExaminado = "emo-trabajador";
        private const string FilaFecha     = "emo-fecha";
        private const string FilaHora      = "emo-hora";
        private const string FilaProyecto  = "emo-proyecto";
        private const string FilaClinica   = "emo-clinica";
        private const string FilaDireccion = "emo-direccion";

        /// <summary>Afiche de la clínica con las indicaciones previas al examen.</summary>
        private const string Recomendaciones = "recomendaciones-emo.jpg";

        /// <summary>Datos que se listan en la tarjeta central del correo.</summary>
        /// <param name="Examinado">Nombre de la persona citada (trabajador o postulante).</param>
        /// <param name="EsPostulante">
        /// true = la ficha todavía es de pre-ingreso, así que el correo lo llama "postulante" en
        /// vez de "trabajador" (ver <see cref="EmoExaminadoTexto"/>). Cambia la etiqueta de la
        /// tarjeta y el aviso del pie, nada más: la cita es la misma.
        /// </param>
        /// <remarks>
        /// No lleva el tipo de EMO: Salud Ocupacional lo pidió fuera de la tarjeta porque a quien
        /// recibe el correo no le cambia nada —tiene que presentarse igual— y es jerga nuestra.
        /// El dato sigue en la programación y en el correo de resultado; acá no se vuelve a poner.
        /// </remarks>
        public sealed record Datos(
            string Examinado,
            string Fecha,
            string Hora,
            string Proyecto,
            string Clinica,
            string? Direccion,
            bool EsPostulante);

        public static string Construir(SaludOcupacionalEmailLayout l, Datos datos) =>
            l.Documento(
                new AbrilEmailLayout.Cabecera(
                    CabeceraConfirmado, "EMO Confirmado",
                    "Se ha confirmado la programación del Examen Médico Ocupacional:"),
                l.Tarjeta(Filas(datos)),
                l.Franja(FranjaPresentarse, AbrilEmailLayout.Tono.Azul,
                    AbrilEmailLayout.Esc(EmoExaminadoTexto.ConArticulo(datos.EsPostulante))
                    + " debe presentarse en la clínica en la fecha y hora indicadas. "
                    + "Ese mismo día se le brindarán los resultados."),
                l.Imagen(Recomendaciones, "Recomendaciones previas al Examen Médico Ocupacional"));

        /// <summary>
        /// Las filas de la tarjeta. Fecha, hora, proyecto y clínica van siempre —el llamador manda
        /// "—" cuando no hay dato— porque son la cita en sí: un hueco ahí se lee como un error del
        /// sistema, no como un dato que falta. La dirección sí se omite si la clínica no la tiene
        /// cargada.
        /// </summary>
        private static List<AbrilEmailLayout.Fila> Filas(Datos d)
        {
            var filas = new List<AbrilEmailLayout.Fila>
            {
                new(FilaExaminado, EmoExaminadoTexto.Capitalizada(d.EsPostulante),
                    AbrilEmailLayout.Esc(d.Examinado)),
                new(FilaFecha,    "Fecha",    AbrilEmailLayout.Esc(d.Fecha)),
                new(FilaHora,     "Hora",     AbrilEmailLayout.Esc(d.Hora)),
                new(FilaProyecto, "Proyecto", AbrilEmailLayout.Esc(d.Proyecto)),
                new(FilaClinica,  "Clínica",  AbrilEmailLayout.Esc(d.Clinica)),
            };

            if (!string.IsNullOrWhiteSpace(d.Direccion))
                filas.Add(new(FilaDireccion, "Dirección", AbrilEmailLayout.Esc(d.Direccion)));

            return filas;
        }
    }
}
