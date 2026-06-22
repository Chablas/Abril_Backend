using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Topico;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class TopicoRepository : ITopicoRepository
    {
        private const int PageSize = 20;
        private readonly IDbContextFactory<AppDbContext> _factory;

        public TopicoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<PagedResult<TopicoListItemDto>> ListPaged(TopicoFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            var q =
                from t in ctx.SsTopicoAtencion
                join w in ctx.Worker on t.WorkerId equals w.Id
                join em in ctx.Contributor on t.EmpresaId equals em.ContributorId into emj
                from em in emj.DefaultIfEmpty()
                join p in ctx.Project on t.ProyectoId equals p.ProjectId into pj
                from p in pj.DefaultIfEmpty()
                select new { t, w, em, p };

            if (filter.WorkerId.HasValue)
                q = q.Where(x => x.t.WorkerId == filter.WorkerId.Value);
            if (!string.IsNullOrWhiteSpace(filter.TipoAtencion))
                q = q.Where(x => x.t.TipoAtencion == filter.TipoAtencion);
            if (filter.EmpresaId.HasValue)
                q = q.Where(x => x.t.EmpresaId == filter.EmpresaId.Value);
            if (filter.ProyectoId.HasValue)
                q = q.Where(x => x.t.ProyectoId == filter.ProyectoId.Value);
            if (filter.FechaDesde.HasValue)
                q = q.Where(x => x.t.Fecha >= filter.FechaDesde.Value);
            if (filter.FechaHasta.HasValue)
                q = q.Where(x => x.t.Fecha <= filter.FechaHasta.Value);

            var total = await q.CountAsync();
            var page = filter.Page < 1 ? 1 : filter.Page;

            var items = await q
                .OrderByDescending(x => x.t.Fecha)
                .ThenByDescending(x => x.t.Hora)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(x => new TopicoListItemDto
                {
                    Id = x.t.Id,
                    WorkerId = x.t.WorkerId,
                    WorkerNombre = x.w.Person != null ? x.w.Person.FullName : null,
                    WorkerDni = x.w.Person != null ? x.w.Person.DocumentIdentityCode : null,
                    EmpresaNombre = x.em != null ? x.em.ContributorName : null,
                    ProyectoNombre = x.p != null ? x.p.ProjectDescription : null,
                    Fecha = x.t.Fecha,
                    Hora = x.t.Hora,
                    TipoAtencion = x.t.TipoAtencion,
                    Motivo = x.t.Motivo,
                    Diagnostico = x.t.Diagnostico,
                    DerivadoClinica = x.t.DerivadoClinica,
                    GeneraDescanso = x.t.GeneraDescanso,
                    GeneraAccidente = x.t.GeneraAccidente,
                    CreatedAt = x.t.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<TopicoListItemDto>
            {
                Page = page,
                PageSize = PageSize,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)PageSize),
                Data = items
            };
        }

        public async Task<TopicoDetalleDto> GetById(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var row = await (
                from t in ctx.SsTopicoAtencion
                where t.Id == id
                join w in ctx.Worker on t.WorkerId equals w.Id
                join em in ctx.Contributor on t.EmpresaId equals em.ContributorId into emj
                from em in emj.DefaultIfEmpty()
                join p in ctx.Project on t.ProyectoId equals p.ProjectId into pj
                from p in pj.DefaultIfEmpty()
                select new { t, w, em, p }
            ).FirstOrDefaultAsync()
              ?? throw new AbrilException("Atención de tópico no encontrada.", 404);

            return new TopicoDetalleDto
            {
                Id = row.t.Id,
                WorkerId = row.t.WorkerId,
                WorkerNombre = row.w.Person != null ? row.w.Person.FullName : null,
                WorkerDni = row.w.Person != null ? row.w.Person.DocumentIdentityCode : null,
                ProyectoId = row.t.ProyectoId,
                ProyectoNombre = row.p != null ? row.p.ProjectDescription : null,
                EmpresaId = row.t.EmpresaId,
                EmpresaNombre = row.em != null ? row.em.ContributorName : null,
                Fecha = row.t.Fecha,
                Hora = row.t.Hora,
                TipoAtencion = row.t.TipoAtencion,
                Motivo = row.t.Motivo,
                Diagnostico = row.t.Diagnostico,
                DiagnosticoCie10 = row.t.DiagnosticoCie10,
                Tratamiento = row.t.Tratamiento,
                Medicamentos = row.t.Medicamentos,
                PresionArterial = row.t.PresionArterial,
                Temperatura = row.t.Temperatura,
                FrecuenciaCardiaca = row.t.FrecuenciaCardiaca,
                SaturacionOxigeno = row.t.SaturacionOxigeno,
                Peso = row.t.Peso,
                DerivadoClinica = row.t.DerivadoClinica,
                ClinicaDerivacion = row.t.ClinicaDerivacion,
                GeneraDescanso = row.t.GeneraDescanso,
                DescansoDias = row.t.DescansoDias,
                GeneraAccidente = row.t.GeneraAccidente,
                AccidenteId = row.t.AccidenteId,
                Observaciones = row.t.Observaciones,
                RegistradoPorId = row.t.RegistradoPorId,
                CreatedAt = row.t.CreatedAt,
                UpdatedAt = row.t.UpdatedAt
            };
        }

        public async Task<int> Create(TopicoCreateDto dto, int registradoPorId)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = new TopicoAtencion
            {
                WorkerId = dto.WorkerId,
                Fecha = dto.Fecha,
                Hora = dto.Hora,
                TipoAtencion = dto.TipoAtencion,
                Motivo = dto.Motivo,
                Diagnostico = dto.Diagnostico,
                DiagnosticoCie10 = dto.DiagnosticoCie10,
                Tratamiento = dto.Tratamiento,
                Medicamentos = dto.Medicamentos,
                PresionArterial = dto.PresionArterial,
                Temperatura = dto.Temperatura,
                FrecuenciaCardiaca = dto.FrecuenciaCardiaca,
                SaturacionOxigeno = dto.SaturacionOxigeno,
                Peso = dto.Peso,
                DerivadoClinica = dto.DerivadoClinica,
                ClinicaDerivacion = dto.ClinicaDerivacion,
                GeneraDescanso = dto.GeneraDescanso,
                DescansoDias = dto.DescansoDias,
                GeneraAccidente = dto.GeneraAccidente,
                ProyectoId = dto.ProyectoId,
                EmpresaId = dto.EmpresaId,
                Observaciones = dto.Observaciones,
                RegistradoPorId = registradoPorId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            ctx.SsTopicoAtencion.Add(entity);
            await ctx.SaveChangesAsync();
            return entity.Id;
        }

        public async Task Update(int id, TopicoUpdateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.SsTopicoAtencion.FirstOrDefaultAsync(t => t.Id == id)
                ?? throw new AbrilException("Atención de tópico no encontrada.", 404);

            entity.TipoAtencion = dto.TipoAtencion;
            entity.Motivo = dto.Motivo;
            entity.Diagnostico = dto.Diagnostico;
            entity.DiagnosticoCie10 = dto.DiagnosticoCie10;
            entity.Tratamiento = dto.Tratamiento;
            entity.Medicamentos = dto.Medicamentos;
            entity.PresionArterial = dto.PresionArterial;
            entity.Temperatura = dto.Temperatura;
            entity.FrecuenciaCardiaca = dto.FrecuenciaCardiaca;
            entity.SaturacionOxigeno = dto.SaturacionOxigeno;
            entity.Peso = dto.Peso;
            entity.DerivadoClinica = dto.DerivadoClinica;
            entity.ClinicaDerivacion = dto.ClinicaDerivacion;
            entity.GeneraDescanso = dto.GeneraDescanso;
            entity.DescansoDias = dto.DescansoDias;
            entity.GeneraAccidente = dto.GeneraAccidente;
            entity.ProyectoId = dto.ProyectoId;
            entity.EmpresaId = dto.EmpresaId;
            entity.Observaciones = dto.Observaciones;
            entity.UpdatedAt = DateTimeOffset.UtcNow;

            await ctx.SaveChangesAsync();
        }

        public async Task Delete(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.SsTopicoAtencion.FirstOrDefaultAsync(t => t.Id == id)
                ?? throw new AbrilException("Atención de tópico no encontrada.", 404);

            ctx.SsTopicoAtencion.Remove(entity);
            await ctx.SaveChangesAsync();
        }
    }
}
