using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Application.Dtos;
using Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Infrastructure.Models;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Dtos;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Infrastructure.Repositories
{
    public class CroquisRepository : ICroquisRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CroquisRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<ProjectCroquisItemDto>> GetProjectsWithCroquis(string? search)
        {
            using var ctx = _factory.CreateDbContext();

            var query =
                from p in ctx.Project
                where p.State && p.Active
                join cr in ctx.ProjectCroquis.Where(c => c.State)
                    on p.ProjectId equals cr.ProjectId into croquisGroup
                from cr in croquisGroup.DefaultIfEmpty()
                select new ProjectCroquisItemDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    ProjectCroquisId = cr != null ? cr.ProjectCroquisId : (int?)null,
                    ImageUrl = cr != null ? cr.ImageUrl : null,
                    OriginalFileName = cr != null ? cr.OriginalFileName : null,
                    UpdatedDateTime = cr != null ? (cr.UpdatedDateTime ?? cr.CreatedDateTime) : (DateTime?)null,
                };

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(x => x.ProjectDescription.ToLower().Contains(s));
            }

            return await query.OrderBy(x => x.ProjectDescription).ToListAsync();
        }

        public async Task UpsertCroquis(int projectId, string imageUrl, string? originalFileName, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var projectExists = await ctx.Project.AnyAsync(p => p.ProjectId == projectId && p.State);
            if (!projectExists)
                throw new AbrilException("El proyecto no existe.", 404);

            var now = DateTime.UtcNow;

            // Soft-delete del croquis activo anterior (si existe) para conservar historial/auditoría.
            var existing = await ctx.ProjectCroquis
                .Where(c => c.ProjectId == projectId && c.State)
                .ToListAsync();

            foreach (var old in existing)
            {
                old.State = false;
                old.UpdatedDateTime = now;
                old.UpdatedUserId = userId;
            }

            ctx.ProjectCroquis.Add(new ProjectCroquis
            {
                ProjectId = projectId,
                ImageUrl = imageUrl,
                OriginalFileName = originalFileName,
                CreatedDateTime = now,
                CreatedUserId = userId,
                Active = true,
                State = true,
            });

            await ctx.SaveChangesAsync();
        }

        public async Task<List<CroquisLoteDto>> GetLotes(int projectCroquisId)
        {
            using var ctx = _factory.CreateDbContext();

            var rows = await ctx.ProjectCroquisLote
                .Where(l => l.ProjectCroquisId == projectCroquisId && l.State)
                .OrderBy(l => l.ProjectCroquisLoteId)
                .Select(l => new { l.ProjectCroquisLoteId, l.NumeroLote, l.Poligono })
                .ToListAsync();

            return rows.Select(r => new CroquisLoteDto
            {
                ProjectCroquisLoteId = r.ProjectCroquisLoteId,
                NumeroLote = r.NumeroLote,
                Puntos = DeserializePuntos(r.Poligono),
            }).ToList();
        }

        public async Task ReplaceLotes(int projectCroquisId, List<CroquisLoteDto> lotes, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var croquisExists = await ctx.ProjectCroquis.AnyAsync(c => c.ProjectCroquisId == projectCroquisId && c.State);
            if (!croquisExists)
                throw new AbrilException("El croquis no existe.", 404);

            var now = DateTime.UtcNow;

            // Soft-delete de los lotes activos anteriores.
            var existing = await ctx.ProjectCroquisLote
                .Where(l => l.ProjectCroquisId == projectCroquisId && l.State)
                .ToListAsync();

            foreach (var old in existing)
            {
                old.State = false;
                old.UpdatedDateTime = now;
                old.UpdatedUserId = userId;
            }

            foreach (var lote in lotes)
            {
                if (lote.Puntos == null || lote.Puntos.Count < 3)
                    throw new AbrilException($"El lote '{lote.NumeroLote}' debe tener al menos 3 puntos.", 422);

                ctx.ProjectCroquisLote.Add(new ProjectCroquisLote
                {
                    ProjectCroquisId = projectCroquisId,
                    NumeroLote = lote.NumeroLote,
                    Poligono = JsonSerializer.Serialize(lote.Puntos),
                    CreatedDateTime = now,
                    CreatedUserId = userId,
                    Active = true,
                    State = true,
                });
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<CroquisGestionResponseDto> GetGestion()
        {
            using var ctx = _factory.CreateDbContext();

            // Catálogos para el formulario de alta de vecino (siempre se devuelven).
            var colindancias = await ctx.VecinoColindancia
                .Where(c => c.State && c.Active)
                .OrderBy(c => c.VecinoColindanciaId)
                .Select(c => new CatalogOptionDto { Id = c.VecinoColindanciaId, Descripcion = c.Descripcion })
                .ToListAsync();

            var tipos = await ctx.VecinoTipoConstruccion
                .Where(t => t.State && t.Active)
                .OrderBy(t => t.VecinoTipoConstruccionId)
                .Select(t => new CatalogOptionDto { Id = t.VecinoTipoConstruccionId, Descripcion = t.Descripcion })
                .ToListAsync();

            var projects = await ctx.Project
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new ProjectOptionDto { ProjectId = p.ProjectId, ProjectDescription = p.ProjectDescription })
                .ToListAsync();

            var response = new CroquisGestionResponseDto
            {
                Projects = projects,
                Colindancias = colindancias,
                TiposConstruccion = tipos,
            };

            // Croquis registrados (uno activo por proyecto).
            var croquis = await (
                from c in ctx.ProjectCroquis.Where(c => c.State)
                join p in ctx.Project on c.ProjectId equals p.ProjectId
                orderby p.ProjectDescription
                select new { c.ProjectCroquisId, c.ImageUrl, c.ProjectId, p.ProjectDescription }
            ).ToListAsync();

            if (croquis.Count == 0) return response;

            var croquisIds = croquis.Select(c => c.ProjectCroquisId).ToList();
            var projectIds = croquis.Select(c => c.ProjectId).ToList();

            // Lotes de esos croquis con el nombre del vecino asignado (si tiene).
            var lotes = await (
                from l in ctx.ProjectCroquisLote.Where(l => l.State && croquisIds.Contains(l.ProjectCroquisId))
                join v in ctx.Vecino on l.VecinoId equals v.VecinoId into vg
                from v in vg.DefaultIfEmpty()
                select new
                {
                    l.ProjectCroquisLoteId,
                    l.ProjectCroquisId,
                    l.NumeroLote,
                    l.Poligono,
                    l.VecinoId,
                    VecinoNombre = v != null ? v.NombrePropietario : null,
                }
            ).ToListAsync();

            // Vecinos de esos proyectos (para los desplegables de asignación).
            var vecinos = await (
                from v in ctx.Vecino.Where(v => v.State && v.Active && projectIds.Contains(v.ProjectId))
                join p in ctx.Project on v.ProjectId equals p.ProjectId
                join col in ctx.VecinoColindancia on v.VecinoColindanciaId equals col.VecinoColindanciaId
                join tc in ctx.VecinoTipoConstruccion on v.VecinoTipoConstruccionId equals tc.VecinoTipoConstruccionId
                orderby v.NombrePropietario
                select new { v, p, col, tc }
            ).ToListAsync();

            response.Croquis = croquis.Select(c => new CroquisGestionDto
            {
                ProjectId = c.ProjectId,
                ProjectDescription = c.ProjectDescription,
                ProjectCroquisId = c.ProjectCroquisId,
                ImageUrl = c.ImageUrl,
                Lotes = lotes
                    .Where(l => l.ProjectCroquisId == c.ProjectCroquisId)
                    .Select(l => new CroquisGestionLoteDto
                    {
                        ProjectCroquisLoteId = l.ProjectCroquisLoteId,
                        NumeroLote = l.NumeroLote,
                        Puntos = DeserializePuntos(l.Poligono),
                        VecinoId = l.VecinoId,
                        VecinoNombre = l.VecinoNombre,
                    })
                    .ToList(),
                Vecinos = vecinos
                    .Where(x => x.v.ProjectId == c.ProjectId)
                    .Select(x => new VecinoListItemDto
                    {
                        VecinoId = x.v.VecinoId,
                        ProjectId = x.v.ProjectId,
                        ProjectDescription = x.p.ProjectDescription,
                        Predio = x.v.Predio,
                        Direccion = x.v.Direccion,
                        InteriorDepartamento = x.v.InteriorDepartamento,
                        NombrePropietario = x.v.NombrePropietario,
                        Dni = x.v.Dni,
                        Celular = x.v.Celular,
                        VecinoColindanciaId = x.v.VecinoColindanciaId,
                        ColindanciaDescripcion = x.col.Descripcion,
                        VecinoTipoConstruccionId = x.v.VecinoTipoConstruccionId,
                        TipoConstruccionDescripcion = x.tc.Descripcion,
                        CreatedDateTime = x.v.CreatedDateTime,
                    })
                    .ToList(),
            }).ToList();

            return response;
        }

        public async Task AssignVecinoToLote(int loteId, int? vecinoId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var lote = await ctx.ProjectCroquisLote.FirstOrDefaultAsync(l => l.ProjectCroquisLoteId == loteId && l.State);
            if (lote == null)
                throw new AbrilException("El lote no existe.", 404);

            if (vecinoId.HasValue)
            {
                // El vecino debe pertenecer al mismo proyecto del croquis del lote.
                var ok = await (
                    from l in ctx.ProjectCroquisLote.Where(l => l.ProjectCroquisLoteId == loteId)
                    join c in ctx.ProjectCroquis on l.ProjectCroquisId equals c.ProjectCroquisId
                    join v in ctx.Vecino on c.ProjectId equals v.ProjectId
                    where v.VecinoId == vecinoId.Value && v.State
                    select v.VecinoId
                ).AnyAsync();

                if (!ok)
                    throw new AbrilException("El vecino no pertenece al proyecto de este croquis.", 422);
            }

            lote.VecinoId = vecinoId;
            lote.UpdatedDateTime = DateTime.UtcNow;
            lote.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        private static List<List<double>> DeserializePuntos(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<List<double>>>(json) ?? new();
            }
            catch
            {
                return new();
            }
        }
    }
}
