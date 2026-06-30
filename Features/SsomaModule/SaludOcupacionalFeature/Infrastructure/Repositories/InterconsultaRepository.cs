using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Interconsulta;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
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
                select new { i, w, m };

            if (!string.IsNullOrWhiteSpace(filter.Estado))
                q = q.Where(x => x.i.Estado == filter.Estado);
            if (filter.WorkerId.HasValue)
                q = q.Where(x => x.i.WorkerId == filter.WorkerId.Value);
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

            var items = await q
                .OrderByDescending(x => x.i.FechaDerivacion)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new InterconsultaListDto
                {
                    Id = x.i.Id,
                    EmoId = x.i.EmoId,
                    WorkerId = x.i.WorkerId,
                    WorkerNombre = x.w.Person != null ? x.w.Person.FullName : null,
                    WorkerDni = x.w.Person != null ? x.w.Person.DocumentIdentityCode : null,
                    Especialidad = x.i.Especialidad,
                    MedicoDeriva = x.m != null ? x.m.ApellidoNombre : null,
                    FechaDerivacion = x.i.FechaDerivacion,
                    FechaAtencion = x.i.FechaAtencion,
                    CentroAtencion = x.i.CentroAtencion,
                    Diagnostico = x.i.Diagnostico,
                    Resultado = x.i.Resultado,
                    Estado = x.i.Estado,
                    RequiereSeguimiento = x.i.RequiereSeguimiento,
                    UrlInforme = x.i.UrlInforme
                })
                .ToListAsync();

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
                if (ent.EmoId.HasValue)
                {
                    var emo = await ctx.WorkerEmo.FirstOrDefaultAsync(e => e.Id == ent.EmoId.Value);
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

                var prog = await ctx.SsProgramacionEmo
                    .Where(p => p.WorkerId == ent.WorkerId
                             && p.Estado != "Completado"
                             && p.Estado != "Cancelado"
                             && p.Estado != "Rechazado por Clínica")
                    .OrderByDescending(p => p.FechaProgramada)
                    .FirstOrDefaultAsync();
                if (prog != null)
                {
                    prog.Estado = "En Atención";
                    prog.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            await ctx.SaveChangesAsync();
        }
    }
}
