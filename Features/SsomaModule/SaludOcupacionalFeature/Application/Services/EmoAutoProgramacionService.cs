using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Alerta;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class EmoAutoProgramacionService : IEmoAutoProgramacionService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IEmailService _emailService;
        private readonly ILogger<EmoAutoProgramacionService> _logger;

        public EmoAutoProgramacionService(
            IDbContextFactory<AppDbContext> factory,
            IEmailService emailService,
            ILogger<EmoAutoProgramacionService> logger)
        {
            _factory = factory;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<EmoAutoProgramacionResultDto> ProcesarAutoProgramacion()
        {
            var result = new EmoAutoProgramacionResultDto();
            using var ctx = _factory.CreateDbContext();

            var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5).Date);
            var ventanaFin = hoy.AddDays(6);

            var candidatosRaw = await (
                from e in ctx.WorkerEmo
                join w in ctx.Worker on e.WorkerId equals w.Id
                join t in ctx.SsEmoTipo on e.TipoEmoId equals t.Id
                join v in ctx.WorkerVinculacion on w.Id equals v.WorkerId
                join contrib in ctx.Contributor on v.EmpresaId equals contrib.ContributorId
                where e.Activo
                    && t.RequiereNuevo
                    && t.VigenciaMeses != null
                    && v.FechaFin == null
                    && contrib.EsAbril
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) != null
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) >= hoy.AddDays(1)
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) <= ventanaFin
                select new
                {
                    Emo = e,
                    Worker = w,
                    TipoEmo = t,
                    Vinculacion = v,
                    WorkerNombre = w.Person != null ? w.Person.FullName : null
                }
            ).AsNoTracking().ToListAsync();

            if (candidatosRaw.Count == 0) return result;

            var candidatos = candidatosRaw
                .GroupBy(x => (x.Emo.WorkerId, x.Emo.TipoEmoId))
                .Select(g => g.OrderByDescending(x => x.Vinculacion.CreatedAt).First())
                .ToList();

            var workerIds = candidatos.Select(x => x.Emo.WorkerId).Distinct().ToList();

            var programacionesExistentes = await ctx.SsProgramacionEmo
                .AsNoTracking()
                .Where(p =>
                    workerIds.Contains(p.WorkerId)
                    && p.FechaProgramada >= hoy
                    && p.Estado != "Completado"
                    && p.Estado != "Cancelado"
                    && p.Estado != "Rechazado por Clínica")
                .Select(p => new { p.WorkerId, p.TipoEmoId })
                .ToListAsync();

            var existentesSet = new HashSet<(int, int)>(
                programacionesExistentes.Select(p => (p.WorkerId, p.TipoEmoId)));

            var programados = new List<(string Nombre, DateOnly Fecha)>();

            foreach (var c in candidatos)
            {
                try
                {
                    var tipoEmoId = c.Emo.TipoEmoId!.Value;
                    var clave = (c.Emo.WorkerId, tipoEmoId);

                    if (existentesSet.Contains(clave))
                    {
                        result.YaTenianProgramacion++;
                        result.Detalle.Add($"Worker {c.Worker.Id} ({c.WorkerNombre}) / TipoEMO {tipoEmoId} — ya tiene programación activa. Omitido.");
                        continue;
                    }

                    var fv = (c.Emo.FechaVencimientoCalculada ?? c.Emo.FechaVencimiento)!.Value;
                    var esOficina = EsCalendarioOficina(c.Worker);
                    var fechaDesdeVencimiento = RestarDiasHabiles(fv, 4, esOficina);
                    var fechaMinima = SiguienteDiaHabil(SiguienteDiaHabil(hoy, esOficina), esOficina);
                    var fechaProg = fechaDesdeVencimiento > fechaMinima ? fechaDesdeVencimiento : fechaMinima;

                    var nueva = new SsProgramacionEmo
                    {
                        WorkerId          = c.Emo.WorkerId,
                        EmpresaId         = c.Vinculacion.EmpresaId,
                        TipoEmoId         = tipoEmoId,
                        ClinicaId         = 1,
                        FechaProgramada   = fechaProg,
                        Estado            = "Programado",
                        Origen            = "Automatico",
                        Motivo            = "Programación automática por vencimiento de EMO",
                        RegistradoPorId   = null,
                        FechaNotificacion = DateTimeOffset.UtcNow,
                        CreatedAt         = DateTimeOffset.UtcNow,
                        UpdatedAt         = DateTimeOffset.UtcNow
                    };

                    ctx.SsProgramacionEmo.Add(nueva);
                    await ctx.SaveChangesAsync();

                    programados.Add((c.WorkerNombre ?? $"Worker {c.Worker.Id}", fechaProg));

                    result.Procesados++;
                    result.Detalle.Add($"Worker {c.Worker.Id} ({c.WorkerNombre}) / TipoEMO {tipoEmoId} — programado para {fechaProg:yyyy-MM-dd}.");
                }
                catch (Exception ex)
                {
                    result.Errores++;
                    _logger.LogError(ex, "Error procesando auto-programación para Worker {WorkerId}", c.Worker.Id);
                    result.Detalle.Add($"Worker {c.Worker.Id} ({c.WorkerNombre}) — error: {ex.Message}");
                }
            }

            if (programados.Count > 0)
                await EnviarResumenClinicaAsync(ctx, programados);

            return result;
        }

        private async Task EnviarResumenClinicaAsync(
            AppDbContext ctx,
            List<(string Nombre, DateOnly Fecha)> programados)
        {
            try
            {
                const int clinicaId = 1;

                var toRaw = await ctx.SsClinicaEmail.AsNoTracking()
                    .Where(e => e.ClinicaId == clinicaId && e.Activo)
                    .Select(e => e.Email)
                    .ToListAsync();

                var clinica = await ctx.SsClinica.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == clinicaId);

                if (toRaw.Count == 0 && clinica?.Email is not null)
                    toRaw.Add(clinica.Email);

                var to = toRaw
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (to.Count == 0) return;

                var filas = string.Join("", programados
                    .OrderBy(p => p.Fecha)
                    .Select(p => $@"
                <tr>
                    <td style='border:1px solid #ddd;padding:8px;'>{p.Nombre}</td>
                    <td style='border:1px solid #ddd;padding:8px;text-align:center;'>{p.Fecha:dd/MM/yyyy}</td>
                </tr>"));

                var body = $@"
            <p>Estimados,</p>
            <p>Se han programado automáticamente los siguientes <strong>{programados.Count} Exámenes Médicos Ocupacionales (EMO)</strong> para los próximos días:</p>
            <table style='border-collapse:collapse;font-family:Arial,sans-serif;font-size:14px;'>
                <thead>
                    <tr>
                        <th style='border:1px solid #ddd;padding:8px;background:#f3f4f6;'>Trabajador</th>
                        <th style='border:1px solid #ddd;padding:8px;background:#f3f4f6;'>Fecha programada</th>
                    </tr>
                </thead>
                <tbody>{filas}</tbody>
            </table>
            <p style='margin-top:16px;'>Por favor revisar y confirmar cada programación en el sistema.</p>
            <p style='font-size:12px;color:#666;margin-top:24px;'>
                Esta notificación se generó automáticamente por el sistema Abril.
            </p>";

                var subject = $"[PRUEBAS - NO RESPONDER] [EMO Programados] {programados.Count} trabajadores — {DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)):dd/MM/yyyy}";

                await _emailService.SendAsync(
                    to: to,
                    subject: subject,
                    body: body,
                    isHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando resumen de auto-programación a clínica.");
            }
        }

        private static DateOnly RestarDiasHabiles(DateOnly fecha, int dias, bool excluirSabado)
        {
            var resultado = fecha;
            int conteo = 0;
            while (conteo < dias)
            {
                resultado = resultado.AddDays(-1);
                var dow = resultado.DayOfWeek;
                if (dow == DayOfWeek.Sunday) continue;
                if (excluirSabado && dow == DayOfWeek.Saturday) continue;
                conteo++;
            }
            return resultado;
        }

        private static DateOnly SiguienteDiaHabil(DateOnly fecha, bool excluirSabado)
        {
            var resultado = fecha.AddDays(1);
            while (true)
            {
                var dow = resultado.DayOfWeek;
                if (dow == DayOfWeek.Sunday) { resultado = resultado.AddDays(1); continue; }
                if (excluirSabado && dow == DayOfWeek.Saturday) { resultado = resultado.AddDays(1); continue; }
                return resultado;
            }
        }

        private static bool EsCalendarioOficina(Worker worker)
        {
            return string.Equals(worker.ContrataCasa, "Casa", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(worker.ObraOficina, "Staff", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(worker.ObraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase));
        }
    }
}
