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

            q = q.Where(x => x.em != null && x.em.EsAbril);

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

            if (dto.FechaProgramada == default)
                throw new AbrilException("La fecha es obligatoria.", 400);

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

            var worker = await ctx.Worker.Include(w => w.Person)
                .FirstOrDefaultAsync(w => w.Id == ent.WorkerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            switch (dto.Accion.Trim())
            {
                case "Aceptar":
                    ent.Estado = "Aceptado por Clínica";
                    ent.MotivoRechazo = null;
                    if (dto.CheckInHora.HasValue) ent.HoraProgramada = dto.CheckInHora.Value;
                    await EnviarNotificacionAceptacionAsync(ctx, ent, worker);
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
                // Solo enviar si tiene clínica asignada
                if (!prog.ClinicaId.HasValue) return;

                var toRaw = await ctx.SsClinicaEmail.AsNoTracking()
                    .Where(e => e.ClinicaId == prog.ClinicaId.Value && e.Activo)
                    .Select(e => e.Email!)
                    .ToListAsync();

                var clinica = await ctx.SsClinica.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == prog.ClinicaId.Value);
                if (toRaw.Count == 0 && clinica?.Email is not null)
                    toRaw.Add(clinica.Email);

                if (toRaw.Count == 0) return;

                var tipoEmo = await ctx.SsEmoTipo.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == prog.TipoEmoId);

                var vinculacion = await ctx.WorkerVinculacion.AsNoTracking()
                    .Where(v => v.WorkerId == worker.Id && v.FechaFin == null)
                    .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                    .FirstOrDefaultAsync();

                Project? proyecto = null;
                if (vinculacion?.ProyectoId.HasValue == true)
                    proyecto = await ctx.Project.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.ProjectId == vinculacion.ProyectoId.Value);

                var to = toRaw.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                var workerNombre = worker.Person?.FullName ?? worker.Id.ToString();
                var fechaStr = prog.FechaProgramada.ToString("dd/MM/yyyy");
                var horaStr = prog.HoraProgramada.HasValue ? prog.HoraProgramada.Value.ToString("HH:mm") : "—";
                var proyectoStr = proyecto?.ProjectDescription ?? "—";
                var tipoStr = tipoEmo?.Nombre ?? "—";
                var clinicaNombre = clinica?.Nombre ?? "—";

                var html = $@"<h2>Nueva programación EMO</h2>
<p>Se ha programado un Examen Médico Ocupacional para el siguiente trabajador:</p>
<table style='border-collapse:collapse;width:100%;max-width:500px'>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Trabajador</td><td style='padding:6px 12px'>{workerNombre}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Tipo EMO</td><td style='padding:6px 12px'>{tipoStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Fecha</td><td style='padding:6px 12px'>{fechaStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Hora</td><td style='padding:6px 12px'>{horaStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Proyecto</td><td style='padding:6px 12px'>{proyectoStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Clínica</td><td style='padding:6px 12px'>{clinicaNombre}</td></tr>
</table>
<p style='margin-top:16px;color:#6b7280;font-size:0.9em'>Por favor confirmar la programación en el sistema.</p>";

                await _emailService.SendAsync(
                    to: to,
                    subject: $"[PRUEBAS - NO RESPONDER] [EMO Programado] {workerNombre} — {fechaStr}",
                    body: html,
                    isHtml: true);

                prog.FechaNotificacion = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar notificación de creación de programación.");
            }
        }

        private async Task EnviarNotificacionAceptacionAsync(
            AppDbContext ctx,
            SsProgramacionEmo prog,
            Worker worker)
        {
            try
            {
                var esCasa = string.Equals(worker.ContrataCasa, "Casa", StringComparison.OrdinalIgnoreCase);
                var esOficinaCentral = esCasa && string.Equals(worker.ObraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase);
                var esStaff = esCasa && string.Equals(worker.ObraOficina, "Staff", StringComparison.OrdinalIgnoreCase);
                var esObrero = esCasa && !esOficinaCentral && !esStaff;

                if (!esCasa) return; // Contratistas: sin notificación en aceptación

                var toRaw = new List<string?>();

                var medOcupacional = _configuration["EmailsArea:MedicinaOcupacional"];
                var gth = _configuration["EmailsArea:GTH"];

                var vinculacion = await ctx.WorkerVinculacion.AsNoTracking()
                    .Where(v => v.WorkerId == worker.Id && v.FechaFin == null)
                    .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                    .FirstOrDefaultAsync();

                Project? proyecto = null;
                if (vinculacion?.ProyectoId.HasValue == true)
                    proyecto = await ctx.Project.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.ProjectId == vinculacion.ProyectoId.Value);

                var adminEmail = worker.ContributorId.HasValue
                    ? await ctx.Contributor.AsNoTracking()
                        .Where(c => c.ContributorId == worker.ContributorId.Value)
                        .Select(c => c.EmailAdministrador)
                        .FirstOrDefaultAsync()
                    : null;

                if (esObrero)
                {
                    // Obrero: administrador del proyecto + SSOMA + residente + médico ocupacional + admin empresa
                    if (proyecto != null)
                    {
                        var projectEmails = await ctx.Project.AsNoTracking()
                            .Where(p => p.ProjectId == proyecto.ProjectId)
                            .Select(p => new { p.EmailCoordAdmin, p.EmailResidente, p.EmailCoordSsoma })
                            .FirstOrDefaultAsync();
                        toRaw.Add(projectEmails?.EmailCoordAdmin);
                        toRaw.Add(projectEmails?.EmailResidente);
                        toRaw.Add(projectEmails?.EmailCoordSsoma);
                    }
                    toRaw.Add(medOcupacional);
                    toRaw.Add(adminEmail);
                }
                else if (esStaff)
                {
                    // Staff: correo corporativo + residente + administrador + SSOMA + admin empresa
                    toRaw.Add(worker.EmailCorporativo);
                    if (proyecto != null)
                    {
                        var projectEmails = await ctx.Project.AsNoTracking()
                            .Where(p => p.ProjectId == proyecto.ProjectId)
                            .Select(p => new { p.EmailCoordAdmin, p.EmailResidente, p.EmailCoordSsoma })
                            .FirstOrDefaultAsync();
                        toRaw.Add(projectEmails?.EmailResidente);
                        toRaw.Add(projectEmails?.EmailCoordAdmin);
                        toRaw.Add(projectEmails?.EmailCoordSsoma);
                    }
                    toRaw.Add(adminEmail);
                }
                else if (esOficinaCentral)
                {
                    // Oficina Central: correo corporativo + jefatura + GTH + médico ocupacional + admin empresa
                    toRaw.Add(worker.EmailCorporativo);
                    toRaw.Add(gth);
                    toRaw.Add(medOcupacional);
                    toRaw.Add(adminEmail);
                    if (!string.IsNullOrWhiteSpace(worker.Jefatura))
                    {
                        var jefaturaEmails = await ctx.CatJefatura.AsNoTracking()
                            .Where(j => j.Nombre == worker.Jefatura && j.Activo)
                            .Select(j => j.Email!)
                            .ToListAsync();
                        toRaw.AddRange(jefaturaEmails);
                    }
                }

                var to = toRaw.Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (to.Count == 0) return;

                var tipoEmo = await ctx.SsEmoTipo.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == prog.TipoEmoId);

                var clinica = prog.ClinicaId.HasValue
                    ? await ctx.SsClinica.AsNoTracking().FirstOrDefaultAsync(c => c.Id == prog.ClinicaId.Value)
                    : null;

                var workerNombre = worker.Person?.FullName ?? worker.Id.ToString();
                var fechaStr = prog.FechaProgramada.ToString("dd/MM/yyyy");
                var horaStr = prog.HoraProgramada.HasValue ? prog.HoraProgramada.Value.ToString("HH:mm") : "—";
                var proyectoStr = proyecto?.ProjectDescription ?? "—";
                var tipoStr = tipoEmo?.Nombre ?? "—";
                var clinicaNombre = clinica?.Nombre ?? "—";

                var html = $@"<h2>EMO Confirmado por Clínica</h2>
<p>La clínica ha confirmado la programación del Examen Médico Ocupacional:</p>
<table style='border-collapse:collapse;width:100%;max-width:500px'>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Trabajador</td><td style='padding:6px 12px'>{workerNombre}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Tipo EMO</td><td style='padding:6px 12px'>{tipoStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Fecha</td><td style='padding:6px 12px'>{fechaStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Hora</td><td style='padding:6px 12px'>{horaStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Proyecto</td><td style='padding:6px 12px'>{proyectoStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Clínica</td><td style='padding:6px 12px'>{clinicaNombre}</td></tr>
</table>
<p style='margin-top:16px;color:#6b7280;font-size:0.9em'>El trabajador debe presentarse en la clínica en la fecha y hora indicadas.</p>";

                await _emailService.SendAsync(
                    to: to,
                    subject: $"[PRUEBAS - NO RESPONDER] [EMO Confirmado] {workerNombre} — {fechaStr}",
                    body: html,
                    isHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar notificación de aceptación de programación.");
            }
        }

    }

}
