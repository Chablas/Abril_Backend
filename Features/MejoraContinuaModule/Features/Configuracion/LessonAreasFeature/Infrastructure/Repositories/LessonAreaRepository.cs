using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Infrastructure.Repositories
{
    public class LessonAreaRepository : ILessonAreaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public LessonAreaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Lista TODOS los area_item activos (fuente de verdad global) y para cada uno
        /// busca su fila en lesson_area. Si no existe, se muestra como inactivo (filtro apagado).
        /// </summary>
        public async Task<List<LessonAreaConfigItemDTO>> GetAllAsync()
        {
            using var ctx = _factory.CreateDbContext();

            var rows = await (
                from a in ctx.AreaItem
                join at in ctx.AreaType on a.AreaTypeId equals at.AreaTypeId
                join parentJ in ctx.AreaItem on a.AreaItemParentId equals parentJ.AreaItemId into parentGrp
                from parent in parentGrp.DefaultIfEmpty()
                join laJ in ctx.LessonArea on a.AreaItemId equals laJ.AreaItemId into laGrp
                from la in laGrp.DefaultIfEmpty()
                where a.Active
                orderby at.AreaTypeName, a.AreaItemName
                select new LessonAreaConfigItemDTO
                {
                    LessonAreaId = la == null ? (int?)null : (int?)la.LessonAreaId,
                    AreaItemId   = a.AreaItemId,
                    AreaItemName = a.AreaItemName,
                    AreaTypeName = at.AreaTypeName,
                    ParentName   = parent == null ? null : parent.AreaItemName,
                    Active       = la != null && la.Active
                }
            ).ToListAsync();

            return rows;
        }

        /// <summary>
        /// Toggle del flag activo para un area_item.
        /// Si no existe fila en lesson_area, la crea con active=true.
        /// Si ya existe, invierte el flag.
        /// </summary>
        public async Task<ToggleLessonAreaResultDTO> ToggleAsync(int areaItemId)
        {
            using var ctx = _factory.CreateDbContext();

            var areaExists = await ctx.AreaItem.AnyAsync(a => a.AreaItemId == areaItemId && a.Active);
            if (!areaExists)
                throw new AbrilException("El área no existe o está inactiva.", 404);

            var row = await ctx.LessonArea.FirstOrDefaultAsync(la => la.AreaItemId == areaItemId);

            if (row == null)
            {
                row = new LessonArea
                {
                    AreaItemId = areaItemId,
                    Active     = true,
                    CreatedAt  = DateTimeOffset.UtcNow
                };
                ctx.LessonArea.Add(row);
            }
            else
            {
                row.Active = !row.Active;
            }

            await ctx.SaveChangesAsync();
            return new ToggleLessonAreaResultDTO { LessonAreaId = row.LessonAreaId, Active = row.Active };
        }
    }
}
