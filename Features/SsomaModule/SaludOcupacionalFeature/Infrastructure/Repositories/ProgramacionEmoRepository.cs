using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class ProgramacionEmoRepository : IProgramacionEmoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ProgramacionEmoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<ProgramacionListDto>> List(ProgramacionFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            var q =
                from p in ctx.SsProgramacionEmo
                join w in ctx.Worker on p.WorkerId equals w.Id
                join em in ctx.Empresa on p.EmpresaId equals em.Id into ej
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

            return await q
                .OrderBy(x => x.p.FechaProgramada)
                .ThenBy(x => x.p.HoraProgramada)
                .Select(x => new ProgramacionListDto
                {
                    Id = x.p.Id,
                    WorkerId = x.p.WorkerId,
                    WorkerNombre = x.w.ApellidoNombre,
                    WorkerDni = x.w.Dni,
                    Empresa = x.em != null ? x.em.RazonSocial : null,
                    TipoEmo = x.t != null ? x.t.Nombre : null,
                    FechaProgramada = x.p.FechaProgramada,
                    HoraProgramada = x.p.HoraProgramada,
                    Clinica = x.c != null ? x.c.Nombre : null,
                    Medico = x.m != null ? x.m.ApellidoNombre : null,
                    Estado = x.p.Estado,
                    Motivo = x.p.Motivo,
                    EmoResultadoId = x.p.EmoResultadoId
                })
                .ToListAsync();
        }

        public async Task<int> Create(ProgramacionCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == dto.WorkerId)
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
                Estado = "Programado",
                RegistradoPorId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            ctx.SsProgramacionEmo.Add(ent);
            await ctx.SaveChangesAsync();
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
    }
}
