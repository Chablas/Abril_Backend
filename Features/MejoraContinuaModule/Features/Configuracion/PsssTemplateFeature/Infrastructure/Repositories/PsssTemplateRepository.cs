using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Infrastructure.Repositories
{
    public class PsssTemplateRepository : IPsssTemplateRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public PsssTemplateRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<PsssTemplatePagedDTO> GetPagedAsync(int page)
        {
            const int pageSize = 10;
            using var ctx = _factory.CreateDbContext();

            var query = ctx.PsssTemplate
                .Where(t => t.State)
                .OrderBy(t => t.TemplateName);

            var totalRecords = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // get counts
            var ids = items.Select(t => t.PsssTemplateId).ToList();
            var counts = await ctx.PsssTemplateDetail
                .Where(d => ids.Contains(d.PsssTemplateId) && d.State)
                .GroupBy(d => d.PsssTemplateId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            var data = items.Select(t => new PsssTemplateDTO
            {
                PsssTemplateId = t.PsssTemplateId,
                TemplateName = t.TemplateName,
                Description = t.Description,
                Active = t.Active,
                PsssCount = counts.TryGetValue(t.PsssTemplateId, out var c) ? c : 0
            }).ToList();

            return new PsssTemplatePagedDTO
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
        }

        public async Task<List<PsssTemplateSimpleDTO>> GetAllSimpleAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.PsssTemplate
                .Where(t => t.State && t.Active)
                .OrderBy(t => t.TemplateName)
                .Select(t => new PsssTemplateSimpleDTO
                {
                    PsssTemplateId = t.PsssTemplateId,
                    TemplateName = t.TemplateName
                })
                .ToListAsync();
        }

        public async Task<int> CreateAsync(PsssTemplateCreateDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var template = new PsssTemplate
            {
                TemplateName = dto.TemplateName.Trim(),
                Description = dto.Description?.Trim(),
                State = true,
                Active = true,
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId = userId
            };

            ctx.PsssTemplate.Add(template);
            await ctx.SaveChangesAsync();
            return template.PsssTemplateId;
        }

        public async Task<bool> DeleteSoftAsync(int templateId, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var template = await ctx.PsssTemplate
                .FirstOrDefaultAsync(t => t.PsssTemplateId == templateId && t.State);

            if (template == null) return false;

            template.State = false;
            template.Active = false;
            template.UpdatedDateTime = DateTimeOffset.UtcNow;
            template.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
            return true;
        }

        public async Task<List<int>> GetPsssIdsAsync(int templateId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.PsssTemplateDetail
                .Where(d => d.PsssTemplateId == templateId && d.State)
                .Select(d => d.PhaseStageSubStageSubSpecialtyId)
                .ToListAsync();
        }

        public async Task UpdatePsssAsync(int templateId, List<int> psssIds)
        {
            using var ctx = _factory.CreateDbContext();

            // Remove all existing details for this template
            var existing = await ctx.PsssTemplateDetail
                .Where(d => d.PsssTemplateId == templateId)
                .ToListAsync();
            ctx.PsssTemplateDetail.RemoveRange(existing);

            // Add new ones
            var details = psssIds.Distinct().Select(id => new PsssTemplateDetail
            {
                PsssTemplateId = templateId,
                PhaseStageSubStageSubSpecialtyId = id,
                State = true
            }).ToList();

            ctx.PsssTemplateDetail.AddRange(details);
            await ctx.SaveChangesAsync();
        }

        public async Task<List<PsssAllFlatDTO>> GetAllPsssFlat()
        {
            using var ctx = _factory.CreateDbContext();

            // Load all PSSS with their related entities
            var psssData = await (
                from link in ctx.PhaseStageSubStageSubSpecialty
                join p in ctx.Phase on link.PhaseId equals p.PhaseId
                join s in ctx.Stage on link.StageId equals s.StageId into sj
                from s in sj.DefaultIfEmpty()
                join l in ctx.Layer on link.LayerId equals l.LayerId into lj
                from l in lj.DefaultIfEmpty()
                join ss in ctx.SubStage on link.SubStageId equals ss.SubStageId into ssj
                from ss in ssj.DefaultIfEmpty()
                join sp in ctx.SubSpecialty on link.SubSpecialtyId equals sp.SubSpecialtyId into spj
                from sp in spj.DefaultIfEmpty()
                join pa in ctx.Partida on link.PartidaId equals pa.PartidaId into paj
                from pa in paj.DefaultIfEmpty()
                where link.Active && link.State
                select new
                {
                    PsssId = link.PhaseStageSubStageSubSpecialtyId,
                    PhaseId = p.PhaseId,
                    PhaseOrder = p.Order,
                    PhaseDescription = p.PhaseDescription,
                    StageDescription = s != null ? s.StageDescription : null,
                    LayerDescription = l != null ? l.LayerDescription : null,
                    SubStageDescription = ss != null ? ss.SubStageDescription : null,
                    SubSpecialtyDescription = sp != null ? sp.SubSpecialtyDescription : null,
                    PartidaDescription = pa != null ? pa.PartidaDescription : null
                }
            ).ToListAsync();

            // Load first template assignment per PSSS
            var templateAssignments = await (
                from d in ctx.PsssTemplateDetail
                join t in ctx.PsssTemplate on d.PsssTemplateId equals t.PsssTemplateId
                where d.State && t.State && t.Active
                select new { d.PhaseStageSubStageSubSpecialtyId, t.PsssTemplateId, t.TemplateName }
            ).ToListAsync();

            var templateLookup = templateAssignments
                .GroupBy(x => x.PhaseStageSubStageSubSpecialtyId)
                .ToDictionary(g => g.Key, g => g.First());

            var result = psssData.Select(x =>
            {
                // Build label from non-null parts
                var parts = new List<string> { x.PhaseDescription };
                if (x.StageDescription != null) parts.Add(x.StageDescription);
                if (x.PartidaDescription != null) parts.Add(x.PartidaDescription);
                else
                {
                    if (x.LayerDescription != null) parts.Add(x.LayerDescription);
                    if (x.SubStageDescription != null) parts.Add(x.SubStageDescription);
                    if (x.SubSpecialtyDescription != null) parts.Add(x.SubSpecialtyDescription);
                }

                templateLookup.TryGetValue(x.PsssId, out var ta);

                return new PsssAllFlatDTO
                {
                    PsssId = x.PsssId,
                    Label = string.Join(" › ", parts),
                    PhaseId = x.PhaseId,
                    PhaseDescription = x.PhaseDescription,
                    TemplateId = ta?.PsssTemplateId,
                    TemplateName = ta?.TemplateName
                };
            })
            .OrderBy(x => x.PhaseDescription)
            .ThenBy(x => x.Label)
            .ToList();

            return result;
        }
    }
}
