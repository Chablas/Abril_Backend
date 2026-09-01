using System.Globalization;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Shared.Helpers;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Helpers
{
    /// <summary>
    /// Rellena la plantilla Word de la carta oferta. Es lo único que sabe qué placeholder existe y
    /// con qué se llena: el servicio solo le pasa el contexto del requerimiento y las condiciones
    /// que puso GTH, y recibe el .docx listo para subir.
    ///
    /// Agregar un placeholder es agregar una entrada acá y ponerlo en el .docx. Los que la plantilla
    /// no tenga se ignoran solos, así que sobra con que las dos listas se toquen.
    /// </summary>
    public static class CartaOfertaPlantilla
    {
        /// <summary>
        /// Ruta de la plantilla en el servidor. Se copia al output por la regla del csproj
        /// (<c>Features\GestionGthModule\Features\ReclutamientoFeature\Templates\**\*</c>).
        /// </summary>
        public static string RutaPlantilla => Path.Combine(
            AppContext.BaseDirectory,
            "Features", "GestionGthModule", "Features", "ReclutamientoFeature",
            "Templates", "plantilla_carta_oferta_con_placeholders.docx");

        /// <summary>
        /// es-PE y no "es" a secas: el sueldo se imprime a la peruana (1,500.00) y con la cultura de
        /// España saldría al revés (1.500,00), que en una carta de sueldo es un error de tres cifras.
        ///
        /// Los nombres de mes, en cambio, se toman de es-ES: la única diferencia entre las dos
        /// culturas es el noveno mes —es-PE dice «setiembre» y es-ES «septiembre»— y la carta que
        /// redactó GTH usa «septiembre». Las dos formas son correctas, pero el documento tiene que
        /// leerse igual que el que ellos escribieron.
        /// </summary>
        private static readonly CultureInfo Pe = CrearCulturaCarta();

        private static CultureInfo CrearCulturaCarta()
        {
            var pe = (CultureInfo)CultureInfo.GetCultureInfo("es-PE").Clone();
            var es = CultureInfo.GetCultureInfo("es-ES");

            pe.DateTimeFormat.MonthNames         = es.DateTimeFormat.MonthNames;
            pe.DateTimeFormat.MonthGenitiveNames = es.DateTimeFormat.MonthGenitiveNames;
            pe.DateTimeFormat.AbbreviatedMonthNames         = es.DateTimeFormat.AbbreviatedMonthNames;
            pe.DateTimeFormat.AbbreviatedMonthGenitiveNames = es.DateTimeFormat.AbbreviatedMonthGenitiveNames;

            return CultureInfo.ReadOnly(pe);
        }

        /// <summary>Perú no tiene horario de verano, así que el desfase es fijo.</summary>
        private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);

        /// <summary>Hoy en hora de Perú: la fecha que la carta declara como fecha de emisión.</summary>
        public static DateOnly HoyEnPeru() =>
            DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(PeruOffset).DateTime);

        public static byte[] Generar(CartaOfertaGeneracionContextoDto ctx, CartaOfertaGenerarDto datos)
        {
            var replacements = Reemplazos(ctx, datos);

            using var plantilla = File.OpenRead(RutaPlantilla);
            return WordTemplateHelper.FillTemplate(plantilla, replacements);
        }

        /// <summary>
        /// Los valores de cada marcador del documento.
        ///
        /// Dos claves van repetidas con y sin tilde ({{JEFATURA_AREA_NOMBRE}},
        /// {{FECHA_LIMITE_ACEPTACION}}) a propósito: la plantilla de hoy las lleva acentuadas, pero
        /// quien la reemplace mañana la va a retipear en Word y una tilde de menos dejaría el
        /// marcador crudo dentro de la carta, sin error y sin que nadie lo note hasta que el
        /// candidato la abra. Un marcador que la plantilla no tenga simplemente no se usa.
        /// </summary>
        private static Dictionary<string, string> Reemplazos(
            CartaOfertaGeneracionContextoDto ctx, CartaOfertaGenerarDto datos)
        {
            var jefatura   = Jefatura(ctx.AreaDestino);
            var fechaLimite = datos.FechaLimiteAceptacion?.ToString("dd/MM/yyyy") ?? "";

            return new Dictionary<string, string>
            {
                { "{{FECHA_HOY}}",              FechaLarga(HoyEnPeru(), conCero: false) },
                { "{{POSTULANTE_NOMBRE}}",      FormatoTitulo(ctx.PostulanteNombre) },
                { "{{PUESTO_NOMBRE}}",          ctx.Puesto ?? "" },
                { "{{JEFATURA_ÁREA_NOMBRE}}",   jefatura },
                { "{{JEFATURA_AREA_NOMBRE}}",   jefatura },
                // El "S/." lo pone la plantilla: acá va solo el número.
                { "{{SUELDO}}",                 datos.Sueldo?.ToString("N2", Pe) ?? "" },
                { "{{FECHA_INICIO_LABORES}}",   datos.FechaIngreso.HasValue ? FechaLarga(datos.FechaIngreso.Value, conCero: true) : "" },
                { "{{RAZON_SOCIAL}}",           (ctx.RazonSocial ?? "").ToUpper(Pe) },
                { "{{FECHA_LÍMITE_ACEPTACIÓN}}", fechaLimite },
                { "{{FECHA_LIMITE_ACEPTACION}}", fechaLimite },
            };
        }

        /// <summary>
        /// «Jefatura de Logística»: la jefatura a la que reporta el puesto, derivada del área a la
        /// que entra el contratado. Hay áreas cuyo jefe no se llama así, pero hoy no hay de dónde
        /// leer ese nombre: la regla es una sola para todas hasta que exista el dato.
        /// </summary>
        private static string Jefatura(string? area) =>
            string.IsNullOrWhiteSpace(area) ? "" : $"Jefatura de {area.Trim()}";

        /// <summary>
        /// «13 de agosto del 2026». Con <paramref name="conCero"/> el día va a dos dígitos, que es
        /// como la plantilla escribe la fecha de inicio de labores.
        /// </summary>
        private static string FechaLarga(DateOnly fecha, bool conCero) =>
            fecha.ToString(conCero ? "dd 'de' MMMM 'del' yyyy" : "d 'de' MMMM 'del' yyyy", Pe);

        /// <summary>
        /// El nombre en formato título. En <c>person</c> los nombres viven en MAYÚSCULAS y la carta
        /// es un documento formal dirigido a la persona, no un listado. Se baja a minúsculas antes
        /// porque ToTitleCase respeta las palabras que ya están en mayúscula (las toma por siglas) y
        /// devolvería el nombre igual de gritado.
        /// </summary>
        private static string FormatoTitulo(string nombre) =>
            string.IsNullOrWhiteSpace(nombre)
                ? ""
                : Pe.TextInfo.ToTitleCase(nombre.Trim().ToLower(Pe));
    }
}
