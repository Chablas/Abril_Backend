using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Features.Habilitacion.Application.Dtos;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Helpers;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Application.Services
{
    public class RetiroAutomaticoService : IRetiroAutomaticoService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RetiroAutomaticoService> _logger;

        public RetiroAutomaticoService(
            IDbContextFactory<AppDbContext> factory,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<RetiroAutomaticoService> logger)
        {
            _factory = factory;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<RetiroAutomaticoResultDto> EjecutarAsync()
        {
            var result = new RetiroAutomaticoResultDto();
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var hoyDt = hoy.ToDateTime(TimeOnly.MinValue);

            using var ctx = _factory.CreateDbContext();

            // Cargar entregables vencidos con vigencia pasada e items que requieren vigencia
            var habVencidos = await ctx.SsHabTrabajador
                .Where(h => (h.Estado == "Vencido" || h.Estado == "Falta") &&
                            h.Vigencia != null && h.Vigencia.Value < hoyDt)
                .Join(ctx.SsItemTrabajador.Where(i => i.RequiereVigencia && i.Activo),
                      h => h.ItemId, i => i.Id,
                      (h, i) => new { h.WorkerId, h.Vigencia, i.Nombre })
                .ToListAsync();

            if (habVencidos.Count == 0) return result;

            var workerEntregables = habVencidos
                .GroupBy(x => x.WorkerId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        VigenciaMasAntigua = DateOnly.FromDateTime(g.Min(x => x.Vigencia!.Value)),
                        Nombres = g.Select(x => x.Nombre).Distinct().ToList()
                    });

            var workerIds = workerEntregables.Keys.ToList();

            var workers = await ctx.Worker
                .Where(w => workerIds.Contains(w.Id) && w.Estado != "RETIRADO")
                .Include(w => w.Person)
                .ToListAsync();

            if (workers.Count == 0) return result;

            var activeWorkerIds = workers.Select(w => w.Id).ToList();

            var vinculacionesActivas = await ctx.WorkerVinculacion
                .Where(v => activeWorkerIds.Contains(v.WorkerId) && v.FechaFin == null)
                .ToListAsync();

            var vinculacionDict = vinculacionesActivas
                .GroupBy(v => v.WorkerId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id).First());

            var empresaIds = vinculacionesActivas
                .Where(v => v.EmpresaId.HasValue).Select(v => v.EmpresaId!.Value).Distinct().ToList();
            var workerContribIds = workers
                .Where(w => w.ContributorId.HasValue).Select(w => w.ContributorId!.Value).Distinct().ToList();
            var allContribIds = empresaIds.Union(workerContribIds).Distinct().ToList();

            var contributorsDict = await ctx.Contributor
                .Where(c => allContribIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId);

            // Clasificar workers en: retirar hoy vs aviso (mañana)
            var workersARetirar = new List<WorkerClasificado>();
            var workersAviso = new List<WorkerClasificado>();

            foreach (var worker in workers)
            {
                try
                {
                    if (!workerEntregables.TryGetValue(worker.Id, out var entregablesInfo)) continue;

                    vinculacionDict.TryGetValue(worker.Id, out var vinc);
                    var empresaId = vinc?.EmpresaId;

                    bool esCasa = worker.ContrataCasa == "Casa";
                    if (!esCasa && empresaId.HasValue &&
                        contributorsDict.TryGetValue(empresaId.Value, out var empContrib))
                        esCasa = empContrib.EsAbril;
                    if (!esCasa && worker.ContributorId.HasValue &&
                        contributorsDict.TryGetValue(worker.ContributorId.Value, out var workerContrib))
                        esCasa = workerContrib.EsAbril;

                    int diasGracia = esCasa ? 7 : 2;
                    int diasEnMora = hoy.DayNumber - entregablesInfo.VigenciaMasAntigua.DayNumber;

                    var nombre = worker.Person?.FullName ?? $"Worker #{worker.Id}";
                    var dni = worker.Person?.DocumentIdentityCode;

                    var clasificado = new WorkerClasificado(
                        worker.Id, nombre, dni, empresaId, esCasa,
                        diasGracia, entregablesInfo.Nombres, diasEnMora);

                    if (diasEnMora >= diasGracia)
                        workersARetirar.Add(clasificado);
                    else if (diasEnMora == diasGracia - 1)
                        workersAviso.Add(clasificado);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[RetiroAutomatico] Error clasificando worker {WorkerId}", worker.Id);
                }
            }

            // Ejecutar retiros — contexto separado por worker para que un fallo no afecte a los demás
            foreach (var w in workersARetirar)
            {
                try
                {
                    using var writeCtx = _factory.CreateDbContext();

                    var workerEnt = await writeCtx.Worker.FirstOrDefaultAsync(x => x.Id == w.WorkerId);
                    if (workerEnt == null || workerEnt.Estado == "RETIRADO") continue;

                    workerEnt.Estado = "RETIRADO";
                    workerEnt.FechaRetiro = hoy;
                    workerEnt.UpdatedAt = DateTimeOffset.UtcNow;

                    var vinc = await writeCtx.WorkerVinculacion
                        .Where(v => v.WorkerId == w.WorkerId && v.FechaFin == null)
                        .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                        .FirstOrDefaultAsync();

                    if (vinc != null) { vinc.FechaFin = hoy; vinc.UpdatedAt = DateTimeOffset.UtcNow; }

                    var asignaciones = await writeCtx.WorkerProyecto
                        .Where(wp => wp.WorkerId == w.WorkerId && wp.FechaFin == null)
                        .ToListAsync();

                    var nowOffset = DateTimeOffset.UtcNow;
                    foreach (var ap in asignaciones) { ap.FechaFin = hoy; ap.UpdatedAt = nowOffset; }

                    writeCtx.WorkerEvento.Add(new WorkerEvento
                    {
                        WorkerId = w.WorkerId,
                        TipoEvento = WorkerTipoEvento.Baja,
                        Descripcion = "Retiro automático por documentación vencida.",
                        ProyectoAnteriorId = vinc?.ProyectoId,
                        EmpresaAnteriorId = vinc?.EmpresaId,
                        CreatedAt = DateTime.UtcNow
                    });

                    writeCtx.SsRetiroAutomaticoLog.Add(new SsRetiroAutomaticoLog
                    {
                        WorkerId = w.WorkerId,
                        EmpresaId = w.EmpresaId,
                        Motivo = "Documentación vencida",
                        EjecutadoEn = DateTimeOffset.UtcNow,
                        TipoRetiro = "AUTOMATICO",
                        DiasGracia = w.DiasGracia,
                        EntregablesVencidos = string.Join(", ", w.EntregablesVencidos)
                    });

                    await writeCtx.SaveChangesAsync();

                    result.TotalRetirados++;
                    result.Detalles.Add(
                        $"[RETIRADO] {w.Nombre} (ID:{w.WorkerId}) Empresa:{w.EmpresaId} Docs:{string.Join(", ", w.EntregablesVencidos)}");

                    _logger.LogInformation(
                        "[RetiroAutomatico] Worker {WorkerId} retirado. Empresa={EmpresaId}",
                        w.WorkerId, w.EmpresaId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RetiroAutomatico] Error retirando worker {WorkerId}", w.WorkerId);
                }
            }

            foreach (var w in workersAviso)
            {
                result.TotalAvisados++;
                result.Detalles.Add(
                    $"[AVISO] {w.Nombre} (ID:{w.WorkerId}) Se retirará mañana. Docs:{string.Join(", ", w.EntregablesVencidos)}");
            }

            // Enviar un email por empresa (agrupando retirados + avisos de esa empresa)
            await EnviarEmailsPorEmpresaAsync(workersARetirar, workersAviso, contributorsDict, hoy);

            return result;
        }

        private async Task EnviarEmailsPorEmpresaAsync(
            List<WorkerClasificado> retirados,
            List<WorkerClasificado> avisos,
            Dictionary<int, Contributor> contributorsDict,
            DateOnly hoy)
        {
            var grupos = retirados.Concat(avisos)
                .GroupBy(w => (w.EsCasa, w.EmpresaId))
                .ToList();

            foreach (var grupo in grupos)
            {
                try
                {
                    var (esCasa, empresaId) = grupo.Key;

                    var retiradosGrupo = retirados
                        .Where(w => w.EsCasa == esCasa && w.EmpresaId == empresaId).ToList();
                    var avisosGrupo = avisos
                        .Where(w => w.EsCasa == esCasa && w.EmpresaId == empresaId).ToList();

                    string nombreEmpresa = esCasa ? "Abril" :
                        (empresaId.HasValue && contributorsDict.TryGetValue(empresaId.Value, out var c))
                        ? c.ContributorName : "Empresa desconocida";

                    List<string> emails = [];

                    if (esCasa)
                    {
                        var config = _configuration["RetiroAutomatico:SsomaAdminEmails"];
                        if (!string.IsNullOrWhiteSpace(config))
                            emails = config.Split(',')
                                .Select(e => e.Trim())
                                .Where(e => !string.IsNullOrEmpty(e))
                                .ToList();

                        if (emails.Count == 0)
                        {
                            var abrilEmail = contributorsDict.Values
                                .FirstOrDefault(x => x.EsAbril)?.EmailAdministrador;
                            if (!string.IsNullOrEmpty(abrilEmail))
                                emails = [abrilEmail];
                        }
                    }
                    else if (empresaId.HasValue)
                    {
                        using var emailCtx = _factory.CreateDbContext();
                        var usuarioEmails = await emailCtx.SsContratistaUsuarios
                            .Where(cu => cu.ContractorId == empresaId.Value && cu.Activo)
                            .Join(emailCtx.User,
                                cu => cu.UserId, u => u.UserId,
                                (cu, u) => u.Email)
                            .Where(e => !string.IsNullOrEmpty(e))
                            .ToListAsync();

                        emails = usuarioEmails.Where(e => e != null).Select(e => e!).ToList();

                        if (emails.Count == 0 &&
                            contributorsDict.TryGetValue(empresaId.Value, out var empContrib) &&
                            !string.IsNullOrEmpty(empContrib.EmailAdministrador))
                            emails = [empContrib.EmailAdministrador];
                    }

                    if (emails.Count == 0)
                    {
                        _logger.LogWarning(
                            "[RetiroAutomatico] Sin destinatarios para empresa '{Empresa}' (id={EmpresaId})",
                            nombreEmpresa, empresaId);
                        continue;
                    }

                    var asunto = $"Retiro automático de trabajadores — {nombreEmpresa} — {hoy:dd/MM/yyyy}";
                    var body = BuildEmailHtml(nombreEmpresa, retiradosGrupo, avisosGrupo, hoy);

                    try
                    {
                        await _emailService.SendAsync(emails, asunto, body, isHtml: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "[RetiroAutomatico] Error enviando email para empresa '{Empresa}'", nombreEmpresa);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[RetiroAutomatico] Error procesando grupo de emails {Key}", grupo.Key);
                }
            }
        }

        private static string BuildEmailHtml(
            string nombreEmpresa,
            List<WorkerClasificado> retirados,
            List<WorkerClasificado> avisos,
            DateOnly hoy)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"<html><body style=\"font-family:Arial,sans-serif;color:#333\">");
            sb.Append($"<h2>Retiro automático de trabajadores — {nombreEmpresa} — {hoy:dd/MM/yyyy}</h2>");

            sb.Append("<h3 style=\"color:#c00\">RETIRADOS HOY</h3>");
            sb.Append(retirados.Count == 0 ? "<p>Ninguno</p>" : BuildTabla(retirados));

            sb.Append("<h3 style=\"color:#e65c00\">SE RETIRARÁN MAÑANA (AVISO)</h3>");
            sb.Append(avisos.Count == 0 ? "<p>Ninguno</p>" : BuildTabla(avisos));

            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static string BuildTabla(List<WorkerClasificado> workers)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;width:100%\">");
            sb.Append("<thead style=\"background:#f0f0f0\"><tr>");
            sb.Append("<th>Trabajador</th><th>DNI</th><th>Documentos Vencidos</th><th>Días de mora</th>");
            sb.Append("</tr></thead><tbody>");
            foreach (var w in workers)
            {
                sb.Append("<tr>");
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(w.Nombre)}</td>");
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(w.Dni ?? "-")}</td>");
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(string.Join(", ", w.EntregablesVencidos))}</td>");
                sb.Append($"<td>{w.DiasEnMora}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private record WorkerClasificado(
            int WorkerId,
            string Nombre,
            string? Dni,
            int? EmpresaId,
            bool EsCasa,
            int DiasGracia,
            List<string> EntregablesVencidos,
            int DiasEnMora);
    }
}
