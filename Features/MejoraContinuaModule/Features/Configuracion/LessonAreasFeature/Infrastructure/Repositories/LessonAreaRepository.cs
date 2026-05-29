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
        /// Lista cada HOJA del árbol area_scope como una rama con su path completo
        /// (desde la raíz hasta esa hoja). Cada rama trae su estado en lesson_area
        /// (active=false si todavía no se ha togglado).
        /// </summary>
        public async Task<List<LessonAreaConfigItemDTO>> GetAllAsync()
        {
            using var ctx = _factory.CreateDbContext();

            // Cargar todos los nodos del scope con su area_item + area_type (solo vivos)
            var scopeNodes = await (
                from s in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State && at.State && s.Active
                orderby s.DisplayOrder
                select new
                {
                    s.AreaScopeId,
                    s.AreaScopeParentId,
                    AreaItemName = ai.AreaItemName,
                    AreaTypeName = at.AreaTypeName
                }
            ).ToListAsync();

            var byId = scopeNodes.ToDictionary(n => n.AreaScopeId);
            var hasChildren = scopeNodes
                .Where(n => n.AreaScopeParentId.HasValue)
                .Select(n => n.AreaScopeParentId!.Value)
                .ToHashSet();

            var lessonAreas = await ctx.LessonArea.ToListAsync();
            var laByScopeId = lessonAreas.ToDictionary(l => l.AreaScopeId);

            var result = new List<LessonAreaConfigItemDTO>();
            foreach (var node in scopeNodes)
            {
                // Solo las hojas (nodos sin hijos) representan "ramas" completas
                if (hasChildren.Contains(node.AreaScopeId)) continue;

                // Reconstruir path desde la raíz hasta esta hoja
                var path = new List<LessonAreaSegmentDTO>();
                int? cur = node.AreaScopeId;
                int safety = 50;
                while (cur.HasValue && safety-- > 0 && byId.TryGetValue(cur.Value, out var n))
                {
                    path.Insert(0, new LessonAreaSegmentDTO
                    {
                        AreaItemName = n.AreaItemName,
                        AreaTypeName = n.AreaTypeName
                    });
                    cur = n.AreaScopeParentId;
                }

                laByScopeId.TryGetValue(node.AreaScopeId, out var la);
                result.Add(new LessonAreaConfigItemDTO
                {
                    LessonAreaId = la?.LessonAreaId,
                    AreaScopeId  = node.AreaScopeId,
                    Path         = path,
                    Active       = la != null && la.Active
                });
            }

            // Ordenar por path concatenado para visualización estable
            return result
                .OrderBy(r => string.Join(" > ", r.Path.Select(p => p.AreaItemName)))
                .ToList();
        }

        /// <summary>
        /// Igual que GetAllAsync pero filtrado: solo devuelve ramas que están ACTIVAS
        /// (lesson_area.active=true) Y tienen al menos un scope_item configurado.
        /// Esto se usa en el dropdown de "Área" al crear una lección.
        /// </summary>
        public async Task<List<LessonAreaConfigItemDTO>> GetAllWithScopeAsync()
        {
            using var ctx = _factory.CreateDbContext();

            // Lesson_areas activas que tienen al menos un scope_item activo
            var validLessonAreaIds = await ctx.ScopeItem
                .Where(si => si.Active)
                .Select(si => si.LessonAreaId)
                .Distinct()
                .ToListAsync();

            var activeLessonAreas = await ctx.LessonArea
                .Where(la => la.Active && validLessonAreaIds.Contains(la.LessonAreaId))
                .ToListAsync();

            if (activeLessonAreas.Count == 0)
                return new List<LessonAreaConfigItemDTO>();

            var areaScopeIds = activeLessonAreas.Select(la => la.AreaScopeId).Distinct().ToList();

            // Cargar todos los nodos del scope (todos los antecesores también)
            var allScopeNodes = await (
                from s in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State && at.State
                select new
                {
                    s.AreaScopeId,
                    s.AreaScopeParentId,
                    AreaItemName = ai.AreaItemName,
                    AreaTypeName = at.AreaTypeName
                }
            ).ToListAsync();
            var byId = allScopeNodes.ToDictionary(n => n.AreaScopeId);

            var result = new List<LessonAreaConfigItemDTO>();
            foreach (var la in activeLessonAreas)
            {
                // Construir path desde la raíz hasta el nodo
                var path = new List<LessonAreaSegmentDTO>();
                int? cur = la.AreaScopeId;
                int safety = 50;
                while (cur.HasValue && safety-- > 0 && byId.TryGetValue(cur.Value, out var n))
                {
                    path.Insert(0, new LessonAreaSegmentDTO
                    {
                        AreaItemName = n.AreaItemName,
                        AreaTypeName = n.AreaTypeName
                    });
                    cur = n.AreaScopeParentId;
                }
                if (path.Count == 0) continue; // scope inválido, lo saltamos

                result.Add(new LessonAreaConfigItemDTO
                {
                    LessonAreaId = la.LessonAreaId,
                    AreaScopeId  = la.AreaScopeId,
                    Path         = path,
                    Active       = la.Active
                });
            }

            return result
                .OrderBy(r => string.Join(" > ", r.Path.Select(p => p.AreaItemName)))
                .ToList();
        }

        /// <summary>
        /// Toggle del flag activo para un area_scope.
        /// Si no existe fila en lesson_area, la crea con active=true.
        /// Si ya existe, invierte el flag.
        /// </summary>
        public async Task<ToggleLessonAreaResultDTO> ToggleAsync(int areaScopeId)
        {
            using var ctx = _factory.CreateDbContext();

            var scopeExists = await ctx.AreaScope.AnyAsync(s => s.AreaScopeId == areaScopeId && s.State && s.Active);
            if (!scopeExists)
                throw new AbrilException("La rama no existe o está inactiva.", 404);

            var row = await ctx.LessonArea.FirstOrDefaultAsync(la => la.AreaScopeId == areaScopeId);

            if (row == null)
            {
                row = new LessonArea
                {
                    AreaScopeId = areaScopeId,
                    Active      = true,
                    CreatedAt   = DateTimeOffset.UtcNow
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
