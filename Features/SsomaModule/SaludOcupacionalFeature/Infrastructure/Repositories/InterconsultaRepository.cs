using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Interconsulta;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class InterconsultaRepository : IInterconsultaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public InterconsultaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<PagedResult<InterconsultaListDto>> List(InterconsultaFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var q =
                from i in ctx.SsInterconsulta
                join w in ctx.Worker on i.WorkerId equals w.Id
                join m in ctx.SsMedicoOcupacional on i.MedicoDerivaId equals m.Id into mj
                from m in mj.DefaultIfEmpty()
                // Esta pantalla es solo para personal de Abril activo: excluye contratistas
                // (contrata_casa != "Casa") y trabajadores retirados.
                where w.ContrataCasa == "Casa" && w.Estado != "RETIRADO"
                select new
                {
                    i,
                    w,
                    m,
                    // Fuente principal: ss_hab_worker_proyecto (la que administra Habilitación en
                    // "Trabajadores > Proyectos asignados", más confiable). Solo cuenta la
                    // asignación activa (fecha_fin null) — si no hay ninguna, se deja en blanco en
                    // vez de mostrar un proyecto del que el trabajador ya salió.
                    // Se cae a worker_vinculaciones solo si el trabajador no tiene ninguna fila ahí.
                    ProyAsignada = ctx.WorkerProyecto
                        .Where(wp => wp.WorkerId == w.Id && wp.FechaFin == null)
                        .OrderByDescending(wp => wp.FechaInicio)
                        .ThenByDescending(wp => wp.Id)
                        .FirstOrDefault(),
                    VincActiva = ctx.WorkerVinculacion
                        .Where(v => v.WorkerId == w.Id && v.FechaFin == null)
                        .OrderByDescending(v => v.CreatedAt)
                        .ThenByDescending(v => v.Id)
                        .FirstOrDefault()
                };

            if (!string.IsNullOrWhiteSpace(filter.Estado))
                q = q.Where(x => x.i.Estado == filter.Estado);
            if (filter.WorkerId.HasValue)
                q = q.Where(x => x.i.WorkerId == filter.WorkerId.Value);
            if (filter.ProyectoId.HasValue)
                q = q.Where(x =>
                    (x.ProyAsignada != null && x.ProyAsignada.ProyectoId == filter.ProyectoId.Value) ||
                    (x.ProyAsignada == null && x.VincActiva != null && x.VincActiva.ProyectoId == filter.ProyectoId.Value));
            if (filter.ContributorId.HasValue)
                q = q.Where(x =>
                    (x.ProyAsignada != null && x.ProyAsignada.EmpresaId == filter.ContributorId.Value) ||
                    (x.ProyAsignada == null && x.VincActiva != null && x.VincActiva.EmpresaId == filter.ContributorId.Value));
            if (!string.IsNullOrWhiteSpace(filter.ObraOficina))
            {
                // Si obra_oficina viene vacío/nulo se asume "Obra": solo Staff/Oficina Central
                // se marcan explícitamente en el dato, el resto son obreros por defecto.
                q = filter.ObraOficina == "Obra"
                    ? q.Where(x => x.w.ObraOficina == "Obra" || string.IsNullOrWhiteSpace(x.w.ObraOficina))
                    : q.Where(x => x.w.ObraOficina == filter.ObraOficina);
            }
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                q = q.Where(x =>
                    (x.w.Person != null && x.w.Person.FullName != null &&
                     EF.Functions.ILike(x.w.Person.FullName, $"%{term}%"))
                    || (x.w.Person != null && x.w.Person.DocumentIdentityCode != null &&
                     EF.Functions.ILike(x.w.Person.DocumentIdentityCode, $"%{term}%")));
            }

            var total = await q.CountAsync();
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize <= 0 ? 15 : Math.Min(filter.PageSize, 100);

            var raw = await q
                .OrderByDescending(x => x.i.FechaDerivacion)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.i.Id,
                    x.i.EmoId,
                    x.i.WorkerId,
                    WorkerNombre = x.w.Person != null ? x.w.Person.FullName : null,
                    WorkerDni = x.w.Person != null ? x.w.Person.DocumentIdentityCode : null,
                    x.i.Especialidad,
                    MedicoDeriva = x.m != null ? x.m.ApellidoNombre : null,
                    x.i.FechaDerivacion,
                    x.i.FechaAtencion,
                    x.i.CentroAtencion,
                    x.i.Diagnostico,
                    x.i.Resultado,
                    x.i.Estado,
                    x.i.RequiereSeguimiento,
                    x.i.UrlInforme,
                    x.w.ObraOficina,
                    x.w.ContrataCasa,
                    x.w.Jefatura,
                    x.w.Categoria,
                    x.w.Ocupacion,
                    x.w.AreaScopeId,
                    JefeWorkerId = x.w.WorkerLessonJefeId ?? x.w.WorkerSalidaJefeId,
                    WorkerEmail = x.w.EmailCorporativo,
                    ProyectoId = (x.ProyAsignada != null ? (int?)x.ProyAsignada.ProyectoId : null) ?? (x.VincActiva != null ? (int?)x.VincActiva.ProyectoId : null),
                    EmpresaId = (x.ProyAsignada != null ? x.ProyAsignada.EmpresaId : null) ?? (x.VincActiva != null ? x.VincActiva.EmpresaId : null)
                })
                .ToListAsync();

            var proyectoIds = raw.Where(x => x.ProyectoId.HasValue).Select(x => x.ProyectoId!.Value).Distinct().ToList();
            var empresaIds = raw.Where(x => x.EmpresaId.HasValue).Select(x => x.EmpresaId!.Value).Distinct().ToList();
            var jefaturaNombres = raw.Where(x => !string.IsNullOrWhiteSpace(x.Jefatura)).Select(x => x.Jefatura!).Distinct().ToList();
            var jefeWorkerIds = raw.Where(x => x.JefeWorkerId.HasValue).Select(x => x.JefeWorkerId!.Value).Distinct().ToList();

            var proyectoMap = await ctx.Project
                .Where(p => proyectoIds.Contains(p.ProjectId))
                .ToDictionaryAsync(p => p.ProjectId, p => p);
            var empresaMap = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId, c => c);
            var jefaturaMap = await ctx.CatJefatura
                .Where(j => jefaturaNombres.Contains(j.Nombre) && j.Activo)
                .ToDictionaryAsync(j => j.Nombre, j => j.Email);
            var jefeWorkerMap = await ctx.Worker
                .Where(jw => jefeWorkerIds.Contains(jw.Id))
                .Select(jw => new { jw.Id, Nombre = jw.Person != null ? jw.Person.FullName : null, jw.EmailCorporativo })
                .ToDictionaryAsync(jw => jw.Id, jw => jw);
            var (parentByScope, candidatosPorScope) = await LoadAreaJefeContextAsync(ctx);

            var items = raw.Select(x =>
            {
                Project? proyecto = x.ProyectoId.HasValue && proyectoMap.TryGetValue(x.ProyectoId.Value, out var p) ? p : null;
                Contributor? empresa = x.EmpresaId.HasValue && empresaMap.TryGetValue(x.EmpresaId.Value, out var e) ? e : null;
                var esOficinaCentral = string.Equals(x.ObraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase);

                // Prioridad: 1) jefe real (worker_lesson_jefe_id/worker_salida_jefe_id), 2) texto
                // libre workers.jefatura + cat_jefatura, 3) árbol area_scope (mismo algoritmo que
                // ApproverResolver de Solicitud de Salidas) — cubre a quienes no tienen ninguno de
                // los dos primeros pero sí un área asignada.
                string? jefaturaNombre = null;
                string? jefaturaEmail = null;
                if (x.JefeWorkerId.HasValue && jefeWorkerMap.TryGetValue(x.JefeWorkerId.Value, out var jefe))
                {
                    jefaturaNombre = jefe.Nombre;
                    jefaturaEmail = jefe.EmailCorporativo;
                }
                else if (!string.IsNullOrWhiteSpace(x.Jefatura))
                {
                    jefaturaNombre = x.Jefatura;
                    jefaturaMap.TryGetValue(x.Jefatura, out jefaturaEmail);
                }
                else
                {
                    var porArea = ResolveJefePorArea(x.AreaScopeId, x.WorkerId, parentByScope, candidatosPorScope);
                    jefaturaNombre = porArea.Nombre;
                    jefaturaEmail = porArea.Email;
                }

                return new InterconsultaListDto
                {
                    Id = x.Id,
                    EmoId = x.EmoId,
                    WorkerId = x.WorkerId,
                    WorkerNombre = x.WorkerNombre,
                    WorkerDni = x.WorkerDni,
                    // Oficina Central no pertenece a un proyecto de obra: su unidad organizativa es
                    // la jefatura, no la última vinculación (que puede quedar obsoleta si el trabajador
                    // pasó de obra a oficina central sin cerrarse en worker_vinculaciones).
                    ProyectoId = esOficinaCentral ? null : x.ProyectoId,
                    ProyectoNombre = esOficinaCentral ? "Oficina Central" : proyecto?.ProjectDescription,
                    ContributorId = x.EmpresaId,
                    RazonSocial = empresa?.ContributorName,
                    ObraOficina = x.ObraOficina,
                    ContrataCasa = x.ContrataCasa,
                    Categoria = x.Categoria,
                    Ocupacion = x.Ocupacion,
                    WorkerEmail = x.WorkerEmail,
                    AdministradorEmail = proyecto?.EmailCoordAdmin ?? empresa?.EmailAdministrador,
                    Jefatura = jefaturaNombre,
                    JefaturaEmail = jefaturaEmail,
                    Especialidad = x.Especialidad,
                    MedicoDeriva = x.MedicoDeriva,
                    FechaDerivacion = x.FechaDerivacion,
                    FechaAtencion = x.FechaAtencion,
                    CentroAtencion = x.CentroAtencion,
                    Diagnostico = x.Diagnostico,
                    Resultado = x.Resultado,
                    Estado = x.Estado,
                    RequiereSeguimiento = x.RequiereSeguimiento,
                    UrlInforme = x.UrlInforme
                };
            }).ToList();

            foreach (var it in items)
                if (it.Estado == "Pendiente")
                    it.DiasPendiente = hoy.DayNumber - it.FechaDerivacion.DayNumber;

            return new PagedResult<InterconsultaListDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Data = items
            };
        }

        public async Task<List<InterconsultaEnvioInfoDto>> GetForEnvioCorreo(List<int> ids)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var raw = await (
                from i in ctx.SsInterconsulta
                join w in ctx.Worker on i.WorkerId equals w.Id
                where ids.Contains(i.Id)
                select new
                {
                    i.Id,
                    i.WorkerId,
                    WorkerNombre = w.Person != null ? w.Person.FullName : null,
                    WorkerDni = w.Person != null ? w.Person.DocumentIdentityCode : null,
                    i.Especialidad,
                    i.FechaDerivacion,
                    w.ObraOficina,
                    w.ContrataCasa,
                    w.Jefatura,
                    w.AreaScopeId,
                    JefeWorkerId = w.WorkerLessonJefeId ?? w.WorkerSalidaJefeId,
                    WorkerEmail = w.EmailCorporativo,
                    ProyAsignada = ctx.WorkerProyecto
                        .Where(wp => wp.WorkerId == w.Id && wp.FechaFin == null)
                        .OrderByDescending(wp => wp.FechaInicio)
                        .ThenByDescending(wp => wp.Id)
                        .FirstOrDefault(),
                    VincActiva = ctx.WorkerVinculacion
                        .Where(v => v.WorkerId == w.Id && v.FechaFin == null)
                        .OrderByDescending(v => v.CreatedAt)
                        .ThenByDescending(v => v.Id)
                        .FirstOrDefault()
                }
            ).ToListAsync();

            var proyectoIds = raw
                .Select(x => x.ProyAsignada?.ProyectoId ?? x.VincActiva?.ProyectoId)
                .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var empresaIds = raw
                .Select(x => x.ProyAsignada?.EmpresaId ?? x.VincActiva?.EmpresaId)
                .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var jefaturaNombres = raw.Where(x => !string.IsNullOrWhiteSpace(x.Jefatura)).Select(x => x.Jefatura!).Distinct().ToList();
            var jefeWorkerIds = raw.Where(x => x.JefeWorkerId.HasValue).Select(x => x.JefeWorkerId!.Value).Distinct().ToList();

            var proyectoMap = await ctx.Project
                .Where(p => proyectoIds.Contains(p.ProjectId))
                .ToDictionaryAsync(p => p.ProjectId, p => p);
            var empresaMap = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId, c => c);
            var jefaturaMap = await ctx.CatJefatura
                .Where(j => jefaturaNombres.Contains(j.Nombre) && j.Activo)
                .ToDictionaryAsync(j => j.Nombre, j => j.Email);
            var jefeWorkerMap = await ctx.Worker
                .Where(jw => jefeWorkerIds.Contains(jw.Id))
                .Select(jw => new { jw.Id, Nombre = jw.Person != null ? jw.Person.FullName : null, jw.EmailCorporativo })
                .ToDictionaryAsync(jw => jw.Id, jw => jw);
            var (parentByScope, candidatosPorScope) = await LoadAreaJefeContextAsync(ctx);

            return raw.Select(x =>
            {
                var esOficinaCentral = string.Equals(x.ObraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase);
                var proyectoId = esOficinaCentral ? null : (x.ProyAsignada?.ProyectoId ?? x.VincActiva?.ProyectoId);
                var empresaId = x.ProyAsignada?.EmpresaId ?? x.VincActiva?.EmpresaId;
                Project? proyecto = proyectoId.HasValue && proyectoMap.TryGetValue(proyectoId.Value, out var p) ? p : null;
                Contributor? empresa = empresaId.HasValue && empresaMap.TryGetValue(empresaId.Value, out var e) ? e : null;

                string? jefaturaNombre = null;
                string? jefaturaEmail = null;
                if (x.JefeWorkerId.HasValue && jefeWorkerMap.TryGetValue(x.JefeWorkerId.Value, out var jefe))
                {
                    jefaturaNombre = jefe.Nombre;
                    jefaturaEmail = jefe.EmailCorporativo;
                }
                else if (!string.IsNullOrWhiteSpace(x.Jefatura))
                {
                    jefaturaNombre = x.Jefatura;
                    jefaturaMap.TryGetValue(x.Jefatura, out jefaturaEmail);
                }
                else
                {
                    var porArea = ResolveJefePorArea(x.AreaScopeId, x.WorkerId, parentByScope, candidatosPorScope);
                    jefaturaNombre = porArea.Nombre;
                    jefaturaEmail = porArea.Email;
                }

                return new InterconsultaEnvioInfoDto
                {
                    Id = x.Id,
                    WorkerId = x.WorkerId,
                    WorkerNombre = x.WorkerNombre,
                    WorkerDni = x.WorkerDni,
                    Especialidad = x.Especialidad,
                    FechaDerivacion = x.FechaDerivacion,
                    DiasPendiente = hoy.DayNumber - x.FechaDerivacion.DayNumber,
                    WorkerEmailCorporativo = x.WorkerEmail,
                    ObraOficina = x.ObraOficina,
                    ContrataCasa = x.ContrataCasa,
                    Jefatura = jefaturaNombre,
                    JefaturaEmail = jefaturaEmail,
                    ProyectoId = proyectoId,
                    ProyectoNombre = esOficinaCentral ? "Oficina Central" : proyecto?.ProjectDescription,
                    ProyectoEmailCoordAdmin = proyecto?.EmailCoordAdmin,
                    ProyectoEmailResidente = proyecto?.EmailResidente,
                    ProyectoEmailResponsable = proyecto?.EmailResponsable,
                    ProyectoEmailRrhh = proyecto?.EmailRrhh,
                    ProyectoEmailCoordSsoma = proyecto?.EmailCoordSsoma,
                    ContributorId = empresaId,
                    ContributorNombre = empresa?.ContributorName,
                    ContributorEmailAdministrador = empresa?.EmailAdministrador
                };
            }).ToList();
        }

        public async Task<InterconsultaDetalleDto> GetById(int id)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var row = await (
                from i in ctx.SsInterconsulta
                join w in ctx.Worker on i.WorkerId equals w.Id
                join m in ctx.SsMedicoOcupacional on i.MedicoDerivaId equals m.Id into mj
                from m in mj.DefaultIfEmpty()
                where i.Id == id
                select new InterconsultaDetalleDto
                {
                    Id = i.Id,
                    EmoId = i.EmoId,
                    WorkerId = i.WorkerId,
                    WorkerNombre = w.Person != null ? w.Person.FullName : null,
                    WorkerDni = w.Person != null ? w.Person.DocumentIdentityCode : null,
                    Especialidad = i.Especialidad,
                    Medico = m != null ? m.ApellidoNombre : null,
                    FechaDerivacion = i.FechaDerivacion,
                    FechaAtencion = i.FechaAtencion,
                    CentroAtencion = i.CentroAtencion,
                    Diagnostico = i.Diagnostico,
                    Cie10 = i.Cie10,
                    Resultado = i.Resultado,
                    UrlInforme = i.UrlInforme,
                    Estado = i.Estado,
                    RequiereSeguimiento = i.RequiereSeguimiento
                }
            ).FirstOrDefaultAsync() ?? throw new AbrilException("Interconsulta no encontrada.", 404);

            if (row.Estado == "Pendiente")
                row.DiasPendiente = hoy.DayNumber - row.FechaDerivacion.DayNumber;

            return row;
        }

        public async Task<int> Create(InterconsultaCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            if (dto.EmoId.HasValue)
                _ = await ctx.WorkerEmo.FirstOrDefaultAsync(e => e.Id == dto.EmoId.Value)
                    ?? throw new AbrilException("EMO no encontrado.", 404);
            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == dto.WorkerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var ent = new SsInterconsulta
            {
                EmoId = dto.EmoId,
                WorkerId = dto.WorkerId,
                Especialidad = dto.Especialidad,
                MedicoDerivaId = dto.MedicoDerivaId,
                FechaDerivacion = DateOnly.FromDateTime(DateTime.Today),
                CentroAtencion = dto.CentroAtencion,
                RequiereSeguimiento = dto.RequiereSeguimiento,
                UrlInforme = dto.UrlInforme,
                Estado = "Pendiente",
                RegistradoPorId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            ctx.SsInterconsulta.Add(ent);

            // Mover la programación activa a "En Interconsulta" para que
            // no aparezca en la agenda normal hasta que la clínica suba el levantamiento.
            var prog = await ctx.SsProgramacionEmo
                .Where(p => p.WorkerId == dto.WorkerId
                         && p.Estado != "Completado"
                         && p.Estado != "Cancelado"
                         && p.Estado != "Rechazado por Clínica"
                         && p.Estado != "En Interconsulta")
                .OrderByDescending(p => p.FechaProgramada)
                .FirstOrDefaultAsync();
            if (prog != null)
            {
                prog.Estado = "En Interconsulta";
                prog.UpdatedAt = DateTimeOffset.UtcNow;
            }

            var lecturaEmo = await ctx.SsHabTrabajador
                .FirstOrDefaultAsync(h => h.WorkerId == dto.WorkerId && h.ItemId == 25);
            if (lecturaEmo != null)
            {
                lecturaEmo.Estado = "En revision";
                lecturaEmo.ObsAbril = $"Interconsulta pendiente — {dto.Especialidad}";
                lecturaEmo.UpdatedAt = DateTime.UtcNow;
            }

            await ctx.SaveChangesAsync();
            return ent.Id;
        }

        public async Task Update(int id, InterconsultaUpdateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var ent = await ctx.SsInterconsulta.FirstOrDefaultAsync(i => i.Id == id)
                ?? throw new AbrilException("Interconsulta no encontrada.", 404);
            ent.Especialidad = dto.Especialidad;
            ent.MedicoDerivaId = dto.MedicoDerivaId;
            ent.FechaDerivacion = dto.FechaDerivacion;
            ent.FechaAtencion = dto.FechaAtencion;
            ent.CentroAtencion = dto.CentroAtencion;
            ent.Diagnostico = dto.Diagnostico;
            ent.Cie10 = dto.Cie10;
            ent.Resultado = dto.Resultado;
            ent.UrlInforme = dto.UrlInforme;
            ent.RequiereSeguimiento = dto.RequiereSeguimiento;
            ent.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task UpdateResultado(int id, InterconsultaResultadoPatchDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var ent = await ctx.SsInterconsulta.FirstOrDefaultAsync(i => i.Id == id)
                ?? throw new AbrilException("Interconsulta no encontrada.", 404);
            ent.Estado = dto.Estado;
            if (dto.FechaAtencion.HasValue) ent.FechaAtencion = dto.FechaAtencion;
            if (dto.Diagnostico != null) ent.Diagnostico = dto.Diagnostico;
            if (dto.Cie10 != null) ent.Cie10 = dto.Cie10;
            if (dto.Resultado != null) ent.Resultado = dto.Resultado;
            if (dto.UrlInforme != null) ent.UrlInforme = dto.UrlInforme;
            if (dto.RequiereSeguimiento.HasValue) ent.RequiereSeguimiento = dto.RequiereSeguimiento.Value;
            ent.UpdatedAt = DateTimeOffset.UtcNow;

            if (dto.Estado == "Atendida" || dto.Estado == "Completado")
            {
                WorkerEmo? emo = null;
                if (ent.EmoId.HasValue)
                {
                    emo = await ctx.WorkerEmo.FirstOrDefaultAsync(e => e.Id == ent.EmoId.Value);
                    if (emo != null)
                    {
                        emo.InterconsultaResuelta = true;
                        emo.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                }

                var lecturaEmo = await ctx.SsHabTrabajador
                    .FirstOrDefaultAsync(h => h.WorkerId == ent.WorkerId && h.ItemId == 25);
                if (lecturaEmo != null)
                {
                    lecturaEmo.Estado = "En revision";
                    lecturaEmo.ObsAbril = $"Interconsulta levantada — pendiente EMO — {dto.FechaAtencion}";
                    lecturaEmo.UpdatedAt = DateTime.UtcNow;
                }

                // Se ubica la programación por el vínculo real (EmoResultadoId), no por heurística de
                // "la más reciente del trabajador" — con varias programaciones abiertas esa heurística
                // podía tocar la fila equivocada.
                var prog = ent.EmoId.HasValue
                    ? await ctx.SsProgramacionEmo.FirstOrDefaultAsync(p => p.EmoResultadoId == ent.EmoId.Value)
                    : null;

                if (prog != null)
                {
                    prog.Estado = "En Atención";
                    prog.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else if (emo != null)
                {
                    // El EMO se registró sin pasar por una cita agendada (sin fila en
                    // ss_programacion_emos que reabrir), así que se crea una para que el caso
                    // aparezca en la bandeja de Atenciones y se pueda subir lectura/EMO y dar aptitud.
                    ctx.SsProgramacionEmo.Add(new SsProgramacionEmo
                    {
                        WorkerId = ent.WorkerId,
                        TipoEmoId = emo.TipoEmoId ?? 0,
                        FechaProgramada = DateOnly.FromDateTime(DateTime.UtcNow),
                        ClinicaId = emo.ClinicaId,
                        MedicoId = emo.MedicoId,
                        Estado = "En Atención",
                        EmoResultadoId = emo.Id,
                        Origen = "Interconsulta",
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
            }

            await ctx.SaveChangesAsync();
        }

        public async Task UpdateDerivacion(int id, InterconsultaDerivacionPatchDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var ent = await ctx.SsInterconsulta.FirstOrDefaultAsync(i => i.Id == id)
                ?? throw new AbrilException("Interconsulta no encontrada.", 404);
            ent.Especialidad = dto.Especialidad;
            ent.Diagnostico = dto.Diagnostico;
            ent.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // ── Resolución de jefatura por árbol area_scope (fallback cuando no hay
        // worker_lesson_jefe_id/worker_salida_jefe_id ni workers.jefatura) ──────────
        // Mismo algoritmo que ApproverResolver (Solicitud de Salidas): camina hacia
        // arriba por area_scope buscando el primer Jefe/Sub Gerente/Coordinador/Gerente
        // con correo corporativo.

        private static readonly string[] CategoriasLider = { "Jefe", "Sub Gerente", "Coordinador", "Gerente" };

        private static async Task<(Dictionary<int, int?> ParentByScope, List<(int WorkerId, int AreaScopeId, string Categoria, string? Nombre, string? Email)> Candidatos)>
            LoadAreaJefeContextAsync(AppDbContext ctx)
        {
            var parentByScope = await ctx.AreaScope
                .AsNoTracking()
                .Select(s => new { s.AreaScopeId, s.AreaScopeParentId })
                .ToDictionaryAsync(s => s.AreaScopeId, s => s.AreaScopeParentId);

            var candidatos = await (
                from w in ctx.Worker.AsNoTracking()
                join cat in ctx.WorkersCategory on w.WorkerCategoryId equals cat.WorkersCategoryId
                where w.AreaScopeId != null
                      && CategoriasLider.Contains(cat.Name)
                      && w.EmailCorporativo != null && w.EmailCorporativo != ""
                select new { w.Id, AreaScopeId = w.AreaScopeId!.Value, Categoria = cat.Name, Nombre = w.Person != null ? w.Person.FullName : null, w.EmailCorporativo }
            ).ToListAsync();

            var lista = candidatos.Select(c => (c.Id, c.AreaScopeId, c.Categoria, c.Nombre, c.EmailCorporativo)).ToList();
            return (parentByScope, lista);
        }

        private static int CategoriaPriority(string categoria) => categoria switch
        {
            "Jefe" => 1,
            "Sub Gerente" => 2,
            "Coordinador" => 3,
            "Gerente" => 4,
            _ => 99,
        };

        private static (string? Nombre, string? Email) ResolveJefePorArea(
            int? areaScopeId,
            int excludeWorkerId,
            Dictionary<int, int?> parentByScope,
            List<(int WorkerId, int AreaScopeId, string Categoria, string? Nombre, string? Email)> candidatos)
        {
            if (!areaScopeId.HasValue) return (null, null);

            var porScope = candidatos.ToLookup(c => c.AreaScopeId);

            var seen = new HashSet<int>();
            int? curr = areaScopeId;
            while (curr.HasValue && seen.Add(curr.Value))
            {
                var elegido = porScope[curr.Value]
                    .Where(c => c.WorkerId != excludeWorkerId)
                    .OrderBy(c => CategoriaPriority(c.Categoria))
                    .FirstOrDefault();

                if (elegido.Email != null)
                    return (elegido.Nombre, elegido.Email);

                parentByScope.TryGetValue(curr.Value, out var parent);
                curr = parent;
            }

            // Nadie en la cadena de ancestros directos (ej. "Residencia" cuelga de "Gerencia de
            // Proyectos", pero el Gerente está asignado a un área hermana como "Unidad de
            // Proyectos", no al nodo padre en sí). Se busca entonces cualquier Gerente que cuelgue
            // de la misma raíz del árbol — igual que el fallback de ApproverResolver.
            var rootId = RootOf(areaScopeId.Value, parentByScope);
            var gerente = candidatos
                .Where(c => c.WorkerId != excludeWorkerId && c.Categoria == "Gerente"
                            && RootOf(c.AreaScopeId, parentByScope) == rootId)
                .FirstOrDefault();

            return gerente.Email != null ? (gerente.Nombre, gerente.Email) : (null, null);
        }

        /// <summary>Camina hacia arriba devolviendo el id de la raíz de un scope.</summary>
        private static int RootOf(int scopeId, Dictionary<int, int?> parentByScope)
        {
            var seen = new HashSet<int>();
            int curr = scopeId;
            while (seen.Add(curr) && parentByScope.TryGetValue(curr, out var parent) && parent.HasValue)
                curr = parent.Value;
            return curr;
        }
    }
}
