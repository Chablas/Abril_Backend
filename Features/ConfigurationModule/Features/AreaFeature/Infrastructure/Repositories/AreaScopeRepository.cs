using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Dtos;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models;

namespace Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Repositories
{
    public class AreaScopeRepository : IAreaScopeRepository
    {
        private readonly AppDbContext _context;

        public AreaScopeRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<AreaScopeTreeDto>> GetTreeAsync()
        {
            var flat = await (
                from s in _context.AreaScope
                join ai in _context.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in _context.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State && at.State && s.Active
                orderby s.DisplayOrder
                select new AreaScopeTreeDto
                {
                    AreaScopeId       = s.AreaScopeId,
                    AreaItemId        = s.AreaItemId,
                    AreaItemName      = ai.AreaItemName,
                    AreaTypeId        = ai.AreaTypeId,
                    AreaTypeName      = at.AreaTypeName,
                    AreaScopeParentId = s.AreaScopeParentId,
                    DisplayOrder      = s.DisplayOrder,
                    Active            = s.Active,
                }
            ).ToListAsync();

            var byId = flat.ToDictionary(n => n.AreaScopeId);
            var roots = new List<AreaScopeTreeDto>();
            foreach (var n in flat)
            {
                if (n.AreaScopeParentId.HasValue && byId.TryGetValue(n.AreaScopeParentId.Value, out var p))
                    p.Children.Add(n);
                else
                    roots.Add(n);
            }
            return roots;
        }

        /// <summary>
        /// Crea una nueva rama (subárbol) en area_scope. Procesa los nodos en topo-orden
        /// (padres antes que hijos) usando un mapeo tempId → areaScopeId real.
        ///
        /// Si un nodo (area_item_id, area_scope_parent_id) ya existe en BD, se REUTILIZA
        /// en lugar de duplicarse. Si TODA la rama ya existe (no se insertó ningún nodo
        /// nuevo), se lanza un error.
        /// </summary>
        public async Task CreateBranchAsync(AreaScopeBranchDto dto)
        {
            if (dto.Nodes == null || dto.Nodes.Count == 0)
                throw new AbrilException("La rama no puede estar vacía.");

            // Validar que todos los area_item existen y están vivos
            var areaItemIds = dto.Nodes.Select(n => n.AreaItemId).Distinct().ToList();
            var existingCount = await _context.AreaItem
                .CountAsync(a => a.State && areaItemIds.Contains(a.AreaItemId));
            if (existingCount != areaItemIds.Count)
                throw new AbrilException("Una o más áreas seleccionadas no existen.");

            var idMap = new Dictionary<int, int>(); // tempId → areaScopeId real (existente o nuevo)
            var pending = dto.Nodes.ToList();
            int safety = pending.Count + 5;
            int newlyInserted = 0;

            while (pending.Count > 0 && safety-- > 0)
            {
                var ready = pending
                    .Where(n => !n.ParentTempId.HasValue || idMap.ContainsKey(n.ParentTempId.Value))
                    .OrderBy(n => n.DisplayOrder)
                    .ToList();

                if (ready.Count == 0) break; // huérfanos o ciclo

                foreach (var node in ready)
                {
                    int? parentScopeId = node.ParentTempId.HasValue
                        ? idMap[node.ParentTempId.Value]
                        : (int?)null;

                    // ¿Existe ya un nodo vivo con esta combinación (area_item_id, parent_id)?
                    var existing = await _context.AreaScope.FirstOrDefaultAsync(s =>
                        s.State &&
                        s.AreaItemId == node.AreaItemId &&
                        s.AreaScopeParentId == parentScopeId);

                    if (existing != null)
                    {
                        idMap[node.TempId] = existing.AreaScopeId;
                    }
                    else
                    {
                        var entity = new AreaScope
                        {
                            AreaItemId        = node.AreaItemId,
                            AreaScopeParentId = parentScopeId,
                            DisplayOrder      = node.DisplayOrder,
                            Active            = true,
                            State             = true
                        };
                        _context.AreaScope.Add(entity);
                        await _context.SaveChangesAsync();
                        idMap[node.TempId] = entity.AreaScopeId;
                        newlyInserted++;
                    }
                }

                pending = pending.Except(ready).ToList();
            }

            if (newlyInserted == 0)
                throw new AbrilException("Esta rama ya existe.");
        }

        /// <summary>Soft delete: marca state = false (el registro se mantiene en BD para auditoría).</summary>
        public async Task DeleteAsync(int areaScopeId)
        {
            var entity = await _context.AreaScope.FirstOrDefaultAsync(s => s.State && s.AreaScopeId == areaScopeId);
            if (entity == null) return;

            var hasChildren = await _context.AreaScope.AnyAsync(c => c.State && c.AreaScopeParentId == areaScopeId);
            if (hasChildren)
                throw new AbrilException("No se puede eliminar: el nodo tiene hijos. Elimina los hijos primero.");

            entity.State = false;
            await _context.SaveChangesAsync();
        }
    }
}
