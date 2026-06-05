using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Services
{
    /// <summary>
    /// Implementación del resolver de jefatura para lecciones aprendidas. La lógica
    /// de walk-up por <c>area_scope</c> está copiada (no compartida) de
    /// <c>ApproverResolver</c> de GestionAdministrativa, pero filtrando solo
    /// <c>Categoria == "Jefe"</c> y devolviendo <c>user_id</c>.
    /// </summary>
    public class LessonJefeResolver : ILessonJefeResolver
    {
        private const string CategoriaJefe = "Jefe";

        private readonly IDbContextFactory<AppDbContext> _factory;

        public LessonJefeResolver(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // Proyección mínima de un worker para resolver jerarquía.
        private sealed record WorkerNode(int Id, int? PersonId, int? AreaScopeId, string? Categoria);

        public async Task<int?> ResolveJefeUserIdAsync(int autorUserId)
        {
            using var ctx = _factory.CreateDbContext();

            var autor = await GetWorkerByUserIdAsync(ctx, autorUserId);
            if (autor == null || !autor.AreaScopeId.HasValue) return null;

            var parentByScope = await LoadParentMapAsync(ctx);
            var jefeByScope = await BuildJefeByScopeAsync(ctx);

            var jefeWorkerId = FindNearestJefeWorkerId(autor.AreaScopeId.Value, autor.Id, jefeByScope, parentByScope);
            if (jefeWorkerId == null) return null;

            return await GetUserIdByWorkerIdAsync(ctx, jefeWorkerId.Value);
        }

        public async Task<List<int>> GetSubordinateUserIdsAsync(int jefeUserId)
        {
            using var ctx = _factory.CreateDbContext();

            var jefe = await GetWorkerByUserIdAsync(ctx, jefeUserId);
            if (jefe == null) return new List<int>();

            var parentByScope = await LoadParentMapAsync(ctx);
            var jefeByScope = await BuildJefeByScopeAsync(ctx);

            // Todos los workers con área en el árbol y persona asociada.
            var workers = await ctx.Worker
                .Where(w => w.AreaScopeId.HasValue && w.PersonId.HasValue)
                .Select(w => new WorkerNode(w.Id, w.PersonId, w.AreaScopeId, w.Categoria))
                .ToListAsync();

            // person_id → user_id (un solo viaje a BD).
            var personIds = workers.Where(w => w.PersonId.HasValue).Select(w => w.PersonId!.Value).Distinct().ToList();
            var userByPerson = await ctx.Person
                .Where(p => personIds.Contains(p.PersonId))
                .Select(p => new { p.PersonId, p.UserId })
                .ToDictionaryAsync(x => x.PersonId, x => x.UserId);

            var result = new HashSet<int>();
            foreach (var w in workers)
            {
                if (w.Id == jefe.Id) continue;
                var nearest = FindNearestJefeWorkerId(w.AreaScopeId!.Value, w.Id, jefeByScope, parentByScope);
                if (nearest != jefe.Id) continue;
                if (w.PersonId.HasValue && userByPerson.TryGetValue(w.PersonId.Value, out var uid) && uid.HasValue)
                    result.Add(uid.Value);
            }
            return result.ToList();
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static async Task<WorkerNode?> GetWorkerByUserIdAsync(AppDbContext ctx, int userId)
        {
            return await (
                from w in ctx.Worker
                join p in ctx.Person on w.PersonId equals p.PersonId
                where p.UserId == userId
                select new WorkerNode(w.Id, w.PersonId, w.AreaScopeId, w.Categoria)
            ).FirstOrDefaultAsync();
        }

        private static async Task<int?> GetUserIdByWorkerIdAsync(AppDbContext ctx, int workerId)
        {
            return await (
                from w in ctx.Worker
                join p in ctx.Person on w.PersonId equals p.PersonId
                where w.Id == workerId
                select (int?)p.UserId
            ).FirstOrDefaultAsync();
        }

        private static async Task<Dictionary<int, int?>> LoadParentMapAsync(AppDbContext ctx)
        {
            return await ctx.AreaScope
                .AsNoTracking()
                .Select(s => new { s.AreaScopeId, s.AreaScopeParentId })
                .ToDictionaryAsync(s => s.AreaScopeId, s => s.AreaScopeParentId);
        }

        /// <summary>area_scope_id → worker_id del Jefe en ese nodo exacto (si lo hay).</summary>
        private static async Task<Dictionary<int, int>> BuildJefeByScopeAsync(AppDbContext ctx)
        {
            var jefes = await ctx.Worker
                .AsNoTracking()
                .Where(w => w.AreaScopeId.HasValue && w.Categoria == CategoriaJefe)
                .Select(w => new { w.Id, ScopeId = w.AreaScopeId!.Value })
                .ToListAsync();

            return jefes
                .GroupBy(j => j.ScopeId)
                .ToDictionary(g => g.Key, g => g.First().Id);
        }

        /// <summary>
        /// Camina hacia arriba desde <paramref name="startScopeId"/> (incluyéndolo) y
        /// devuelve el worker_id del primer Jefe encontrado, excluyendo al propio
        /// <paramref name="selfWorkerId"/>. null si no hay Jefe en la cadena.
        /// </summary>
        private static int? FindNearestJefeWorkerId(
            int startScopeId,
            int selfWorkerId,
            IReadOnlyDictionary<int, int> jefeByScope,
            IReadOnlyDictionary<int, int?> parentByScope)
        {
            var seen = new HashSet<int>();
            int? curr = startScopeId;
            while (curr.HasValue && seen.Add(curr.Value))
            {
                if (jefeByScope.TryGetValue(curr.Value, out var jefeWorkerId) && jefeWorkerId != selfWorkerId)
                    return jefeWorkerId;
                parentByScope.TryGetValue(curr.Value, out var parent);
                curr = parent;
            }
            return null;
        }
    }
}
