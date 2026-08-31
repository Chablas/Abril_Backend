using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Shared.Correos;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Helpers;
using Abril_Backend.Shared.Services.Email.Configuration;
using Abril_Backend.Shared.Services.Pdf;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Layout = Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Shared.OnboardingEmailLayout;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Services
{
    /// <inheritdoc cref="ICartaOfertaFirmaService"/>
    public class CartaOfertaFirmaService : ICartaOfertaFirmaService
    {
        private readonly ICartaOfertaFirmaRepository _repo;
        private readonly IFileDigitalColaboradorService _fileDigital;
        private readonly IGraphSharePointService _sharePoint;
        private readonly IEmailService _email;
        private readonly ICorreoDestinatariosResolver _destinatarios;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CartaOfertaFirmaService> _logger;

        public CartaOfertaFirmaService(
            ICartaOfertaFirmaRepository repo,
            IFileDigitalColaboradorService fileDigital,
            IGraphSharePointService sharePoint,
            IEmailService email,
            ICorreoDestinatariosResolver destinatarios,
            IConfiguration configuration,
            ILogger<CartaOfertaFirmaService> logger)
        {
            _repo          = repo;
            _fileDigital   = fileDigital;
            _sharePoint    = sharePoint;
            _email         = email;
            _destinatarios = destinatarios;
            _configuration = configuration;
            _logger        = logger;
        }

        public Task<CartaOfertaFirmaPublicoDto> GetPublico(string token) =>
            _repo.GetPublicoByToken(ExigirToken(token));

        public async Task<(byte[] Content, string ContentType, string FileName)> GetDocumento(string token)
        {
            var ctx = await _repo.PrepararPorToken(ExigirToken(token));

            // Si ya firmó, el visor muestra el documento FIRMADO: es el que a él le importa revisar y
            // descargar, y ver el original después de firmar solo confunde.
            var (content, nombreOrigen) = ctx.YaFirmada
                ? (await DescargarAsync(ctx.CartaFirmadaDriveId, ctx.CartaFirmadaItemId, ctx.CartaFirmadaUrl),
                   ctx.CartaFirmadaNombre)
                : (await DescargarAsync(ctx.CartaOfertaDriveId, ctx.CartaOfertaItemId, ctx.CartaOfertaUrl),
                   ctx.CartaOfertaNombre);

            // La carta que sube GTH para firmar es siempre PDF, y lo que firma el postulante también.
            // Pero la carta firmada que GTH puede adjuntar a mano (la vía de respaldo) admite además
            // DOC/DOCX, así que el tipo se deriva de la extensión real en vez de asumir PDF: servir un
            // .docx como application/pdf le rompe la descarga al postulante.
            var extension = Path.GetExtension(nombreOrigen ?? string.Empty);
            var (contentType, extensionSalida) = TipoContenido(extension);

            // Nombre neutro: el de SharePoint lleva el código del requerimiento y un sello de tiempo,
            // que no le dicen nada al postulante.
            var fileName = (ctx.YaFirmada ? "carta-oferta-firmada" : "carta-oferta") + extensionSalida;

            return (content, contentType, fileName);
        }

        /// <summary>Tipo MIME y extensión con los que se sirve el documento, según su extensión real.</summary>
        private static (string ContentType, string Extension) TipoContenido(string extension) =>
            extension.ToLowerInvariant() switch
            {
                ".doc"  => ("application/msword", ".doc"),
                ".docx" => ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx"),
                _       => ("application/pdf", ".pdf"),
            };

        public async Task<CartaOfertaFirmaGuardarResultDto> GuardarFirma(string token, CartaOfertaFirmaGuardarDto dto)
        {
            var ctx = await _repo.PrepararPorToken(ExigirToken(token));

            // Una vez aprobada, la firma que quedó estampada es la definitiva: cambiarla en la ficha
            // dejaría el documento aprobado firmado con una imagen que ya no es la registrada.
            if (ctx.Aprobada)
                throw new AbrilException(
                    "Tu carta oferta ya fue revisada y aprobada por Gestión de Talento Humano: la firma ya no se puede cambiar.", 409);

            // Mismas reglas que la firma del Gerente General en Contabilidad: las dos van a las mismas
            // columnas de la ficha y las dos se estampan con el mismo helper de PDF.
            var bytes = FirmaImagenHelper.DecodePng(dto?.ImageBase64);

            return await _repo.GuardarFirma(ctx.PersonId, bytes, FirmaImagenHelper.Mime);
        }

        public async Task<CartaOfertaFirmarResultDto> Firmar(string token)
        {
            var ctx = await _repo.PrepararPorToken(ExigirToken(token));

            if (ctx.Aprobada)
                throw new AbrilException(
                    "Tu carta oferta ya fue revisada y aprobada por Gestión de Talento Humano: el proceso de firma está cerrado.", 409);

            // Ya firmada pero sin aprobar: se permite volver a firmar (por ejemplo si rehízo su firma
            // porque la anterior salió mal). El documento nuevo reemplaza al anterior y la revisión de
            // GTH vuelve a quedar pendiente.
            var firma = await _repo.GetFirmaBytes(ctx.PersonId)
                ?? throw new AbrilException(
                    "Primero registra tu firma en esta página y después presiona «Firmar».", 409);

            byte[] original;
            try
            {
                original = await DescargarAsync(ctx.CartaOfertaDriveId, ctx.CartaOfertaItemId, ctx.CartaOfertaUrl);
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CARTA OFERTA FIRMA · falló la descarga del original (onboarding {OnboardingId})", ctx.OnboardingId);
                throw new AbrilException(
                    "No pudimos abrir tu carta oferta para firmarla. Inténtalo de nuevo en unos minutos.", 502);
            }

            byte[] firmado;
            try
            {
                // Última página: es donde va la línea de firma de la carta oferta. (En una factura la
                // firma del Gerente General se estampa en todas, que es el otro uso del mismo helper.)
                firmado = SignaturePdfStamper.Stamp(original, firma.Bytes, SignatureStampScope.LastPage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CARTA OFERTA FIRMA · falló el estampado (onboarding {OnboardingId}, bytes={Bytes}, firma={Firma})",
                    ctx.OnboardingId, original.Length, firma.Bytes.Length);
                throw new AbrilException(
                    "No pudimos estampar tu firma en el documento. Escríbele a Gestión de Talento Humano para que lo revise.", 502);
            }

            // El documento firmado va a la misma carpeta donde GTH dejaría la carta firmada si la
            // subiera a mano, para que el expediente se lea igual sin importar por dónde entró.
            var carpeta = ctx.Carpeta ?? await _fileDigital.ResolverCarpetaAsync(ctx.Dni, ctx.Nombre);

            var carta = await _fileDigital.SubirDocumentoAsync(
                carpeta, SubcarpetaFileDigital.CartaFirmada,
                _fileDigital.NombreArchivo("carta_oferta_firmada", ctx.Codigo, ".pdf"),
                firmado, "application/pdf", "tu carta oferta firmada");

            var firmadaEn = await _repo.GuardarCartaFirmadaPorPostulante(ctx.OnboardingId, carta, carpeta);

            // Aviso a GTH de que ya hay una carta firmada esperando su revisión. Best-effort a
            // propósito: el colaborador ya firmó y el documento ya está en su file digital, así que
            // no puede ver un error —ni verse empujado a firmar de nuevo, que reemplazaría el
            // documento— porque un correo interno no salió.
            try
            {
                await EnviarAvisoCartaFirmadaAsync(ctx, firmadaEn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CARTA OFERTA FIRMA · no se pudo avisar a GTH de la firma (onboarding {OnboardingId})",
                    ctx.OnboardingId);
            }

            return new CartaOfertaFirmarResultDto
            {
                Message   = "¡Listo! Tu carta oferta quedó firmada y enviada a Gestión de Talento Humano.",
                FirmadaEn = firmadaEn,
            };
        }

        // ── Aviso a GTH ────────────────────────────────────────────────────────

        /// <summary>
        /// Le avisa a GTH que el colaborador firmó su carta oferta, para que entre a revisarla: es
        /// la primera actividad obligatoria del checklist y hasta que no la apruebe el onboarding no
        /// avanza de fase. Nadie de la empresa dispara este correo —lo dispara el colaborador al
        /// firmar—, así que sin él la carta firmada se quedaría esperando a que alguien pase por la
        /// bandeja.
        ///
        /// A quién le llega sale entero de Configuración
        /// (<c>/gestion-gth/onboarding/configuracion</c>): no hay destinatario principal automático,
        /// igual que en el aviso de «formulario completado» de Reclutamiento. Sin destinatarios
        /// activos no se envía nada, que es lo que esa pantalla quiso decir al apagarlos.
        /// </summary>
        private async Task EnviarAvisoCartaFirmadaAsync(CartaOfertaFirmaContextoDto ctx, DateTime firmadaEn)
        {
            var dest = await _destinatarios.ResolverAsync(CorreoTipoGth.CartaOfertaFirmada);
            var para = dest.EmailsPara;
            if (para.Count == 0) return;

            var copias = dest.EmailsCopias;

            await _email.SendAsync(
                to:      para,
                subject: $"[Onboarding] {ctx.NombreColaborador} firmó su carta oferta — {ctx.Codigo} · {ctx.Puesto}",
                body:    ConstruirCuerpoAvisoCartaFirmada(ctx, firmadaEn),
                isHtml:  true,
                cc:      copias.Count > 0 ? copias : null,
                // Sale de aprobaciones@abril.pe y no del buzón de GTH: es un aviso INTERNO que llega
                // justamente al buzón de GTH, y mandarlo desde ese mismo buzón lo dejaría como un
                // correo que el área se escribe a sí misma. El de la carta oferta, que va al
                // colaborador, sí sale de GTH.
                sender:  EmailSenders.Aprobaciones);
        }

        /// <summary>
        /// Enlace al colaborador dentro de la bandeja de Onboarding, con su detalle ya abierto: es
        /// donde se revisa y se aprueba la carta firmada. Mismo mecanismo que el resto de correos
        /// internos del módulo: sin sesión, el <c>authGuard</c> del frontend manda al login con esta
        /// URL como <c>returnUrl</c> y lo devuelve acá al entrar.
        /// </summary>
        private string ConstruirLinkDetalleColaborador(int onboardingId)
        {
            var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
            return $"{frontendUrl}/gestion-gth/onboarding/colaborador/{onboardingId}";
        }

        /// <summary>
        /// Cuerpo del aviso, en el chrome de marca de Abril One (ver <see cref="Layout"/>). Es un
        /// correo INTERNO, así que no lleva la primera persona ni las explicaciones que sí puede
        /// llevar el de la carta oferta: la tarjeta trae los datos del ingreso y el botón lleva a
        /// donde se aprueba. El documento firmado tampoco va adjunto — se ve en el detalle, junto
        /// al botón de aprobarlo.
        ///
        /// Las filas en blanco se omiten, igual que en el correo de la carta oferta: el onboarding
        /// se puede abrir con la ficha a medio llenar y una etiqueta con el valor vacío se lee como
        /// un error nuestro.
        /// </summary>
        private string ConstruirCuerpoAvisoCartaFirmada(CartaOfertaFirmaContextoDto ctx, DateTime firmadaEn)
        {
            var l = Layout.Desde(_configuration);

            var datos = new List<Layout.Fila>();
            void Fila(string icono, string etiqueta, string? valor)
            {
                if (!string.IsNullOrWhiteSpace(valor))
                    datos.Add(new(icono, etiqueta, Layout.Esc(valor)));
            }

            Fila("req-codigo",      "Requerimiento",    ctx.Codigo);
            Fila("req-candidato",   "Colaborador",      ctx.NombreColaborador);
            Fila("req-puesto",      "Puesto",           ctx.Puesto);
            Fila("req-area",        "Área",             ctx.Area);
            Fila("req-proyecto",    "Proyecto / obra",  ctx.ProyectoObra);
            Fila("onb-empresa",     "Empresa",          ctx.Empresa);
            Fila("req-solicitante", "Jefe directo",     ctx.JefeDirecto);
            Fila("req-correo",      "Correo",           ctx.Correo);
            Fila("req-fecha",       "Fecha de ingreso", ctx.FechaIngreso?.ToString("dd/MM/yyyy"));
            Fila("req-hora",        "Firmada el",       firmadaEn.ToString("dd/MM/yyyy HH:mm"));

            var nombre = string.IsNullOrWhiteSpace(ctx.NombreColaborador)
                ? "El colaborador" : Layout.Esc(ctx.NombreColaborador);

            var link = ConstruirLinkDetalleColaborador(ctx.OnboardingId);

            return l.Documento(
                new Layout.Cabecera(
                    "onb-carta", "Carta Oferta Firmada",
                    $"<b>{nombre}</b> firmó su carta oferta desde el enlace."),
                l.Tarjeta(datos),
                l.Boton("Revisar carta firmada", link),
                l.EnlaceDirecto(link));
        }

        /// <summary>
        /// Descarga un documento del file digital. Se prefiere driveId + itemId, que es lo que quedó
        /// guardado al subirlo; la webUrl es el respaldo para los documentos anteriores a que se
        /// persistiera el itemId.
        /// </summary>
        private async Task<byte[]> DescargarAsync(string? driveId, string? itemId, string? webUrl)
        {
            if (!string.IsNullOrWhiteSpace(driveId) && !string.IsNullOrWhiteSpace(itemId))
            {
                var (content, _) = await _sharePoint.DownloadFromOneDriveByItemIdAsync(driveId!, itemId!);
                return content;
            }

            if (!string.IsNullOrWhiteSpace(webUrl))
                return await _sharePoint.DownloadOneDriveFileByWebUrlAsync(webUrl!);

            throw new AbrilException(
                "No encontramos el archivo de tu carta oferta. Escríbele a Gestión de Talento Humano.", 409);
        }

        /// <summary>Token vacío: se corta acá con el mismo mensaje que un token inválido.</summary>
        private static string ExigirToken(string token) =>
            string.IsNullOrWhiteSpace(token)
                ? throw new AbrilException(
                    "El enlace no es válido o ya no está disponible. Escríbele a Gestión de Talento Humano para que te envíe uno nuevo.", 404)
                : token.Trim();
    }
}
