using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Shared.Correos;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Email.Configuration;
using Layout = Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Shared.OnboardingEmailLayout;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Services
{
    /// <summary>
    /// El correo de bienvenida y el formulario «Nuevos Talentos» que lleva adentro: la primera
    /// actividad del onboarding y la única que le habla al colaborador.
    ///
    /// Los documentos normativos van ADJUNTOS y los elige GTH al enviar, con un tope duro: Graph
    /// rechaza el <c>sendMail</c> cuando el mensaje completo pasa de 4 MB y los adjuntos viajan en
    /// base64, que infla ~4/3. El paquete completo del área (manual de onboarding, RIT, reglamento
    /// SST, procedimiento de hostigamiento, anexos y cargos) pasa de 7 MB, así que no entra entero
    /// en un solo correo: por eso el tope se valida ACÁ y con un mensaje que dice cuánto pesa y
    /// cuánto entra, en vez de dejar que el proveedor devuelva un error genérico al final.
    /// </summary>
    public class OnboardingFormularioService : IOnboardingFormularioService
    {
        private readonly IOnboardingFormularioRepository _repo;
        private readonly ICorreoDestinatariosResolver _destinatarios;
        private readonly IEmailService _email;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OnboardingFormularioService> _logger;

        public OnboardingFormularioService(
            IOnboardingFormularioRepository repo,
            ICorreoDestinatariosResolver destinatarios,
            IEmailService email,
            IConfiguration configuration,
            ILogger<OnboardingFormularioService> logger)
        {
            _repo          = repo;
            _destinatarios = destinatarios;
            _email         = email;
            _configuration = configuration;
            _logger        = logger;
        }

        /// <summary>
        /// Plazo por defecto para completar el formulario y mandar la documentación, en días
        /// calendario desde el envío. GTH puede cambiarlo al mandar el correo; esto es lo que se usa
        /// cuando no elige nada. Siete días es el margen con el que el área trabaja hoy y deja
        /// tiempo para programar el EMO antes del ingreso.
        /// </summary>
        private const int DiasPlazoPorDefecto = 7;

        /// <summary>Buzón de GTH al que el colaborador manda su documentación, tal como sale en el correo.</summary>
        private const string CorreoGth = "gthnm@abril.pe";

        /// <summary>
        /// Tope de lo que puede pesar el conjunto de documentos adjuntos. No es una política
        /// nuestra sino el límite del proveedor: Graph rechaza el <c>sendMail</c> cuando el mensaje
        /// completo pasa de 4 MB y los adjuntos van en base64, que infla ~4/3. Es el mismo número
        /// que usa la long list de Reclutamiento, por el mismo motivo.
        /// </summary>
        private const long MaxAdjuntosBytes = 2_800_000; // ~2.8 MB reales ≈ 3.7 MB en base64

        /// <summary>
        /// Formatos aceptados en los documentos normativos. No se aceptan comprimidos (.zip/.rar):
        /// los filtros de correo de la organización los bloquean y el envío es bloqueante, así que
        /// tumbarían el correo entero.
        /// </summary>
        private static readonly string[] AllowedAdjuntoExt =
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".jpg", ".jpeg", ".png" };

        // ── Envío del correo de bienvenida ────────────────────────────────────

        /// <summary>
        /// Igual que el aviso al responsable de obra, este correo ES la acción: si no sale, no hay
        /// nada que dar por hecho —el colaborador no tiene su enlace— así que el error se propaga y
        /// la actividad queda pendiente para reintentar.
        /// </summary>
        public async Task<OnboardingAccionResultDto> EnviarBienvenida(
            int onboardingId, EnviarBienvenidaDto? dto, IReadOnlyList<IFormFile>? archivos, int? userId)
        {
            var fechaLimite = dto?.FechaLimite ?? FechaLimitePorDefecto();

            if (fechaLimite < HoyPeru())
                throw new AbrilException(
                    "La fecha límite no puede ser anterior a hoy: el colaborador no tendría plazo para responder.",
                    400);

            // Los adjuntos se leen y validan ANTES de tocar la base: si no entran en el correo, no
            // tiene sentido dejar el formulario abierto para un envío que va a fallar.
            var adjuntos = await LeerAdjuntosAsync(archivos);

            // Deja el formulario abierto (o recupera el que ya tenía, si es un reenvío) y trae todo
            // lo que el cuerpo del correo necesita.
            var ctx = await _repo.PrepararBienvenida(onboardingId, fechaLimite, userId);

            var configurados = await _destinatarios.ResolverAsync(CorreoTipoGth.OnbBienvenida);
            var (para, copias) = CorreoDestinatariosCombinador.Combinar(ctx.Correo, configurados);

            if (para.Count == 0)
                throw new AbrilException(
                    "Este correo no tiene destinatarios activos: revisa Onboarding → Configuración.", 409);

            var link = ConstruirLink(ctx.Token);

            await _email.SendAsync(
                to:      para,
                subject: $"¡Bienvenido(a) al equipo Abril, {PrimerNombre(ctx.Nombre)}!",
                body:    ConstruirCuerpoBienvenida(ctx, link, adjuntos),
                isHtml:  true,
                cc:      copias.Count > 0 ? copias : null,
                attachments: adjuntos.Count > 0 ? adjuntos : null,
                sender:  EmailSenders.Gth);

            var colaborador = await _repo.MarcarBienvenidaEnviada(onboardingId, para[0], userId);

            _logger.LogInformation(
                "Correo de bienvenida enviado (onboarding {OnboardingId}, {Destinatarios} destinatarios)",
                onboardingId, para.Count);

            return new OnboardingAccionResultDto
            {
                Colaborador = colaborador,
                Message     = $"Correo de bienvenida enviado a {para[0]}.",
            };
        }

        /// <summary>
        /// Cuerpo del correo: la bienvenida, las condiciones que ya se pactaron, el botón al
        /// formulario y la documentación que tiene que mandar. Es de los pocos correos del sistema
        /// que sí explica el proceso: quien lo recibe todavía no tiene ninguna pantalla del sistema
        /// donde verlo.
        /// </summary>
        /// <summary>
        /// Valida y lee los documentos que GTH adjuntó. Devuelve la lista vacía cuando no mandó
        /// ninguno: el correo sale igual, solo que sin la línea de adjuntos.
        /// </summary>
        private static async Task<List<EmailAttachment>> LeerAdjuntosAsync(IReadOnlyList<IFormFile>? archivos)
        {
            var adjuntos = new List<EmailAttachment>();
            if (archivos == null || archivos.Count == 0) return adjuntos;

            long total = 0;
            foreach (var archivo in archivos)
            {
                if (archivo.Length == 0)
                    throw new AbrilException($"El archivo «{archivo.FileName}» llegó vacío.", 400);

                var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (!AllowedAdjuntoExt.Contains(ext))
                    throw new AbrilException(
                        $"El archivo «{archivo.FileName}» no tiene un formato permitido. "
                        + "Se aceptan PDF, Word, Excel, PowerPoint e imágenes.", 400);

                total += archivo.Length;

                using var ms = new MemoryStream();
                await archivo.CopyToAsync(ms);
                adjuntos.Add(new EmailAttachment
                {
                    FileName    = Path.GetFileName(archivo.FileName),
                    ContentType = string.IsNullOrWhiteSpace(archivo.ContentType)
                                  ? "application/octet-stream" : archivo.ContentType,
                    Content     = ms.ToArray(),
                });
            }

            if (total > MaxAdjuntosBytes)
                throw new AbrilException(
                    $"Los documentos pesan {Mb(total)} en total y el correo admite hasta "
                    + $"{Mb(MaxAdjuntosBytes)}. Quita o comprime alguno antes de enviar la bienvenida.",
                    400);

            return adjuntos;
        }

        /// <summary>Tamaño en MB con un decimal, para los mensajes de error de los adjuntos.</summary>
        private static string Mb(long bytes) => $"{bytes / 1024d / 1024d:0.#} MB";

        /// <summary>
        /// «A, B y C»: los nombres de los adjuntos en prosa. El cliente de correo ya los lista, así
        /// que esta línea es para quien lee el cuerpo y no baja hasta los adjuntos.
        /// </summary>
        private static string ListaAdjuntos(IReadOnlyList<EmailAttachment> adjuntos)
        {
            var nombres = adjuntos
                .Select(a => $"<b>{Layout.Esc(Path.GetFileNameWithoutExtension(a.FileName))}</b>")
                .ToList();

            return nombres.Count == 1
                ? nombres[0]
                : string.Join(", ", nombres.Take(nombres.Count - 1)) + " y " + nombres[^1];
        }

        private string ConstruirCuerpoBienvenida(
            BienvenidaContextoDto ctx, string link, IReadOnlyList<EmailAttachment> adjuntos)
        {
            var l = Layout.Desde(_configuration);

            var datos = new List<Layout.Fila>
            {
                new("req-puesto", "Puesto", OGuion(ctx.Puesto)),
                new("req-area", "Área", OGuion(ctx.Area)),
            };

            // Es el proyecto/sede destino de la vacante: para quien entra, su lugar de trabajo.
            if (!string.IsNullOrWhiteSpace(ctx.ProyectoObra))
                datos.Add(new("req-proyecto", "Lugar de trabajo", Layout.Esc(ctx.ProyectoObra)));
            if (!string.IsNullOrWhiteSpace(ctx.Empresa))
                datos.Add(new("onb-empresa", "Razón social", Layout.Esc(ctx.Empresa)));

            datos.Add(new("req-fecha", "Fecha de ingreso",
                ctx.FechaIngreso.HasValue
                    ? Layout.Esc(ctx.FechaIngreso.Value.ToString("dd/MM/yyyy"))
                    : "Por confirmar"));

            var plazo = ctx.FechaLimite.HasValue
                ? $"Tienes hasta el <b>{Layout.Esc(FechaLarga(ctx.FechaLimite.Value))}</b> para "
                  + "completar tu formulario y enviarnos tu documentación."
                : "Completa tu formulario y envíanos tu documentación lo antes posible.";

            return l.Documento(
                new Layout.Cabecera(
                    "onb-bienvenida", "Bienvenido al Equipo",
                    $"¡Hola {Layout.Esc(PrimerNombre(ctx.Nombre))}! Estamos muy contentos de que te "
                    + "unas a <b>Abril Grupo Inmobiliario</b>."),

                l.Tarjeta(datos),

                l.Franja("req-aviso", Layout.Tono.Info, plazo),

                l.Seccion("req-formulario", "1. Completa tu registro de información"),
                l.Parrafo(
                    "En el formulario te pedimos tus datos de contacto, las condiciones de tu "
                    + "ingreso, tus tallas y la fecha en la que quieres dar tu Examen Médico "
                    + "Ocupacional (EMO) de entrada. Si ya pasaste el examen médico, no necesitas "
                    + "agendar una nueva fecha."),
                l.Boton("Completar formulario", link),
                l.EnlaceDirecto(link),

                l.Seccion("onb-documentos", "2. Envía tu documentación"),
                l.Tarjeta(new List<Layout.Fila>
                {
                    new("onb-documentos", "Bloque A",
                        "Tus <b>documentos personales</b> y los anexos de Abril, según el Manual "
                        + "de Onboarding que te compartimos."),
                    new("req-vistobueno", "Bloque B",
                        "Los <b>cargos de recepción firmados</b> de los documentos normativos: "
                        + "Reglamento y Recomendaciones de Seguridad y Salud en el Trabajo (SST), "
                        + "Procedimiento de Denuncia ante Acoso y Hostigamiento Sexual, y "
                        + "Reglamento Interno de Trabajo (RIT)."),
                    new("req-correo", "Envíalos a", Layout.Esc(CorreoGth)),
                }),

                // La línea de adjuntos solo sale si GTH adjuntó algo: prometer documentos que no
                // están es peor que no mencionarlos.
                adjuntos.Count == 0
                    ? ""
                    : l.Parrafo("Adjuntamos a este correo: " + ListaAdjuntos(adjuntos) + "."),

                l.Parrafo(
                    "Si tienes alguna duda, escríbenos y con gusto te ayudamos. ¡Nos vemos pronto!"),
                l.Parrafo("Atentamente,<br /><b>Equipo de Gestión del Talento Humano</b>"));
        }

        // ── Cara pública (el colaborador, por token) ──────────────────────────

        public async Task<OnboardingFormularioPublicoDto> GetPublico(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new AbrilException("Enlace del formulario no válido.", 400);

            return await _repo.GetPublico(token.Trim())
                ?? throw new AbrilException(
                    "El enlace del formulario no es válido o ya no está disponible.", 404);
        }

        public async Task GuardarPublico(string token, OnboardingFormularioRespuestasDto respuestas)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new AbrilException("Enlace del formulario no válido.", 400);
            if (respuestas == null)
                throw new AbrilException("No se recibieron los datos del formulario.", 400);

            // La página ya exige todo esto, pero el endpoint es anónimo: las reglas se vuelven a
            // exigir acá o no existen.
            Exigir(respuestas.Direccion,          "Indica tu dirección de domicilio actual.");
            ExigirId(respuestas.PuestoId,         "Indica el puesto al que estás ingresando.");
            if (!respuestas.FechaIngreso.HasValue)
                throw new AbrilException("Indica tu fecha de ingreso.", 400);
            if (respuestas.RemuneracionMensual is null or <= 0)
                throw new AbrilException("Indica tu remuneración mensual bruta.", 400);
            ExigirId(respuestas.UbicacionId,      "Indica tu ubicación de trabajo.");
            ExigirId(respuestas.ContributorId,    "Indica la razón social con la que ingresas.");
            ExigirId(respuestas.SexoId,           "Indica tu género.");
            Exigir(respuestas.ContactoEmergencia, "Indica tu contacto de emergencia.");
            Exigir(respuestas.CelularEmergencia,  "Indica el celular de tu contacto de emergencia.");
            if (respuestas.NumeroHijos is null or < 0)
                throw new AbrilException("Indica tu número de hijos.", 400);
            ExigirId(respuestas.TallaCalzadoId,   "Indica tu talla de botas.");
            ExigirId(respuestas.TallaId,          "Indica tu talla de blusa o camisa.");
            if (respuestas.UsaLentes == null)
                throw new AbrilException("Indica si usas lentes de medida.", 400);
            ExigirId(respuestas.RentaQuintaId,    "Indica tu situación con el certificado de renta de 5ta categoría.");
            if (!respuestas.FechaEmo.HasValue)
                throw new AbrilException("Elige la fecha de tu Examen Médico Ocupacional.", 400);

            // El EMO es de ENTRADA: se da antes de empezar a trabajar.
            if (respuestas.FechaEmo.Value > respuestas.FechaIngreso.Value)
                throw new AbrilException(
                    "La fecha del EMO debe ser anterior a tu fecha de ingreso.", 400);

            if (respuestas.DeclaracionVeracidad != true)
                throw new AbrilException(
                    "Debes declarar que la información consignada es veraz para enviar el formulario.", 400);

            var nombre = await _repo.GuardarPublico(token.Trim(), respuestas);

            _logger.LogInformation("Formulario de onboarding completado por {Nombre}", nombre);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private string ConstruirLink(string token)
        {
            var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
            return $"{frontendUrl}/colaborador/formulario?token={Uri.EscapeDataString(token)}";
        }

        /// <summary>Hoy en el calendario de Perú (UTC-5), no en el del servidor.</summary>
        private static DateOnly HoyPeru() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

        private static DateOnly FechaLimitePorDefecto() => HoyPeru().AddDays(DiasPlazoPorDefecto);

        /// <summary>«jueves 03 de septiembre de 2026»: la forma en que GTH escribe los plazos.</summary>
        private static string FechaLarga(DateOnly fecha) =>
            fecha.ToDateTime(TimeOnly.MinValue)
                 .ToString("dddd dd 'de' MMMM 'de' yyyy",
                           System.Globalization.CultureInfo.GetCultureInfo("es-PE"));

        /// <summary>
        /// Con qué nombre se le saluda. Las fichas guardan «APELLIDO APELLIDO, Nombre Nombre» o
        /// «Nombre Apellido»: se toma la primera palabra después de la coma cuando la hay, y la
        /// primera de todas cuando no.
        /// </summary>
        private static string PrimerNombre(string? nombreCompleto)
        {
            var nombre = (nombreCompleto ?? "").Trim();
            if (nombre.Length == 0) return "colaborador";

            var coma = nombre.IndexOf(',');
            if (coma >= 0 && coma < nombre.Length - 1) nombre = nombre[(coma + 1)..].Trim();

            var primera = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(primera)) return "colaborador";

            // Las fichas vienen en MAYÚSCULAS: gritarle el nombre en el saludo no queda.
            return char.ToUpperInvariant(primera[0]) + primera[1..].ToLowerInvariant();
        }

        /// <summary>Guion cuando el dato no está: nunca se deja una fila de la tarjeta en blanco.</summary>
        private static string OGuion(string? valor) =>
            string.IsNullOrWhiteSpace(valor) ? "—" : Layout.Esc(valor);

        private static void Exigir(string? valor, string mensaje)
        {
            if (string.IsNullOrWhiteSpace(valor)) throw new AbrilException(mensaje, 400);
        }

        private static void ExigirId(int? valor, string mensaje)
        {
            if (valor is null or <= 0) throw new AbrilException(mensaje, 400);
        }
    }
}
