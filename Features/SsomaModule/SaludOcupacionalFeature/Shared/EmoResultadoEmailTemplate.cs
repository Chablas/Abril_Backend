using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Shared
{
    /// <summary>
    /// Datos del correo "Resultado de EMO". Se arma con una sola consulta en
    /// <c>EmoResultadoNotificacionService</c>: la plantilla no vuelve a la base a buscar nada.
    /// </summary>
    public sealed class EmoResultadoCorreoDatos
    {
        public string Trabajador { get; set; } = string.Empty;
        public string? Dni { get; set; }
        public string? Puesto { get; set; }
        public string? TipoEmo { get; set; }
        public DateOnly FechaEmo { get; set; }
        /// <summary>Vencimiento del examen. Null en No Apto y Observado: esos no dan vigencia.</summary>
        public DateOnly? FechaVencimiento { get; set; }
        public string? Clinica { get; set; }
        public string? Proyecto { get; set; }
        /// <summary>Apto, Apto con Restricciones o No Apto — las únicas que envían correo.</summary>
        public string Aptitud { get; set; } = string.Empty;
        /// <summary>Restricciones registradas con el examen. Solo llegan con "Apto con Restricciones".</summary>
        public List<string> Restricciones { get; set; } = new();
        /// <summary>
        /// true = la ficha todavía es de pre-ingreso: quien lee el correo es el solicitante de la
        /// vacante, no la jefatura de alguien que ya trabaja acá.
        ///
        /// Cambia cómo se le llama a la persona examinada en todo el correo —la bajada y la
        /// etiqueta de la primera fila— vía <see cref="EmoExaminadoTexto"/>. Quién lo recibe no
        /// sale de acá: la versión del postulante es su propia sección de Configuración de EMOs
        /// (<c>RESULTADO_POSTULANTE</c>), con sus propios destinatarios.
        /// </summary>
        public bool EsPostulante { get; set; }
    }

    /// <summary>
    /// El correo que avisa el resultado del EMO al médico ocupacional, a GTH y a la jefatura del
    /// trabajador (o al solicitante de la vacante, si todavía es un postulante).
    ///
    /// Sirve a las dos versiones del correo —<c>RESULTADO</c> y <c>RESULTADO_POSTULANTE</c>— con
    /// el mismo HTML: lo único que cambia es cómo se le llama a la persona examinada, vía
    /// <see cref="EmoExaminadoTexto"/>. A quién le llega cada versión sale de la matriz de
    /// Configuración de EMOs, que las tiene como dos secciones separadas.
    ///
    /// Es un correo que solo informa: no lleva botón ni enlace, a diferencia del resto de la
    /// familia. Quien lo recibe no tiene que ir a hacer nada — el certificado de aptitud, la
    /// habilitación y el requerimiento los mueve el sistema solo cuando se guarda el examen.
    ///
    /// Se envía únicamente con un veredicto cerrado. "Observado" no manda nada: significa que
    /// falta derivar a interconsulta y todavía no hay resultado que comunicar.
    /// </summary>
    public static class EmoResultadoEmailTemplate
    {
        // Íconos del catálogo de public/images/emails/icons. Los emo-* los usa también el correo
        // de confirmación de cita; los req-* vienen del juego de Reclutamiento y se reutilizan
        // como en Gestión Administrativa · Salidas, en lugar de generar un juego propio.
        private const string CabeceraApto     = "emo-check";       // 96x96
        private const string CabeceraVeredicto = "req-decision";   // 96x96

        private const string FranjaOk         = "req-check";          // 88x88, verde
        private const string FranjaRestriccion = "req-observaciones"; // 88x88, ámbar
        private const string FranjaNo         = "req-rechazadas";     // 88x88, rojo

        private const string FilaTrabajador   = "emo-trabajador";
        private const string FilaDni          = "req-codigo";
        private const string FilaPuesto       = "req-puesto";
        private const string FilaTipo         = "emo-tipo";
        private const string FilaFecha        = "emo-fecha";
        private const string FilaVigencia     = "req-plazo";
        private const string FilaClinica      = "emo-clinica";
        private const string FilaProyecto     = "emo-proyecto";
        private const string FilaRestricciones = "req-comentario";

        /// <summary>Las tres aptitudes que sí disparan el correo.</summary>
        public const string AptitudApto              = "Apto";
        public const string AptitudAptoRestricciones = "Apto con Restricciones";
        public const string AptitudNoApto            = "No Apto";

        public static string Construir(SaludOcupacionalEmailLayout l, EmoResultadoCorreoDatos d)
        {
            var esNoApto        = Es(d.Aptitud, AptitudNoApto);
            var esRestricciones = Es(d.Aptitud, AptitudAptoRestricciones);

            var quien = "del " + EmoExaminadoTexto.Minuscula(d.EsPostulante);

            return l.Documento(
                new AbrilEmailLayout.Cabecera(
                    esNoApto ? CabeceraVeredicto : CabeceraApto,
                    "Resultado de EMO",
                    $"Ya está registrado el resultado del examen médico ocupacional {quien} "
                    + $"<b>{AbrilEmailLayout.Esc(d.Trabajador)}</b>."),
                Veredicto(l, d, esNoApto, esRestricciones),
                l.Tarjeta(Filas(d)));
        }

        /// <summary>
        /// La franja con el veredicto: es lo único que quien recibe el correo tiene que leer, así
        /// que va antes de la tarjeta de datos y con el color del resultado.
        /// </summary>
        private static string Veredicto(
            SaludOcupacionalEmailLayout l, EmoResultadoCorreoDatos d, bool esNoApto, bool esRestricciones)
        {
            if (esNoApto)
                return l.Franja(FranjaNo, AbrilEmailLayout.Tono.Rojo,
                    "<b>No Apto.</b> No corresponde su ingreso ni su habilitación con este examen.");

            if (esRestricciones)
                return l.Franja(FranjaRestriccion, AbrilEmailLayout.Tono.Ambar,
                    "<b>Apto con Restricciones.</b> Puede desempeñarse en el puesto respetando las "
                    + "restricciones indicadas abajo.");

            return l.Franja(FranjaOk, AbrilEmailLayout.Tono.Verde,
                "<b>Apto.</b> Sin observaciones para el puesto evaluado.");
        }

        /// <summary>
        /// Las filas de la tarjeta. Las que no tienen dato no se agregan: una tarjeta con "—"
        /// repetidos no informa nada.
        /// </summary>
        private static List<AbrilEmailLayout.Fila> Filas(EmoResultadoCorreoDatos d)
        {
            var filas = new List<AbrilEmailLayout.Fila>
            {
                new(FilaTrabajador, EmoExaminadoTexto.Capitalizada(d.EsPostulante),
                    AbrilEmailLayout.Esc(d.Trabajador)),
            };

            if (!string.IsNullOrWhiteSpace(d.Dni))
                filas.Add(new(FilaDni, "Documento", AbrilEmailLayout.Esc(d.Dni)));

            if (!string.IsNullOrWhiteSpace(d.Puesto))
                filas.Add(new(FilaPuesto, "Puesto", AbrilEmailLayout.Esc(d.Puesto)));

            if (!string.IsNullOrWhiteSpace(d.TipoEmo))
                filas.Add(new(FilaTipo, "Tipo de EMO", AbrilEmailLayout.Esc(d.TipoEmo)));

            filas.Add(new(FilaFecha, "Fecha del examen", $"{d.FechaEmo:dd/MM/yyyy}"));

            // Solo las aptitudes que dan vigencia la traen; en No Apto el examen no vence porque
            // nunca llegó a habilitar nada.
            if (d.FechaVencimiento.HasValue)
                filas.Add(new(FilaVigencia, "Vigente hasta", $"{d.FechaVencimiento.Value:dd/MM/yyyy}"));

            if (!string.IsNullOrWhiteSpace(d.Clinica))
                filas.Add(new(FilaClinica, "Clínica", AbrilEmailLayout.Esc(d.Clinica)));

            if (!string.IsNullOrWhiteSpace(d.Proyecto))
                filas.Add(new(FilaProyecto, "Proyecto", AbrilEmailLayout.Esc(d.Proyecto)));

            if (d.Restricciones.Count > 0)
                filas.Add(new(FilaRestricciones, "Restricciones",
                    string.Join("<br />", d.Restricciones.Select(r => "• " + AbrilEmailLayout.Esc(r)))));

            return filas;
        }

        /// <summary>Asunto del correo. Mismo formato que el resto de los correos de EMO.</summary>
        public static string Asunto(EmoResultadoCorreoDatos d) =>
            $"[EMO {d.Aptitud}] {d.Trabajador} — {d.FechaEmo:dd/MM/yyyy}";

        private static bool Es(string? valor, string esperado) =>
            string.Equals(valor?.Trim(), esperado, StringComparison.OrdinalIgnoreCase);
    }
}
