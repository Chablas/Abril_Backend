using System.Globalization;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Shared.Helpers;
using Humanizer;

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

        /// <summary>
        /// El marcador de las condiciones de contrato. Es el único que se expande a N párrafos: cada
        /// condición que escribió GTH sale como una viñeta propia, heredando la numeración y la
        /// fuente del párrafo que la plantilla tiene en su lugar.
        /// </summary>
        private const string Condiciones = "{{CONDICIONES}}";

        /// <summary>
        /// La fecha de conformidad del formato de aceptación. Es el ÚNICO marcador que
        /// <see cref="Generar"/> deja sin resolver a propósito: su valor es el día en que el
        /// colaborador abre su enlace por primera vez, que cuando GTH arma el documento todavía no ha
        /// pasado. Lo rellena <see cref="RellenarConformidad"/> en ese momento, sobre el mismo .docx.
        ///
        /// A diferencia de los demás no lleva variante sin tilde: no tiene ninguna.
        /// </summary>
        private const string ClaveConformidad = "{{FECHA_HOY_CONFORMIDAD_DE_COLABORADOR}}";

        /// <summary>
        /// Rellena la fecha de conformidad sobre el .docx YA GENERADO —el que está en el file del
        /// colaborador, con las correcciones que GTH le haya hecho en Word— y devuelve los bytes
        /// nuevos. Se llama una sola vez, cuando el colaborador abre su enlace por primera vez.
        ///
        /// No vuelve a resolver ningún otro marcador: a esta altura ya no queda ninguno, y rearmar el
        /// documento desde la plantilla borraría esas correcciones de GTH.
        /// </summary>
        public static byte[] RellenarConformidad(byte[] docx, DateOnly fecha)
        {
            var valor = fecha.ToString("dd/MM/yyyy");
            var replacements = new Dictionary<string, string> { { ClaveConformidad, valor } };

            using var origen = new MemoryStream(docx, writable: false);
            return WordTemplateHelper.FillTemplate(origen, replacements);
        }

        public static byte[] Generar(CartaOfertaGeneracionContextoDto ctx, CartaOfertaGenerarDto datos)
        {
            var replacements = Reemplazos(ctx, datos);

            var condiciones = new Dictionary<string, List<string>>
            {
                { Condiciones, datos.Condiciones },
            };

            using var plantilla = File.OpenRead(RutaPlantilla);
            return WordTemplateHelper.FillTemplate(
                plantilla, replacements, condiciones,
                // Sin la línea en blanco que el helper intercala por defecto: las condiciones son una
                // lista corta y seguida, no las cláusulas separadas de un contrato.
                compactMultiParagraphPlaceholders: new HashSet<string> { Condiciones });
        }

        /// <summary>
        /// Los valores de cada marcador del documento.
        ///
        /// Varias claves van repetidas con y sin tilde ({{JEFATURA_AREA_NOMBRE}},
        /// {{FECHA_LIMITE_ACEPTACION}}, {{SUELDO_EN_NUMERO}}, {{PROYECTO_UBICACION}}) a propósito: la
        /// plantilla de hoy las lleva acentuadas, pero quien la reemplace mañana la va a retipear en
        /// Word y una tilde de menos dejaría el marcador crudo dentro de la carta, sin error y sin
        /// que nadie lo note hasta que el candidato la abra. Un marcador que la plantilla no tenga
        /// simplemente no se usa.
        ///
        /// La razón social va tal como está en el catálogo de contribuyentes («Bahia de oro
        /// Inmobiliaria S.A.C»), no en mayúsculas: la plantilla anterior la usaba solo dentro de una
        /// cláusula («bajo la razón social X») y gritada se leía como un dato de formulario; la
        /// nueva la mete en frases corridas del cuerpo y del formato de aceptación.
        /// </summary>
        private static Dictionary<string, string> Reemplazos(
            CartaOfertaGeneracionContextoDto ctx, CartaOfertaGenerarDto datos)
        {
            var hoy         = HoyEnPeru();
            var jefatura    = Jefatura(ctx.AreaDestino);
            var fechaLimite = datos.FechaLimiteAceptacion?.ToString("dd/MM/yyyy") ?? "";

            var inicioLargo = datos.FechaIngreso.HasValue ? FechaLarga(datos.FechaIngreso.Value, conCero: true) : "";
            var inicioCorto = datos.FechaIngreso?.ToString("dd/MM/yyyy") ?? "";

            var sueldoNumero = datos.Sueldo?.ToString("N2", Pe) ?? "";
            var sueldoLetras = datos.Sueldo.HasValue ? EnLetras(datos.Sueldo.Value) : "";

            var ubicacion = ctx.ProyectoUbicacion ?? "";

            // La fecha que encabeza la carta («Lima, 31 de agosto del 2026»): el día en que se armó
            // el documento. Se llamaba {{FECHA_HOY}} hasta que GTH separó las dos fechas del
            // documento por nombre; la clave vieja se mantiene por si vuelve a aparecer una
            // plantilla anterior.
            var fechaEnvio = FechaLarga(hoy, conCero: false);

            return new Dictionary<string, string>
            {
                { "{{FECHA_HOY_ENVÍO_CORREO}}", fechaEnvio },
                { "{{FECHA_HOY_ENVIO_CORREO}}", fechaEnvio },
                { "{{FECHA_HOY}}",              fechaEnvio },
                { "{{POSTULANTE_NOMBRE}}",      FormatoTitulo(ctx.PostulanteNombre) },
                { "{{PUESTO_NOMBRE}}",          ctx.Puesto ?? "" },
                { "{{JEFATURA_ÁREA_NOMBRE}}",   jefatura },
                { "{{JEFATURA_AREA_NOMBRE}}",   jefatura },
                { "{{FECHA_INICIO_LABORES}}",   inicioLargo },
                { "{{RAZON_SOCIAL}}",           ctx.RazonSocial ?? "" },
                { "{{FECHA_LÍMITE_ACEPTACIÓN}}", fechaLimite },
                { "{{FECHA_LIMITE_ACEPTACION}}", fechaLimite },

                // El "S/." y la palabra "soles" los pone la plantilla; acá van solo el número y el
                // monto escrito. Se mantiene {{SUELDO}} —el nombre que usaba la plantilla anterior—
                // porque no cuesta nada y evita que una carta vieja reimpresa salga con el marcador
                // crudo si alguien vuelve a poner ese documento como plantilla.
                { "{{SUELDO_EN_NÚMERO}}",       sueldoNumero },
                { "{{SUELDO_EN_NUMERO}}",       sueldoNumero },
                { "{{SUELDO}}",                 sueldoNumero },
                { "{{SUELDO_EN_LETRAS}}",       sueldoLetras },

                // Ubicación de trabajo. Se imprime lo que haya cargado el proyecto: un dato en
                // blanco deja el hueco a la vista en el .docx, que GTH revisa antes de enviarlo.
                { "{{PROYECTO_NOMBRE}}",        ctx.ProyectoNombre ?? "" },
                { "{{PROYECTO_UBICACIÓN}}",     ubicacion },
                { "{{PROYECTO_UBICACION}}",     ubicacion },
                { "{{PROYECTO_DISTRITO}}",      ctx.ProyectoDistrito ?? "" },
                { "{{PROYECTO_PROVINCIA}}",     ctx.ProyectoProvincia ?? "" },
                { "{{PROYECTO_DEPARTAMENTO}}",  ctx.ProyectoDepartamento ?? "" },

                // El formato de aceptación del pie escribe las dos fechas en dd/MM/yyyy, mientras
                // que el cuerpo de la carta las escribe largas. Son los mismos dos datos con otro
                // formato, así que llevan marcador propio: un placeholder solo puede tener un valor.
                { "{{FECHA_HOY_CORTA}}",             hoy.ToString("dd/MM/yyyy") },
                { "{{FECHA_INICIO_LABORES_CORTA}}",  inicioCorto },
            };
        }

        /// <summary>
        /// El sueldo escrito, como lo pide la plantilla: «Mil ciento treinta con 00/100» (la palabra
        /// «soles» la pone el documento). Va en formato oración y no en mayúsculas —a diferencia de
        /// los montos de las órdenes de compra— porque acá cae en medio de un párrafo corrido.
        /// </summary>
        private static string EnLetras(decimal monto)
        {
            var abs      = Math.Abs(monto);
            var entero   = (long)Math.Truncate(abs);
            var centavos = (int)Math.Round((abs - entero) * 100m);
            if (centavos == 100) { entero++; centavos = 0; }

            var palabras = entero.ToWords(CultureInfo.GetCultureInfo("es"));
            return $"{Capitalizar(palabras)} con {centavos:D2}/100";
        }

        /// <summary>Primera letra en mayúscula y el resto intacto (no es ToTitleCase).</summary>
        private static string Capitalizar(string texto) =>
            string.IsNullOrEmpty(texto) ? texto : char.ToUpper(texto[0], Pe) + texto[1..];

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
