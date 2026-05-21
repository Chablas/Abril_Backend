using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
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

        public SolicitudSalidaService(
            ISolicitudSalidaRepository repo,
            IApproverResolver approverResolver,
            ISolicitudSalidaTokenService tokenService,
            IEmailService emailService,
            IDbContextFactory<AppDbContext> factory,
            IConfiguration configuration,
            ILogger<SolicitudSalidaService> logger)
        {
            _repo             = repo;
            _approverResolver = approverResolver;
            _tokenService     = tokenService;
            _emailService     = emailService;
            _factory          = factory;
            _configuration    = configuration;
            _logger           = logger;
        }

        public async Task<SolicitudSalidaFormDataDto> GetFormData(int? userId)
        {
            var data = await _repo.GetFormData();

            // Best-effort: resolver el aprobador para que el frontend lo muestre como info
            if (userId.HasValue)
            {
                try
                {
                    using var ctx = _factory.CreateDbContext();
                    var solicitante = await ctx.Worker
                        .Where(w => w.Person != null && w.Person.UserId == userId.Value)
                        .FirstOrDefaultAsync();
                    if (solicitante != null)
                        data.AprobadorEmail = await _approverResolver.ResolveApproverEmailAsync(solicitante);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo resolver el aprobador para userId {UserId}", userId);
                }
            }

            return data;
        }

        public Task<List<SolicitudSalidaListItemDto>> GetByUserId(int userId) => _repo.GetByUserId(userId);

        public async Task<int> Create(SolicitudSalidaCreateDto dto, int? userId)
        {
            if (dto.HoraRetorno.HasValue && dto.HoraRetorno.Value <= dto.HoraSalida)
                throw new AbrilException("La hora de retorno debe ser posterior a la hora de salida.", 400);

            var tieneMotivoId    = dto.MotivoId.HasValue;
            var tieneMotivoLibre = !string.IsNullOrWhiteSpace(dto.MotivoLibre);
            if (!tieneMotivoId && !tieneMotivoLibre)
                throw new AbrilException("Debe indicar un motivo.", 400);

            var tieneOrigenId    = dto.LugarOrigenId.HasValue;
            var tieneOrigenLibre = !string.IsNullOrWhiteSpace(dto.LugarOrigenLibre);
            if (!tieneOrigenId && !tieneOrigenLibre)
                throw new AbrilException("Debe indicar un lugar de origen.", 400);

            var tieneDestinoId    = dto.LugarDestinoId.HasValue;
            var tieneDestinoLibre = !string.IsNullOrWhiteSpace(dto.LugarDestinoLibre);
            if (!tieneDestinoId && !tieneDestinoLibre)
                throw new AbrilException("Debe indicar un lugar de destino.", 400);

            if (tieneOrigenId && tieneDestinoId && dto.LugarOrigenId == dto.LugarDestinoId)
                throw new AbrilException("El lugar de origen y el lugar de destino no pueden ser iguales.", 400);

            // 1. Persistir
            var (id, solicitante) = await _repo.Create(dto, userId);

            // 2. Resolver aprobador + enviar email (best-effort: no romper la creación si falla)
            try
            {
                var aprobadorEmail = await _approverResolver.ResolveApproverEmailAsync(solicitante);
                if (!string.IsNullOrWhiteSpace(aprobadorEmail))
                {
                    await _repo.SetAprobadorEmail(id, aprobadorEmail);
                    await SendNotificacionAprobadorAsync(id, aprobadorEmail, solicitante, dto);
                }
                else
                {
                    _logger.LogWarning(
                        "No se pudo resolver aprobador para solicitud {SolicitudId} (worker {WorkerId} - area={Area} subarea={Subarea} categoria={Categoria})",
                        id, solicitante.Id, solicitante.Area, solicitante.Subarea, solicitante.Categoria);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email de aprobación para solicitud {SolicitudId}", id);
            }

            return id;
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

        private async Task SendNotificacionAprobadorAsync(int solicitudId, string aprobadorEmail, Worker solicitante, SolicitudSalidaCreateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            // Resolver nombre del solicitante + motivo + lugares para el email
            var nombre = await ctx.Person
                .Where(p => p.PersonId == solicitante.PersonId)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync() ?? "Trabajador";

            string motivo;
            if (dto.MotivoId.HasValue)
            {
                motivo = await ctx.GaMotivoSalida
                    .Where(m => m.Id == dto.MotivoId.Value)
                    .Select(m => m.Descripcion)
                    .FirstOrDefaultAsync() ?? "—";
            }
            else motivo = dto.MotivoLibre ?? "—";

            var origen  = await ResolveLugarDisplay(ctx, dto.LugarOrigenId, dto.LugarOrigenLibre);
            var destino = await ResolveLugarDisplay(ctx, dto.LugarDestinoId, dto.LugarDestinoLibre);

            var tokenAprobar  = _tokenService.Generate(solicitudId, SolicitudSalidaAction.Aprobar,  TimeSpan.FromDays(7));
            var tokenRechazar = _tokenService.Generate(solicitudId, SolicitudSalidaAction.Rechazar, TimeSpan.FromDays(7));

            var backendUrl = (_configuration["BackendSettings:PublicUrl"] ?? "http://localhost:5236").TrimEnd('/');
            var basePath   = "/api/v1/gestion-administrativa/solicitud-salidas";
            var urlAprobar  = $"{backendUrl}{basePath}/aprobar?token={WebUtility.UrlEncode(tokenAprobar)}";
            var urlRechazar = $"{backendUrl}{basePath}/rechazar?token={WebUtility.UrlEncode(tokenRechazar)}";

            var body = BuildEmailBody(nombre, dto, motivo, origen, destino, urlAprobar, urlRechazar);
            var subject = $"Solicitud de salida - {nombre} - {dto.FechaSalida:dd/MM/yyyy}";

            await _emailService.SendAsync(
                to: new List<string> { aprobadorEmail },
                subject: subject,
                body: body,
                isHtml: true);
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
            string nombre, SolicitudSalidaCreateDto dto,
            string motivo, string origen, string destino,
            string urlAprobar, string urlRechazar)
        {
            string row(string label, string value) =>
                $@"<tr><td style=""padding:6px 12px;color:#777"">{label}</td><td style=""padding:6px 12px;color:#222;font-weight:500"">{WebUtility.HtmlEncode(value)}</td></tr>";

            var horaRetorno = dto.HoraRetorno.HasValue ? dto.HoraRetorno.Value.ToString("HH:mm") : "Sin retorno";

            return $@"<!DOCTYPE html><html><body style=""font-family:Segoe UI,Arial,sans-serif;background:#f5f5f5;margin:0;padding:24px;color:#222"">
  <div style=""max-width:600px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.06)"">
    <div style=""background:#64BC04;padding:20px 24px;color:#fff"">
      <h2 style=""margin:0;font-size:18px"">Nueva solicitud de salida</h2>
    </div>
    <div style=""padding:24px"">
      <p style=""margin:0 0 16px""><b>{WebUtility.HtmlEncode(nombre)}</b> ha registrado una solicitud de salida que requiere tu aprobación:</p>
      <table style=""width:100%;border-collapse:collapse;font-size:14px;margin-bottom:24px"">
        {row("Fecha",          dto.FechaSalida.ToString("dd/MM/yyyy"))}
        {row("Hora de salida", dto.HoraSalida.ToString("HH:mm"))}
        {row("Hora de retorno",horaRetorno)}
        {row("Motivo",         motivo)}
        {row("Origen",         origen)}
        {row("Destino",        destino)}
      </table>
      <div style=""text-align:center"">
        <a href=""{urlAprobar}"" style=""display:inline-block;background:#009C87;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;margin:0 8px;font-weight:600"">Aprobar</a>
        <a href=""{urlRechazar}"" style=""display:inline-block;background:#D30000;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;margin:0 8px;font-weight:600"">Rechazar</a>
      </div>
      <p style=""color:#999;font-size:12px;margin-top:24px"">Los enlaces son válidos por 7 días.</p>
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
    }
}
