using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Configuracion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Shared;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Helpers;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class ProgramacionEmoRepository : IProgramacionEmoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IEmoDestinatariosResolver _destinatarios;
        private readonly ILogger<ProgramacionEmoRepository> _logger;

        public ProgramacionEmoRepository(
            IDbContextFactory<AppDbContext> factory,
            IEmailService emailService,
            IConfiguration configuration,
            IEmoDestinatariosResolver destinatarios,
            ILogger<ProgramacionEmoRepository> logger)
        {
            _factory = factory;
            _emailService = emailService;
            _configuration = configuration;
            _destinatarios = destinatarios;
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

                q = q.Where(x => x.p.State);
                q = q.Where(x => x.em != null && x.em.EsAbril);
                q = q.Where(x => x.w.Estado == null || x.w.Estado != "RETIRADO");

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
                if (filter.AreaScopeId.HasValue)
                {
                    var idsArea = await ctx.ResolveDescendantsAsync(filter.AreaScopeId.Value);
                    q = q.Where(x => x.w.AreaScopeId != null && idsArea.Contains(x.w.AreaScopeId.Value));
                }
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
                        Puesto = x.w.PuestoCatalogo == null ? null : x.w.PuestoCatalogo.Nombre,
                        Categoria = x.w.Categoria,
                        TipoTrabajador = x.w.ContrataCasa == "Casa" && x.w.ObraOficinaStaffId == ObraOficinaStaffIds.OficinaCentral
                            ? "Oficina Central"
                            : x.w.ContrataCasa == "Casa" && x.w.ObraOficinaStaffId == ObraOficinaStaffIds.Staff
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

            q = q.Where(x => x.p.State);
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
            if (filter.AreaScopeId.HasValue)
            {
                var idsArea = await ctx.ResolveDescendantsAsync(filter.AreaScopeId.Value);
                q = q.Where(x => x.p.Worker != null && x.p.Worker.AreaScopeId != null && idsArea.Contains(x.p.Worker.AreaScopeId.Value));
            }
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
            // Una programación dada de baja (State = false) no bloquea: justamente se da de
            // baja para poder rehacerla.
            //
            // Excepción: "En Interconsulta" (InterconsultaRepository.Create la deja en ese
            // estado mientras se espera el levantamiento — ver ese archivo) puede quedar
            // atascada semanas si la interconsulta se demora. Acá no se bloquea: se cierra esa
            // fila vieja como "Cancelado" (con nota) y se sigue con la programación nueva, para
            // no dejar dos filas "activas" del mismo trabajador/tipo EMO compitiendo entre sí.
            // La interconsulta original (ss_interconsulta) no se toca — sigue como historial,
            // solo deja de tener una programación "en curso" que la sostenga.
            var progBloqueante = await ctx.SsProgramacionEmo.FirstOrDefaultAsync(p =>
                p.State &&
                p.WorkerId == dto.WorkerId &&
                p.TipoEmoId == dto.TipoEmoId &&
                p.Estado != "Completado" &&
                p.Estado != "Cancelado" &&
                p.Estado != "Rechazado por Clínica" &&
                p.Estado != "No se presentó");

            if (progBloqueante != null && progBloqueante.Estado != "En Interconsulta")
                throw new AbrilException("Este trabajador ya tiene una programación activa para este tipo de EMO.", 409);

            if (progBloqueante != null && progBloqueante.Estado == "En Interconsulta")
            {
                progBloqueante.Estado = "Cancelado";
                progBloqueante.Motivo = string.IsNullOrWhiteSpace(progBloqueante.Motivo)
                    ? "Reprogramado: la interconsulta pendiente se demoró demasiado."
                    : $"{progBloqueante.Motivo} — Reprogramado: la interconsulta pendiente se demoró demasiado.";
                progBloqueante.UpdatedAt = DateTimeOffset.UtcNow;
            }

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
            var ent = await ctx.SsProgramacionEmo.FirstOrDefaultAsync(p => p.Id == id && p.State)
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

        // Estados que solo debe asignar el flujo de la clínica (Agenda → ClinicaAccion),
        // el cual siempre completa ClinicaId/EmpresaId (y CheckInHora para "En Atención").
        // Sin esta validación, UpdateEstado permitía dejar programaciones "En Atención"
        // sin clínica ni empresa: invisibles en la Agenda (que exige empresa) pero
        // visibles como huérfanas en Habilitación/Trabajadores (ver programación #798).
        private static readonly HashSet<string> EstadosRequierenClinica = new()
        {
            "Aceptado por Clínica", "En Atención"
        };

        public async Task UpdateEstado(int id, string estado, int? emoResultadoId, int? userId)
        {
            if (estado == "Completado")
                throw new AbrilException("El estado 'Completado' solo puede asignarse al registrar el resultado del EMO.", 400);

            using var ctx = _factory.CreateDbContext();
            var ent = await ctx.SsProgramacionEmo.FirstOrDefaultAsync(p => p.Id == id && p.State)
                ?? throw new AbrilException("Programación no encontrada.", 404);

            if (EstadosRequierenClinica.Contains(estado) && (ent.ClinicaId is null || ent.EmpresaId is null))
                throw new AbrilException(
                    $"No se puede asignar el estado '{estado}' sin clínica y empresa asignadas. Use el flujo de la clínica (Agenda).",
                    400);

            ent.Estado = estado;
            if (emoResultadoId.HasValue) ent.EmoResultadoId = emoResultadoId;
            ent.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task ClinicaAccion(int id, ProgramacionClinicaAccionDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var ent = await ctx.SsProgramacionEmo.FirstOrDefaultAsync(p => p.Id == id && p.State)
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
            var ent = await ctx.SsProgramacionEmo.FirstOrDefaultAsync(p => p.Id == id && p.State)
                ?? throw new AbrilException("Programación no encontrada.", 404);

            if (ent.Estado != "En Atención")
                throw new AbrilException("Solo se puede deshacer el ingreso cuando el estado es 'En Atención'.", 409);

            ent.Estado     = "Aceptado por Clínica";
            ent.CheckInHora = null;
            ent.UpdatedAt  = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        /// <summary>
        /// Vista previa del modal "Programar EMO con clínica": los DOS correos que dispara el
        /// flujo — el de ahora (programación manual) y el que sale después si la clínica acepta.
        /// Ambos salen del mismo resolver que el envío real, así que lo que el usuario ve antes
        /// de guardar es exactamente lo que se va a enviar en cada momento.
        ///
        /// Secuencial a propósito: cada Resolver abre su propio contexto y la convención del
        /// proyecto es no paralelizar accesos a la base de datos.
        /// </summary>
        public async Task<ProgramacionDestinatariosPreviewDto> GetDestinatarios(int workerId, int? clinicaId)
        {
            var manual = await _destinatarios.ResolverAsync(
                EmoCorreoEventoCodigo.ProgramacionManual, workerId, clinicaId);
            var aceptada = await _destinatarios.ResolverAsync(
                EmoCorreoEventoCodigo.Aceptada, workerId, clinicaId);

            return new ProgramacionDestinatariosPreviewDto { Manual = manual, Aceptada = aceptada };
        }

        private async Task EnviarNotificacionCreacionAsync(
            AppDbContext ctx,
            SsProgramacionEmo prog,
            Worker worker)
        {
            var toRaw = new List<string>();
            try
            {
                // Destinatarios según la matriz de Configuración de EMOs → sección
                // "Programación manual", para el perfil del trabajador.
                var destinatarios = await _destinatarios.ResolverAsync(
                    EmoCorreoEventoCodigo.ProgramacionManual, worker.Id, prog.ClinicaId);

                var to = destinatarios.Para.Select(d => d.Email).ToList();
                var cc = destinatarios.Copias.Select(d => d.Email).ToList();
                toRaw = to;

                // Sin ningún destinatario principal activo no se envía nada (ni las
                // copias): es justamente la forma de silenciar el correo desde la
                // pantalla de Configuración de EMOs para hacer pruebas.
                if (to.Count == 0)
                {
                    _logger.LogWarning("Programación {Id}: sin destinatarios principales activos, no se envía notificación de creación.", prog.Id);
                    return;
                }

                SsClinica? clinica = prog.ClinicaId.HasValue
                    ? await ctx.SsClinica.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == prog.ClinicaId.Value)
                    : null;

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
                    cc: cc.Count > 0 ? cc : null,
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
                // Destinatarios según la matriz de Configuración de EMOs → sección
                // "Programación aceptada por la clínica", para el perfil del trabajador.
                // Que los contratistas no reciban este correo también sale de ahí (su
                // columna viene sin destinatarios activos), ya no de un corte en el código.
                var destinatarios = await _destinatarios.ResolverAsync(
                    EmoCorreoEventoCodigo.Aceptada, worker.Id, prog.ClinicaId);

                var to = destinatarios.Para.Select(d => d.Email).ToList();
                var cc = destinatarios.Copias.Select(d => d.Email).ToList();

                if (to.Count == 0) return;

                var vinculacion = await ctx.WorkerVinculacion.AsNoTracking()
                    .Where(v => v.WorkerId == worker.Id && v.FechaFin == null)
                    .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                    .FirstOrDefaultAsync();

                Project? proyecto = null;
                if (vinculacion?.ProyectoId.HasValue == true)
                    proyecto = await ctx.Project.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.ProjectId == vinculacion.ProyectoId.Value);

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
                var clinicaDireccion = clinica?.Direccion;

                // El logo, los íconos y la imagen de recomendaciones se sirven desde los estáticos
                // del frontend (public/images/), no desde el wwwroot del backend: en producción
                // intranet.abril.pe es nginx, que solo proxea /api/** al contenedor. Cualquier otra
                // ruta cae en el fallback SPA y devolvía index.html (200 text/html) en vez de la
                // imagen, por eso salían rotas.
                //
                // El origen es una clave aparte de App:FrontendUrl a propósito: Outlook no descarga
                // las imágenes desde el cliente sino a través del proxy de imágenes de Microsoft,
                // que nunca puede alcanzar un localhost. Con App:FrontendUrl (que en dev tiene que
                // seguir apuntando a localhost para los links clicables de los otros correos) las
                // imágenes salen siempre rotas al probar en local; App:EmailAssetsUrl permite
                // apuntarlas a un host público sin tocar esos links.
                var assetsUrl = _configuration["App:EmailAssetsUrl"]
                    ?? _configuration["App:FrontendUrl"]
                    ?? "https://intranet.abril.pe";

                var html = EmoConfirmacionEmailTemplate.Construir(
                    new EmoConfirmacionEmailTemplate.Datos(
                        Trabajador: workerNombre,
                        TipoEmo: tipoStr,
                        Fecha: fechaStr,
                        Hora: horaStr,
                        Proyecto: proyectoStr,
                        Clinica: clinicaNombre,
                        Direccion: clinicaDireccion),
                    assetsUrl);

                await _emailService.SendAsync(
                    to: to,
                    subject: $"[EMO Confirmado] {workerNombre} — {fechaStr}",
                    body: html,
                    isHtml: true,
                    cc: cc.Count > 0 ? cc : null,
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
                // Destinatarios según la matriz de Configuración de EMOs → sección
                // "Programación rechazada por la clínica", para el perfil del trabajador.
                var destinatarios = await _destinatarios.ResolverAsync(
                    EmoCorreoEventoCodigo.Rechazada, worker.Id, prog.ClinicaId);

                var to = destinatarios.Para.Select(d => d.Email).ToList();
                var cc = destinatarios.Copias.Select(d => d.Email).ToList();

                if (to.Count == 0) return;

                var vinculacion = await ctx.WorkerVinculacion.AsNoTracking()
                    .Where(v => v.WorkerId == worker.Id && v.FechaFin == null)
                    .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                    .FirstOrDefaultAsync();

                Project? proyecto = null;
                if (vinculacion?.ProyectoId.HasValue == true)
                    proyecto = await ctx.Project.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.ProjectId == vinculacion.ProyectoId.Value);

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
                    cc: cc.Count > 0 ? cc : null,
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
                .Where(p => p.State && estados.Contains(p.Estado))
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
            var prog = await ctx.SsProgramacionEmo.FirstOrDefaultAsync(p => p.Id == id && p.State)
                ?? throw new AbrilException("Programación no encontrada.", 404);
            prog.Notificado = notificado;
            prog.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

    }

}
