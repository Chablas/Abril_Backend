using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Alerta;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Shared;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Shared.Constants;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class EmoAlertaService : IEmoAlertaService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmoAlertaService> _logger;

        public EmoAlertaService(
            IDbContextFactory<AppDbContext> factory,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<EmoAlertaService> logger)
        {
            _factory = factory;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<EmoAlertaResultDto> ProcesarAlertas()
        {
            var result = new EmoAlertaResultDto();
            using var ctx = _factory.CreateDbContext();

            var ventanaDias      = _configuration.GetValue<int?>("EmoProgramacion:VentanaDias") ?? 14;
            var diasHabilesAntes = _configuration.GetValue<int?>("EmoProgramacion:DiasHabilesAntes") ?? 4;

            var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5).Date);

            // Ventana amplia en SQL para cubrir cualquier combinación de días hábiles antes
            // del vencimiento (con feriados de por medio el tramo en calendario se estira).
            var ventanaInicio = hoy.AddDays(1);
            var ventanaFin = hoy.AddDays(ventanaDias);

            // Feriados/días no laborables para que el cálculo de días hábiles los salte.
            var cal = await CargarCalendarioHabilAsync(ctx);

            // Bug 1 fix: COALESCE(fecha_vencimiento_calculada, fecha_vencimiento)
            // Bug 3 fix: JOIN con SsEmoTipo para leer VigenciaMeses real
            // Bug 2 fix: JOIN con Worker para determinar calendario hábil en memoria
            var candidatosRaw = await (
                from e in ctx.WorkerEmo
                join w in ctx.Worker on e.WorkerId equals w.Id
                join t in ctx.SsEmoTipo on e.TipoEmoId equals t.Id into tj
                from t in tj.DefaultIfEmpty()
                where e.Activo
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) != null
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) >= ventanaInicio
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) <= ventanaFin
                select new
                {
                    Emo = e,
                    Worker = w,
                    TipoEmo = t,
                    WorkerNombre = w.Person != null ? w.Person.FullName : null,
                    WorkerDni = w.Person != null ? w.Person.DocumentIdentityCode : null
                }
            ).AsNoTracking().ToListAsync();

            // Disparar alerta solo cuando hoy == fechaVenc - N días hábiles (feriados excluidos)
            var candidatos = candidatosRaw
                .Where(x =>
                {
                    var fv = (x.Emo.FechaVencimientoCalculada ?? x.Emo.FechaVencimiento)!.Value;
                    var fechaAlerta = cal.RestarDiasHabiles(fv, diasHabilesAntes, EsCalendarioOficina(x.Worker));
                    return fechaAlerta == hoy;
                })
                .ToList();

            if (candidatos.Count == 0) return result;

            var emoIds = candidatos.Select(x => x.Emo.Id).ToList();
            var workerIds = candidatos.Select(x => x.Worker.Id).Distinct().ToList();

            var vinculaciones = await ctx.WorkerVinculacion
                .AsNoTracking()
                .Where(v => workerIds.Contains(v.WorkerId) && (v.FechaFin == null || v.FechaFin >= hoy))
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .ToListAsync();

            var vinculacionPorWorker = vinculaciones
                .GroupBy(v => v.WorkerId)
                .ToDictionary(g => g.Key, g => g.First());

            var proyectoIds = vinculaciones
                .Where(v => v.ProyectoId.HasValue)
                .Select(v => v.ProyectoId!.Value)
                .Distinct().ToList();

            var empresaIds = vinculaciones
                .Where(v => v.EmpresaId.HasValue)
                .Select(v => v.EmpresaId!.Value)
                .Distinct().ToList();

            var proyectosDict = await ctx.Project
                .AsNoTracking()
                .Where(p => proyectoIds.Contains(p.ProjectId))
                .ToDictionaryAsync(p => p.ProjectId);

            var empresasDict = await ctx.Contributor
                .AsNoTracking()
                .Where(em => empresaIds.Contains(em.ContributorId))
                .ToDictionaryAsync(em => em.ContributorId);

            var alertasYaEnviadasHoy = await ctx.SsAlertaEmo
                .AsNoTracking()
                .Where(a => a.EmoId != null && emoIds.Contains(a.EmoId.Value) && a.FechaAlerta == hoy)
                .Select(a => a.EmoId!.Value)
                .ToListAsync();
            var alertasSet = new HashSet<int>(alertasYaEnviadasHoy);

            foreach (var c in candidatos)
            {
                result.TotalProcesados++;

                if (alertasSet.Contains(c.Emo.Id))
                {
                    result.Detalles.Add($"EMO {c.Emo.Id} — alerta ya registrada hoy. Omitido.");
                    continue;
                }

                vinculacionPorWorker.TryGetValue(c.Worker.Id, out var vinculacion);

                Project? proyecto = null;
                if (vinculacion?.ProyectoId.HasValue == true)
                    proyectosDict.TryGetValue(vinculacion.ProyectoId.Value, out proyecto);

                Contributor? empresa = null;
                if (vinculacion?.EmpresaId.HasValue == true)
                    empresasDict.TryGetValue(vinculacion.EmpresaId.Value, out empresa);

                var destinatarios = BuildDestinatarios(c.Worker, proyecto);
                if (destinatarios.Count == 0)
                {
                    result.Detalles.Add($"EMO {c.Emo.Id} ({c.WorkerNombre}) — sin destinatarios. Omitido.");
                    continue;
                }

                // Bug 3 fix: leer vigencia real de SsEmoTipo.VigenciaMeses
                var vigenciaMeses = c.TipoEmo?.VigenciaMeses ?? 12;
                var vigenciaTexto = vigenciaMeses % 12 == 0
                    ? $"{vigenciaMeses / 12} año(s)"
                    : $"{vigenciaMeses} mes(es)";

                var fv = (c.Emo.FechaVencimientoCalculada ?? c.Emo.FechaVencimiento)!.Value;
                var subject = $"Vencimiento de EMO - {c.WorkerNombre} - {fv:yyyy-MM-dd}";
                var body = BuildBody(c.Worker, c.WorkerNombre, c.WorkerDni, c.Emo, fv, proyecto, empresa, vigenciaTexto);

                try
                {
                    await _emailService.SendAsync(
                        to: destinatarios,
                        subject: subject,
                        body: body,
                        isHtml: true,
                        fromOverride: SaludOcupacionalEmailConstants.Remitente);

                    ctx.SsAlertaEmo.Add(new SsAlertaEmo
                    {
                        WorkerId = c.Worker.Id,
                        EmoId = c.Emo.Id,
                        TipoAlerta = "VENCIMIENTO",
                        FechaAlerta = hoy,
                        EnviadoEmail = true,
                        FechaEnvio = DateTimeOffset.UtcNow,
                        Destinatarios = string.Join(",", destinatarios),
                        CreatedAt = DateTimeOffset.UtcNow
                    });

                    result.TotalEnviados++;
                    result.Detalles.Add($"EMO {c.Emo.Id} ({c.WorkerNombre}) — enviado a {destinatarios.Count} destinatario(s).");
                }
                catch (Exception ex)
                {
                    result.TotalErrores++;
                    _logger.LogError(ex, "Error enviando alerta de EMO {EmoId}", c.Emo.Id);
                    result.Detalles.Add($"EMO {c.Emo.Id} ({c.WorkerNombre}) — error al enviar: {ex.Message}");
                }
            }

            await ctx.SaveChangesAsync();
            return result;
        }

        private static async Task<CalendarioHabil> CargarCalendarioHabilAsync(AppDbContext ctx)
        {
            var feriados = await ctx.Holiday
                .AsNoTracking()
                .Where(h => h.Active && h.State)
                .Select(h => new { h.HolidayDate, h.RecurringYearly })
                .ToListAsync();

            return new CalendarioHabil(feriados.Select(f => (f.HolidayDate, f.RecurringYearly)));
        }

        // Casa + Staff u Oficina Central → Mon-Fri (excluye sáb y dom).
        // Contratista u Obra → Mon-Sat (excluye solo dom).
        private static bool EsCalendarioOficina(Worker worker)
        {
            return string.Equals(worker.ContrataCasa, "Casa", StringComparison.OrdinalIgnoreCase)
                && ObraOficinaStaffIds.StaffUOficinaCentral.Contains(worker.ObraOficinaStaffId ?? 0);
        }

        private static List<string> BuildDestinatarios(Worker worker, Project? proyecto)
        {
            var raw = new List<string?> { worker.EmailCorporativo };

            // Casa y Staff: agregar también email corporativo
            if (string.Equals(worker.ContrataCasa, "Casa", StringComparison.OrdinalIgnoreCase)
                || string.Equals(worker.ContrataCasa, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                raw.Add(worker.EmailCorporativo);
            }

            if (proyecto != null)
            {
                raw.Add(proyecto.EmailResidente);
                raw.Add(proyecto.EmailResponsable);
                raw.Add(proyecto.EmailRrhh);
                raw.Add(proyecto.EmailCoordSsoma);
                raw.Add(proyecto.EmailCoordAdmin);
            }

            return raw
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildBody(
            Worker worker,
            string? workerNombre,
            string? workerDni,
            WorkerEmo emo,
            DateOnly fechaVencimiento,
            Project? proyecto,
            Contributor? empresa,
            string vigenciaTexto)
        {
            return $@"
            <p>Estimados,</p>

            <p>
                Se notifica el <strong>próximo vencimiento del Examen Médico Ocupacional (EMO)</strong>
                del siguiente trabajador:
            </p>

            <table style='border-collapse: collapse; font-family: Arial, sans-serif; font-size: 14px;'>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Trabajador</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{workerNombre}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>DNI</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{workerDni}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Ocupación</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{worker.Ocupacion}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Modalidad</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{ObraOficinaStaffIds.Nombre(worker.ObraOficinaStaffId)}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Empresa</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{empresa?.ContributorName ?? "—"}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Proyecto</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{proyecto?.ProjectDescription ?? "—"}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Fecha del EMO</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{emo.FechaEmo:dd/MM/yyyy}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Fecha de vencimiento</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px; color: #b00020;'><strong>{fechaVencimiento:dd/MM/yyyy}</strong></td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Vigencia de EMO</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{vigenciaTexto}</td>
                </tr>
            </table>

            <p>
                Por favor coordinar la programación del nuevo EMO antes de la fecha de vencimiento
                para mantener la habilitación del trabajador.
            </p>

            <p style='font-size: 12px; color: #666;'>
                Esta alerta se genera automáticamente cuando un EMO está próximo a vencer.
            </p>
            ";
        }
    }
}
