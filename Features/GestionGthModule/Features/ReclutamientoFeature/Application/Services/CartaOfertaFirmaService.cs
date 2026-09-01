using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Helpers;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Shared.Correos;
using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Interfaces;
using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Services;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Helpers;
using Abril_Backend.Shared.Services.Email.Configuration;
using Abril_Backend.Shared.Services.Pdf;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Layout = Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared.ReclutamientoEmailLayout;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
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

        /// <summary>Perú no tiene horario de verano, así que el desfase es fijo.</summary>
        private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);

        /// <summary>
        /// Contexto de la página y, en la PRIMERA apertura, el sellado de la fecha de conformidad.
        ///
        /// Esa fecha —el día en que el documento llegó a manos del colaborador— es el único dato de
        /// la carta que no se puede saber cuando GTH la arma, así que la generación deja su marcador
        /// sin resolver y se rellena acá: se baja el .docx del file, se le pone la fecha, se vuelve a
        /// subir y se reconvierte a PDF. El colaborador lee y firma el documento ya completo.
        ///
        /// Si algo de eso falla, la página se abre igual con el documento como esté y la fecha NO se
        /// sella: la próxima apertura lo reintenta. Nunca se sella una fecha que el documento no
        /// llegó a imprimir — el sello es de una sola vez y dejaría la carta en falso para siempre.
        /// </summary>
        public async Task<CartaOfertaFirmaPublicoDto> GetPublico(string token)
        {
            var t   = ExigirToken(token);
            var dto = await _repo.GetPublicoByToken(t);

            if (dto.PrimeraAperturaEn == null)
            {
                try
                {
                    var sellada = await SellarConformidadAsync(t);
                    dto.PrimeraAperturaEn = sellada?.ToOffset(PeruOffset).DateTime;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "CARTA OFERTA FIRMA · no se pudo estampar la fecha de conformidad en la carta");
                }
            }

            return dto;
        }

        /// <summary>
        /// Rellena la fecha de conformidad en el documento y la sella en la fila. Devuelve la fecha
        /// que quedó registrada, o null si el token no resuelve.
        ///
        /// No se rehace nada en dos casos, y en los dos solo se sella la fecha:
        ///   • la carta se adjuntó ya armada (cartas anteriores a que se retirara esa vía): no hay
        ///     .docx del que salga, y su PDF no tiene ningún marcador que rellenar;
        ///   • la carta YA está firmada: el documento firmado es el que vale y rehacer el original
        ///     dejaría al expediente con dos versiones distintas del mismo texto.
        /// </summary>
        private async Task<DateTimeOffset?> SellarConformidadAsync(string token)
        {
            var ctx = await _repo.PrepararPorToken(token);

            // Otra pestaña pudo sellarla entre la lectura y esta línea.
            if (ctx.PrimeraApertura != null) return ctx.PrimeraApertura;

            var fecha = DateTimeOffset.UtcNow;

            var sinDocumentoQueRehacer =
                ctx.YaFirmada
                || string.IsNullOrWhiteSpace(ctx.GeneradaDriveId)
                || string.IsNullOrWhiteSpace(ctx.GeneradaItemId);

            if (sinDocumentoQueRehacer)
                return await _repo.GuardarConformidad(token, fecha, null, null);

            var carpeta = ctx.Carpeta ?? await _fileDigital.ResolverCarpetaAsync(ctx.Dni, ctx.Nombre);
            var dia     = DateOnly.FromDateTime(fecha.ToOffset(PeruOffset).DateTime);

            // 1) El .docx del file, con las correcciones que GTH le haya hecho en Word, más la fecha.
            var (original, _) = await _sharePoint.DownloadFromOneDriveByItemIdAsync(
                ctx.GeneradaDriveId!, ctx.GeneradaItemId!);
            var conFecha = CartaOfertaPlantilla.RellenarConformidad(original, dia);

            // 2) Se sube con el MISMO nombre estable, así que reemplaza al anterior en vez de dejar
            //    dos Word en la carpeta. Es el documento de trabajo, no una versión nueva.
            var generada = await _fileDigital.SubirDocumentoAsync(
                carpeta, SubcarpetaFileDigital.CartaEnviada,
                ctx.GeneradaNombre ?? CartaOfertaArchivos.Docx(ctx.Codigo),
                conFecha, DocxMime, "tu carta oferta");

            // 3) Y de ese Word sale el PDF que el colaborador lee y firma.
            var pdf = await _fileDigital.DescargarComoPdfAsync(
                generada.DriveId, generada.ItemId, "tu carta oferta");

            //    Con el MISMO nombre que le puso el envío, para que reemplace a aquel en vez de
            //    dejar dos PDF en la carpeta: es la misma carta, solo que con su fecha puesta.
            var carta = await _fileDigital.SubirDocumentoAsync(
                carpeta, SubcarpetaFileDigital.CartaEnviada,
                CartaOfertaArchivos.Pdf(ctx.Codigo),
                pdf, "application/pdf", "tu carta oferta");

            return await _repo.GuardarConformidad(token, fecha, generada, carta);
        }

        /// <summary>
        /// MIME del .docx, para que SharePoint lo abra en Word Online al hacer clic y no lo baje como
        /// binario suelto. Mismo valor que usa la generación.
        /// </summary>
        private const string DocxMime =
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        public async Task<(byte[] Content, string ContentType, string FileName)> GetDocumento(string token)
        {
            var ctx = await _repo.PrepararPorToken(ExigirToken(token));

            // Si ya firmó, el visor muestra el documento FIRMADO: es el que a él le importa revisar y
            // descargar, y ver el original después de firmar solo confunde.
            var (content, nombreOrigen) = ctx.YaFirmada
                ? (await DescargarAsync(ctx.FirmadaDriveId, ctx.FirmadaItemId, ctx.FirmadaUrl), ctx.FirmadaNombre)
                : (await DescargarAsync(ctx.CartaDriveId, ctx.CartaItemId, ctx.CartaUrl), ctx.CartaNombre);

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

            // Finalizar es el cierre explícito del trámite: desde ahí el documento firmado es el
            // definitivo, así que la firma con la que se estampó tampoco se toca.
            if (ctx.Finalizada)
                throw new AbrilException(
                    "Ya finalizaste tu carta oferta: la firma con la que quedó firmada ya no se puede cambiar.", 409);

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

            // Ya finalizada: el propio colaborador cerró el trámite y el documento firmado quedó
            // como definitivo. Volver a firmar generaría un documento nuevo después de que el
            // solicitante ya recibió el aviso de que la oferta estaba aceptada.
            if (ctx.Finalizada)
                throw new AbrilException(
                    "Ya finalizaste tu carta oferta: el proceso de firma está cerrado. Si necesitas corregir algo, "
                    + "escríbele a Gestión de Talento Humano.", 409);

            // Ya firmada pero sin finalizar: se permite volver a firmar (por ejemplo si rehízo su
            // firma porque la anterior salió mal). El documento nuevo reemplaza al anterior y la
            // revisión de GTH vuelve a quedar pendiente. Es justamente el margen que el paso de
            // «Finalizar» convierte en explícito.
            var firma = await _repo.GetFirmaBytes(ctx.PersonId)
                ?? throw new AbrilException(
                    "Primero registra tu firma en esta página y después presiona «Firmar».", 409);

            byte[] original;
            try
            {
                original = await DescargarAsync(ctx.CartaDriveId, ctx.CartaItemId, ctx.CartaUrl);
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CARTA OFERTA FIRMA · falló la descarga del original (carta {CartaOfertaId})", ctx.CartaOfertaId);
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
                    "CARTA OFERTA FIRMA · falló el estampado (carta {CartaOfertaId}, bytes={Bytes}, firma={Firma})",
                    ctx.CartaOfertaId, original.Length, firma.Bytes.Length);
                throw new AbrilException(
                    "No pudimos estampar tu firma en el documento. Escríbele a Gestión de Talento Humano para que lo revise.", 502);
            }

            // El documento firmado va a la misma carpeta donde GTH dejaría la carta firmada si la
            // subiera a mano, para que el expediente se lea igual sin importar por dónde entró.
            var carpeta = ctx.Carpeta ?? await _fileDigital.ResolverCarpetaAsync(ctx.Dni, ctx.Nombre);

            var documento = await _fileDigital.SubirDocumentoAsync(
                carpeta, SubcarpetaFileDigital.CartaFirmada,
                _fileDigital.NombreArchivo("carta_oferta_firmada", ctx.Codigo, ".pdf"),
                firmado, "application/pdf", "tu carta oferta firmada");

            var firmadaEn = await _repo.GuardarFirmadaPorPostulante(ctx.CartaOfertaId, documento, carpeta);

            // Aviso a GTH de que ya hay una carta firmada esperando su revisión. Best-effort a
            // propósito: el colaborador ya firmó, el documento ya está en su file digital y el
            // requerimiento ya se movió de fase, así que no puede ver un error —ni verse empujado a
            // firmar de nuevo, que reemplazaría el documento— porque un correo interno no salió.
            try
            {
                await EnviarAvisoCartaFirmadaAsync(ctx, firmadaEn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CARTA OFERTA FIRMA · no se pudo avisar a GTH de la firma (carta {CartaOfertaId})",
                    ctx.CartaOfertaId);
            }

            return new CartaOfertaFirmarResultDto
            {
                Message   = "¡Listo! Tu carta oferta quedó firmada y enviada a Gestión de Talento Humano.",
                FirmadaEn = firmadaEn,
            };
        }

        public async Task<CartaOfertaFinalizarResultDto> Finalizar(string token)
        {
            var ctx = await _repo.PrepararPorToken(ExigirToken(token));

            // Finalizar es cerrar lo que ya se firmó: sin documento firmado no hay nada que cerrar y
            // el botón no debería siquiera estar visible.
            if (!ctx.YaFirmada)
                throw new AbrilException(
                    "Primero firma tu carta oferta y después presiona «Finalizar».", 409);

            // Ya finalizada: se responde igual pero sin volver a avisarle al solicitante. Recargar la
            // pantalla de confirmación no es un cierre nuevo.
            var finalizadaEn = await _repo.MarcarFinalizada(ctx.CartaOfertaId);
            if (finalizadaEn == null)
                return new CartaOfertaFinalizarResultDto
                {
                    Message = "Tu carta oferta ya estaba finalizada. No tienes nada más que hacer.",
                };

            // Aviso al solicitante. Best-effort, igual que el de GTH al firmar: el trámite del
            // colaborador ya quedó cerrado en la base de datos, así que no puede ver un error —ni
            // verse empujado a finalizar de nuevo— porque un correo interno no salió.
            try
            {
                await EnviarAvisoFinalizadaAlSolicitanteAsync(ctx, finalizadaEn.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CARTA OFERTA FIRMA · no se pudo avisar al solicitante del cierre (carta {CartaOfertaId})",
                    ctx.CartaOfertaId);
            }

            return new CartaOfertaFinalizarResultDto
            {
                Message      = "¡Listo! Tu carta oferta quedó firmada y enviada. No tienes nada más que hacer.",
                FinalizadaEn = finalizadaEn,
            };
        }

        // ── Aviso al solicitante ───────────────────────────────────────────────

        /// <summary>
        /// Le avisa al SOLICITANTE de la vacante que el colaborador firmó y cerró su carta oferta
        /// (tipo CARTA_OFERTA_FINALIZADA). Es el último eslabón de un proceso que él arrancó semanas
        /// antes y del que, hasta ahora, lo último que supo fue la decisión del finalista.
        ///
        /// No pide nada —aprobar la carta le toca a GTH, que ya recibió lo suyo al firmarse— así que
        /// no lleva llamada a la acción: lleva la fecha de ingreso, que es lo que él necesita para
        /// preparar la llegada.
        ///
        /// El destinatario principal es SIEMPRE el solicitante; la configuración solo aporta
        /// principales adicionales y copias. Sin nadie a quien mandarlo no se envía: es una decisión
        /// de Configuración (o una solicitud vieja sin usuario), no una falla que reintentar.
        /// </summary>
        private async Task EnviarAvisoFinalizadaAlSolicitanteAsync(
            CartaOfertaFirmaContextoDto ctx, DateTime finalizadaEn)
        {
            var dest = await _destinatarios.ResolverAsync(CorreoTipoGth.CartaOfertaFinalizada);
            var (principales, copias) = CorreoDestinatariosCombinador.Combinar(ctx.SolicitanteEmail, dest);

            if (principales.Count == 0)
            {
                _logger.LogWarning(
                    "Carta oferta finalizada del requerimiento {Codigo}: el solicitante no tiene correo cargado "
                    + "y el correo CARTA_OFERTA_FINALIZADA no tiene destinatarios principales activos, así que el "
                    + "aviso no sale.", ctx.Codigo);
                return;
            }

            await _email.SendAsync(
                to:      principales,
                subject: $"[Reclutamiento] {ctx.NombreColaborador} aceptó la oferta — {ctx.Codigo} · {ctx.Puesto}",
                body:    ConstruirCuerpoFinalizadaSolicitante(ctx, finalizadaEn),
                isHtml:  true,
                cc:      copias.Count > 0 ? copias : null,
                // Buzón de GTH: es el área que lleva el proceso la que le está dando la noticia, igual
                // que en la long list, el finalista y la entrevista confirmada.
                sender:  EmailSenders.Gth);
        }

        /// <summary>
        /// Cuerpo del aviso al solicitante, en el chrome de marca de Abril One (ver
        /// <see cref="Layout"/>). Lo que importa es que el ingreso está confirmado y desde cuándo,
        /// así que la fecha de ingreso va en la franja —se lee de un vistazo— y repetida en la
        /// tarjeta con el resto del contexto. El botón lleva al seguimiento del requerimiento, que es
        /// donde él puede mirar el proceso; no hay nada que aprobar de su lado.
        ///
        /// Las filas en blanco se omiten: la vacante puede no tener proyecto ni razón social, y una
        /// etiqueta con el valor vacío se lee como un error nuestro.
        /// </summary>
        private string ConstruirCuerpoFinalizadaSolicitante(
            CartaOfertaFirmaContextoDto ctx, DateTime finalizadaEn)
        {
            var l = Layout.Desde(_configuration);

            var datos = new List<Layout.Fila>();
            void Fila(string icono, string etiqueta, string? valor)
            {
                if (!string.IsNullOrWhiteSpace(valor))
                    datos.Add(new(icono, etiqueta, Layout.Esc(valor)));
            }

            Fila("req-codigo",    "Requerimiento",    ctx.Codigo);
            Fila("req-candidato", "Colaborador",      ctx.NombreColaborador);
            Fila("req-puesto",    "Puesto",           ctx.Puesto);
            Fila("req-area",      "Área",             ctx.Area);
            Fila("req-proyecto",  "Proyecto / obra",  ctx.ProyectoObra);
            Fila("onb-empresa",   "Empresa",          ctx.Empresa);
            Fila("req-fecha",     "Fecha de ingreso", ctx.FechaIngreso?.ToString("dd/MM/yyyy"));
            Fila("req-hora",      "Aceptada el",      finalizadaEn.ToString("dd/MM/yyyy HH:mm"));

            var colaborador = string.IsNullOrWhiteSpace(ctx.NombreColaborador)
                ? "El colaborador" : Layout.Esc(ctx.NombreColaborador);

            var link = ConstruirLinkSeguimientoRequerimiento(ctx.RequerimientoId);

            return l.Documento(
                new Layout.Cabecera(
                    "onb-carta", "Oferta Aceptada",
                    $"<b>{colaborador}</b> firmó y aceptó su carta oferta para "
                    + $"<b>{Layout.Esc(ctx.Puesto)}</b>."),
                ctx.FechaIngreso == null
                    ? l.Franja("req-check", Layout.Tono.Verde,
                        "La vacante que pediste ya tiene a su colaborador confirmado.")
                    : l.Franja("req-check", Layout.Tono.Verde,
                        $"Ingresa el <b>{ctx.FechaIngreso.Value:dd/MM/yyyy}</b>."),
                l.Tarjeta(datos),
                l.Boton("Ver el requerimiento", link),
                l.EnlaceDirecto(link));
        }

        /// <summary>
        /// Enlace al SEGUIMIENTO del requerimiento, que es la pantalla del solicitante: la bandeja de
        /// Reclutamiento a la que lleva el aviso de GTH es de GTH, y quien pidió la vacante no
        /// necesariamente entra ahí. Es el mismo enlace que llevan los demás correos que le hablan a
        /// él (finalista, entrevista confirmada, candidato retomado).
        ///
        /// Sin sesión, el <c>authGuard</c> del frontend manda al login con esta URL como
        /// <c>returnUrl</c> y lo devuelve acá al entrar.
        /// </summary>
        private string ConstruirLinkSeguimientoRequerimiento(int requerimientoId)
        {
            var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
            return $"{frontendUrl}/gestion-gth/solicitud-personal/seguimiento/{requerimientoId}";
        }

        // ── Aviso a GTH ────────────────────────────────────────────────────────

        /// <summary>
        /// Le avisa a GTH que el candidato firmó su carta oferta, para que entre a revisarla: aprobarla
        /// es lo que CIERRA el proceso de reclutamiento. Nadie de la empresa dispara este correo —lo
        /// dispara el candidato al firmar—, así que sin él la carta firmada se quedaría esperando a que
        /// alguien pase por la bandeja.
        ///
        /// A quién le llega sale entero de Configuración de correos de Reclutamiento: no hay
        /// destinatario principal automático, igual que en el aviso de «formulario completado». Sin
        /// destinatarios activos no se envía nada, que es lo que esa pantalla quiso decir al apagarlos.
        /// </summary>
        private async Task EnviarAvisoCartaFirmadaAsync(CartaOfertaFirmaContextoDto ctx, DateTime firmadaEn)
        {
            var dest = await _destinatarios.ResolverAsync(CorreoTipoGth.CartaOfertaFirmada);
            var para = dest.EmailsPara;
            if (para.Count == 0) return;

            var copias = dest.EmailsCopias;

            await _email.SendAsync(
                to:      para,
                subject: $"[Reclutamiento] {ctx.NombreColaborador} firmó su carta oferta — {ctx.Codigo} · {ctx.Puesto}",
                body:    ConstruirCuerpoAvisoCartaFirmada(ctx, firmadaEn),
                isHtml:  true,
                cc:      copias.Count > 0 ? copias : null,
                // Sale de aprobaciones@abril.pe y no del buzón de GTH: es un aviso INTERNO que llega
                // justamente al buzón de GTH, y mandarlo desde ese mismo buzón lo dejaría como un
                // correo que el área se escribe a sí misma. El de la carta oferta, que va al
                // candidato, sí sale de GTH.
                sender:  EmailSenders.Aprobaciones);
        }

        /// <summary>
        /// Enlace al requerimiento dentro de la bandeja de Reclutamiento, con su detalle ya abierto:
        /// es donde se revisa y se aprueba la carta firmada. Mismo mecanismo que el resto de correos
        /// internos del módulo: sin sesión, el <c>authGuard</c> del frontend manda al login con esta
        /// URL como <c>returnUrl</c> y lo devuelve acá al entrar.
        /// </summary>
        private string ConstruirLinkDetalleRequerimiento(int requerimientoId)
        {
            var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
            return $"{frontendUrl}/gestion-gth/reclutamiento/requerimiento/{requerimientoId}";
        }

        /// <summary>
        /// Cuerpo del aviso, en el chrome de marca de Abril One (ver <see cref="Layout"/>). Es un
        /// correo INTERNO, así que lleva datos y un acceso, no explicaciones: la tarjeta trae los
        /// datos del ingreso y el botón lleva a donde se aprueba. El documento firmado tampoco va
        /// adjunto — se ve en el detalle, junto al botón de aprobarlo.
        ///
        /// Las filas en blanco se omiten: la vacante puede estar a medio llenar y una etiqueta con el
        /// valor vacío se lee como un error nuestro.
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

            var link = ConstruirLinkDetalleRequerimiento(ctx.RequerimientoId);

            return l.Documento(
                new Layout.Cabecera(
                    "onb-carta", "Carta Oferta Firmada",
                    $"<b>{nombre}</b> firmó su carta oferta desde el enlace."),
                l.Tarjeta(datos),
                l.Boton("Revisar y aprobar la carta", link),
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
