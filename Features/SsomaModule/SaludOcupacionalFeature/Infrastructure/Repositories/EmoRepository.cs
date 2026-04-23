using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Emo;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class EmoRepository : IEmoRepository
    {
        private const int PageSize = 10;
        private readonly IDbContextFactory<AppDbContext> _factory;

        public EmoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
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
                join em in ctx.Empresa on e.EmpresaOrigenId equals em.Id into ej
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
                    WorkerNombre = x.w.ApellidoNombre,
                    WorkerDni = x.w.Dni,
                    TipoEmo = x.t != null ? x.t.Nombre : null,
                    Empresa = x.em != null ? x.em.RazonSocial : null,
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

        public async Task<EmoDetalleDto> GetById(int id)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var row = await (
                from e in ctx.WorkerEmo
                join w in ctx.Worker on e.WorkerId equals w.Id
                join t in ctx.SsEmoTipo on e.TipoEmoId equals t.Id into tj
                from t in tj.DefaultIfEmpty()
                join em in ctx.Empresa on e.EmpresaOrigenId equals em.Id into ej
                from em in ej.DefaultIfEmpty()
                join c in ctx.SsClinica on e.ClinicaId equals c.Id into cj
                from c in cj.DefaultIfEmpty()
                join m in ctx.SsMedicoOcupacional on e.MedicoId equals m.Id into mj
                from m in mj.DefaultIfEmpty()
                where e.Id == id
                select new { e, w, t, em, c, m }
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
                join em in ctx.Empresa on cv.EmpresaDestinoId equals em.Id into ej
                from em in ej.DefaultIfEmpty()
                where cv.EmoId == id
                orderby cv.FechaConvalidacion descending
                select new EmoConvalidacionResumenDto
                {
                    Id = cv.Id,
                    EmpresaDestinoId = cv.EmpresaDestinoId,
                    EmpresaDestinoNombre = em != null ? em.RazonSocial : null,
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
                WorkerNombre = row.w.ApellidoNombre,
                WorkerDni = row.w.Dni,
                TipoEmoId = row.e.TipoEmoId,
                TipoEmoNombre = row.t != null ? row.t.Nombre : null,
                EmpresaOrigenId = row.e.EmpresaOrigenId,
                EmpresaOrigenNombre = row.em != null ? row.em.RazonSocial : null,
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

            var w = await ctx.Worker.FirstOrDefaultAsync(x => x.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var vinculaciones = await (
                from v in ctx.WorkerVinculacion
                join em in ctx.Empresa on v.EmpresaId equals em.Id into ej
                from em in ej.DefaultIfEmpty()
                where v.WorkerId == workerId
                orderby v.FechaInicio descending
                select new VinculacionHistorialDto
                {
                    Id = v.Id,
                    EmpresaId = v.EmpresaId,
                    EmpresaNombre = em != null ? em.RazonSocial : null,
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
                join em in ctx.Empresa on e.EmpresaOrigenId equals em.Id into ej
                from em in ej.DefaultIfEmpty()
                where e.WorkerId == workerId
                orderby e.FechaEmo descending
                select new EmoListItemDto
                {
                    Id = e.Id,
                    WorkerId = e.WorkerId,
                    WorkerNombre = w.ApellidoNombre,
                    WorkerDni = w.Dni,
                    TipoEmo = t != null ? t.Nombre : null,
                    Empresa = em != null ? em.RazonSocial : null,
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
                ApellidoNombre = w.ApellidoNombre,
                Dni = w.Dni,
                ContrataCasa = w.ContrataCasa,
                HabilitadoObra = w.HabilitadoObra,
                Vinculaciones = vinculaciones
            };
        }

        public async Task<int> Create(EmoCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var tipo = await ctx.SsEmoTipo.FirstOrDefaultAsync(t => t.Id == dto.TipoEmoId)
                ?? throw new AbrilException("Tipo de EMO no válido.", 400);
            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == dto.WorkerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var fechaVencCalc = tipo.VigenciaMeses > 0
                ? (DateOnly?)dto.FechaEmo.AddMonths(tipo.VigenciaMeses)
                : null;

            var emo = new WorkerEmo
            {
                WorkerId = dto.WorkerId,
                EmpresaOrigenId = dto.EmpresaOrigenId,
                TipoEmoId = dto.TipoEmoId,
                FechaEmo = dto.FechaEmo,
                FechaVencimiento = fechaVencCalc,
                FechaVencimientoCalculada = fechaVencCalc,
                ClinicaId = dto.ClinicaId,
                MedicoId = dto.MedicoId,
                Aptitud = dto.Aptitud,
                RequiereInterconsulta = dto.RequiereInterconsulta,
                NumeroInforme = dto.NumeroInforme,
                UrlResultado = dto.UrlResultado,
                Notas = dto.Notas,
                Estado = "Vigente",
                Activo = true,
                RegistradoPorId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            ctx.WorkerEmo.Add(emo);
            await ctx.SaveChangesAsync();

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

            if (dto.Aptitud == "Observado" && dto.RequiereInterconsulta)
            {
                ctx.SsInterconsulta.Add(new SsInterconsulta
                {
                    EmoId = emo.Id,
                    WorkerId = emo.WorkerId,
                    Especialidad = "Por definir",
                    MedicoDerivaId = dto.MedicoId,
                    FechaDerivacion = dto.FechaEmo,
                    Estado = "Pendiente",
                    RequiereSeguimiento = false,
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

            await ctx.SaveChangesAsync();
            return emo.Id;
        }

        public async Task Update(int id, EmoUpdateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var emo = await ctx.WorkerEmo.FirstOrDefaultAsync(e => e.Id == id)
                ?? throw new AbrilException("EMO no encontrado.", 404);

            var tipo = await ctx.SsEmoTipo.FirstOrDefaultAsync(t => t.Id == dto.TipoEmoId)
                ?? throw new AbrilException("Tipo de EMO no válido.", 400);

            var fechaVencCalc = tipo.VigenciaMeses > 0
                ? (DateOnly?)dto.FechaEmo.AddMonths(tipo.VigenciaMeses)
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

            if (dto.Aptitud == "No Apto")
            {
                var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == emo.WorkerId);
                if (worker != null)
                {
                    worker.HabilitadoObra = false;
                    worker.UpdatedAt = DateTimeOffset.UtcNow;
                }
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
    }
}
