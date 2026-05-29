using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Data;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Helpers
{
    public class LessonEnrichmentData
    {
        public string? AreaDescription { get; set; }
        public string? PhaseDescription { get; set; }
        public string? StageDescription { get; set; }
        public string? LayerDescription { get; set; }
        public string? SubStageDescription { get; set; }
        public string? SubSpecialtyDescription { get; set; }
        public string? PartidaDescription { get; set; }
    }

    /// <summary>
    /// Calcula el path de área (vía lesson_area → area_scope → area_item) y la
    /// clasificación por tipo de catálogo (vía scope_item walk-up) para una lista
    /// de lecciones. Usado para enriquecer los DTOs de lista y de detalle desde el
    /// nuevo modelo, manteniendo los nombres legacy (PhaseDescription, etc.) para
    /// no romper el frontend.
    /// </summary>
    public static class LessonEnrichmentHelper
    {
        public static async Task<Dictionary<int, LessonEnrichmentData>> ComputeAsync(
            AppDbContext ctx,
            IReadOnlyList<(int LessonId, int? LessonAreaId, int? CatalogItemId)> lessons)
        {
            var result = new Dictionary<int, LessonEnrichmentData>();
            if (lessons.Count == 0) return result;

            // 1. Path de área por lesson_area_id
            var lessonAreaIds = lessons
                .Where(l => l.LessonAreaId.HasValue)
                .Select(l => l.LessonAreaId!.Value)
                .Distinct()
                .ToList();

            var pathByLessonAreaId = new Dictionary<int, string>();
            if (lessonAreaIds.Count > 0)
            {
                var laToScope = await ctx.LessonArea
                    .Where(la => lessonAreaIds.Contains(la.LessonAreaId))
                    .Select(la => new { la.LessonAreaId, la.AreaScopeId })
                    .ToListAsync();

                var allAreaScope = await (
                    from s in ctx.AreaScope
                    join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                    where s.State && ai.State
                    select new { s.AreaScopeId, s.AreaScopeParentId, ai.AreaItemName }
                ).ToListAsync();
                var areaScopeById = allAreaScope.ToDictionary(n => n.AreaScopeId);

                foreach (var la in laToScope)
                {
                    var parts = new List<string>();
                    int? cur = la.AreaScopeId;
                    int safety = 50;
                    while (cur.HasValue && safety-- > 0 && areaScopeById.TryGetValue(cur.Value, out var n))
                    {
                        parts.Insert(0, n.AreaItemName);
                        cur = n.AreaScopeParentId;
                    }
                    if (parts.Count > 0)
                        pathByLessonAreaId[la.LessonAreaId] = string.Join(" / ", parts);
                }
            }

            // 2. Clasificación por (lesson_area_id, catalog_item_id) usando scope_item
            var pairs = lessons
                .Where(l => l.LessonAreaId.HasValue && l.CatalogItemId.HasValue)
                .Select(l => (LessonAreaId: l.LessonAreaId!.Value, CatalogItemId: l.CatalogItemId!.Value))
                .Distinct()
                .ToList();

            var classByPair = new Dictionary<(int, int), Dictionary<string, string>>();

            if (pairs.Count > 0)
            {
                var activeLessonAreaIds = pairs.Select(p => p.LessonAreaId).Distinct().ToList();
                var allScope = await (
                    from si in ctx.ScopeItem
                    join ci in ctx.CatalogItem on si.CatalogItemId equals ci.CatalogItemId
                    join ct in ctx.CatalogType on ci.CatalogTypeId equals ct.CatalogTypeId
                    where activeLessonAreaIds.Contains(si.LessonAreaId) && si.Active
                    select new
                    {
                        si.ScopeItemId,
                        si.LessonAreaId,
                        si.CatalogItemId,
                        si.ScopeItemParentId,
                        ct.CatalogTypeName,
                        ci.CatalogItemDescription
                    }
                ).ToListAsync();
                var scopeById = allScope.ToDictionary(s => s.ScopeItemId);

                foreach (var pair in pairs)
                {
                    var leaf = allScope.FirstOrDefault(s =>
                        s.LessonAreaId == pair.LessonAreaId && s.CatalogItemId == pair.CatalogItemId);

                    var map = new Dictionary<string, string>();
                    if (leaf != null)
                    {
                        int? cur = leaf.ScopeItemId;
                        int safety = 50;
                        while (cur.HasValue && safety-- > 0 && scopeById.TryGetValue(cur.Value, out var s))
                        {
                            map[s.CatalogTypeName] = s.CatalogItemDescription;
                            cur = s.ScopeItemParentId;
                        }
                    }
                    classByPair[pair] = map;
                }

                // Fallback: catalog_items que no están en scope_item del área
                // (raro pero posible si se cambió el scope después de crear la lección)
                var unmappedCatIds = classByPair
                    .Where(kv => kv.Value.Count == 0)
                    .Select(kv => kv.Key.Item2)
                    .Distinct()
                    .ToList();
                if (unmappedCatIds.Count > 0)
                {
                    var fallback = await (
                        from ci in ctx.CatalogItem
                        join ct in ctx.CatalogType on ci.CatalogTypeId equals ct.CatalogTypeId
                        where unmappedCatIds.Contains(ci.CatalogItemId)
                        select new { ci.CatalogItemId, ct.CatalogTypeName, ci.CatalogItemDescription }
                    ).ToListAsync();
                    var fallbackByCat = fallback.ToDictionary(f => f.CatalogItemId);
                    foreach (var key in classByPair.Keys.ToList())
                    {
                        if (classByPair[key].Count > 0) continue;
                        if (fallbackByCat.TryGetValue(key.Item2, out var f))
                            classByPair[key] = new Dictionary<string, string> { { f.CatalogTypeName, f.CatalogItemDescription } };
                    }
                }
            }

            // 3. Construir resultado
            foreach (var l in lessons)
            {
                var e = new LessonEnrichmentData();
                if (l.LessonAreaId.HasValue
                    && pathByLessonAreaId.TryGetValue(l.LessonAreaId.Value, out var path))
                {
                    e.AreaDescription = path;
                }
                if (l.LessonAreaId.HasValue && l.CatalogItemId.HasValue
                    && classByPair.TryGetValue((l.LessonAreaId.Value, l.CatalogItemId.Value), out var classMap))
                {
                    classMap.TryGetValue("Fase", out var phase);
                    classMap.TryGetValue("Etapa", out var stage);
                    classMap.TryGetValue("Nivel", out var layer);
                    classMap.TryGetValue("Subetapa", out var substage);
                    classMap.TryGetValue("Subespecialidad", out var subspec);
                    classMap.TryGetValue("Partida", out var partida);
                    e.PhaseDescription = phase;
                    e.StageDescription = stage;
                    e.LayerDescription = layer;
                    e.SubStageDescription = substage;
                    e.SubSpecialtyDescription = subspec;
                    e.PartidaDescription = partida;
                }
                result[l.LessonId] = e;
            }

            return result;
        }
    }
}
