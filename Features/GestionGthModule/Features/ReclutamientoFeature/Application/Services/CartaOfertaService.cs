using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Helpers;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Shared.Correos;
using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Interfaces;
using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Services;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Email.Configuration;
using Layout = Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared.ReclutamientoEmailLayout;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    /// <inheritdoc cref="ICartaOfertaService"/>
    public class CartaOfertaService : ICartaOfertaService
    {
        private readonly ICartaOfertaRepository _repo;
        private readonly IFileDigitalColaboradorService _fileDigital;
        private readonly IEmailService _email;
        private readonly ICorreoDestinatariosResolver _destinatarios;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CartaOfertaService> _logger;

        public CartaOfertaService(
            ICartaOfertaRepository repo,
            IFileDigitalColaboradorService fileDigital,
            IEmailService email,
            ICorreoDestinatariosResolver destinatarios,
            IConfiguration configuration,
            ILogger<CartaOfertaService> logger)
        {
            _repo          = repo;
            _fileDigital   = fileDigital;
            _email         = email;
            _destinatarios = destinatarios;
            _configuration = configuration;
            _logger        = logger;
        }

        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        /// <summary>
        /// Formato aceptado para la carta oferta: PDF y nada más. El candidato la ve dentro de la
        /// intranet y la firma ahí mismo, y las dos cosas necesitan un PDF — un DOC/DOCX no se puede
        /// mostrar en el navegador ni estampar sin convertirlo, y convertirlo puede mover el formato
        /// de la carta que GTH revisó. Se corta al subir, que es donde el mensaje sirve de algo.
        /// </summary>
        private static readonly HashSet<string> AllowedCartaExt = new(StringComparer.OrdinalIgnoreCase)
            { ".pdf" };

        /// <summary>
        /// Formatos aceptados para la carta oferta FIRMADA que sube GTH a mano (la vía de respaldo,
        /// para el candidato que firma en papel). Acá no hay nada que estampar, así que se mantienen
        /// los formatos que ya se aceptaban.
        /// </summary>
        private static readonly HashSet<string> AllowedCartaFirmadaExt = new(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".doc", ".docx" };

        private const long MaxCartaBytes = 15L * 1024 * 1024; // 15 MB

        /// <summary>
        /// MIME del .docx generado, para que SharePoint lo abra en Word Online al hacer clic y no lo
        /// baje como binario suelto.
        /// </summary>
        private const string DocxMime =
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        // ── Generación desde la plantilla ──────────────────────────────────────

        public async Task<CartaOfertaAccionResultDto> Generar(
            int requerimientoId, CartaOfertaGenerarDto dto, int? userId)
        {
            if (dto == null)
                throw new AbrilException("No llegaron las condiciones de la carta oferta.", 400);

            // Las tres son obligatorias porque las tres se imprimen: una vacía deja un hueco en el
            // documento que le llega al candidato.
            if (dto.FechaIngreso == null)
                throw new AbrilException("Indica la fecha de ingreso: es la fecha de inicio de labores que dice la carta.", 400);
            if (dto.Sueldo == null || dto.Sueldo <= 0)
                throw new AbrilException("Indica el sueldo mensual que se le ofrece al candidato.", 400);
            if (dto.FechaLimiteAceptacion == null)
                throw new AbrilException("Indica hasta cuándo el candidato puede aceptar la propuesta.", 400);

            // El plazo para aceptar tiene que estar por delante: una carta que vence antes de salir
            // le pide al candidato algo imposible.
            if (dto.FechaLimiteAceptacion < CartaOfertaPlantilla.HoyEnPeru())
                throw new AbrilException("La fecha límite de aceptación ya pasó.", 400);

            if (!File.Exists(CartaOfertaPlantilla.RutaPlantilla))
                throw new AbrilException(
                    "No se encontró la plantilla de la carta oferta en el servidor. "
                    + "Contacta al administrador del sistema.", 500);

            // 1) Validar la fase y resolver los datos del documento ANTES de tocar SharePoint.
            var ctx = await _repo.PrepararGeneracion(requerimientoId);

            // 2) Armar el Word. Es puro cómputo: si algo falla acá no queda nada a medias.
            var docx = CartaOfertaPlantilla.Generar(ctx, dto);

            // 3) Dejarlo en el file del colaborador, en la misma carpeta donde después va el PDF que
            //    se envía: son el mismo documento del expediente en dos formatos.
            var carpeta = ctx.Carpeta ?? await _fileDigital.ResolverCarpetaAsync(ctx.Dni, ctx.Nombre);

            // Nombre estable a propósito (sin sello de tiempo, a diferencia del resto del file):
            // regenerar pisa el mismo archivo, así que el enlace que GTH ya tenía abierto sigue
            // sirviendo y la carpeta no se llena de borradores.
            var documento = await _fileDigital.SubirDocumentoAsync(
                carpeta, SubcarpetaFileDigital.CartaEnviada,
                $"carta_oferta_{ctx.Codigo}.docx",
                docx, DocxMime, "la carta oferta generada");

            var result = await _repo.GuardarGenerada(ctx, dto, documento, carpeta, userId);
            result.Message =
                "Carta oferta generada. Revísala —puedes corregirla en Word desde el file del "
                + "colaborador— y después envíasela: al candidato le llega en PDF.";
            return result;
        }

        public async Task<CartaOfertaAccionResultDto> Enviar(
            int requerimientoId,
            CartaOfertaEnviarDto dto,
            string? cartaFileName,
            string? cartaContentType,
            byte[]? cartaContent,
            int? userId)
        {
            // Sin archivo adjunto se manda la carta generada en el sistema. El PDF se saca recién
            // más abajo, con la fila ya validada.
            var adjunta = cartaContent != null && cartaContent.Length > 0;

            var ext = adjunta ? Path.GetExtension(cartaFileName) : ".pdf";
            if (adjunta)
            {
                if (!AllowedCartaExt.Contains(ext))
                    throw new AbrilException(
                        "La carta oferta debe ser un PDF: es el formato que el candidato puede ver y firmar desde el enlace.", 400);
                if (cartaContent!.Length > MaxCartaBytes)
                    throw new AbrilException("La carta oferta supera el tamaño máximo permitido (15 MB).", 400);
            }

            // Un correo escrito a mano se valida acá; el que sale de la base de datos ya es válido.
            var correoManual = string.IsNullOrWhiteSpace(dto?.Correo) ? null : dto!.Correo!.Trim().ToLowerInvariant();
            if (correoManual != null && !EmailRegex.IsMatch(correoManual))
                throw new AbrilException("El correo indicado para la carta oferta no es válido.", 400);

            // 1) Validar la fase, el seleccionado y su ficha de la base maestra, y resolver el correo
            //    destino ANTES de tocar SharePoint o mandar correos.
            var ctx = await _repo.PrepararEnvio(requerimientoId, dto?.FechaIngreso, correoManual, NuevoToken());

            // 1.b) Sin archivo adjunto, la carta es la que se generó acá: se pide su PDF a
            //      SharePoint. Se convierte el archivo tal como está HOY, no los bytes del momento
            //      de generarlo, para que el candidato reciba también las correcciones que GTH le
            //      haya hecho en Word.
            if (!adjunta)
            {
                if (string.IsNullOrWhiteSpace(ctx.GeneradaItemId))
                    throw new AbrilException(
                        "Todavía no hay carta oferta que enviar: genérala en el sistema o adjunta el PDF.", 400);

                cartaContent = await _fileDigital.DescargarComoPdfAsync(
                    ctx.GeneradaDriveId!, ctx.GeneradaItemId!, "la carta oferta generada");
                cartaContentType = "application/pdf";
            }

            // 2) Resolver la carpeta destino ANTES de enviar el correo. Si la biblioteca no está
            //    configurada o no se puede resolver, esto corta acá: lo contrario sería avisarle al
            //    candidato para después fallar al guardar la carta y dejarlo con un enlace que no
            //    tiene nada que mostrar. La generación ya la dejó resuelta en la fila; solo hay que
            //    ir a SharePoint cuando la carta se adjuntó sin pasar por ahí.
            var carpeta = ctx.Carpeta ?? await _fileDigital.ResolverCarpetaAsync(ctx.Dni, ctx.Nombre);

            // 3) Guardar la carta en el file del colaborador y registrar la carta oferta (lo que mueve
            //    el requerimiento a CARTA_OFERTA). Va ANTES del correo porque el correo solo lleva un
            //    enlace: si saliera primero, apuntaría a un token que todavía no existe y a una carta
            //    que todavía no está guardada. Con este orden, un correo que falla deja una carta
            //    completa de la que GTH reenvía el enlace.
            var carta = await _fileDigital.SubirDocumentoAsync(
                carpeta, SubcarpetaFileDigital.CartaEnviada,
                _fileDigital.NombreArchivo("carta_oferta", ctx.Codigo, ext),
                cartaContent!, cartaContentType ?? "application/pdf", "la carta oferta");

            var result = await _repo.Crear(ctx, carta, carpeta, userId);

            // 4) Avisarle al candidato con el enlace a su carta. La carta NO va adjunta: se ve dentro
            //    de la intranet, que es donde también la firma.
            try
            {
                await EnviarCorreoEnlaceAsync(ctx);
            }
            catch (AbrilException ex)
            {
                // El correo quedó sin destinatarios: es una decisión de Configuración, no una falla
                // del proveedor, así que se pasa el motivo tal cual en vez de esconderlo detrás de un
                // 502 genérico que invitaría a reintentar.
                _logger.LogWarning(ex,
                    "La carta oferta del requerimiento {RequerimientoId} no se envió por configuración de correos",
                    requerimientoId);
                throw new AbrilException(
                    "La carta oferta quedó guardada, pero el correo no se envió. " + ex.Message
                    + " Después reenvía el enlace desde el detalle del requerimiento.",
                    ex.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Falló el correo del enlace de la carta oferta del requerimiento {RequerimientoId}",
                    requerimientoId);
                throw new AbrilException(
                    $"La carta oferta quedó guardada, pero no se pudo enviar el correo a {ctx.Correo}. " +
                    "Reenvía el enlace desde el detalle del requerimiento.", 502);
            }

            result.Message = $"Carta oferta enviada. Se le mandó a {ctx.Correo} el enlace para revisarla y firmarla.";
            return result;
        }

        public async Task<CartaOfertaAccionResultDto> ReenviarEnlace(int requerimientoId, string? correo, int? userId)
        {
            var correoManual = string.IsNullOrWhiteSpace(correo) ? null : correo.Trim().ToLowerInvariant();
            if (correoManual != null && !EmailRegex.IsMatch(correoManual))
                throw new AbrilException("El correo indicado para el enlace de firma no es válido.", 400);

            var ctx = await _repo.PrepararReenvio(requerimientoId, correoManual, NuevoToken());

            await EnviarCorreoEnlaceAsync(ctx);

            // Recién con el correo afuera se deja registrado el envío.
            var result = await _repo.MarcarEnlaceEnviado(requerimientoId, ctx, userId);
            result.Message = $"Enlace de firma reenviado a {ctx.Correo}.";
            return result;
        }

        public async Task<CartaOfertaAccionResultDto> SubirFirmada(
            int requerimientoId, string fileName, string contentType, byte[] content, int? userId)
        {
            if (content == null || content.Length == 0)
                throw new AbrilException("Adjunta la carta oferta firmada.", 400);

            var ext = Path.GetExtension(fileName);
            if (!AllowedCartaFirmadaExt.Contains(ext))
                throw new AbrilException("La carta oferta firmada tiene un formato no permitido. Solo PDF, DOC o DOCX.", 400);
            if (content.Length > MaxCartaBytes)
                throw new AbrilException("La carta oferta firmada supera el tamaño máximo permitido (15 MB).", 400);

            var ctx = await _repo.PrepararDocumentoFirmado(requerimientoId);

            // La carta firmada va al MISMO file que la enviada, pero a su propia subcarpeta. El file
            // normalmente ya está guardado en la fila; las cartas anteriores a que se persistiera se
            // resuelven por nombre, que es exactamente como se resolvió la primera vez
            // (EnsureChildFolder devuelve la existente).
            var carpeta = ctx.Carpeta ?? await _fileDigital.ResolverCarpetaAsync(ctx.Dni, ctx.Nombre);

            var documento = await _fileDigital.SubirDocumentoAsync(
                carpeta, SubcarpetaFileDigital.CartaFirmada,
                _fileDigital.NombreArchivo("carta_oferta_firmada", ctx.Codigo, ext),
                content, contentType, "la carta oferta firmada");

            var result = await _repo.GuardarFirmada(requerimientoId, documento, carpeta, userId);
            result.Message = "Carta oferta firmada adjuntada al file digital. Queda pendiente de tu aprobación.";
            return result;
        }

        public async Task<CartaOfertaAccionResultDto> Aprobar(int requerimientoId, int? userId)
        {
            var result = await _repo.Aprobar(requerimientoId, userId);
            result.Message =
                "Carta oferta aprobada. El proceso de reclutamiento quedó cerrado y el colaborador "
                + "ya aparece en Onboarding como candidato por ingresar.";
            return result;
        }

        // ── Correo del enlace de firma ─────────────────────────────────────────

        /// <summary>
        /// Manda el correo con el enlace a la página donde el candidato ve y firma su carta oferta.
        /// Lo usan el envío y el reenvío, así que el correo que recibe es el mismo en los dos casos.
        /// Las excepciones se dejan salir: cada quien decide qué hacer con un correo que no salió (el
        /// envío ya tiene la fila creada, el reenvío no escribió nada).
        ///
        /// El destinatario principal es SIEMPRE el candidato; la pantalla de Configuración de correos
        /// de Reclutamiento solo aporta principales adicionales y copias, y puede apagarlo a él con
        /// su propio interruptor o al correo entero con el maestro. Si no queda nadie, no hay nada
        /// que reintentar: es una decisión de Configuración y se dice tal cual en vez de fallar como
        /// si fuera el proveedor de correo.
        /// </summary>
        private async Task EnviarCorreoEnlaceAsync(CartaOfertaContextoDto ctx)
        {
            var dest = await _destinatarios.ResolverAsync(CorreoTipoGth.CartaOferta);
            var (principales, copias) = CorreoDestinatariosCombinador.Combinar(ctx.Correo, dest);

            if (principales.Count == 0)
                throw new AbrilException(
                    "No hay a quién enviarle la carta oferta: revisa la sección «Carta oferta al "
                    + "colaborador» en Configuración de correos de Reclutamiento.", 409);

            await _email.SendAsync(
                to:      principales,
                subject: $"Carta oferta — {ctx.Puesto} · Abril Grupo Inmobiliario",
                body:    ConstruirCuerpoEnlaceCartaOferta(ctx, ConstruirLinkFirma(ctx.Token)),
                isHtml:  true,
                cc:      copias.Count > 0 ? copias : null,
                sender:  EmailSenders.Gth);
        }

        /// <summary>Token del enlace público (hex, url-safe). Mismo formato que el del formulario del postulante.</summary>
        private static string NuevoToken() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        /// <summary>Enlace público donde el candidato ve y firma su carta oferta.</summary>
        private string ConstruirLinkFirma(string token)
        {
            var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
            return $"{frontendUrl}/postulante/carta-oferta?token={Uri.EscapeDataString(token)}";
        }

        /// <summary>
        /// Correo con el enlace a la carta oferta, en el chrome de marca de Abril One (ver
        /// <see cref="Layout"/>). La carta NO va adjunta: el candidato entra al enlace, la lee ahí,
        /// registra su firma y la firma en la misma página. La tarjeta resume la posición para que
        /// reconozca de qué proceso se trata sin abrir nada, pero las condiciones de la propuesta
        /// solo se ven dentro del enlace, que es personal.
        ///
        /// Es uno de los correos que van a alguien de fuera, así que vale la excepción editorial del
        /// módulo: se le escribe en primera persona y se le cuenta qué tiene que hacer.
        ///
        /// Las filas en blanco se omiten: la vacante puede no tener proyecto asignado ni jefe
        /// directo, y una etiqueta con el valor vacío se lee como un error nuestro.
        /// </summary>
        private string ConstruirCuerpoEnlaceCartaOferta(CartaOfertaContextoDto ctx, string link)
        {
            var l = Layout.Desde(_configuration);

            var datos = new List<Layout.Fila>();
            void Fila(string icono, string etiqueta, string? valor)
            {
                if (!string.IsNullOrWhiteSpace(valor))
                    datos.Add(new(icono, etiqueta, Layout.Esc(valor)));
            }

            Fila("req-puesto",      "Puesto",           ctx.Puesto);
            Fila("req-area",        "Área",             ctx.Area);
            Fila("req-proyecto",    "Proyecto / obra",  ctx.ProyectoObra);
            Fila("onb-empresa",     "Empresa",          ctx.Empresa);
            Fila("req-solicitante", "Jefe directo",     ctx.JefeDirecto);
            Fila("req-fecha",       "Fecha de ingreso", ctx.FechaIngreso?.ToString("dd/MM/yyyy"));

            var nombre = string.IsNullOrWhiteSpace(ctx.Nombre) ? "colaborador(a)" : ctx.Nombre;

            return l.Documento(
                new Layout.Cabecera(
                    "onb-carta", "¡Bienvenido(a) a Abril!",
                    $"Estimado(a) {Layout.Esc(nombre)}: fuiste seleccionado(a) para la posición de "
                    + $"<b>{Layout.Esc(ctx.Puesto)}</b> en Abril Grupo Inmobiliario."),
                l.Tarjeta(datos),
                l.Franja("req-aviso", Layout.Tono.Info,
                    "Ya tienes disponible tu <b>carta oferta</b> con las condiciones de la propuesta: "
                    + "puedes leerla, registrar tu firma y firmarla en línea. No necesitas imprimir "
                    + "ni escanear nada."),
                l.Boton("Ver y firmar mi carta oferta", link),
                l.EnlaceDirecto(link),
                l.Parrafo(
                    "El enlace es personal: no lo compartas. Si tienes alguna consulta sobre la "
                    + "propuesta, respóndenos este correo y con gusto te ayudamos."),
                l.Parrafo("Atentamente,<br /><b>Equipo de Gestión del Talento Humano</b>"));
        }
    }
}
