using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Shared;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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

        public async Task<PagedResponseDto<ProgramacionListDto>> List(ProgramacionFilterDto filter)
        {
            try
            {
                using var ctx = _factory.CreateDbContext();

                var q =
                    from p in ctx.SsProgramacionEmo
                    join w in ctx.Worker on p.WorkerId equals w.Id
                    join per in ctx.Person on w.PersonId equals per.PersonId into perj
                    from per in perj.DefaultIfEmpty()
                    join em in ctx.Contributor on p.EmpresaId equals em.ContributorId into ej
                    from em in ej.DefaultIfEmpty()
                    join t in ctx.SsEmoTipo on p.TipoEmoId equals t.Id into tj
                    from t in tj.DefaultIfEmpty()
                    join c in ctx.SsClinica on p.ClinicaId equals c.Id into cj
                    from c in cj.DefaultIfEmpty()
                    join m in ctx.SsMedicoOcupacional on p.MedicoId equals m.Id into mj
                    from m in mj.DefaultIfEmpty()
                    select new { p, w, per, em, t, c, m };

                q = q.Where(x => x.em != null && x.em.EsAbril);

                // La clínica no puede procesar trabajadores con interconsulta pendiente.
                // El médico SSOMA (IncluirConInterconsulta = true) ve todas sin excepción.
                if (!filter.IncluirConInterconsulta)
                {
                    q = q.Where(x => x.p.Estado != "En Interconsulta");
                    q = q.Where(x => !ctx.SsInterconsulta
                        .Any(i => i.WorkerId == x.p.WorkerId && i.Estado == "Pendiente"));
                }

                if (filter.Desde.HasValue)
                    q = q.Where(x => x.p.FechaProgramada >= filter.Desde.Value);
                if (filter.Hasta.HasValue)
                    q = q.Where(x => x.p.FechaProgramada <= filter.Hasta.Value);
                if (!string.IsNullOrWhiteSpace(filter.Estado))
                    q = q.Where(x => x.p.Estado == filter.Estado);
                if (filter.WorkerId.HasValue)
                    q = q.Where(x => x.p.WorkerId == filter.WorkerId.Value);
                if (filter.ClinicaId.HasValue)
                    q = q.Where(x => x.p.ClinicaId == filter.ClinicaId.Value);
                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var term = filter.Search.Trim().ToLower();
                    q = q.Where(x =>
                        (x.per != null && x.per.FullName != null && x.per.FullName.ToLower().Contains(term)) ||
                        (x.per != null && x.per.DocumentIdentityCode != null && x.per.DocumentIdentityCode.Contains(term)));
                }

                var totalRecords = await q.CountAsync();
                var page = Math.Max(filter.Page, 1);
                // La Agenda pide pageSize=500 para traer todo sin paginar; el tope viejo de 200
                // cortaba silenciosamente el resto de registros (los mas antiguos por el ORDER BY).
                var pageSize = Math.Clamp(filter.PageSize, 1, 2000);
                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                // Con el historico creciendo, "traer todo sin filtro de fecha" (como hace la Agenda
                // de clinica) puede superar pageSize y el ORDER BY antiguo (solo por fecha ascendente)
                // cortaba las programaciones activas mas recientes dejandolas fuera de la pagina 1.
                // Mostrar primero las no terminales garantiza que Programado/Aceptado/En Atencion
                // nunca desaparezcan por el corte de pagina, sin cambiar el filtrado ni el conteo.
                var estadosTerminales = new HashSet<string> { "Completado", "Cancelado", "Rechazado por Clínica", "No se presentó" };

                var data = await q
                    .OrderBy(x => estadosTerminales.Contains(x.p.Estado) ? 1 : 0)
                    .ThenByDescending(x => x.p.FechaProgramada)
                    .ThenBy(x => x.p.HoraProgramada)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new ProgramacionListDto
                    {
                        Id = x.p.Id,
                        WorkerId = x.p.WorkerId,
                        WorkerNombre = x.per != null ? x.per.FullName : null,
                        WorkerDni = x.per != null ? x.per.DocumentIdentityCode : null,
                        Empresa = x.em != null ? x.em.ContributorName : null,
                        Proyecto = (from v in ctx.WorkerVinculacion
                                    join pr in ctx.Project on v.ProyectoId equals (int?)pr.ProjectId
                                    where v.WorkerId == x.p.WorkerId && v.FechaFin == null
                                    orderby v.CreatedAt descending
                                    select (string?)pr.ProjectDescription)
                                   .FirstOrDefault(),
                        TipoEmoId = x.p.TipoEmoId,
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
                        FechaNotificacion = x.p.FechaNotificacion,
                        Ocupacion = x.w.Ocupacion,
                        Categoria = x.w.Categoria,
                        TipoTrabajador = x.w.ContrataCasa == "Casa" && x.w.ObraOficina == "Oficina Central"
                            ? "Oficina Central"
                            : x.w.ContrataCasa == "Casa" && x.w.ObraOficina == "Staff"
                                ? "Staff Obra"
                                : "Obrero",
                        FechaVencimientoEmo = ctx.WorkerEmo
                            .Where(e => e.WorkerId == x.p.WorkerId && e.Activo)
                            .OrderByDescending(e => e.FechaVencimientoCalculada ?? e.FechaVencimiento)
                            .Select(e => (DateOnly?)(e.FechaVencimientoCalculada ?? e.FechaVencimiento))
                            .FirstOrDefault(),
                        InterconsultaEstado = ctx.SsInterconsulta
                            .Where(i => i.WorkerId == x.p.WorkerId)
                            .OrderByDescending(i => i.FechaDerivacion)
                            .Select(i => (string?)i.Estado)
                            .FirstOrDefault(),
                        TieneInterconsulta = ctx.SsInterconsulta
                            .Any(i => i.WorkerId == x.p.WorkerId && i.Estado == "Pendiente")
                    })
                    .ToListAsync();

                return new PagedResponseDto<ProgramacionListDto>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalRecords = totalRecords,
                    TotalPages = totalPages == 0 ? 1 : totalPages,
                    Data = data
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("PROGRAMACION_LIST_ERROR estado={Estado} | {Ex}", filter.Estado, ex.ToString());
                throw;
            }
        }

        public async Task<ProgramacionResumenDto> GetResumen(ProgramacionFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            var q =
                from p in ctx.SsProgramacionEmo
                join w in ctx.Worker on p.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals per.PersonId into perj
                from per in perj.DefaultIfEmpty()
                join em in ctx.Contributor on p.EmpresaId equals em.ContributorId into ej
                from em in ej.DefaultIfEmpty()
                select new { p, per, em };

            q = q.Where(x => x.em != null && x.em.EsAbril);

            if (!filter.IncluirConInterconsulta)
            {
                q = q.Where(x => x.p.Estado != "En Interconsulta");
                q = q.Where(x => !ctx.SsInterconsulta
                    .Any(i => i.WorkerId == x.p.WorkerId && i.Estado == "Pendiente"));
            }

            // El resumen ignora filter.Estado a propósito: debe mostrar el desglose
            // por estado sobre el resto de filtros, no solo el estado seleccionado.
            if (filter.Desde.HasValue)
                q = q.Where(x => x.p.FechaProgramada >= filter.Desde.Value);
            if (filter.Hasta.HasValue)
                q = q.Where(x => x.p.FechaProgramada <= filter.Hasta.Value);
            if (filter.WorkerId.HasValue)
                q = q.Where(x => x.p.WorkerId == filter.WorkerId.Value);
            if (filter.ClinicaId.HasValue)
                q = q.Where(x => x.p.ClinicaId == filter.ClinicaId.Value);
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim().ToLower();
                q = q.Where(x =>
                    (x.per != null && x.per.FullName != null && x.per.FullName.ToLower().Contains(term)) ||
                    (x.per != null && x.per.DocumentIdentityCode != null && x.per.DocumentIdentityCode.Contains(term)));
            }

            var estadoCounts = await q
                .GroupBy(x => x.p.Estado)
                .Select(g => new { Estado = g.Key, Count = g.Count() })
                .ToListAsync();

            var automaticos = await q.CountAsync(x => x.p.Origen == "Automatico");

            int CountFor(string estado) => estadoCounts.FirstOrDefault(e => e.Estado == estado)?.Count ?? 0;

            return new ProgramacionResumenDto
            {
                Programados = CountFor("Programado"),
                Aceptados = CountFor("Aceptado por Clínica"),
                EnAtencion = CountFor("En Atención"),
                Completados = CountFor("Completado"),
                Rechazados = CountFor("Rechazado por Clínica"),
                NoPresento = CountFor("No se presentó"),
                Automaticos = automaticos,
                Total = estadoCounts.Sum(e => e.Count),
            };
        }

        public async Task<int> Create(ProgramacionCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.Include(w => w.Person).FirstOrDefaultAsync(w => w.Id == dto.WorkerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            if (dto.FechaProgramada == default)
                throw new AbrilException("La fecha es obligatoria.", 400);

            // Evita duplicados: si ya hay una programación activa para este trabajador
            // y este tipo de EMO, no crear otra (antes solo el auto-programador validaba esto).
            var yaTieneActiva = await ctx.SsProgramacionEmo.AnyAsync(p =>
                p.WorkerId == dto.WorkerId &&
                p.TipoEmoId == dto.TipoEmoId &&
                p.Estado != "Completado" &&
                p.Estado != "Cancelado" &&
                p.Estado != "Rechazado por Clínica" &&
                p.Estado != "No se presentó");
            if (yaTieneActiva)
                throw new AbrilException("Este trabajador ya tiene una programación activa para este tipo de EMO.", 409);

            var empresaId = dto.EmpresaId;
            if (empresaId == null)
            {
                var hoy = DateOnly.FromDateTime(DateTime.Today);
                empresaId = await ctx.WorkerVinculacion
                    .Where(v => v.WorkerId == dto.WorkerId && (v.FechaFin == null || v.FechaFin >= hoy))
                    .OrderByDescending(v => v.FechaInicio)
                    .Select(v => (int?)v.EmpresaId)
                    .FirstOrDefaultAsync();
            }

            var ent = new SsProgramacionEmo
            {
                WorkerId = dto.WorkerId,
                EmpresaId = empresaId,
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
            if (estado == "Completado")
                throw new AbrilException("El estado 'Completado' solo puede asignarse al registrar el resultado del EMO.", 400);

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
                    if (dto.HoraNueva.HasValue) ent.HoraProgramada = dto.HoraNueva.Value;
                    else if (dto.CheckInHora.HasValue) ent.HoraProgramada = dto.CheckInHora.Value;
                    if (dto.NuevaFecha.HasValue) ent.FechaProgramada = dto.NuevaFecha.Value;
                    ent.UpdatedAt = DateTimeOffset.UtcNow;
                    await ctx.SaveChangesAsync();
                    await EnviarNotificacionAceptacionAsync(ctx, ent, worker);
                    return;
                case "Rechazar":
                    ent.Estado = "Rechazado por Clínica";
                    ent.MotivoRechazo = dto.MotivoRechazo;
                    ent.UpdatedAt = DateTimeOffset.UtcNow;
                    await ctx.SaveChangesAsync();
                    await EnviarNotificacionRechazoAsync(ctx, ent, worker, dto.MotivoRechazo);
                    return;
                case "CheckIn":
                    ent.Estado = "En Atención";
                    ent.CheckInHora = dto.CheckInHora ?? TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
                    break;
                case "Completar":
                    if (dto.EmoResultadoId.HasValue) ent.EmoResultadoId = dto.EmoResultadoId;
                    break;
                case "No Asistió":
                    ent.Estado = "No se presentó";
                    ent.UpdatedAt = DateTimeOffset.UtcNow;

                    var habCert = await ctx.SsHabTrabajador
                        .FirstOrDefaultAsync(h => h.WorkerId == ent.WorkerId && h.ItemId == 4);
                    if (habCert != null)
                    {
                        var hoyNoAsistio = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
                        var emoActivo = await ctx.WorkerEmo
                            .Where(e => e.WorkerId == ent.WorkerId && e.Activo)
                            .OrderByDescending(e => e.FechaVencimiento)
                            .FirstOrDefaultAsync();

                        if (emoActivo == null)
                        {
                            habCert.Estado = "Falta";
                        }
                        else if (emoActivo.FechaVencimiento < hoyNoAsistio)
                        {
                            habCert.Estado = "Vencido";
                        }
                        else
                        {
                            habCert.Estado = "Aprobado";
                            var venc = emoActivo.FechaVencimientoCalculada ?? emoActivo.FechaVencimiento;
                            if (venc.HasValue)
                                habCert.Vigencia = DateTime.SpecifyKind(venc.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                        }
                        habCert.UpdatedAt = DateTime.UtcNow;
                    }

                    await ctx.SaveChangesAsync();
                    return;
            }

            ent.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task UndoCheckInAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            var ent = await ctx.SsProgramacionEmo.FirstOrDefaultAsync(p => p.Id == id)
                ?? throw new AbrilException("Programación no encontrada.", 404);

            if (ent.Estado != "En Atención")
                throw new AbrilException("Solo se puede deshacer el ingreso cuando el estado es 'En Atención'.", 409);

            ent.Estado     = "Aceptado por Clínica";
            ent.CheckInHora = null;
            ent.UpdatedAt  = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        private async Task EnviarNotificacionCreacionAsync(
            AppDbContext ctx,
            SsProgramacionEmo prog,
            Worker worker)
        {
            var toRaw = new List<string>();
            try
            {
                // Solo enviar si tiene clínica asignada
                if (!prog.ClinicaId.HasValue) return;

                toRaw = await ctx.SsClinicaEmail.AsNoTracking()
                    .Where(e => e.ClinicaId == prog.ClinicaId.Value && e.Activo)
                    .Select(e => e.Email!)
                    .ToListAsync();

                var clinica = await ctx.SsClinica.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == prog.ClinicaId.Value);
                if (toRaw.Count == 0 && clinica?.Email is not null)
                    toRaw.Add(clinica.Email);

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

                if (to.Count == 0)
                {
                    _logger.LogWarning("Programación {Id}: sin emails de clínica, no se envía notificación de creación.", prog.Id);
                    return;
                }

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
                    subject: $"[EMO Programado] {workerNombre} — {fechaStr}",
                    body: html,
                    isHtml: true,
                    fromOverride: SaludOcupacionalEmailConstants.Remitente);

                prog.FechaNotificacion = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Programación {Id}: error enviando notificación de creación. Provider={Provider} To={To} Error={Error}",
                    prog.Id,
                    _configuration["Email:EmailProvider"],
                    string.Join(",", toRaw),
                    ex.Message);
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

                var medOcupacional     = _configuration["EmailsArea:MedicinaOcupacional"];
                var gth                = _configuration["EmailsArea:GTH"];
                var emailJefeArqCom    = _configuration["EmailsArea:JefeArqCom"];
                var emailJefePostVenta = _configuration["EmailsArea:JefePostVenta"];
                var emailPrevArqCom    = _configuration["EmailsArea:PrevenicionistaArqCom"];
                var emailPrevPostVenta = _configuration["EmailsArea:PrevenicionistaPostVenta"];

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
                    if (proyecto?.TieneArquitecturaComercial == true)
                    {
                        var extraEmails = new[] { emailJefeArqCom, emailJefePostVenta, emailPrevArqCom, emailPrevPostVenta };
                        foreach (var e in extraEmails)
                            if (!string.IsNullOrWhiteSpace(e)) toRaw.Add(e);
                    }
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
                    if (proyecto?.TieneArquitecturaComercial == true)
                    {
                        var extraEmails = new[] { emailJefeArqCom, emailJefePostVenta, emailPrevArqCom, emailPrevPostVenta };
                        foreach (var e in extraEmails)
                            if (!string.IsNullOrWhiteSpace(e)) toRaw.Add(e);
                    }
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
                    if (proyecto?.TieneArquitecturaComercial == true)
                    {
                        var extraEmails = new[] { emailJefeArqCom, emailJefePostVenta, emailPrevArqCom, emailPrevPostVenta };
                        foreach (var e in extraEmails)
                            if (!string.IsNullOrWhiteSpace(e)) toRaw.Add(e);
                    }
                }

                var to = toRaw.Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (to.Count == 0) return;

                var tipoEmo = await ctx.SsEmoTipo.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == prog.TipoEmoId);

                var workerNombre = worker.Person?.FullName ?? worker.Id.ToString();
                var fechaStr = prog.FechaProgramada.ToString("dd/MM/yyyy");
                var horaStr = prog.HoraProgramada.HasValue ? prog.HoraProgramada.Value.ToString("HH:mm") : "—";
                var proyectoStr = proyecto?.ProjectDescription ?? "—";
                var tipoStr = tipoEmo?.Nombre ?? "—";

                var backendUrl = (_configuration["BackendSettings:PublicUrl"] ?? "http://localhost:5236").TrimEnd('/');
                var recomendacionesImgUrl = $"{backendUrl}/emails/recomendaciones-emo.jpg";

                var html = $@"<h2>EMO Confirmado</h2>
<p>Se ha confirmado la programación del Examen Médico Ocupacional:</p>
<table style='border-collapse:collapse;width:100%;max-width:500px'>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Trabajador</td><td style='padding:6px 12px'>{workerNombre}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Tipo EMO</td><td style='padding:6px 12px'>{tipoStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Fecha</td><td style='padding:6px 12px'>{fechaStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Hora</td><td style='padding:6px 12px'>{horaStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Proyecto</td><td style='padding:6px 12px'>{proyectoStr}</td></tr>
</table>
<p style='margin-top:16px;color:#6b7280;font-size:0.9em'>El trabajador debe presentarse en la clínica en la fecha y hora indicadas.</p>
<img src='{recomendacionesImgUrl}' alt='Recomendaciones previas al Examen Médico Ocupacional' style='margin-top:16px;max-width:500px;width:100%' />";

                await _emailService.SendAsync(
                    to: to,
                    subject: $"[EMO Confirmado] {workerNombre} — {fechaStr}",
                    body: html,
                    isHtml: true,
                    fromOverride: SaludOcupacionalEmailConstants.Remitente);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar notificación de aceptación de programación.");
            }
        }

        private async Task EnviarNotificacionRechazoAsync(
            AppDbContext ctx,
            SsProgramacionEmo prog,
            Worker worker,
            string? motivo)
        {
            try
            {
                var esCasa = string.Equals(worker.ContrataCasa, "Casa", StringComparison.OrdinalIgnoreCase);
                var esOficinaCentral = esCasa && string.Equals(worker.ObraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase);
                var esStaff = esCasa && string.Equals(worker.ObraOficina, "Staff", StringComparison.OrdinalIgnoreCase);
                var esObrero = esCasa && !esOficinaCentral && !esStaff;

                if (!esCasa) return;

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
                var motivoStr = !string.IsNullOrWhiteSpace(motivo) ? motivo : "—";

                var html = $@"<h2>EMO Rechazado por Clínica</h2>
<p>La clínica ha rechazado la programación del Examen Médico Ocupacional:</p>
<table style='border-collapse:collapse;width:100%;max-width:500px'>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Trabajador</td><td style='padding:6px 12px'>{workerNombre}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Tipo EMO</td><td style='padding:6px 12px'>{tipoStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Fecha</td><td style='padding:6px 12px'>{fechaStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Hora</td><td style='padding:6px 12px'>{horaStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Proyecto</td><td style='padding:6px 12px'>{proyectoStr}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Clínica</td><td style='padding:6px 12px'>{clinicaNombre}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#fef2f2;color:#b91c1c'>Motivo de rechazo</td><td style='padding:6px 12px;color:#b91c1c'>{motivoStr}</td></tr>
</table>
<p style='margin-top:16px;color:#6b7280;font-size:0.9em'>Por favor coordinar una nueva fecha de programación con la clínica.</p>";

                await _emailService.SendAsync(
                    to: to,
                    subject: $"[EMO Rechazado] {workerNombre} — {fechaStr}",
                    body: html,
                    isHtml: true,
                    fromOverride: SaludOcupacionalEmailConstants.Remitente);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar notificación de rechazo de programación.");
            }
        }

        public async Task<List<ProgramacionHabilitacionDto>> GetHabilitacionAsync(ProgramacionHabilitacionFiltrosDto f)
        {
            using var ctx = _factory.CreateDbContext();
            var estados = new[] { "Programado", "Aceptado por Clínica", "En Atención", "En Interconsulta", "Aceptado" };

            var q = ctx.SsProgramacionEmo
                .Where(p => estados.Contains(p.Estado))
                .Include(p => p.Worker)
                    .ThenInclude(w => w!.Person)
                .AsQueryable();

            if (!string.IsNullOrEmpty(f.Estado))
                q = q.Where(p => p.Estado == f.Estado);

            if (!string.IsNullOrEmpty(f.Fecha))
                q = q.Where(p => p.FechaProgramada.ToString() == f.Fecha);

            if (f.SoloNoNotificados == true)
                q = q.Where(p => !p.Notificado);

            var list = await q
                .OrderBy(p => p.FechaProgramada)
                .ThenBy(p => p.HoraProgramada)
                .ToListAsync();

            var workerIds = list.Select(p => p.WorkerId).Distinct().ToList();

            var vinculaciones = await ctx.WorkerVinculacion
                .Where(v => workerIds.Contains(v.WorkerId) && v.FechaFin == null)
                .Include(v => v.Empresa)
                .Include(v => v.Proyecto)
                .ToListAsync();

            var vinMap = vinculaciones
                .GroupBy(v => v.WorkerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.CreatedAt).First());

            var result = list
                .Where(p => !f.ProyectoId.HasValue || (vinMap.TryGetValue(p.WorkerId, out var vCheck) && vCheck.ProyectoId == f.ProyectoId.Value))
                .Select(p =>
                {
                    vinMap.TryGetValue(p.WorkerId, out var vin);
                    var person = p.Worker?.Person;

                    return new ProgramacionHabilitacionDto
                    {
                        Id            = p.Id,
                        Trabajador    = person?.FullName ?? "",
                        Dni           = person?.DocumentIdentityCode ?? "",
                        Proyecto      = vin?.Proyecto?.ProjectDescription ?? "",
                        RazonSocial   = vin?.Empresa?.ContributorName ?? "",
                        Estado        = p.Estado,
                        FechaProgramada = p.FechaProgramada.ToString("yyyy-MM-dd"),
                        Hora          = p.HoraProgramada?.ToString(@"hh\:mm"),
                        Notificado    = p.Notificado,
                    };
                })
                .ToList();

            if (result.Count > 0)
                _logger.LogInformation("[GetHabilitacion] primer item — Id={Id} Trabajador={Trabajador} RazonSocial={RazonSocial} Proyecto={Proyecto}",
                    result[0].Id, result[0].Trabajador, result[0].RazonSocial, result[0].Proyecto);

            return result;
        }

        public async Task PatchNotificadoAsync(int id, bool notificado)
        {
            using var ctx = _factory.CreateDbContext();
            var prog = await ctx.SsProgramacionEmo.FindAsync(id)
                ?? throw new AbrilException("Programación no encontrada.", 404);
            prog.Notificado = notificado;
            prog.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

    }

}
