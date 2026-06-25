using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Emo;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Features.Habilitacion.Infrastructure.Helpers;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class EmoRepository : IEmoRepository
    {
        private const int PageSize = 10;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ISharePointHabService _sharePoint;
        private readonly ILogger<EmoRepository> _logger;

        public EmoRepository(
            IDbContextFactory<AppDbContext> factory,
            ISharePointHabService sharePoint,
            ILogger<EmoRepository> logger)
        {
            _factory = factory;
            _sharePoint = sharePoint;
            _logger = logger;
        }

        public async Task<PagedResult<EmoListItemDto>> ListPaged(EmoFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var q =
                from e in ctx.WorkerEmo
                join w in ctx.Worker on e.WorkerId equals w.Id
                join t in ctx.SsEmoTipo on e.TipoEmoId equals t.Id into tj
                from t in tj.DefaultIfEmpty()
                join em in ctx.Contributor on e.EmpresaOrigenId equals em.ContributorId into ej
                from em in ej.DefaultIfEmpty()
                select new { e, w, t, em };

            if (filter.WorkerId.HasValue)
                q = q.Where(x => x.e.WorkerId == filter.WorkerId.Value);
            if (!string.IsNullOrWhiteSpace(filter.Estado))
                q = q.Where(x => x.e.Estado == filter.Estado);
            if (!string.IsNullOrWhiteSpace(filter.Aptitud))
                q = q.Where(x => x.e.Aptitud == filter.Aptitud);
            if (filter.EmpresaId.HasValue)
                q = q.Where(x => x.e.EmpresaOrigenId == filter.EmpresaId.Value);

            var total = await q.CountAsync();
            var page = filter.Page < 1 ? 1 : filter.Page;

            var items = await q
                .OrderByDescending(x => x.e.FechaEmo)
                .ThenByDescending(x => x.e.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(x => new EmoListItemDto
                {
                    Id = x.e.Id,
                    WorkerId = x.e.WorkerId,
                    WorkerNombre = x.w.Person != null ? x.w.Person.FullName : null,
                    WorkerDni = x.w.Person != null ? x.w.Person.DocumentIdentityCode : null,
                    TipoEmo = x.t != null ? x.t.Nombre : null,
                    Empresa = x.em != null ? x.em.ContributorName : null,
                    FechaEmo = x.e.FechaEmo,
                    FechaVencimiento = x.e.FechaVencimientoCalculada ?? x.e.FechaVencimiento,
                    Aptitud = x.e.Aptitud,
                    Estado = x.e.Estado
                })
                .ToListAsync();

            foreach (var it in items)
            {
                if (it.FechaVencimiento.HasValue)
                    it.DiasParaVencer = it.FechaVencimiento.Value.DayNumber - hoy.DayNumber;
            }

            return new PagedResult<EmoListItemDto>
            {
                Page = page,
                PageSize = PageSize,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)PageSize),
                Data = items
            };
        }

        public async Task<PagedResult<EmoPorTrabajadorDto>> ListPorTrabajador(EmoPorTrabajadorFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            // Último EMO activo por worker (WHERE NOT EXISTS emo posterior del mismo worker)
            var ultimoEmo = ctx.WorkerEmo
                .Where(e => e.Activo)
                .Where(e => !ctx.WorkerEmo.Any(e2 =>
                    e2.Activo
                    && e2.WorkerId == e.WorkerId
                    && (e2.FechaEmo > e.FechaEmo || (e2.FechaEmo == e.FechaEmo && e2.Id > e.Id))));

            // Vinculación vigente por worker (la más reciente cuya fecha_fin es null o >= hoy)
            var vinculacionVigente = ctx.WorkerVinculacion
                .Where(v => v.FechaFin == null || v.FechaFin >= hoy)
                .Where(v => !ctx.WorkerVinculacion.Any(v2 =>
                    (v2.FechaFin == null || v2.FechaFin >= hoy)
                    && v2.WorkerId == v.WorkerId
                    && (v2.FechaInicio > v.FechaInicio || (v2.FechaInicio == v.FechaInicio && v2.Id > v.Id))));

            var q =
                from w in ctx.Worker
                join ue in ultimoEmo on w.Id equals ue.WorkerId into ueJ
                from ue in ueJ.DefaultIfEmpty()
                join t in ctx.SsEmoTipo on ue.TipoEmoId equals t.Id into tJ
                from t in tJ.DefaultIfEmpty()
                join vv in vinculacionVigente on w.Id equals vv.WorkerId into vvJ
                from vv in vvJ.DefaultIfEmpty()
                join em in ctx.Contributor on vv.EmpresaId equals em.ContributorId into emJ
                from em in emJ.DefaultIfEmpty()
                join eop in ctx.Contributor on ue.EmpresaOrigenId equals eop.ContributorId into eopJ
                from eop in eopJ.DefaultIfEmpty()
                join proy in ctx.Project on (vv != null ? vv.ProyectoId : -1) equals proy.ProjectId into proyJ
                from proy in proyJ.DefaultIfEmpty()
                select new { w, ue, t, vv, em, eop, proy };

            q = q.Where(x => x.em != null && x.em.EsAbril);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                q = q.Where(x =>
                    (x.w.Person != null && x.w.Person.FullName != null &&
                     EF.Functions.ILike(x.w.Person.FullName, $"%{term}%"))
                    || (x.w.Person != null && x.w.Person.DocumentIdentityCode != null &&
                     EF.Functions.ILike(x.w.Person.DocumentIdentityCode, $"%{term}%")));
            }
            if (!string.IsNullOrWhiteSpace(filter.Aptitud))
                q = q.Where(x => x.ue != null && x.ue.Aptitud == filter.Aptitud);
            if (!string.IsNullOrWhiteSpace(filter.Estado))
            {
                if (filter.Estado == "Sin EMO")
                    q = q.Where(x => x.ue == null);
                else
                    q = q.Where(x => x.ue != null && x.ue.Estado == filter.Estado);
            }
            if (filter.EmpresaId.HasValue)
                q = q.Where(x => x.vv != null && x.vv.EmpresaId == filter.EmpresaId.Value);

            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize <= 0 ? 50 : Math.Min(filter.PageSize, 200);

            var total = await q.CountAsync();

            var rows = await q
                .OrderBy(x => x.w.Person != null ? x.w.Person.FullName : null)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new EmoPorTrabajadorDto
                {
                    WorkerId = x.w.Id,
                    NombreCompleto = (x.w.Person != null ? x.w.Person.FullName : null) ?? string.Empty,
                    Dni = (x.w.Person != null ? x.w.Person.DocumentIdentityCode : null) ?? string.Empty,
                    EmpresaId = x.vv != null ? x.vv.EmpresaId : null,
                    Empresa = x.em != null ? x.em.ContributorName : null,
                    EmpresaOrigenNombre = x.eop != null ? x.eop.ContributorName : null,
                    ProyectoNombre = x.proy != null ? x.proy.ProjectDescription : null,
                    ObraOficina = x.w.ObraOficina,
                    TipoContrata = x.w.ContrataCasa,
                    TieneEmo = x.ue != null,
                    EmoId = x.ue != null ? x.ue.Id : (int?)null,
                    TipoEmo = x.t != null ? x.t.Nombre : null,
                    FechaEmo = x.ue != null ? (DateOnly?)x.ue.FechaEmo : null,
                    FechaVencimiento = x.ue != null ? (x.ue.FechaVencimientoCalculada ?? x.ue.FechaVencimiento) : null,
                    Aptitud = x.ue != null ? x.ue.Aptitud : null,
                    Estado = x.ue != null ? x.ue.Estado : null
                })
                .ToListAsync();

            foreach (var r in rows)
            {
                if (r.FechaVencimiento.HasValue)
                    r.DiasRestantes = r.FechaVencimiento.Value.DayNumber - hoy.DayNumber;
            }

            return new PagedResult<EmoPorTrabajadorDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Data = rows
            };
        }

        public async Task<EmoDetalleDto> GetById(int id)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var row = await (
                from e in ctx.WorkerEmo
                join w in ctx.Worker on e.WorkerId equals w.Id
                join t in ctx.SsEmoTipo on e.TipoEmoId equals t.Id into tj
                from t in tj.DefaultIfEmpty()
                join em in ctx.Contributor on e.EmpresaOrigenId equals em.ContributorId into ej
                from em in ej.DefaultIfEmpty()
                join c in ctx.SsClinica on e.ClinicaId equals c.Id into cj
                from c in cj.DefaultIfEmpty()
                join m in ctx.SsMedicoOcupacional on e.MedicoId equals m.Id into mj
                from m in mj.DefaultIfEmpty()
                where e.Id == id
                select new
                {
                    e, w, t, em, c, m,
                    WorkerNombre = w.Person != null ? w.Person.FullName : null,
                    WorkerDni = w.Person != null ? w.Person.DocumentIdentityCode : null
                }
            ).FirstOrDefaultAsync()
              ?? throw new AbrilException("EMO no encontrado.", 404);

            var examenes = await (
                from d in ctx.SsEmoExamenDetalle
                join x in ctx.SsExamenTipo on d.ExamenTipoId equals x.Id
                where d.EmoId == id
                select new EmoExamenDetalleDto
                {
                    Id = d.Id,
                    ExamenTipoId = d.ExamenTipoId,
                    ExamenNombre = x.Nombre,
                    Categoria = x.Categoria,
                    Resultado = d.Resultado,
                    Valor = d.Valor,
                    Unidad = d.Unidad,
                    Observacion = d.Observacion
                }).ToListAsync();

            var restricciones = await (
                from r in ctx.SsEmoRestriccion
                join rt in ctx.SsRestriccionTipo on r.RestriccionTipoId equals rt.Id into rtj
                from rt in rtj.DefaultIfEmpty()
                where r.EmoId == id
                select new EmoRestriccionDetalleDto
                {
                    Id = r.Id,
                    RestriccionTipoId = r.RestriccionTipoId,
                    RestriccionDescripcion = rt != null ? rt.Descripcion : null,
                    DescripcionLibre = r.DescripcionLibre,
                    Vigente = r.Vigente
                }).ToListAsync();

            var convalidaciones = await (
                from cv in ctx.WorkerEmoConvalidacion
                join em in ctx.Contributor on cv.EmpresaDestinoId equals em.ContributorId into ej
                from em in ej.DefaultIfEmpty()
                where cv.EmoId == id
                orderby cv.FechaConvalidacion descending
                select new EmoConvalidacionResumenDto
                {
                    Id = cv.Id,
                    EmpresaDestinoId = cv.EmpresaDestinoId,
                    EmpresaDestinoNombre = em != null ? em.ContributorName : null,
                    FechaConvalidacion = cv.FechaConvalidacion,
                    Resultado = cv.Resultado,
                    FechaVencimiento = cv.FechaVencimiento,
                    Observaciones = cv.Observaciones,
                    UrlDocumento = cv.UrlDocumento
                }).ToListAsync();

            var fechaVenc = row.e.FechaVencimientoCalculada ?? row.e.FechaVencimiento;

            return new EmoDetalleDto
            {
                Id = row.e.Id,
                WorkerId = row.e.WorkerId,
                WorkerNombre = row.WorkerNombre,
                WorkerDni = row.WorkerDni,
                TipoEmoId = row.e.TipoEmoId,
                TipoEmoNombre = row.t != null ? row.t.Nombre : null,
                EmpresaOrigenId = row.e.EmpresaOrigenId,
                EmpresaOrigenNombre = row.em != null ? row.em.ContributorName : null,
                FechaEmo = row.e.FechaEmo,
                FechaVencimiento = row.e.FechaVencimiento,
                FechaVencimientoCalculada = row.e.FechaVencimientoCalculada,
                ClinicaId = row.e.ClinicaId,
                ClinicaNombre = row.c != null ? row.c.Nombre : null,
                MedicoId = row.e.MedicoId,
                MedicoNombre = row.m != null ? row.m.ApellidoNombre : null,
                Aptitud = row.e.Aptitud,
                RequiereInterconsulta = row.e.RequiereInterconsulta,
                NumeroInforme = row.e.NumeroInforme,
                UrlResultado = row.e.UrlResultado,
                Estado = row.e.Estado,
                Notas = row.e.Notas,
                Activo = row.e.Activo,
                DiasParaVencer = fechaVenc.HasValue ? fechaVenc.Value.DayNumber - hoy.DayNumber : (int?)null,
                Examenes = examenes,
                Restricciones = restricciones,
                Convalidaciones = convalidaciones
            };
        }

        public async Task<WorkerEmoHistorialDto> GetHistorialByWorker(int workerId)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var w = await ctx.Worker
                .Include(x => x.Person)
                .FirstOrDefaultAsync(x => x.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var vinculaciones = await (
                from v in ctx.WorkerVinculacion
                join em in ctx.Contributor on v.EmpresaId equals em.ContributorId into ej
                from em in ej.DefaultIfEmpty()
                where v.WorkerId == workerId
                orderby v.FechaInicio descending
                select new VinculacionHistorialDto
                {
                    Id = v.Id,
                    EmpresaId = v.EmpresaId,
                    EmpresaNombre = em != null ? em.ContributorName : null,
                    Puesto = v.Puesto,
                    TipoVinculacion = v.TipoVinculacion,
                    FechaInicio = v.FechaInicio,
                    FechaFin = v.FechaFin,
                    MotivoRetiro = v.MotivoRetiro
                }).ToListAsync();

            var emos = await (
                from e in ctx.WorkerEmo
                join t in ctx.SsEmoTipo on e.TipoEmoId equals t.Id into tj
                from t in tj.DefaultIfEmpty()
                join em in ctx.Contributor on e.EmpresaOrigenId equals em.ContributorId into ej
                from em in ej.DefaultIfEmpty()
                where e.WorkerId == workerId
                orderby e.FechaEmo descending
                select new EmoListItemDto
                {
                    Id = e.Id,
                    WorkerId = e.WorkerId,
                    WorkerNombre = w.Person != null ? w.Person.FullName : null,
                    WorkerDni = w.Person != null ? w.Person.DocumentIdentityCode : null,
                    TipoEmo = t != null ? t.Nombre : null,
                    Empresa = em != null ? em.ContributorName : null,
                    FechaEmo = e.FechaEmo,
                    FechaVencimiento = e.FechaVencimientoCalculada ?? e.FechaVencimiento,
                    Aptitud = e.Aptitud,
                    Estado = e.Estado
                }).ToListAsync();

            foreach (var em in emos)
                if (em.FechaVencimiento.HasValue)
                    em.DiasParaVencer = em.FechaVencimiento.Value.DayNumber - hoy.DayNumber;

            foreach (var v in vinculaciones)
            {
                var fin = v.FechaFin ?? DateOnly.MaxValue;
                v.Emos = emos
                    .Where(e => e.FechaEmo >= v.FechaInicio && e.FechaEmo <= fin)
                    .ToList();
            }

            return new WorkerEmoHistorialDto
            {
                WorkerId = w.Id,
                ApellidoNombre = w.Person?.FullName,
                Dni = w.Person?.DocumentIdentityCode,
                ContrataCasa = w.ContrataCasa,
                HabilitadoObra = w.HabilitadoObra,
                Vinculaciones = vinculaciones
            };
        }

        public async Task<EmoCreateResultDto> Create(EmoCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var tipo = await ctx.SsEmoTipo.FirstOrDefaultAsync(t => t.Id == dto.TipoEmoId)
                ?? throw new AbrilException("Tipo de EMO no válido.", 400);
            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == dto.WorkerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var fechaVencCalc = tipo.VigenciaMeses > 0
                ? (DateOnly?)dto.FechaEmo.AddMonths(tipo.VigenciaMeses.Value)
                : null;

            var esApto = !string.Equals(dto.Aptitud, "Observado", StringComparison.OrdinalIgnoreCase)
                      && !string.Equals(dto.Aptitud, "No Apto", StringComparison.OrdinalIgnoreCase);

            var emo = new WorkerEmo
            {
                WorkerId = dto.WorkerId,
                EmpresaOrigenId = dto.EmpresaOrigenId,
                TipoEmoId = dto.TipoEmoId,
                FechaEmo = dto.FechaEmo,
                FechaVencimiento = esApto ? fechaVencCalc : null,
                FechaVencimientoCalculada = esApto ? fechaVencCalc : null,
                ClinicaId = dto.ClinicaId,
                MedicoId = dto.MedicoId,
                Aptitud = dto.Aptitud,
                RequiereInterconsulta = dto.RequiereInterconsulta,
                NumeroInforme = dto.NumeroInforme,
                FechaLectura = dto.FechaLectura,
                UrlResultado = dto.UrlResultado,
                Notas = dto.Notas,
                Estado = esApto ? "Vigente" : "Observado",
                Activo = true,
                RegistradoPorId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            var emosAnteriores = await ctx.WorkerEmo
                .Where(e => e.WorkerId == emo.WorkerId && e.Activo)
                .ToListAsync();
            foreach (var e in emosAnteriores)
            {
                e.Activo = false;
                e.UpdatedAt = DateTimeOffset.UtcNow;
            }

            ctx.WorkerEmo.Add(emo);
            await ctx.SaveChangesAsync();  // necesario para generar emo.Id antes de usarlo

            // Vincular interconsulta pendiente (sin EmoId) al nuevo EMO
            var interconsultaPendiente = await ctx.SsInterconsulta
                .Where(i => i.WorkerId == dto.WorkerId
                         && i.EmoId == null
                         && i.Estado == "Pendiente")
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefaultAsync();

            if (interconsultaPendiente != null)
            {
                interconsultaPendiente.EmoId = emo.Id;
                interconsultaPendiente.UpdatedAt = DateTimeOffset.UtcNow;

                if (dto.DocumentoInterconsulta != null && dto.DocumentoInterconsulta.Length > 0)
                {
                    try
                    {
                        using var stream = dto.DocumentoInterconsulta.OpenReadStream();
                        interconsultaPendiente.UrlInforme = await _sharePoint.SubirArchivoAsync(
                            stream, dto.DocumentoInterconsulta.FileName, "interconsulta");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Error subiendo documento de interconsulta para worker {WorkerId}", dto.WorkerId);
                    }
                }
            }

            foreach (var ex in dto.Examenes)
            {
                ctx.SsEmoExamenDetalle.Add(new SsEmoExamenDetalle
                {
                    EmoId = emo.Id,
                    ExamenTipoId = ex.ExamenTipoId,
                    Resultado = ex.Resultado,
                    Valor = ex.Valor,
                    Unidad = ex.Unidad,
                    Observacion = ex.Observacion,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            foreach (var r in dto.Restricciones)
            {
                ctx.SsEmoRestriccion.Add(new SsEmoRestriccion
                {
                    EmoId = emo.Id,
                    RestriccionTipoId = r.RestriccionTipoId,
                    DescripcionLibre = r.DescripcionLibre,
                    Vigente = r.Vigente,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            if (dto.InterconsultaInline != null ||
                (dto.Aptitud == "Observado" && dto.RequiereInterconsulta) ||
                dto.Aptitud == "No Apto")
            {
                var ic = dto.InterconsultaInline;
                ctx.SsInterconsulta.Add(new SsInterconsulta
                {
                    EmoId = emo.Id,
                    WorkerId = emo.WorkerId,
                    Especialidad = ic?.Especialidad ?? "Por definir",
                    MedicoDerivaId = ic?.MedicoDerivaId ?? dto.MedicoId,
                    FechaDerivacion = dto.FechaEmo,
                    CentroAtencion = ic?.CentroAtencion,
                    Diagnostico = ic?.Diagnostico,
                    Cie10 = ic?.Cie10,
                    Estado = "Pendiente",
                    RequiereSeguimiento = ic?.RequiereSeguimiento ?? false,
                    RegistradoPorId = userId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }

            if (dto.Aptitud == "No Apto")
            {
                worker.HabilitadoObra = false;
                worker.UpdatedAt = DateTimeOffset.UtcNow;
            }

            if (string.Equals(tipo.Nombre, "Retiro", StringComparison.OrdinalIgnoreCase))
            {
                var previos = await ctx.WorkerEmo
                    .Where(e => e.WorkerId == emo.WorkerId
                             && e.EmpresaOrigenId == emo.EmpresaOrigenId
                             && e.Id != emo.Id
                             && e.Activo)
                    .ToListAsync();
                foreach (var p in previos)
                {
                    p.Activo = false;
                    p.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            _logger.LogInformation("ArchivoLectura: {val}", dto.ArchivoLectura == null ? "NULL" : dto.ArchivoLectura.FileName);
            if (dto.ArchivoLectura != null && dto.ArchivoLectura.Length > 0)
            {
                try
                {
                    using var stream = dto.ArchivoLectura.OpenReadStream();
                    emo.UrlResultado = await _sharePoint.SubirArchivoAsync(
                        stream, dto.ArchivoLectura.FileName, "lectura-emo");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Error subiendo archivo de lectura EMO para worker {WorkerId}", dto.WorkerId);
                }
            }

            await SincronizarEntregableEmoAsync(ctx, emo, worker);

            var progActiva = await ctx.SsProgramacionEmo
                .Where(p => p.WorkerId == emo.WorkerId
                         && p.Estado == "En Atención")
                .OrderByDescending(p => p.FechaProgramada)
                .FirstOrDefaultAsync();
            if (progActiva != null)
            {
                progActiva.EmoResultadoId = emo.Id;
                progActiva.UpdatedAt = DateTimeOffset.UtcNow;
                if (emo.RequiereInterconsulta != true)
                {
                    progActiva.Estado = "Completado";
                }
                // Si requiere interconsulta, se queda en "En Atención"
                // hasta que la interconsulta sea resuelta
            }

            if (dto.FechaLectura.HasValue)
            {
                var lecturaEmo = await ctx.SsHabTrabajador
                    .Include(h => h.Item)
                    .FirstOrDefaultAsync(h => h.WorkerId == emo.WorkerId && h.ItemId == 25);
                if (lecturaEmo != null)
                {
                    lecturaEmo.Estado = "Aprobado";
                    lecturaEmo.Vigencia = HabilitacionDateHelper.AsUtc(
                        dto.FechaLectura.Value.ToDateTime(TimeOnly.MinValue));
                    lecturaEmo.UpdatedAt = DateTime.UtcNow;
                }
            }

            await ctx.SaveChangesAsync();
            return new EmoCreateResultDto
            {
                EmoId = emo.Id,
                InterconsultaId = interconsultaPendiente?.Id
            };
        }

        public async Task Update(int id, EmoUpdateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var emo = await ctx.WorkerEmo.FirstOrDefaultAsync(e => e.Id == id)
                ?? throw new AbrilException("EMO no encontrado.", 404);

            var tipo = await ctx.SsEmoTipo.FirstOrDefaultAsync(t => t.Id == dto.TipoEmoId)
                ?? throw new AbrilException("Tipo de EMO no válido.", 400);

            var fechaVencCalc = tipo.VigenciaMeses > 0
                ? (DateOnly?)dto.FechaEmo.AddMonths(tipo.VigenciaMeses.Value)
                : null;

            emo.TipoEmoId = dto.TipoEmoId;
            emo.EmpresaOrigenId = dto.EmpresaOrigenId;
            emo.FechaEmo = dto.FechaEmo;
            emo.FechaVencimiento = fechaVencCalc;
            emo.FechaVencimientoCalculada = fechaVencCalc;
            emo.ClinicaId = dto.ClinicaId;
            emo.MedicoId = dto.MedicoId;
            emo.Aptitud = dto.Aptitud;
            emo.RequiereInterconsulta = dto.RequiereInterconsulta;
            emo.NumeroInforme = dto.NumeroInforme;
            emo.UrlResultado = dto.UrlResultado;
            emo.Notas = dto.Notas;
            emo.UpdatedAt = DateTimeOffset.UtcNow;

            var examenesExistentes = await ctx.SsEmoExamenDetalle.Where(x => x.EmoId == id).ToListAsync();
            ctx.SsEmoExamenDetalle.RemoveRange(examenesExistentes);
            foreach (var ex in dto.Examenes)
            {
                ctx.SsEmoExamenDetalle.Add(new SsEmoExamenDetalle
                {
                    EmoId = id,
                    ExamenTipoId = ex.ExamenTipoId,
                    Resultado = ex.Resultado,
                    Valor = ex.Valor,
                    Unidad = ex.Unidad,
                    Observacion = ex.Observacion,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            var restriccionesExistentes = await ctx.SsEmoRestriccion.Where(x => x.EmoId == id).ToListAsync();
            ctx.SsEmoRestriccion.RemoveRange(restriccionesExistentes);
            foreach (var r in dto.Restricciones)
            {
                ctx.SsEmoRestriccion.Add(new SsEmoRestriccion
                {
                    EmoId = id,
                    RestriccionTipoId = r.RestriccionTipoId,
                    DescripcionLibre = r.DescripcionLibre,
                    Vigente = r.Vigente,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == emo.WorkerId);
            if (worker != null)
            {
                if (dto.Aptitud == "No Apto")
                {
                    worker.HabilitadoObra = false;
                    worker.UpdatedAt = DateTimeOffset.UtcNow;
                }
                await SincronizarEntregableEmoAsync(ctx, emo, worker);
            }

            await ctx.SaveChangesAsync();
        }

        public async Task UpdateEstado(int id, string estado, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var emo = await ctx.WorkerEmo.FirstOrDefaultAsync(e => e.Id == id)
                ?? throw new AbrilException("EMO no encontrado.", 404);
            emo.Estado = estado;
            emo.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        private static async Task SincronizarEntregableEmoAsync(AppDbContext ctx, WorkerEmo emo, Worker worker)
        {
            var hab = await ctx.SsHabTrabajador
                .FirstOrDefaultAsync(h => h.WorkerId == emo.WorkerId && h.ItemId == HabItemIds.CertAptitud);

            if (hab == null)
            {
                hab = new SsHabTrabajador
                {
                    WorkerId = emo.WorkerId,
                    ItemId = HabItemIds.CertAptitud,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                ctx.SsHabTrabajador.Add(hab);
            }

            switch (emo.Aptitud)
            {
                case "Apto":
                case "Apto con Restricciones":
                    hab.Estado = "Aprobado";
                    var fv = emo.FechaVencimientoCalculada ?? emo.FechaVencimiento;
                    if (fv.HasValue)
                        hab.Vigencia = DateTime.SpecifyKind(fv.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                    break;
                case "No Apto":
                    hab.Estado = "Rechazado";
                    break;
                case "Observado":
                    hab.Estado = "En plazo";
                    break;
                default:
                    return;
            }

            hab.UpdatedAt = DateTime.UtcNow;

            if (emo.UrlResultado != null &&
                (emo.Aptitud == "Apto" || emo.Aptitud == "Apto con Restricciones"))
            {
                var habLectura = await ctx.SsHabTrabajador
                    .FirstOrDefaultAsync(h => h.WorkerId == emo.WorkerId && h.ItemId == HabItemIds.LecturaEmo);
                if (habLectura != null)
                {
                    habLectura.Estado = "Aprobado";
                    var fvLectura = emo.FechaVencimientoCalculada ?? emo.FechaVencimiento;
                    if (fvLectura.HasValue)
                        habLectura.Vigencia = DateTime.SpecifyKind(fvLectura.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                    habLectura.ArchivoUrl = emo.UrlResultado;
                    habLectura.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
