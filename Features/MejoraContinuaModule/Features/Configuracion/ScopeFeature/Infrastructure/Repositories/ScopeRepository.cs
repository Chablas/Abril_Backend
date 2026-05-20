using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.ScopeFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.ScopeFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.ScopeFeature.Infrastructure.Repositories
{
    public class ScopeRepository : IScopeRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ScopeRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // ── AreaSubarea ──────────────────────────────────────────────────────────

        public async Task<AreaSubareaDTO> GetOrCreateAreaSubareaAsync(int areaId, int? subAreaId)
        {
            using var ctx = _factory.CreateDbContext();

            var existing = await ctx.AreaSubarea
                .FirstOrDefaultAsync(a => a.AreaId == areaId && a.SubAreaId == subAreaId);

            if (existing != null)
                return new AreaSubareaDTO { AreaSubareaId = existing.AreaSubareaId, AreaId = existing.AreaId, SubAreaId = existing.SubAreaId };

            var entry = new AreaSubarea { AreaId = areaId, SubAreaId = subAreaId };
            ctx.AreaSubarea.Add(entry);
            await ctx.SaveChangesAsync();

            return new AreaSubareaDTO { AreaSubareaId = entry.AreaSubareaId, AreaId = entry.AreaId, SubAreaId = entry.SubAreaId };
        }

        // ── ScopeItem ────────────────────────────────────────────────────────────

        public async Task<List<ScopeItemDTO>> GetScopeTreeAsync(int areaSubareaId)
        {
            using var ctx = _factory.CreateDbContext();
            return await BuildScopeTreeAsync(ctx, areaSubareaId);
        }

        public async Task<List<ScopeItemDTO>> GetScopeForLessonAsync(int areaId, int? subAreaId)
        {
            using var ctx = _factory.CreateDbContext();

            var areaSubarea = await ctx.AreaSubarea
                .FirstOrDefaultAsync(a => a.AreaId == areaId && a.SubAreaId == subAreaId);

            if (areaSubarea == null)
                return new List<ScopeItemDTO>();

            return await BuildScopeTreeAsync(ctx, areaSubarea.AreaSubareaId);
        }

        private static async Task<List<ScopeItemDTO>> BuildScopeTreeAsync(AppDbContext ctx, int areaSubareaId)
        {
            var flatItems = await (
                from si in ctx.ScopeItem
                join ci in ctx.CatalogItem on si.CatalogItemId equals ci.CatalogItemId
                join ct in ctx.CatalogType on ci.CatalogTypeId equals ct.CatalogTypeId
                where si.AreaSubareaId == areaSubareaId && si.Active
                orderby si.DisplayOrder
                select new ScopeItemDTO
                {
                    ScopeItemId = si.ScopeItemId,
                    AreaSubareaId = si.AreaSubareaId,
                    CatalogItemId = si.CatalogItemId,
                    CatalogItemDescription = ci.CatalogItemDescription,
                    CatalogTypeName = ct.CatalogTypeName,
                    ScopeItemParentId = si.ScopeItemParentId,
                    DisplayOrder = si.DisplayOrder,
                    Active = si.Active
                }
            ).ToListAsync();

            return BuildScopeTreeFromFlat(flatItems, null);
        }

        private static List<ScopeItemDTO> BuildScopeTreeFromFlat(List<ScopeItemDTO> all, int? parentId)
        {
            return all
                .Where(i => i.ScopeItemParentId == parentId)
                .OrderBy(i => i.DisplayOrder)
                .Select(i =>
                {
                    i.Children = BuildScopeTreeFromFlat(all, i.ScopeItemId);
                    return i;
                })
                .ToList();
        }

        public async Task UpsertScopeAsync(ScopeItemUpsertDTO dto)
        {
            using var ctx = _factory.CreateDbContext();

            var areaSubareaExists = await ctx.AreaSubarea.AnyAsync(a => a.AreaSubareaId == dto.AreaSubareaId);
            if (!areaSubareaExists)
                throw new AbrilException("El contexto área/subárea no existe.", 400);

            var existing = await ctx.ScopeItem
                .Where(s => s.AreaSubareaId == dto.AreaSubareaId)
                .ToListAsync();
            ctx.ScopeItem.RemoveRange(existing);

            var idMap = new Dictionary<int, int>(); // catalogItemId → scopeItemId recién creado

            var ordered = dto.Items
                .OrderBy(n => n.ParentCatalogItemId.HasValue ? 1 : 0)
                .ToList();

            foreach (var node in ordered)
            {
                int? parentScopeItemId = null;
                if (node.ParentCatalogItemId.HasValue && idMap.TryGetValue(node.ParentCatalogItemId.Value, out var parentId))
                    parentScopeItemId = parentId;

                var scopeItem = new ScopeItem
                {
                    AreaSubareaId = dto.AreaSubareaId,
                    CatalogItemId = node.CatalogItemId,
                    ScopeItemParentId = parentScopeItemId,
                    DisplayOrder = node.DisplayOrder,
                    Active = true
                };

                ctx.ScopeItem.Add(scopeItem);
                await ctx.SaveChangesAsync();

                idMap[node.CatalogItemId] = scopeItem.ScopeItemId;
            }
        }

        // ── ScopeTemplate ────────────────────────────────────────────────────────

        public async Task<List<ScopeTemplateDTO>> GetTemplatesAsync()
        {
            using var ctx = _factory.CreateDbContext();

            var templates = await ctx.ScopeTemplate
                .Where(t => t.Active)
                .OrderBy(t => t.TemplateName)
                .ToListAsync();

            var templateIds = templates.Select(t => t.ScopeTemplateId).ToList();

            var rawItems = await (
                from sti in ctx.ScopeTemplateItem
                join ci in ctx.CatalogItem on sti.CatalogItemId equals ci.CatalogItemId
                where templateIds.Contains(sti.ScopeTemplateId) && sti.Active
                orderby sti.ScopeTemplateId, sti.DisplayOrder
                select new
                {
                    sti.ScopeTemplateId,
                    sti.ScopeTemplateItemParentId,
                    sti.DisplayOrder,
                    sti.CatalogItemId,
                    ci.CatalogItemDescription
                }
            ).ToListAsync();

            return templates.Select(t => new ScopeTemplateDTO
            {
                ScopeTemplateId = t.ScopeTemplateId,
                TemplateName = t.TemplateName,
                Active = t.Active,
                Items = rawItems
                    .Where(i => i.ScopeTemplateId == t.ScopeTemplateId)
                    .Select(i => new ScopeTemplateItemNodeDTO
                    {
                        CatalogItemId = i.CatalogItemId,
                        CatalogItemDescription = i.CatalogItemDescription,
                        ScopeTemplateItemParentId = i.ScopeTemplateItemParentId,
                        DisplayOrder = i.DisplayOrder
                    })
                    .ToList()
            }).ToList();
        }

        public async Task CreateTemplateAsync(ScopeTemplateCreateDTO dto)
        {
            using var ctx = _factory.CreateDbContext();

            var template = new ScopeTemplate
            {
                TemplateName = dto.TemplateName.Trim(),
                Active = true,
                CreatedDateTime = DateTimeOffset.UtcNow
            };

            ctx.ScopeTemplate.Add(template);
            await ctx.SaveChangesAsync();

            if (dto.Items.Count > 0)
            {
                var items = dto.Items.Select((node, idx) => new ScopeTemplateItem
                {
                    ScopeTemplateId = template.ScopeTemplateId,
                    CatalogItemId = node.CatalogItemId,
                    ScopeTemplateItemParentId = node.ScopeTemplateItemParentId,
                    DisplayOrder = node.DisplayOrder > 0 ? node.DisplayOrder : idx + 1,
                    Active = true
                }).ToList();

                ctx.ScopeTemplateItem.AddRange(items);
                await ctx.SaveChangesAsync();
            }
        }

        public async Task UpdateTemplateAsync(ScopeTemplateUpdateDTO dto)
        {
            using var ctx = _factory.CreateDbContext();

            var template = await ctx.ScopeTemplate.FirstOrDefaultAsync(t => t.ScopeTemplateId == dto.ScopeTemplateId);
            if (template == null)
                throw new AbrilException("La plantilla no existe.", 404);

            template.TemplateName = dto.TemplateName.Trim();
            template.UpdatedDateTime = DateTimeOffset.UtcNow;

            var existingItems = await ctx.ScopeTemplateItem
                .Where(i => i.ScopeTemplateId == dto.ScopeTemplateId)
                .ToListAsync();
            ctx.ScopeTemplateItem.RemoveRange(existingItems);

            if (dto.Items.Count > 0)
            {
                var newItems = dto.Items.Select((node, idx) => new ScopeTemplateItem
                {
                    ScopeTemplateId = dto.ScopeTemplateId,
                    CatalogItemId = node.CatalogItemId,
                    ScopeTemplateItemParentId = node.ScopeTemplateItemParentId,
                    DisplayOrder = node.DisplayOrder > 0 ? node.DisplayOrder : idx + 1,
                    Active = true
                }).ToList();

                ctx.ScopeTemplateItem.AddRange(newItems);
            }

            await ctx.SaveChangesAsync();
        }

        public async Task DeleteTemplateAsync(int scopeTemplateId)
        {
            using var ctx = _factory.CreateDbContext();

            var template = await ctx.ScopeTemplate.FirstOrDefaultAsync(t => t.ScopeTemplateId == scopeTemplateId);
            if (template == null)
                throw new AbrilException("La plantilla no existe.", 404);

            template.Active = false;
            template.UpdatedDateTime = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }
    }
}
