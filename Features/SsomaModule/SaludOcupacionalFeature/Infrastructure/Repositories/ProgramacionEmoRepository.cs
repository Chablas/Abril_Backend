using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class ProgramacionEmoRepository : IProgramacionEmoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProgramacionEmoRepository> _logger;

        public ProgramacionEmoRepository(
            IDbContextFactory<AppDbContext> factory,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<ProgramacionEmoRepository> logger)
        {
            _factory = factory;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<List<ProgramacionListDto>> List(ProgramacionFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            var q =
                from p in ctx.SsProgramacionEmo
                join w in ctx.Worker on p.WorkerId equals w.Id
                join em in ctx.Contributor on p.EmpresaId equals em.ContributorId into ej
                from em in ej.DefaultIfEmpty()
                join t in ctx.SsEmoTipo on p.TipoEmoId equals t.Id into tj
                from t in tj.DefaultIfEmpty()
                join c in ctx.SsClinica on p.ClinicaId equals c.Id into cj
                from c in cj.DefaultIfEmpty()
                join m in ctx.SsMedicoOcupacional on p.MedicoId equals m.Id into mj
                from m in mj.DefaultIfEmpty()
                select new { p, w, em, t, c, m };

            if (filter.FechaDesde.HasValue)
                q = q.Where(x => x.p.FechaProgramada >= filter.FechaDesde.Value);
            if (filter.FechaHasta.HasValue)
                q = q.Where(x => x.p.FechaProgramada <= filter.FechaHasta.Value);
            if (!string.IsNullOrWhiteSpace(filter.Estado))
                q = q.Where(x => x.p.Estado == filter.Estado);
            if (filter.WorkerId.HasValue)
                q = q.Where(x => x.p.WorkerId == filter.WorkerId.Value);
            if (filter.ClinicaId.HasValue)
                q = q.Where(x => x.p.ClinicaId == filter.ClinicaId.Value);

            return await q
                .OrderBy(x => x.p.FechaProgramada)
                .ThenBy(x => x.p.HoraProgramada)
                .Select(x => new ProgramacionListDto
                {
                    Id = x.p.Id,
                    WorkerId = x.p.WorkerId,
                    WorkerNombre = x.w.Person != null ? x.w.Person.FullName : null,
                    WorkerDni = x.w.Person != null ? x.w.Person.DocumentIdentityCode : null,
                    Empresa = x.em != null ? x.em.ContributorName : null,
                    TipoEmo = x.t != null ? x.t.Nombre : null,
                    FechaProgramada = x.p.FechaProgramada,
                    HoraProgramada = x.p.HoraProgramada,
                    Clinica = x.c != null ? x.c.Nombre : null,
                    Medico = x.m != null ? x.m.ApellidoNombre : null,
                    Estado = x.p.Estado,
                    Motivo = x.p.Motivo,
                    EmoResultadoId = x.p.EmoResultadoId,
                    Origen = x.p.Origen,
                    CheckInHora = x.p.CheckInHora,
                    MotivoRechazo = x.p.MotivoRechazo,
                    FechaNotificacion = x.p.FechaNotificacion
                })
                .ToListAsync();
        }

        public async Task<int> Create(ProgramacionCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.Include(w => w.Person).FirstOrDefaultAsync(w => w.Id == dto.WorkerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var ent = new SsProgramacionEmo
            {
                WorkerId = dto.WorkerId,
                EmpresaId = dto.EmpresaId,
                TipoEmoId = dto.TipoEmoId,
                FechaProgramada = dto.FechaProgramada,
                HoraProgramada = dto.HoraProgramada,
                ClinicaId = dto.ClinicaId,
                MedicoId = dto.MedicoId,
                Motivo = dto.Motivo,
                Notas = dto.Notas,
                Origen = string.IsNullOrWhiteSpace(dto.Origen) ? "Manual" : dto.Origen,
                Estado = "Programado",
                RegistradoPorId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            ctx.SsProgramacionEmo.Add(ent);
            await ctx.SaveChangesAsync();

            await EnviarNotificacionCreacionAsync(ctx, ent, worker);

            return ent.Id;
        }

        public async Task Update(int id, ProgramacionUpdateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var ent = await ctx.SsProgramacionEmo.FirstOrDefaultAsync(p => p.Id == id)
                ?? throw new AbrilException("Programación no encontrada.", 404);

            ent.EmpresaId = dto.EmpresaId;
            ent.TipoEmoId = dto.TipoEmoId;
            ent.FechaProgramada = dto.FechaProgramada;
            ent.HoraProgramada = dto.HoraProgramada;
            ent.ClinicaId = dto.ClinicaId;
            ent.MedicoId = dto.MedicoId;
            ent.Motivo = dto.Motivo;
            ent.Notas = dto.Notas;
            ent.EmoResultadoId = dto.EmoResultadoId;
            ent.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task UpdateEstado(int id, string estado, int? emoResultadoId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var ent = await ctx.SsProgramacionEmo.FirstOrDefaultAsync(p => p.Id == id)
                ?? throw new AbrilException("Programación no encontrada.", 404);
            ent.Estado = estado;
            if (emoResultadoId.HasValue) ent.EmoResultadoId = emoResultadoId;
            ent.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task ClinicaAccion(int id, ProgramacionClinicaAccionDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var ent = await ctx.SsProgramacionEmo.FirstOrDefaultAsync(p => p.Id == id)
                ?? throw new AbrilException("Programación no encontrada.", 404);

            switch (dto.Accion.Trim())
            {
                case "Aceptar":
                    ent.Estado = "Aceptado por Clínica";
                    ent.MotivoRechazo = null;
                    break;
                case "Rechazar":
                    ent.Estado = "Rechazado por Clínica";
                    ent.MotivoRechazo = dto.MotivoRechazo;
                    break;
                case "CheckIn":
                    ent.Estado = "En Atención";
                    ent.CheckInHora = dto.CheckInHora ?? TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
                    break;
                case "Completar":
                    ent.Estado = "Completado";
                    if (dto.EmoResultadoId.HasValue) ent.EmoResultadoId = dto.EmoResultadoId;
                    break;
            }

            ent.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        private async Task EnviarNotificacionCreacionAsync(
            AppDbContext ctx,
            SsProgramacionEmo prog,
            Worker worker)
        {
            try
            {
                var esCasa = string.Equals(worker.ContrataCasa, "Casa", StringComparison.OrdinalIgnoreCase);
                var esOficinaCentral = string.Equals(worker.ObraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase);
                var esStaff = esCasa && string.Equals(worker.ObraOficina, "Staff", StringComparison.OrdinalIgnoreCase);
                var esObrero = esCasa && string.Equals(worker.ObraOficina, "Ninguno", StringComparison.OrdinalIgnoreCase);

                if (!esObrero && !esStaff && !esOficinaCentral)
                    return; // Contratista — sin notificación

                // Emails de la clínica desde ss_clinica_emails; fallback a ss_clinicas.email
                var toRaw = new List<string>();
                SsClinica? clinica = null;
                if (prog.ClinicaId.HasValue)
                {
                    toRaw = await ctx.SsClinicaEmail.AsNoTracking()
                        .Where(e => e.ClinicaId == prog.ClinicaId.Value && e.Activo)
                        .Select(e => e.Email)
                        .ToListAsync();
                    clinica = await ctx.SsClinica.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == prog.ClinicaId.Value);
                    if (toRaw.Count == 0 && clinica?.Email is not null)
                        toRaw.Add(clinica.Email);
                }

                var tipoEmo = await ctx.SsEmoTipo.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == prog.TipoEmoId);

                var medico = prog.MedicoId.HasValue
                    ? await ctx.SsMedicoOcupacional.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Id == prog.MedicoId.Value)
                    : null;

                var vinculacion = await ctx.WorkerVinculacion.AsNoTracking()
                    .Where(v => v.WorkerId == worker.Id && v.FechaFin == null)
                    .OrderByDescending(v => v.CreatedAt)
                    .ThenByDescending(v => v.Id)
                    .FirstOrDefaultAsync();

                Project? proyecto = null;
                if (vinculacion?.ProyectoId.HasValue == true)
                    proyecto = await ctx.Project.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.ProjectId == vinculacion.ProyectoId.Value);

                var medOcupacional = _configuration["EmailsArea:MedicinaOcupacional"];
                var gth = _configuration["EmailsArea:GTH"];
                var emoResumenRaw = _configuration["EmoResumen:Destinatarios"];
                var ccSiempre = string.IsNullOrWhiteSpace(emoResumenRaw)
                    ? Enumerable.Empty<string>()
                    : emoResumenRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).AsEnumerable();

                IEnumerable<string?> ccRaw;
                if (esObrero)
                {
                    ccRaw = new[] { proyecto?.EmailResidente, proyecto?.EmailResponsable, medOcupacional };
                }
                else if (esStaff)
                {
                    ccRaw = new[] { worker.EmailPersonal, proyecto?.EmailResidente, proyecto?.EmailResponsable, medOcupacional };
                }
                else // esOficinaCentral
                {
                    var jefaturaEmails = !string.IsNullOrWhiteSpace(worker.Jefatura)
                        ? await ctx.CatJefatura.AsNoTracking()
                            .Where(j => j.Nombre == worker.Jefatura && j.Activo)
                            .Select(j => j.Email)
                            .ToListAsync()
                        : new List<string>();
                    ccRaw = new string?[] { worker.EmailPersonal, gth, medOcupacional }
                        .Concat(jefaturaEmails);
                }

                var to = toRaw
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var cc = ccRaw
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e!.Trim())
                    .Concat(ccSiempre)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (to.Count == 0)
                {
                    _logger.LogWarning("Programación {Id}: sin emails de clínica, no se envía notificación de creación.", prog.Id);
                    return;
                }

                var subject = $"Programación EMO — {worker.Person?.FullName} — {prog.FechaProgramada:dd/MM/yyyy}";
                var body = BuildBodyCreacion(worker, prog, tipoEmo?.Nombre, clinica?.Nombre, medico?.ApellidoNombre, proyecto);

                await _emailService.SendAsync(
                    to: to,
                    subject: subject,
                    body: body,
                    isHtml: true,
                    cc: cc.Count > 0 ? cc : null);

                prog.FechaNotificacion = DateTimeOffset.UtcNow;
                prog.UpdatedAt = DateTimeOffset.UtcNow;
                await ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Programación {Id}: error enviando notificación de creación. La programación ya fue guardada.", prog.Id);
            }
        }

        private static string BuildBodyCreacion(
            Worker worker,
            SsProgramacionEmo prog,
            string? tipoEmoNombre,
            string? clinicaNombre,
            string? medicoNombre,
            Project? proyecto)
        {
            var esCasa = string.Equals(worker.ContrataCasa, "Casa", StringComparison.OrdinalIgnoreCase);
            var esOficinaCentral = string.Equals(worker.ObraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase);
            var esObrero = esCasa && string.Equals(worker.ObraOficina, "Ninguno", StringComparison.OrdinalIgnoreCase);

            var nota = esObrero
                ? "Por favor coordinar con el trabajador para que se presente puntualmente."
                : esOficinaCentral
                    ? "GTH debe notificar al trabajador para que se presente a su EMO."
                    : "El trabajador ha sido notificado. Favor confirmar asistencia.";

            var hora = prog.HoraProgramada.HasValue
                ? prog.HoraProgramada.Value.ToString("HH:mm")
                : "—";

            return $@"
            <p>Estimados,</p>
            <p>Se ha registrado la siguiente <strong>Programación de Examen Médico Ocupacional (EMO)</strong>:</p>

            <table style='border-collapse:collapse;font-family:Arial,sans-serif;font-size:14px;'>
                <tr>
                    <td style='border:1px solid #ddd;padding:8px;'><strong>Trabajador</strong></td>
                    <td style='border:1px solid #ddd;padding:8px;'>{worker.Person?.FullName}</td>
                </tr>
                <tr>
                    <td style='border:1px solid #ddd;padding:8px;'><strong>DNI</strong></td>
                    <td style='border:1px solid #ddd;padding:8px;'>{worker.Person?.DocumentIdentityCode}</td>
                </tr>
                <tr>
                    <td style='border:1px solid #ddd;padding:8px;'><strong>Proyecto</strong></td>
                    <td style='border:1px solid #ddd;padding:8px;'>{proyecto?.ProjectDescription ?? "—"}</td>
                </tr>
                <tr>
                    <td style='border:1px solid #ddd;padding:8px;'><strong>Tipo de EMO</strong></td>
                    <td style='border:1px solid #ddd;padding:8px;'>{tipoEmoNombre ?? "—"}</td>
                </tr>
                <tr>
                    <td style='border:1px solid #ddd;padding:8px;'><strong>Fecha programada</strong></td>
                    <td style='border:1px solid #ddd;padding:8px;'><strong>{prog.FechaProgramada:dd/MM/yyyy}</strong></td>
                </tr>
                <tr>
                    <td style='border:1px solid #ddd;padding:8px;'><strong>Hora</strong></td>
                    <td style='border:1px solid #ddd;padding:8px;'>{hora}</td>
                </tr>
                <tr>
                    <td style='border:1px solid #ddd;padding:8px;'><strong>Clínica</strong></td>
                    <td style='border:1px solid #ddd;padding:8px;'>{clinicaNombre ?? "—"}</td>
                </tr>
                <tr>
                    <td style='border:1px solid #ddd;padding:8px;'><strong>Médico</strong></td>
                    <td style='border:1px solid #ddd;padding:8px;'>{medicoNombre ?? "—"}</td>
                </tr>
            </table>

            <p style='margin-top:16px;'><em>{nota}</em></p>

            <p style='font-size:12px;color:#666;margin-top:24px;'>
                Esta notificación se generó automáticamente al registrar la programación en el sistema Abril.
            </p>";
        }
    }
}
