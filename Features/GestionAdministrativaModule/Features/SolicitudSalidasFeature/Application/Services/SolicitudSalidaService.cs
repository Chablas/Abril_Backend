using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Options;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Services
{
    public class SolicitudSalidaService : ISolicitudSalidaService
    {
        private readonly ISolicitudSalidaRepository    _repo;
        private readonly IApproverResolver             _approverResolver;
        private readonly ISolicitudSalidaTokenService  _tokenService;
        private readonly IEmailService                 _emailService;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IConfiguration                _configuration;
        private readonly ILogger<SolicitudSalidaService> _logger;
        private readonly IGraphSharePointService        _sharePointService;
        private readonly SharePointSiteRef              _site;
        private readonly string                         _solicitudSalidasLibraryId;

        private const string CarpetaCapturas = "Capturas de movilidades";

        public SolicitudSalidaService(
            ISolicitudSalidaRepository repo,
            IApproverResolver approverResolver,
            ISolicitudSalidaTokenService tokenService,
            IEmailService emailService,
            IDbContextFactory<AppDbContext> factory,
            IConfiguration configuration,
            ILogger<SolicitudSalidaService> logger,
            IGraphSharePointService sharePointService)
        {
            _repo             = repo;
            _approverResolver = approverResolver;
            _tokenService     = tokenService;
            _emailService     = emailService;
            _factory          = factory;
            _configuration    = configuration;
            _logger           = logger;
            _sharePointService = sharePointService;
            _site = SharePointSiteRef.FromConfig(configuration, "CostosYPresupuestos");
            _solicitudSalidasLibraryId = configuration["SharePoint:Sites:CostosYPresupuestos:SolicitudSalidasLibraryId"]
                ?? throw new InvalidOperationException("SharePoint:Sites:CostosYPresupuestos:SolicitudSalidasLibraryId no está configurado.");
        }

        public async Task<SolicitudSalidaFormDataDto> GetFormData(int? userId)
        {
            var data = await _repo.GetFormData();

            if (userId.HasValue)
            {
                try
                {
                    using var ctx = _factory.CreateDbContext();
                    var solicitante = await ctx.Worker
                        .Where(w => w.Person != null && w.Person.UserId == userId.Value)
                        .FirstOrDefaultAsync();
                    if (solicitante != null)
                    {
                        // Aprobador (best-effort): el correo se deriva del worker resuelto.
                        var aprobador = await _approverResolver.ResolveApproverAsync(solicitante);
                        data.AprobadorEmail = aprobador?.Email;

                        // Si el trabajador es TI, exponer el catálogo de trayectos para que el
                        // frontend muestre el monto automático al seleccionar origen+destino.
                        if (string.Equals(solicitante.Subarea, "Tecnología de la Información", StringComparison.OrdinalIgnoreCase))
                        {
                            data.EsTI = true;
                            data.TrayectosCatalogo = await ctx.GaTrayecto
                                .Where(g => g.Activo)
                                .Select(g => new TrayectoCatalogoOptionDto
                                {
                                    LugarOrigenId  = g.LugarOrigenId,
                                    LugarDestinoId = g.LugarDestinoId,
                                    Monto          = g.Monto,
                                })
                                .ToListAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo resolver el aprobador/catálogo para userId {UserId}", userId);
                }
            }

            return data;
        }

        public Task<List<SolicitudSalidaListItemDto>> GetByUserId(int userId, SolicitudSalidaFiltersDto? filters = null) =>
            _repo.GetByUserId(userId, filters);

        public Task<SolicitudSalidaFilterDataDto> GetFilterData(int userId) => _repo.GetFilterData(userId);

        public async Task<int> Create(SolicitudSalidaCreateDto dto, int? userId)
        {
            if (dto.Trayectos == null || dto.Trayectos.Count == 0)
                throw new AbrilException("Debe registrar al menos un trayecto.", 400);

            // Validar cada trayecto
            for (int i = 0; i < dto.Trayectos.Count; i++)
            {
                var t = dto.Trayectos[i];
                var pos = i + 1;
                if (t.HoraRetorno.HasValue && t.HoraRetorno.Value <= t.HoraSalida)
                    throw new AbrilException($"Trayecto {pos}: la hora de retorno debe ser posterior a la hora de salida.", 400);

                var tieneMotivoId    = t.MotivoId.HasValue;
                var tieneMotivoLibre = !string.IsNullOrWhiteSpace(t.MotivoLibre);
                if (!tieneMotivoId && !tieneMotivoLibre)
                    throw new AbrilException($"Trayecto {pos}: debe indicar un motivo.", 400);

                var tieneOrigenId    = t.LugarOrigenId.HasValue;
                var tieneOrigenLibre = !string.IsNullOrWhiteSpace(t.LugarOrigenLibre);
                if (!tieneOrigenId && !tieneOrigenLibre)
                    throw new AbrilException($"Trayecto {pos}: debe indicar un lugar de origen.", 400);

                var tieneDestinoId    = t.LugarDestinoId.HasValue;
                var tieneDestinoLibre = !string.IsNullOrWhiteSpace(t.LugarDestinoLibre);
                if (!tieneDestinoId && !tieneDestinoLibre)
                    throw new AbrilException($"Trayecto {pos}: debe indicar un lugar de destino.", 400);

                if (tieneOrigenId && tieneDestinoId && t.LugarOrigenId == t.LugarDestinoId)
                    throw new AbrilException($"Trayecto {pos}: el lugar de origen y el lugar de destino no pueden ser iguales.", 400);
            }

            // 1. Persistir solicitud + trayectos
            var (solicitud, trayectos, solicitante) = await _repo.Create(dto, userId);

            // 2. Resolver aprobador + enviar emails (best-effort)
            //    Hacemos UNA sola resolución de trayectos en memoria y la compartimos
            //    entre los dos correos (al aprobador y de confirmación al solicitante).
            try
            {
                using var ctx = _factory.CreateDbContext();
                var nombreSolicitante = await ctx.Person
                    .Where(p => p.PersonId == solicitante.PersonId)
                    .Select(p => p.FullName)
                    .FirstOrDefaultAsync() ?? "Trabajador";

                var trayectosResueltos = await ResolveTrayectosForEmailAsync(ctx, trayectos);

                // 2a. Email al aprobador. Se guarda el worker aprobador (FK) y el correo se
                //     deriva de su email_personal.
                var aprobador = await _approverResolver.ResolveApproverAsync(solicitante);
                var aprobadorEmail = aprobador?.Email;
                if (aprobador != null && !string.IsNullOrWhiteSpace(aprobador.Email))
                {
                    await _repo.SetAprobadorWorkerId(solicitud.Id, aprobador.WorkerId);
                    await SendNotificacionAprobadorAsync(solicitud, trayectosResueltos, aprobador.Email, nombreSolicitante);
                }
                else
                {
                    _logger.LogWarning(
                        "No se pudo resolver aprobador para solicitud {SolicitudId} (worker {WorkerId} - area={Area} subarea={Subarea} categoria={Categoria})",
                        solicitud.Id, solicitante.Id, solicitante.Area, solicitante.Subarea, solicitante.Categoria);
                }

                // 2b. Email de confirmación al solicitante (al mismo usuario que registró la solicitud)
                if (userId.HasValue)
                {
                    try
                    {
                        await SendConfirmacionSolicitanteAsync(ctx, solicitud, trayectosResueltos, nombreSolicitante, userId.Value, aprobadorEmail);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error enviando confirmación al solicitante para solicitud {SolicitudId}", solicitud.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando emails para solicitud {SolicitudId}", solicitud.Id);
            }

            return solicitud.Id;
        }

        // ── Aprobar / Rechazar desde el email ────────────────────────────────

        public async Task<string> ProcessAprobarFromEmail(string token)
        {
            var payload = _tokenService.Validate(token);
            if (payload == null || payload.Action != SolicitudSalidaAction.Aprobar)
                return RenderResultPage("Enlace inválido o expirado", "El enlace ya no es válido. Solicita uno nuevo al trabajador.", isSuccess: false);

            var s = await _repo.Aprobar(payload.SolicitudId);
            if (s == null)
                return RenderResultPage("Solicitud ya procesada", "Esta solicitud ya había sido aprobada o rechazada anteriormente.", isSuccess: false);

            // Notificar al solicitante (best-effort, no rompe la respuesta HTML)
            await NotifySolicitanteAprobada(s.Id);

            return RenderResultPage("Solicitud aprobada", $"Has aprobado la solicitud de salida #{s.Id}.", isSuccess: true);
        }

        public async Task<string> ProcessRechazarFromEmail(string token, string? motivoRechazo)
        {
            var payload = _tokenService.Validate(token);
            if (payload == null || payload.Action != SolicitudSalidaAction.Rechazar)
                return RenderResultPage("Enlace inválido o expirado", "El enlace ya no es válido. Solicita uno nuevo al trabajador.", isSuccess: false);

            var s = await _repo.Rechazar(payload.SolicitudId, motivoRechazo);
            if (s == null)
                return RenderResultPage("Solicitud ya procesada", "Esta solicitud ya había sido aprobada o rechazada anteriormente.", isSuccess: false);

            return RenderResultPage("Solicitud rechazada", $"Has rechazado la solicitud de salida #{s.Id}.", isSuccess: true);
        }

        public string RenderRechazarForm(string token)
        {
            var payload = _tokenService.Validate(token);
            if (payload == null || payload.Action != SolicitudSalidaAction.Rechazar)
                return RenderResultPage("Enlace inválido o expirado", "El enlace ya no es válido.", isSuccess: false);

            var safeToken = WebUtility.HtmlEncode(token);
            return $@"<!DOCTYPE html>
<html lang=""es""><head><meta charset=""utf-8""><title>Rechazar solicitud</title>
<style>
  body {{ font-family: Segoe UI, Arial, sans-serif; background:#f5f5f5; margin:0; padding:40px; }}
  .card {{ max-width:520px; margin:0 auto; background:#fff; padding:32px; border-radius:12px; box-shadow:0 2px 12px rgba(0,0,0,.06); }}
  h1 {{ color:#D30000; margin-top:0; }}
  textarea {{ width:100%; min-height:120px; padding:12px; border:1px solid #E2E2E2; border-radius:8px; font:inherit; box-sizing:border-box; }}
  button {{ margin-top:16px; background:#D30000; color:#fff; border:0; padding:12px 24px; border-radius:8px; cursor:pointer; font-size:14px; }}
  button:hover {{ background:#a50000; }}
</style></head><body><div class=""card"">
  <h1>Rechazar solicitud #{payload.SolicitudId}</h1>
  <p>Indica el motivo del rechazo (opcional):</p>
  <form method=""post"" action=""rechazar"">
    <input type=""hidden"" name=""token"" value=""{safeToken}"" />
    <textarea name=""motivoRechazo"" placeholder=""Ej: La fecha coincide con una reunión importante...""></textarea>
    <button type=""submit"">Confirmar rechazo</button>
  </form>
</div></body></html>";
        }

        // ── Helpers internos ─────────────────────────────────────────────────

        /// <summary>
        /// Resuelve los trayectos de la entidad a tuplas listas para embeber en el cuerpo del email.
        /// Hace lookups por motivo y por lugar (origen/destino) para mostrar nombres legibles.
        /// </summary>
        private static async Task<List<(int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino)>>
            ResolveTrayectosForEmailAsync(AppDbContext ctx, List<GaSolicitudTrayecto> trayectos)
        {
            var resueltos = new List<(int, string, string, string, string, string)>();
            foreach (var t in trayectos.OrderBy(t => t.Orden))
            {
                string motivo;
                if (t.MotivoId.HasValue)
                {
                    motivo = await ctx.GaMotivoSalida
                        .Where(m => m.Id == t.MotivoId.Value)
                        .Select(m => m.Descripcion)
                        .FirstOrDefaultAsync() ?? "—";
                }
                else motivo = t.MotivoLibre ?? "—";

                var origen  = await ResolveLugarDisplay(ctx, t.LugarOrigenId,  t.LugarOrigenLibre);
                var destino = await ResolveLugarDisplay(ctx, t.LugarDestinoId, t.LugarDestinoLibre);
                var horaRet = t.HoraRetorno.HasValue ? t.HoraRetorno.Value.ToString("HH:mm") : "Sin retorno";
                resueltos.Add((t.Orden + 1, t.HoraSalida.ToString("HH:mm"), horaRet, motivo, origen, destino));
            }
            return resueltos;
        }

        private async Task SendNotificacionAprobadorAsync(
            GaSolicitudSalida solicitud,
            List<(int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino)> trayectos,
            string aprobadorEmail,
            string nombreSolicitante)
        {
            var tokenAprobar  = _tokenService.Generate(solicitud.Id, SolicitudSalidaAction.Aprobar,  TimeSpan.FromDays(30));
            var tokenRechazar = _tokenService.Generate(solicitud.Id, SolicitudSalidaAction.Rechazar, TimeSpan.FromDays(30));

            var backendUrl = (_configuration["BackendSettings:PublicUrl"] ?? "http://localhost:5236").TrimEnd('/');
            var basePath   = "/api/v1/gestion-administrativa/solicitud-salidas";
            var urlAprobar  = $"{backendUrl}{basePath}/aprobar?token={WebUtility.UrlEncode(tokenAprobar)}";
            var urlRechazar = $"{backendUrl}{basePath}/rechazar?token={WebUtility.UrlEncode(tokenRechazar)}";

            var body    = BuildEmailBody(nombreSolicitante, solicitud.FechaSalida, trayectos, urlAprobar, urlRechazar);
            var subject = $"Solicitud de salida - {nombreSolicitante} - {solicitud.FechaSalida:dd/MM/yyyy}";

            await _emailService.SendAsync(
                to: new List<string> { aprobadorEmail },
                subject: subject,
                body: body,
                isHtml: true);
        }

        private async Task SendConfirmacionSolicitanteAsync(
            AppDbContext ctx,
            GaSolicitudSalida solicitud,
            List<(int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino)> trayectos,
            string nombreSolicitante,
            int userId,
            string? aprobadorEmail)
        {
            var emailSolicitante = await ctx.User
                .Where(u => u.UserId == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(emailSolicitante))
            {
                _logger.LogWarning(
                    "No se envió email de confirmación para solicitud {SolicitudId}: el usuario {UserId} no tiene email registrado.",
                    solicitud.Id, userId);
                return;
            }

            var numeroUsuario = await GetUserSolicitudNumeroAsync(ctx, solicitud.WorkerId, solicitud.Id);

            var body    = BuildEmailConfirmacionSolicitante(nombreSolicitante, numeroUsuario, solicitud.FechaSalida, trayectos, aprobadorEmail);
            var subject = $"Tu solicitud de salida #{numeroUsuario} está en revisión - {solicitud.FechaSalida:dd/MM/yyyy}";

            var cc = await GetCcRecepcionAsync(ctx);

            await _emailService.SendAsync(
                to: new List<string> { emailSolicitante },
                subject: subject,
                body: body,
                isHtml: true,
                cc: cc,
                bcc: new List<string> { BccRecepcionMonitoreo });
        }

        // ── CC / BCC recepción ──────────────────────────────────────────────
        private const int RoleIdRecepcion = 52;
        private const string CcRecepcionFijo = "recepcionnm@abril.pe";
        /// <summary>Correo de GTH que va siempre en copia en los envíos al solicitante + recepción.</summary>
        private const string CcGthFijo = "gthnm@abril.pe";
        /// <summary>Correo con copia oculta en todos los envíos que también van a recepción.</summary>
        private const string BccRecepcionMonitoreo = "calvarez@abril.pe";

        /// <summary>
        /// Devuelve los correos para CC del flujo de salidas:
        /// (a) <c>worker.email_personal</c> de todos los users con rol id 52 (USUARIO DE RECEPCIÓN),
        /// (b) más el correo fijo <c>recepcionnm@abril.pe</c> (siempre),
        /// (c) más el correo fijo de GTH <c>gthnm@abril.pe</c> (siempre).
        /// </summary>
        private static async Task<List<string>> GetCcRecepcionAsync(AppDbContext ctx)
        {
            var emails = await (
                from ur in ctx.UserRole
                where ur.RoleId == RoleIdRecepcion && ur.State
                join p in ctx.Person  on (int?)ur.UserId  equals p.UserId
                join w in ctx.Worker  on (int?)p.PersonId equals w.PersonId
                where w.EmailPersonal != null && w.EmailPersonal != ""
                select w.EmailPersonal!
            ).Distinct().ToListAsync();

            if (!emails.Any(e => string.Equals(e, CcRecepcionFijo, StringComparison.OrdinalIgnoreCase)))
                emails.Add(CcRecepcionFijo);

            if (!emails.Any(e => string.Equals(e, CcGthFijo, StringComparison.OrdinalIgnoreCase)))
                emails.Add(CcGthFijo);

            return emails;
        }

        /// <summary>
        /// Devuelve el número de orden de esta solicitud dentro del historial del propio
        /// solicitante (1 = primera solicitud del worker, 2 = segunda, ...). Se usa para
        /// mostrar un identificador estable y por-usuario en los correos del solicitante,
        /// en lugar del id global de la tabla.
        /// </summary>
        private static async Task<int> GetUserSolicitudNumeroAsync(AppDbContext ctx, int workerId, int solicitudId)
        {
            return await ctx.GaSolicitudSalida
                .Where(s => s.WorkerId == workerId && s.Id <= solicitudId)
                .CountAsync();
        }

        private static async Task<string> ResolveLugarDisplay(AppDbContext ctx, int? lugarId, string? lugarLibre)
        {
            if (!string.IsNullOrWhiteSpace(lugarLibre)) return lugarLibre.Trim();
            if (!lugarId.HasValue) return "—";

            var lugar = await ctx.GaLugar
                .Where(l => l.Id == lugarId.Value)
                .Select(l => new { l.Tipo, l.Nombre, l.ProjectId })
                .FirstOrDefaultAsync();
            if (lugar == null) return "—";

            if (lugar.Tipo == "proyecto" && lugar.ProjectId.HasValue)
            {
                var proj = await ctx.Project
                    .Where(p => p.ProjectId == lugar.ProjectId.Value)
                    .Select(p => p.ProjectDescription)
                    .FirstOrDefaultAsync();
                return proj ?? "[Sin proyecto]";
            }
            return lugar.Nombre ?? "—";
        }

        private static string BuildEmailBody(
            string nombre, DateOnly fechaSalida,
            List<(int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino)> trayectos,
            string urlAprobar, string urlRechazar)
        {
            string esc(string s) => WebUtility.HtmlEncode(s);

            string trayectoBloque((int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino) t)
            {
                var titulo = trayectos.Count > 1 ? $"Trayecto {t.Orden}" : "Trayecto";
                return $@"<div style=""border:1px solid #E2E2E2;border-radius:8px;padding:12px 16px;margin-bottom:10px"">
                    <div style=""font-weight:600;color:#64BC04;margin-bottom:6px;font-size:13px"">{esc(titulo)}</div>
                    <table style=""width:100%;border-collapse:collapse;font-size:13px"">
                      <tr><td style=""padding:3px 0;color:#777;width:40%"">Hora de salida</td><td style=""padding:3px 0;color:#222"">{esc(t.HoraSalida)}</td></tr>
                      <tr><td style=""padding:3px 0;color:#777"">Hora de retorno</td><td style=""padding:3px 0;color:#222"">{esc(t.HoraRetorno)}</td></tr>
                      <tr><td style=""padding:3px 0;color:#777"">Motivo</td><td style=""padding:3px 0;color:#222"">{esc(t.Motivo)}</td></tr>
                      <tr><td style=""padding:3px 0;color:#777"">Origen</td><td style=""padding:3px 0;color:#222"">{esc(t.Origen)}</td></tr>
                      <tr><td style=""padding:3px 0;color:#777"">Destino</td><td style=""padding:3px 0;color:#222"">{esc(t.Destino)}</td></tr>
                    </table>
                  </div>";
            }

            var trayectosHtml = string.Concat(trayectos.Select(trayectoBloque));

            return $@"<!DOCTYPE html><html><body style=""font-family:Segoe UI,Arial,sans-serif;background:#f5f5f5;margin:0;padding:24px;color:#222"">
  <div style=""max-width:620px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.06)"">
    <div style=""background:#64BC04;padding:20px 24px;color:#fff"">
      <h2 style=""margin:0;font-size:18px"">Nueva solicitud de salida</h2>
    </div>
    <div style=""padding:24px"">
      <p style=""margin:0 0 12px""><b>{esc(nombre)}</b> ha registrado una solicitud de salida que requiere tu aprobación:</p>
      <p style=""margin:0 0 16px;color:#777;font-size:13px""><b>Fecha:</b> {esc(fechaSalida.ToString("dd/MM/yyyy"))}{(trayectos.Count > 1 ? $" — {trayectos.Count} trayectos" : "")}</p>
      {trayectosHtml}
      <div style=""text-align:center;margin-top:18px"">
        <a href=""{urlAprobar}"" style=""display:inline-block;background:#009C87;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;margin:0 8px;font-weight:600"">Aprobar</a>
        <a href=""{urlRechazar}"" style=""display:inline-block;background:#D30000;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;margin:0 8px;font-weight:600"">Rechazar</a>
      </div>
      <p style=""color:#999;font-size:12px;margin-top:24px"">Los enlaces son válidos por 30 días.</p>
    </div>
  </div>
</body></html>";
        }

        private static string BuildEmailConfirmacionSolicitante(
            string nombre, int numeroUsuario, DateOnly fechaSalida,
            List<(int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino)> trayectos,
            string? aprobadorEmail)
        {
            string esc(string s) => WebUtility.HtmlEncode(s);

            string trayectoBloque((int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino) t)
            {
                var titulo = trayectos.Count > 1 ? $"Trayecto {t.Orden}" : "Trayecto";
                return $@"<div style=""border:1px solid #E2E2E2;border-radius:8px;padding:12px 16px;margin-bottom:10px"">
                    <div style=""font-weight:600;color:#0086A5;margin-bottom:6px;font-size:13px"">{esc(titulo)}</div>
                    <table style=""width:100%;border-collapse:collapse;font-size:13px"">
                      <tr><td style=""padding:3px 0;color:#777;width:40%"">Hora de salida</td><td style=""padding:3px 0;color:#222"">{esc(t.HoraSalida)}</td></tr>
                      <tr><td style=""padding:3px 0;color:#777"">Hora de retorno</td><td style=""padding:3px 0;color:#222"">{esc(t.HoraRetorno)}</td></tr>
                      <tr><td style=""padding:3px 0;color:#777"">Motivo</td><td style=""padding:3px 0;color:#222"">{esc(t.Motivo)}</td></tr>
                      <tr><td style=""padding:3px 0;color:#777"">Origen</td><td style=""padding:3px 0;color:#222"">{esc(t.Origen)}</td></tr>
                      <tr><td style=""padding:3px 0;color:#777"">Destino</td><td style=""padding:3px 0;color:#222"">{esc(t.Destino)}</td></tr>
                    </table>
                  </div>";
            }

            var trayectosHtml = string.Concat(trayectos.Select(trayectoBloque));

            var aprobadorBloque = string.IsNullOrWhiteSpace(aprobadorEmail)
                ? @"<p style=""margin:14px 0 0;color:#92400E;font-size:13px;background:#FEF9C3;padding:10px 14px;border-radius:8px"">
                     Aún no se identificó a tu jefatura inmediata. El equipo administrativo será notificado para asignarla.
                   </p>"
                : $@"<p style=""margin:14px 0 0;color:#444;font-size:13px"">
                      Tu solicitud fue enviada a
                      <b style=""color:#0086A5"">{esc(aprobadorEmail)}</b>
                      para su revisión.
                    </p>";

            return $@"<!DOCTYPE html><html><body style=""font-family:Segoe UI,Arial,sans-serif;background:#f5f5f5;margin:0;padding:24px;color:#222"">
  <div style=""max-width:620px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.06)"">
    <div style=""background:#0086A5;padding:20px 24px;color:#fff"">
      <h2 style=""margin:0;font-size:18px"">Tu solicitud está en revisión</h2>
    </div>
    <div style=""padding:24px"">
      <p style=""margin:0 0 12px"">Hola <b>{esc(nombre)}</b>,</p>
      <p style=""margin:0 0 16px;color:#444;font-size:14px"">
        Recibimos tu solicitud de salida <b>#{numeroUsuario}</b> y está pendiente de aprobación.
        Te notificaremos por correo cuando sea aprobada o rechazada.
      </p>
      <p style=""margin:0 0 16px;color:#777;font-size:13px""><b>Fecha:</b> {esc(fechaSalida.ToString("dd/MM/yyyy"))}{(trayectos.Count > 1 ? $" — {trayectos.Count} trayectos" : "")}</p>
      {trayectosHtml}
      {aprobadorBloque}
      <p style=""color:#999;font-size:12px;margin-top:24px"">Este es un correo automático, no respondas a este mensaje.</p>
    </div>
  </div>
</body></html>";
        }

        private static string RenderResultPage(string title, string message, bool isSuccess)
        {
            var color = isSuccess ? "#009C87" : "#D30000";
            return $@"<!DOCTYPE html><html lang=""es""><head><meta charset=""utf-8""><title>{WebUtility.HtmlEncode(title)}</title>
<style>
  body {{ font-family:Segoe UI,Arial,sans-serif;background:#f5f5f5;margin:0;padding:60px 20px;text-align:center; }}
  .card {{ max-width:500px;margin:0 auto;background:#fff;padding:40px;border-radius:12px;box-shadow:0 2px 12px rgba(0,0,0,.06); }}
  h1 {{ color:{color};margin:0 0 16px; }}
  p {{ color:#444;line-height:1.6;margin:0; }}
</style></head><body><div class=""card"">
  <h1>{WebUtility.HtmlEncode(title)}</h1><p>{WebUtility.HtmlEncode(message)}</p>
</div></body></html>";
        }

        // ── Detalle + capturas ──────────────────────────────────────────────

        public async Task<SolicitudSalidaDetalleDto> GetDetalle(int solicitudId, int userId)
        {
            var detalle = await _repo.GetDetalleForUser(solicitudId, userId);
            return detalle ?? throw new AbrilException("Solicitud no encontrada o no te pertenece.", 404);
        }

        public async Task<List<SolicitudSalidaCapturaDto>> UploadCapturasToTrayecto(int trayectoId, IEnumerable<(IFormFile File, decimal Monto)> items, int userId)
        {
            var trayecto = await _repo.GetTrayectoForUploadingCapturas(trayectoId, userId)
                ?? throw new AbrilException("No se pueden subir capturas: el trayecto no existe, no te pertenece, no está aprobado, o ya fue rendido.", 404);

            var lista = items?
                .Where(it => it.File != null && it.File.Length > 0)
                .ToList()
                ?? new();
            if (lista.Count == 0)
                throw new AbrilException("No se recibieron capturas.", 400);

            if (lista.Any(it => it.Monto < 0))
                throw new AbrilException("El monto no puede ser negativo.", 400);

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var subidos = new List<(string Url, string? ItemId, string Filename, decimal Monto)>();
            try
            {
                foreach (var it in lista)
                {
                    var f = it.File;
                    var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
                    if (!allowed.Contains(ext))
                        throw new AbrilException($"Tipo de archivo no permitido: {f.FileName}. Solo JPG/PNG/WEBP/GIF.", 400);

                    var safeName = SanitizeFilename(Path.GetFileNameWithoutExtension(f.FileName));
                    var stamp    = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    var filename = $"s{trayecto.SolicitudId}_t{trayecto.Id}_{stamp}_{safeName}{ext}";

                    using var stream = f.OpenReadStream();
                    var result = await _sharePointService.UploadToSharePointLibraryAsync(
                        site:        _site,
                        libraryName: _solicitudSalidasLibraryId,
                        folderPath:  CarpetaCapturas,
                        fileName:    filename,
                        fileStream:  stream,
                        contentType: f.ContentType ?? "application/octet-stream");

                    if (result?.WebUrl is null)
                        throw new AbrilException($"No se pudo subir el archivo {f.FileName}.", 502);

                    subidos.Add((result.WebUrl, result.ItemId, filename, it.Monto));
                }
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló subida de capturas para trayecto {TrayectoId}", trayectoId);
                throw new AbrilException("Error al subir las capturas a SharePoint.", 502);
            }

            return await _repo.InsertCapturas(trayectoId, subidos, userId);
        }

        private static string SanitizeFilename(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "captura";
            var invalid = Path.GetInvalidFileNameChars().Concat(new[] { ' ', '#', '%', '&', '+' }).ToHashSet();
            var clean = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return clean.Length > 60 ? clean.Substring(0, 60) : clean;
        }

        // ── Notificación de aprobación al solicitante ─────────────────────────

        public async Task NotifySolicitanteAprobada(int solicitudId)
        {
            try
            {
                using var ctx = _factory.CreateDbContext();

                // 1. Datos de cabecera + worker + email del solicitante en una query
                var info = await (
                    from s in ctx.GaSolicitudSalida
                    join w in ctx.Worker on s.WorkerId equals w.Id
                    join p in ctx.Person on w.PersonId equals (int?)p.PersonId into pg
                    from p in pg.DefaultIfEmpty()
                    join u in ctx.User on (p != null ? p.UserId : null) equals (int?)u.UserId into ug
                    from u in ug.DefaultIfEmpty()
                    where s.Id == solicitudId
                    select new
                    {
                        s.Id, s.FechaSalida, s.EstadoAprobacionId, s.WorkerId,
                        Nombre = p != null ? (p.FullName ?? "Trabajador") : "Trabajador",
                        Email  = u != null ? u.Email : null,
                    }
                ).FirstOrDefaultAsync();

                if (info == null)
                {
                    _logger.LogWarning("NotifySolicitanteAprobada: solicitud {SolicitudId} no encontrada.", solicitudId);
                    return;
                }
                if (info.EstadoAprobacionId != EstadosSalida.Aprobacion.Aprobado)
                {
                    _logger.LogWarning("NotifySolicitanteAprobada: solicitud {SolicitudId} no está en estado Aprobado (estado actual: {Estado}). Email no enviado.", solicitudId, EstadosSalida.Aprobacion.Nombre(info.EstadoAprobacionId));
                    return;
                }
                if (string.IsNullOrWhiteSpace(info.Email))
                {
                    _logger.LogWarning("NotifySolicitanteAprobada: solicitud {SolicitudId} — el solicitante no tiene email registrado.", solicitudId);
                    return;
                }

                // 2. Trayectos para mostrar en el email
                var trayectos = await ctx.GaSolicitudTrayecto
                    .Where(t => t.SolicitudId == solicitudId)
                    .OrderBy(t => t.Orden)
                    .ToListAsync();

                var resueltos = await ResolveTrayectosForEmailAsync(ctx, trayectos);

                var numeroUsuario = await GetUserSolicitudNumeroAsync(ctx, info.WorkerId, info.Id);

                var body    = BuildEmailAprobacionSolicitante(info.Nombre, numeroUsuario, info.FechaSalida, resueltos);
                var subject = $"Solicitud de salida #{numeroUsuario} APROBADA - {info.FechaSalida:dd/MM/yyyy}";

                var cc = await GetCcRecepcionAsync(ctx);

                await _emailService.SendAsync(
                    to: new List<string> { info.Email },
                    subject: subject,
                    body: body,
                    isHtml: true,
                    cc: cc,
                    bcc: new List<string> { BccRecepcionMonitoreo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email de aprobación al solicitante para solicitud {SolicitudId}", solicitudId);
            }
        }

        private static string BuildEmailAprobacionSolicitante(
            string nombre, int numeroUsuario, DateOnly fechaSalida,
            List<(int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino)> trayectos)
        {
            string esc(string s) => WebUtility.HtmlEncode(s);

            string trayectoBloque((int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino) t)
            {
                var titulo = trayectos.Count > 1 ? $"Trayecto {t.Orden}" : "Trayecto";
                return $@"<div style=""border:1px solid #E2E2E2;border-radius:8px;padding:12px 16px;margin-bottom:10px"">
                    <div style=""font-weight:600;color:#009C87;margin-bottom:6px;font-size:13px"">{esc(titulo)}</div>
                    <table style=""width:100%;border-collapse:collapse;font-size:13px"">
                      <tr><td style=""padding:3px 0;color:#777;width:40%"">Hora de salida</td><td style=""padding:3px 0;color:#222"">{esc(t.HoraSalida)}</td></tr>
                      <tr><td style=""padding:3px 0;color:#777"">Hora de retorno</td><td style=""padding:3px 0;color:#222"">{esc(t.HoraRetorno)}</td></tr>
                      <tr><td style=""padding:3px 0;color:#777"">Motivo</td><td style=""padding:3px 0;color:#222"">{esc(t.Motivo)}</td></tr>
                      <tr><td style=""padding:3px 0;color:#777"">Origen</td><td style=""padding:3px 0;color:#222"">{esc(t.Origen)}</td></tr>
                      <tr><td style=""padding:3px 0;color:#777"">Destino</td><td style=""padding:3px 0;color:#222"">{esc(t.Destino)}</td></tr>
                    </table>
                  </div>";
            }

            var trayectosHtml = string.Concat(trayectos.Select(trayectoBloque));

            return $@"<!DOCTYPE html><html><body style=""font-family:Segoe UI,Arial,sans-serif;background:#f5f5f5;margin:0;padding:24px;color:#222"">
  <div style=""max-width:620px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.06)"">
    <div style=""background:#009C87;padding:20px 24px;color:#fff"">
      <h2 style=""margin:0;font-size:18px"">✓ Tu solicitud fue aprobada</h2>
    </div>
    <div style=""padding:24px"">
      <p style=""margin:0 0 12px"">Hola <b>{esc(nombre)}</b>,</p>
      <p style=""margin:0 0 16px;color:#444;font-size:14px"">
        Tu solicitud de salida <b>#{numeroUsuario}</b> fue <b style=""color:#009C87"">aprobada</b>.
        Recuerda subir las capturas de movilidad (imagen + monto) por cada trayecto para que la rendición pueda procesarse.
      </p>
      <p style=""margin:0 0 16px;color:#777;font-size:13px""><b>Fecha:</b> {esc(fechaSalida.ToString("dd/MM/yyyy"))}{(trayectos.Count > 1 ? $" — {trayectos.Count} trayectos" : "")}</p>
      {trayectosHtml}
      <p style=""color:#999;font-size:12px;margin-top:24px"">Este es un correo automático, no respondas a este mensaje.</p>
    </div>
  </div>
</body></html>";
        }
    }
}
