using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Services
{
    /// <summary>
    /// Resuelve la jefatura (revisor) de una lección caminando hacia arriba por el
    /// árbol <c>area_scope</c>.
    ///
    /// Reglas:
    ///   • Categorías que pueden revisar: Jefe, Coordinador, Residente.
    ///   • Prioridad cuando coexisten en el MISMO nodo: Jefe &gt; Coordinador &gt;
    ///     Residente (orden Jefe&gt;Coordinador tomado de <c>ApproverResolver</c> de
    ///     Solicitud de Salidas; Residente es el de menor prioridad por requerimiento).
    ///   • Solo cuenta como revisor quien además está HABILITADO en la sección
    ///     "Jefaturas" de recordatorios (<c>lesson_jefe_reminder.active = true</c>,
    ///     state = true). Un revisor inactivo no aprueba/rechaza nada: las lecciones
    ///     de su equipo se reasignan al siguiente revisor activo hacia arriba.
    ///   • Gana el revisor activo del NODO más cercano subiendo; la prioridad solo
    ///     desempata dentro de un mismo nodo (igual que ApproverResolver).
    /// </summary>
    public class LessonJefeResolver : ILessonJefeResolver
    {
        // Categorías que pueden revisar lecciones (aprobar/rechazar / tener subordinados).
        private static readonly string[] CategoriasRevisor = { "Jefe", "Coordinador", "Residente" };

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
            var revisorByScope = await BuildActiveRevisorByScopeAsync(ctx);

            var revisorWorkerId = FindNearestRevisorWorkerId(autor.AreaScopeId.Value, autor.Id, revisorByScope, parentByScope);
            if (revisorWorkerId == null) return null;

            return await GetUserIdByWorkerIdAsync(ctx, revisorWorkerId.Value);
        }

        public async Task<List<int>> GetSubordinateUserIdsAsync(int jefeUserId)
        {
            using var ctx = _factory.CreateDbContext();

            var jefe = await GetWorkerByUserIdAsync(ctx, jefeUserId);
            if (jefe == null) return new List<int>();

            var parentByScope = await LoadParentMapAsync(ctx);
            var revisorByScope = await BuildActiveRevisorByScopeAsync(ctx);

            // Todos los workers con área en el árbol y persona asociada.
            // Categoria se omite (null): aquí la jerarquía se resuelve por revisorByScope,
            // no por el texto workers.categoria.
            var workers = await ctx.Worker
                .Where(w => w.AreaScopeId.HasValue && w.PersonId.HasValue)
                .Select(w => new WorkerNode(w.Id, w.PersonId, w.AreaScopeId, null))
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
                var nearest = FindNearestRevisorWorkerId(w.AreaScopeId!.Value, w.Id, revisorByScope, parentByScope);
                if (nearest != jefe.Id) continue;
                if (w.PersonId.HasValue && userByPerson.TryGetValue(w.PersonId.Value, out var uid) && uid.HasValue)
                    result.Add(uid.Value);
            }
            return result.ToList();
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static async Task<WorkerNode?> GetWorkerByUserIdAsync(AppDbContext ctx, int userId)
        {
            // La categoría se lee del catálogo workers_category (FK worker_category_id),
            // no del texto workers.categoria. Left join: si el worker no tiene categoría
            // mapeada, Categoria queda null (no será revisor).
            return await (
                from w in ctx.Worker
                join p in ctx.Person on w.PersonId equals p.PersonId
                join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId into cj
                from c in cj.DefaultIfEmpty()
                where p.UserId == userId
                select new WorkerNode(w.Id, w.PersonId, w.AreaScopeId, c != null ? c.Name : null)
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

        /// <summary>
        /// area_scope_id → worker_id del revisor ACTIVO de mayor prioridad en ese nodo.
        /// Solo entran workers con categoría revisor (Jefe/Coordinador/Residente) que
        /// además tienen una fila viva (state=true) y active=true en lesson_jefe_reminder.
        /// </summary>
        private static async Task<Dictionary<int, int>> BuildActiveRevisorByScopeAsync(AppDbContext ctx)
        {
            // worker_ids habilitados en la sección "Jefaturas" de recordatorios.
            var activeWorkerIds = (await ctx.LessonJefeReminder
                .Where(r => r.State && r.Active)
                .Select(r => r.WorkerId)
                .ToListAsync())
                .ToHashSet();

            if (activeWorkerIds.Count == 0)
                return new Dictionary<int, int>();

            var revisores = await (
                from w in ctx.Worker.AsNoTracking()
                join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId
                where w.AreaScopeId.HasValue && CategoriasRevisor.Contains(c.Name)
                select new { w.Id, ScopeId = w.AreaScopeId!.Value, Categoria = c.Name }
            ).ToListAsync();

            return revisores
                .Where(r => activeWorkerIds.Contains(r.Id))
                .GroupBy(r => r.ScopeId)
                .ToDictionary(
                    g => g.Key,
                    // Mayor prioridad primero; desempate estable por menor worker id.
                    g => g.OrderBy(r => CategoriaPriority(r.Categoria)).ThenBy(r => r.Id).First().Id);
        }

        /// <summary>
        /// Camina hacia arriba desde <paramref name="startScopeId"/> (incluyéndolo) y
        /// devuelve el worker_id del primer revisor ACTIVO encontrado, excluyendo al
        /// propio <paramref name="selfWorkerId"/>. null si no hay revisor en la cadena.
        /// </summary>
        private static int? FindNearestRevisorWorkerId(
            int startScopeId,
            int selfWorkerId,
            IReadOnlyDictionary<int, int> revisorByScope,
            IReadOnlyDictionary<int, int?> parentByScope)
        {
            var seen = new HashSet<int>();
            int? curr = startScopeId;
            while (curr.HasValue && seen.Add(curr.Value))
            {
                if (revisorByScope.TryGetValue(curr.Value, out var revisorWorkerId) && revisorWorkerId != selfWorkerId)
                    return revisorWorkerId;
                parentByScope.TryGetValue(curr.Value, out var parent);
                curr = parent;
            }
            return null;
        }

        /// <summary>Prioridad de revisión: menor número = mayor prioridad.</summary>
        private static int CategoriaPriority(string? categoria) => categoria switch
        {
            "Jefe"        => 1,
            "Coordinador" => 2,
            "Residente"   => 3,
            _             => 99,
        };

        public async Task<bool> CanReviewProjectAsync(int reviewerUserId, int? projectId)
        {
            using var ctx = _factory.CreateDbContext();

            var categoria = await GetCategoriaByUserIdAsync(ctx, reviewerUserId);

            // Solo el Residente está acotado por proyecto; el resto no tiene restricción.
            if (!string.Equals(categoria, "Residente", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!projectId.HasValue) return false;

            return await (
                from up in ctx.UserProject
                join w in ctx.Worker on up.WorkerId equals w.Id
                join p in ctx.Person on w.PersonId equals p.PersonId
                where p.UserId == reviewerUserId
                      && up.ProjectId == projectId.Value
                      && up.State && up.Active
                select up.UserProjectId
            ).AnyAsync();
        }

        public async Task<List<int>?> GetResidenteProjectScopeAsync(int reviewerUserId)
        {
            using var ctx = _factory.CreateDbContext();

            var categoria = await GetCategoriaByUserIdAsync(ctx, reviewerUserId);
            if (!string.Equals(categoria, "Residente", StringComparison.OrdinalIgnoreCase))
                return null; // no es Residente → sin restricción de proyecto

            return await (
                from up in ctx.UserProject
                join w in ctx.Worker on up.WorkerId equals w.Id
                join p in ctx.Person on w.PersonId equals p.PersonId
                where p.UserId == reviewerUserId && up.State && up.Active
                select up.ProjectId
            ).Distinct().ToListAsync();
        }

        /// <summary>Nombre de la categoría (workers_category) del usuario revisor.</summary>
        private static async Task<string?> GetCategoriaByUserIdAsync(AppDbContext ctx, int userId)
        {
            return await (
                from w in ctx.Worker
                join p in ctx.Person on w.PersonId equals p.PersonId
                join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId
                where p.UserId == userId
                select c.Name
            ).FirstOrDefaultAsync();
        }
    }
}
