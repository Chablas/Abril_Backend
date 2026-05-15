using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.CatalogFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.CatalogFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.CatalogFeature.Infrastructure.Repositories
{
    public class CatalogRepository : ICatalogRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CatalogRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<CatalogTypeDTO>> GetAllTypesAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CatalogType
                .Where(t => t.Active)
                .OrderBy(t => t.CatalogTypeName)
                .Select(t => new CatalogTypeDTO
                {
                    CatalogTypeId = t.CatalogTypeId,
                    CatalogTypeName = t.CatalogTypeName,
                    CatalogTypeCode = t.CatalogTypeCode,
                    Active = t.Active
                })
                .ToListAsync();
        }

        public async Task<List<CatalogItemDTO>> GetItemsByTypeAsync(int catalogTypeId)
        {
            using var ctx = _factory.CreateDbContext();
            return await (
                from item in ctx.CatalogItem
                join parent in ctx.CatalogItem
                    on item.CatalogItemParentId equals parent.CatalogItemId into pj
                from parent in pj.DefaultIfEmpty()
                where item.CatalogTypeId == catalogTypeId && item.Active
                orderby item.CatalogItemDescription
                select new CatalogItemDTO
                {
                    CatalogItemId = item.CatalogItemId,
                    CatalogTypeId = item.CatalogTypeId,
                    CatalogItemParentId = item.CatalogItemParentId,
                    ParentDescription = parent != null ? parent.CatalogItemDescription : null,
                    CatalogItemDescription = item.CatalogItemDescription,
                    CatalogItemCode = item.CatalogItemCode,
                    Active = item.Active
                }
            ).ToListAsync();
        }

        public async Task<List<CatalogItemDTO>> GetTreeByTypeAsync(int catalogTypeId)
        {
            using var ctx = _factory.CreateDbContext();

            var allItems = await ctx.CatalogItem
                .Where(i => i.CatalogTypeId == catalogTypeId && i.Active)
                .OrderBy(i => i.CatalogItemDescription)
                .Select(i => new CatalogItemDTO
                {
                    CatalogItemId = i.CatalogItemId,
                    CatalogTypeId = i.CatalogTypeId,
                    CatalogItemParentId = i.CatalogItemParentId,
                    CatalogItemDescription = i.CatalogItemDescription,
                    CatalogItemCode = i.CatalogItemCode,
                    Active = i.Active
                })
                .ToListAsync();

            return BuildTree(allItems, null);
        }

        private static List<CatalogItemDTO> BuildTree(List<CatalogItemDTO> all, int? parentId)
        {
            return all
                .Where(i => i.CatalogItemParentId == parentId)
                .Select(i =>
                {
                    i.Children = BuildTree(all, i.CatalogItemId);
                    return i;
                })
                .ToList();
        }

        public async Task<List<CatalogItemDTO>> GetFullTreeAsync()
        {
            using var ctx = _factory.CreateDbContext();
            var allItems = await (
                from item in ctx.CatalogItem
                join type in ctx.CatalogType on item.CatalogTypeId equals type.CatalogTypeId
                where item.Active
                orderby item.CatalogItemDescription
                select new CatalogItemDTO
                {
                    CatalogItemId = item.CatalogItemId,
                    CatalogTypeId = item.CatalogTypeId,
                    CatalogTypeName = type.CatalogTypeName,
                    CatalogTypeCode = type.CatalogTypeCode,
                    CatalogItemParentId = item.CatalogItemParentId,
                    CatalogItemDescription = item.CatalogItemDescription,
                    CatalogItemCode = item.CatalogItemCode,
                    Active = item.Active
                }
            ).ToListAsync();
            return BuildTree(allItems, null);
        }

        public async Task CreateTypeAsync(CatalogTypeCreateDTO dto)
        {
            using var ctx = _factory.CreateDbContext();

            var duplicate = await ctx.CatalogType
                .AnyAsync(t => t.CatalogTypeCode == dto.CatalogTypeCode.Trim());
            if (duplicate)
                throw new AbrilException("Ya existe un tipo de catálogo con ese código.", 400);

            ctx.CatalogType.Add(new CatalogType
            {
                CatalogTypeName = dto.CatalogTypeName.Trim(),
                CatalogTypeCode = dto.CatalogTypeCode.Trim().ToLower(),
                Active = dto.Active
            });
            await ctx.SaveChangesAsync();
        }

        public async Task CreateItemAsync(CatalogItemCreateDTO dto)
        {
            using var ctx = _factory.CreateDbContext();

            var typeExists = await ctx.CatalogType.AnyAsync(t => t.CatalogTypeId == dto.CatalogTypeId && t.Active);
            if (!typeExists)
                throw new AbrilException("El tipo de catálogo no existe.", 400);

            if (dto.CatalogItemParentId.HasValue)
            {
                var parentExists = await ctx.CatalogItem.AnyAsync(i => i.CatalogItemId == dto.CatalogItemParentId.Value && i.Active);
                if (!parentExists)
                    throw new AbrilException("El ítem padre no existe.", 400);
            }

            ctx.CatalogItem.Add(new CatalogItem
            {
                CatalogTypeId = dto.CatalogTypeId,
                CatalogItemParentId = dto.CatalogItemParentId,
                CatalogItemDescription = dto.CatalogItemDescription.Trim(),
                CatalogItemCode = dto.CatalogItemCode?.Trim(),
                Active = dto.Active
            });
            await ctx.SaveChangesAsync();
        }

        public async Task UpdateItemAsync(CatalogItemEditDTO dto)
        {
            using var ctx = _factory.CreateDbContext();

            var item = await ctx.CatalogItem.FirstOrDefaultAsync(i => i.CatalogItemId == dto.CatalogItemId);
            if (item == null)
                throw new AbrilException("El ítem no existe.", 404);

            item.CatalogTypeId = dto.CatalogTypeId;
            item.CatalogItemParentId = dto.CatalogItemParentId;
            item.CatalogItemDescription = dto.CatalogItemDescription.Trim();
            item.CatalogItemCode = dto.CatalogItemCode?.Trim();
            item.Active = dto.Active;

            await ctx.SaveChangesAsync();
        }

        public async Task DeleteItemAsync(int catalogItemId)
        {
            using var ctx = _factory.CreateDbContext();

            var hasChildren = await ctx.CatalogItem.AnyAsync(i => i.CatalogItemParentId == catalogItemId && i.Active);
            if (hasChildren)
                throw new AbrilException("No se puede eliminar un ítem que tiene hijos activos.", 400);

            var item = await ctx.CatalogItem.FirstOrDefaultAsync(i => i.CatalogItemId == catalogItemId);
            if (item == null)
                throw new AbrilException("El ítem no existe.", 404);

            item.Active = false;
            await ctx.SaveChangesAsync();
        }
    }
}
