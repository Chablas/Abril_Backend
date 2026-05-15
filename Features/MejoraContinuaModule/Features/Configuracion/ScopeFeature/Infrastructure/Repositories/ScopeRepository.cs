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
                    CatalogTypeCode = ct.CatalogTypeCode,
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

            // Verificar que el area_subarea existe
            var areaSubareaExists = await ctx.AreaSubarea.AnyAsync(a => a.AreaSubareaId == dto.AreaSubareaId);
            if (!areaSubareaExists)
                throw new AbrilException("El contexto área/subárea no existe.", 400);

            // Eliminar scope previo
            var existing = await ctx.ScopeItem
                .Where(s => s.AreaSubareaId == dto.AreaSubareaId)
                .ToListAsync();
            ctx.ScopeItem.RemoveRange(existing);

            // Reconstruir desde los nodos enviados
            // Primero insertar los root nodes, luego hijos (necesitamos los ids recién generados)
            var idMap = new Dictionary<int, int>(); // catalogItemId → scopeItemId recién creado

            // Ordenar: primero sin parent, luego con parent
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
                await ctx.SaveChangesAsync(); // para obtener el ScopeItemId generado

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

            var items = await ctx.ScopeTemplateItem
                .Where(i => templateIds.Contains(i.ScopeTemplateId) && i.Active)
                .ToListAsync();

            return templates.Select(t => new ScopeTemplateDTO
            {
                ScopeTemplateId = t.ScopeTemplateId,
                TemplateName = t.TemplateName,
                Active = t.Active,
                CatalogItemIds = items
                    .Where(i => i.ScopeTemplateId == t.ScopeTemplateId)
                    .Select(i => i.CatalogItemId)
                    .ToList()
            }).ToList();
        }

        public async Task CreateTemplateAsync(ScopeTemplateCreateDTO dto)
        {
            using var ctx = _factory.CreateDbContext();

            var template = new ScopeTemplate
            {
                AreaSubareaId = null,   // plantilla global
                TemplateName = dto.TemplateName.Trim(),
                Active = true,
                CreatedDateTime = DateTimeOffset.UtcNow
            };

            ctx.ScopeTemplate.Add(template);
            await ctx.SaveChangesAsync();

            var items = dto.CatalogItemIds.Distinct().Select(id => new ScopeTemplateItem
            {
                ScopeTemplateId = template.ScopeTemplateId,
                CatalogItemId = id,
                Active = true
            }).ToList();

            ctx.ScopeTemplateItem.AddRange(items);
            await ctx.SaveChangesAsync();
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

            var newItems = dto.CatalogItemIds.Distinct().Select(id => new ScopeTemplateItem
            {
                ScopeTemplateId = dto.ScopeTemplateId,
                CatalogItemId = id,
                Active = true
            }).ToList();

            ctx.ScopeTemplateItem.AddRange(newItems);
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
