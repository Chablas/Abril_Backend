using System.Text.RegularExpressions;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Services
{
    public class OnboardingService : IOnboardingService
    {
        private readonly IOnboardingRepository _repo;
        private readonly IGraphSharePointService _sharePoint;
        private readonly IEmailService _email;
        private readonly ILogger<OnboardingService> _logger;

        public OnboardingService(
            IOnboardingRepository repo,
            IGraphSharePointService sharePoint,
            IEmailService email,
            ILogger<OnboardingService> logger)
        {
            _repo       = repo;
            _sharePoint = sharePoint;
            _email      = email;
            _logger     = logger;
        }

        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        /// <summary>Formatos aceptados para la carta oferta (los mismos que el resto de adjuntos del módulo).</summary>
        private static readonly HashSet<string> AllowedCartaExt = new(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".doc", ".docx" };

        private const long MaxCartaBytes = 15L * 1024 * 1024; // 15 MB

        public Task<BandejaOnboardingDto> GetBandeja() => _repo.GetBandeja();

        public async Task<OnboardingCreateResultDto> Iniciar(
            OnboardingCreateDto dto,
            string cartaFileName,
            string cartaContentType,
            byte[] cartaContent,
            int? userId)
        {
            if (dto == null || dto.CandidatoId <= 0)
                throw new AbrilException("Selecciona al colaborador que inicia el onboarding.", 400);

            if (cartaContent == null || cartaContent.Length == 0)
                throw new AbrilException("Adjunta la carta oferta para poder enviarla.", 400);

            var ext = Path.GetExtension(cartaFileName);
            if (!AllowedCartaExt.Contains(ext))
                throw new AbrilException("La carta oferta tiene un formato no permitido. Solo PDF, DOC o DOCX.", 400);
            if (cartaContent.Length > MaxCartaBytes)
                throw new AbrilException("La carta oferta supera el tamaño máximo permitido (15 MB).", 400);

            // Un correo escrito a mano se valida acá; el que sale de la base de datos ya es válido.
            var correoManual = string.IsNullOrWhiteSpace(dto.Correo) ? null : dto.Correo.Trim().ToLowerInvariant();
            if (correoManual != null && !EmailRegex.IsMatch(correoManual))
                throw new AbrilException("El correo indicado para la carta oferta no es válido.", 400);

            // 1) Validar que el candidato pueda entrar a onboarding y resolver el correo destino
            //    ANTES de tocar SharePoint o mandar correos.
            var ctx = await _repo.PrepararInicio(dto.CandidatoId, dto.FechaIngreso, correoManual);

            // 2) Resolver la carpeta destino de la carta ANTES de enviar el correo. Si la biblioteca
            //    no está configurada o no se puede resolver, esto corta acá: lo contrario sería
            //    mandarle la carta al colaborador para después fallar al guardarla y no dejar
            //    onboarding registrado, obligando a reenviarla.
            var carpeta = await ResolverCarpetaOnboardingAsync(ctx.Codigo, ctx.Nombre);

            // 3) Enviar la carta oferta. Va antes de persistir a propósito: mientras la carta no
            //    salga, el colaborador no está en onboarding y GTH puede reintentar sin arrastrar una
            //    fila a medias. Mismo criterio que el envío de la long list en Reclutamiento.
            try
            {
                await _email.SendAsync(
                    to:      new List<string> { ctx.Correo },
                    subject: $"Carta oferta — {ctx.Puesto} · Abril Grupo Inmobiliario",
                    body:    ConstruirCuerpoCartaOferta(ctx),
                    isHtml:  true,
                    attachments: new List<EmailAttachment>
                    {
                        new()
                        {
                            FileName    = cartaFileName,
                            ContentType = string.IsNullOrWhiteSpace(cartaContentType)
                                ? "application/octet-stream" : cartaContentType,
                            Content     = cartaContent,
                        },
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló el correo de la carta oferta del candidato {CandidatoId}", dto.CandidatoId);
                throw new AbrilException(
                    "No se pudo enviar la carta oferta al correo del colaborador. El onboarding no se inició; reintenta.", 502);
            }

            // 4) Carta enviada: guardarla en SharePoint (queda en el file del colaborador) y recién
            //    ahí registrar el onboarding.
            var carta = await SubirCartaOfertaAsync(carpeta, ctx, cartaFileName, cartaContent, cartaContentType);

            var colaborador = await _repo.Crear(ctx, carta, dto.Observacion, userId);

            return new OnboardingCreateResultDto
            {
                OnboardingId = colaborador.OnboardingId,
                Colaborador  = colaborador,
                Message      = $"Onboarding iniciado. Carta oferta enviada a {ctx.Correo}.",
            };
        }

        /// <summary>
        /// Sube la carta oferta a la <paramref name="carpeta"/> ya resuelta (la subcarpeta del
        /// colaborador dentro de la biblioteca de cartas oferta), para que quede armado su file
        /// digital. La fila del onboarding se queda con el driveId/itemId/webUrl de ESTA subida: si
        /// mañana se cambia la biblioteca configurada, esta carta se sigue abriendo desde acá.
        /// </summary>
        private async Task<CartaOfertaPersistDto> SubirCartaOfertaAsync(
            ShareLinkResolveDto carpeta,
            OnboardingContextoDto ctx, string origFileName, byte[] content, string contentType)
        {
            var ext      = Path.GetExtension(origFileName);
            var stamp    = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var filename = $"carta_oferta_{SanitizeFilename(ctx.Codigo)}_{stamp}{ext}";

            try
            {
                using var stream = new MemoryStream(content);
                var result = await _sharePoint.UploadToOneDriveFolderAsync(
                    carpeta.DriveId, carpeta.ItemId, filename,
                    stream, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                    autoRenameOnLock: true);

                if (result?.WebUrl == null)
                    throw new AbrilException("No se pudo subir la carta oferta a SharePoint.", 502);

                return new CartaOfertaPersistDto
                {
                    Nombre  = result.FileName ?? filename,
                    Url     = result.WebUrl,
                    ItemId  = result.ItemId,
                    DriveId = carpeta.DriveId,
                };
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la subida de la carta oferta del requerimiento {Codigo}", ctx.Codigo);
                throw new AbrilException("Error al subir la carta oferta a SharePoint.", 502);
            }
        }

        /// <summary>
        /// Resuelve la biblioteca de cartas oferta (link en <c>gth_carta_oferta_folder</c>) y, dentro,
        /// la subcarpeta del colaborador. Si no se puede crear la subcarpeta se cae a la raíz: el
        /// nombre del archivo ya lleva el código del requerimiento, así que no colisiona.
        ///
        /// El link se lee de la BD en cada envío, así que cambiar esa fila redirige las cartas nuevas
        /// sin redeploy y sin tocar las anteriores.
        /// </summary>
        private async Task<ShareLinkResolveDto> ResolverCarpetaOnboardingAsync(string codigo, string nombre)
        {
            var folderUrl = await _repo.GetCartaOfertaFolderUrl();
            if (string.IsNullOrWhiteSpace(folderUrl))
                throw new AbrilException(
                    "No está configurada la carpeta de SharePoint donde se guardan las cartas oferta.", 500);

            var raiz = await _sharePoint.ResolveSharePointFolderUrlAsync(folderUrl);
            if (raiz == null || !raiz.IsFolder)
                throw new AbrilException(
                    "No se pudo resolver en SharePoint la carpeta configurada para las cartas oferta. Revisa que el link apunte a una carpeta existente y accesible.", 502);

            try
            {
                var subItemId = await _sharePoint.EnsureChildFolderAsync(
                    raiz.DriveId, raiz.ItemId,
                    $"Onboarding {SanitizeFilename(codigo)} - {SanitizeFilename(nombre)}");
                return new ShareLinkResolveDto { DriveId = raiz.DriveId, ItemId = subItemId, IsFolder = true };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "No se pudo crear la subcarpeta de onboarding de {Codigo}; se usa la carpeta raíz", codigo);
                return raiz;
            }
        }

        /// <summary>
        /// Correo de la carta oferta al nuevo colaborador. La carta va adjunta; el cuerpo resume la
        /// posición y le pide devolverla firmada, que es justamente la primera fase del onboarding.
        /// </summary>
        private static string ConstruirCuerpoCartaOferta(OnboardingContextoDto ctx)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            static string Fila(string etiqueta, string? valor) =>
                string.IsNullOrWhiteSpace(valor)
                    ? string.Empty
                    : $"""
                        <tr>
                          <td style="padding:6px 10px;font-size:12px;color:#6b7280;white-space:nowrap">{Esc(etiqueta)}</td>
                          <td style="padding:6px 10px;font-size:13px;color:#1f2937"><b>{Esc(valor)}</b></td>
                        </tr>
                        """;

            var nombre = string.IsNullOrWhiteSpace(ctx.Nombre) ? "colaborador(a)" : Esc(ctx.Nombre);

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:640px">
                  <div style="background:#005D9D;padding:14px 18px">
                    <h2 style="color:#fff;margin:0;font-size:18px">¡Bienvenido(a) a Abril Grupo Inmobiliario!</h2>
                  </div>
                  <div style="padding:18px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0">Estimado(a) {nombre},</p>
                    <p style="font-size:13px">
                      Nos complace informarte que fuiste seleccionado(a) para la posición de
                      <b>{Esc(ctx.Puesto)}</b> en <b>Abril Grupo Inmobiliario</b>. Adjunto a este correo
                      encontrarás tu <b>carta oferta</b> con las condiciones de la propuesta.
                    </p>
                    <table style="border-collapse:collapse;margin:14px 0;background:#f9fafb;border:1px solid #e5e7eb;border-radius:8px">
                      {Fila("Puesto", ctx.Puesto)}
                      {Fila("Área", ctx.Area)}
                      {Fila("Proyecto / obra", ctx.ProyectoObra)}
                      {Fila("Empresa", ctx.Empresa)}
                      {Fila("Jefe directo", ctx.JefeDirecto)}
                      {Fila("Fecha de ingreso", ctx.FechaIngreso?.ToString("dd/MM/yyyy"))}
                    </table>
                    <p style="font-size:13px">
                      Para continuar con tu proceso de incorporación, <b>revisa la carta, fírmala y
                      respóndenos este correo con el documento firmado</b>. Con eso avanzamos a la
                      entrega de tu file digital y al resto de tu onboarding.
                    </p>
                    <p style="font-size:12.5px;color:#555">
                      Si tienes alguna consulta sobre la propuesta, respóndenos este correo y el equipo
                      de Gestión de Talento Humano te apoyará.
                    </p>
                    <p style="font-size:11px;color:#888;margin-top:18px">Correo automático de Abril One · Gestión GTH · Onboarding.</p>
                  </div>
                </div>
                """;
        }

        /// <summary>Deja el texto usable como nombre de archivo/carpeta en SharePoint.</summary>
        private static string SanitizeFilename(string value)
        {
            var limpio = new string((value ?? string.Empty)
                .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch == '#' || ch == '%' ? '_' : ch)
                .ToArray())
                .Trim();
            return string.IsNullOrWhiteSpace(limpio) ? "sin_nombre" : limpio;
        }
    }
}
