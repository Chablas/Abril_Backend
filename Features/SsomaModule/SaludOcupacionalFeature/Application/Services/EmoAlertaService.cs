using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Alerta;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class EmoAlertaService : IEmoAlertaService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IEmailService _emailService;
        private readonly ILogger<EmoAlertaService> _logger;

        public EmoAlertaService(
            IDbContextFactory<AppDbContext> factory,
            IEmailService emailService,
            ILogger<EmoAlertaService> logger)
        {
            _factory = factory;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<EmoAlertaResultDto> ProcesarAlertas()
        {
            var result = new EmoAlertaResultDto();

            using var ctx = _factory.CreateDbContext();

            var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5).Date);
            var inicio = hoy.AddDays(1);
            var fin = hoy.AddDays(5);

            var emos = await ctx.WorkerEmo
                .AsNoTracking()
                .Where(e => e.Activo
                            && e.FechaVencimiento != null
                            && e.FechaVencimiento.Value >= inicio
                            && e.FechaVencimiento.Value <= fin)
                .ToListAsync();

            // Excluir domingos en memoria — DateOnly.DayOfWeek no traduce a SQL portable.
            emos = emos
                .Where(e => e.FechaVencimiento!.Value.DayOfWeek != DayOfWeek.Sunday)
                .ToList();

            if (emos.Count == 0)
            {
                return result;
            }

            var emoIds = emos.Select(e => e.Id).ToList();
            var workerIds = emos.Select(e => e.WorkerId).Distinct().ToList();

            var workers = await ctx.Worker
                .AsNoTracking()
                .Where(w => workerIds.Contains(w.Id))
                .ToListAsync();
            var workersDict = workers.ToDictionary(w => w.Id);

            var vinculaciones = await ctx.WorkerVinculacion
                .AsNoTracking()
                .Where(v => workerIds.Contains(v.WorkerId)
                            && (v.FechaFin == null || v.FechaFin >= hoy))
                .OrderByDescending(v => v.FechaInicio)
                .ToListAsync();

            var vinculacionPorWorker = vinculaciones
                .GroupBy(v => v.WorkerId)
                .ToDictionary(g => g.Key, g => g.First());

            var proyectoIds = vinculaciones
                .Where(v => v.ProyectoId.HasValue)
                .Select(v => v.ProyectoId!.Value)
                .Distinct()
                .ToList();

            var empresaIds = vinculaciones
                .Where(v => v.EmpresaId.HasValue)
                .Select(v => v.EmpresaId!.Value)
                .Distinct()
                .ToList();

            var proyectos = await ctx.Projects
                .AsNoTracking()
                .Where(p => proyectoIds.Contains(p.Id))
                .ToListAsync();
            var proyectosDict = proyectos.ToDictionary(p => p.Id);

            var empresas = await ctx.Empresa
                .AsNoTracking()
                .Where(em => empresaIds.Contains(em.Id))
                .ToListAsync();
            var empresasDict = empresas.ToDictionary(em => em.Id);

            var alertasYaEnviadasHoy = await ctx.SsAlertaEmo
                .AsNoTracking()
                .Where(a => emoIds.Contains(a.EmoId) && a.FechaAlerta == hoy)
                .Select(a => a.EmoId)
                .ToListAsync();
            var alertasSet = new HashSet<int>(alertasYaEnviadasHoy);

            foreach (var emo in emos)
            {
                result.TotalProcesados++;

                if (alertasSet.Contains(emo.Id))
                {
                    result.Detalles.Add($"EMO {emo.Id} — alerta ya registrada hoy. Omitido.");
                    continue;
                }

                if (!workersDict.TryGetValue(emo.WorkerId, out var worker))
                {
                    result.Detalles.Add($"EMO {emo.Id} — worker {emo.WorkerId} no encontrado. Omitido.");
                    continue;
                }

                vinculacionPorWorker.TryGetValue(worker.Id, out var vinculacion);

                Abril_Backend.Infrastructure.Models.Projects? proyecto = null;
                if (vinculacion?.ProyectoId.HasValue == true)
                {
                    proyectosDict.TryGetValue(vinculacion.ProyectoId.Value, out proyecto);
                }

                Abril_Backend.Infrastructure.Models.Empresa? empresa = null;
                if (vinculacion?.EmpresaId.HasValue == true)
                {
                    empresasDict.TryGetValue(vinculacion.EmpresaId.Value, out empresa);
                }

                var destinatarios = BuildDestinatarios(worker.EmailPersonal, proyecto);

                if (destinatarios.Count == 0)
                {
                    result.Detalles.Add($"EMO {emo.Id} ({worker.ApellidoNombre}) — sin destinatarios. Omitido.");
                    continue;
                }

                var vigenciaAnios = string.Equals(worker.ObraOficina?.Trim(), "Oficina Central",
                    StringComparison.OrdinalIgnoreCase) ? 2 : 1;

                var subject = $"Vencimiento de EMO - {worker.ApellidoNombre} - {emo.FechaVencimiento:yyyy-MM-dd}";
                var body = BuildBody(worker, emo, proyecto, empresa, vigenciaAnios);

                try
                {
                    await _emailService.SendAsync(
                        to: destinatarios,
                        subject: subject,
                        body: body,
                        isHtml: true);

                    ctx.SsAlertaEmo.Add(new SsAlertaEmo
                    {
                        WorkerId = worker.Id,
                        EmoId = emo.Id,
                        TipoAlerta = "VENCIMIENTO",
                        FechaAlerta = hoy,
                        EnviadoEmail = true,
                        FechaEnvio = DateTimeOffset.UtcNow,
                        Destinatarios = string.Join(",", destinatarios),
                        CreatedAt = DateTimeOffset.UtcNow
                    });

                    result.TotalEnviados++;
                    result.Detalles.Add($"EMO {emo.Id} ({worker.ApellidoNombre}) — enviado a {destinatarios.Count} destinatario(s).");
                }
                catch (Exception ex)
                {
                    result.TotalErrores++;
                    _logger.LogError(ex, "Error enviando alerta de EMO {EmoId}", emo.Id);
                    result.Detalles.Add($"EMO {emo.Id} ({worker.ApellidoNombre}) — error al enviar: {ex.Message}");
                }
            }

            await ctx.SaveChangesAsync();

            return result;
        }

        private static List<string> BuildDestinatarios(
            string? emailPersonal,
            Abril_Backend.Infrastructure.Models.Projects? proyecto)
        {
            var raw = new[]
            {
                emailPersonal,
                proyecto?.EmailResidente,
                proyecto?.EmailResponsable,
                proyecto?.EmailRrhh,
                proyecto?.EmailCoordSsoma,
                proyecto?.EmailCoordAdmin,
            };

            return raw
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildBody(
            Abril_Backend.Infrastructure.Models.Worker worker,
            Abril_Backend.Infrastructure.Models.WorkerEmo emo,
            Abril_Backend.Infrastructure.Models.Projects? proyecto,
            Abril_Backend.Infrastructure.Models.Empresa? empresa,
            int vigenciaAnios)
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
                    <td style='border: 1px solid #ddd; padding: 8px;'>{worker.ApellidoNombre}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>DNI</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{worker.Dni}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Ocupación</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{worker.Ocupacion}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Modalidad</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{worker.ObraOficina}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Empresa</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{empresa?.RazonSocial ?? "—"}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Proyecto</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{proyecto?.Nombre ?? "—"}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Fecha del EMO</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{emo.FechaEmo:dd/MM/yyyy}</td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Fecha de vencimiento</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px; color: #b00020;'><strong>{emo.FechaVencimiento:dd/MM/yyyy}</strong></td>
                </tr>
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Vigencia de EMO</strong></td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{vigenciaAnios} año(s)</td>
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
