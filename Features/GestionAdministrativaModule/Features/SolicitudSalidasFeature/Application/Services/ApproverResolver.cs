using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Services
{
    /// <summary>
    /// Resuelve el correo del aprobador para una solicitud de salida usando el árbol
    /// <c>area_scope</c> y la categoría del trabajador.
    ///
    /// Reglas:
    ///   1) Gerente                  → no necesita aprobador (null).
    ///   2) Jefe / Sub Gerente        → directo al Gerente del macro-área (mismo root).
    ///   3) Resto                    → walk-up por la cadena ancestral
    ///                                 buscando Jefe → Sub Gerente → Coordinador.
    ///                                 Si la cadena no devuelve nada → fallback al
    ///                                 Gerente del macro-área.
    ///
    /// Sólo se consideran como aprobadores trabajadores con <c>email_personal</c>
    /// que termine en <c>@abril.pe</c> (correo corporativo).
    /// </summary>
    public class ApproverResolver : IApproverResolver
    {
        private const string EmailDomainCorp = "@abril.pe";

        // Categorías que pueden aprobar a un trabajador "regular" (regla C).
        private static readonly string[] CategoriasWalkUp = { "Jefe", "Sub Gerente", "Coordinador" };

        private readonly IDbContextFactory<AppDbContext> _factory;

        public ApproverResolver(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<ApproverResolution?> ResolveApproverAsync(Worker user)
        {
            using var ctx = _factory.CreateDbContext();

            // Regla 0 (override manual): si el trabajador tiene un revisor de salidas asignado
            // (workers.worker_salida_jefe_id, sección "Revisor de Salidas") y ese jefe tiene
            // correo corporativo @abril.pe, se usa directamente. Tiene prioridad sobre todo el
            // algoritmo del árbol. Si el campo es null o el jefe no tiene correo válido, se cae
            // al algoritmo de jerarquía (fallback) definido más abajo.
            if (user.WorkerSalidaJefeId.HasValue)
            {
                var jefe = await ctx.Worker
                    .AsNoTracking()
                    .Where(w => w.Id == user.WorkerSalidaJefeId.Value && w.Id != user.Id)
                    .Select(w => new { w.Id, w.EmailPersonal })
                    .FirstOrDefaultAsync();

                if (jefe != null && jefe.EmailPersonal != null &&
                    jefe.EmailPersonal.Trim().EndsWith(EmailDomainCorp, StringComparison.OrdinalIgnoreCase))
                {
                    return new ApproverResolution(jefe.Id, jefe.EmailPersonal.Trim());
                }
                // Sin correo válido → continúa al fallback por jerarquía.
            }

            // Categoría del solicitante leída del catálogo workers_category
            // (FK worker_category_id), no del texto libre workers.categoria.
            var userCategoria = user.WorkerCategoryId.HasValue
                ? await ctx.WorkersCategory
                    .Where(c => c.WorkersCategoryId == user.WorkerCategoryId.Value)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync()
                : null;

            // Regla A: el Gerente no necesita aprobador
            if (string.Equals(userCategoria, "Gerente", StringComparison.OrdinalIgnoreCase))
                return null;

            // Si el trabajador no tiene área en el árbol, no se puede resolver por jerarquía
            if (!user.AreaScopeId.HasValue)
                return null;

            // Cargamos la topología completa del árbol una sola vez (es una tabla
            // chica, decenas de filas) y caminamos en memoria.
            var parentByScope = await ctx.AreaScope
                .AsNoTracking()
                .Select(s => new { s.AreaScopeId, s.AreaScopeParentId })
                .ToDictionaryAsync(s => s.AreaScopeId, s => s.AreaScopeParentId);

            var ancestros = BuildAncestorsChain(user.AreaScopeId.Value, parentByScope);
            var rootId    = ancestros[^1]; // último = raíz

            // Regla B: Jefe / Sub Gerente → salta directo al Gerente del macro-área
            if (string.Equals(userCategoria, "Jefe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(userCategoria, "Sub Gerente", StringComparison.OrdinalIgnoreCase))
            {
                return await FindGerenteByRootAsync(ctx, rootId, user.Id, parentByScope);
            }

            // Regla C: walk-up Jefe>SubGer>Coord por la cadena ancestral
            foreach (var scopeId in ancestros)
            {
                var candidatos = await (
                    from w in ctx.Worker.AsNoTracking()
                    join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId
                    where w.AreaScopeId == scopeId
                          && w.Id != user.Id
                          && CategoriasWalkUp.Contains(c.Name)
                          && w.EmailPersonal != null
                          && w.EmailPersonal.EndsWith(EmailDomainCorp)
                    select new { w.Id, Categoria = c.Name, w.EmailPersonal }
                ).ToListAsync();

                if (candidatos.Count == 0) continue;

                var elegido = candidatos
                    .OrderBy(c => CategoriaPriority(c.Categoria))
                    .First();

                return new ApproverResolution(elegido.Id, elegido.EmailPersonal!.Trim());
            }

            // Fallback: ningún Jefe/SubGer/Coord en la cadena → Gerente del macro-área
            return await FindGerenteByRootAsync(ctx, rootId, user.Id, parentByScope);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve la cadena (scope propio, padre, abuelo, …, raíz) caminando hacia arriba.
        /// Si hay un ciclo de datos (no debería existir), se corta defensivamente.
        /// </summary>
        private static List<int> BuildAncestorsChain(int startScopeId, IDictionary<int, int?> parentByScope)
        {
            var chain = new List<int>();
            var seen  = new HashSet<int>();
            int? curr = startScopeId;
            while (curr.HasValue && seen.Add(curr.Value))
            {
                chain.Add(curr.Value);
                parentByScope.TryGetValue(curr.Value, out var parent);
                curr = parent;
            }
            return chain;
        }

        /// <summary>
        /// Busca el Gerente entre todos los workers cuyo <c>area_scope_id</c> resuelva
        /// al mismo root que el del solicitante. Excluye self.
        /// </summary>
        private static async Task<ApproverResolution?> FindGerenteByRootAsync(
            AppDbContext ctx,
            int rootId,
            int excludeWorkerId,
            IDictionary<int, int?> parentByScope)
        {
            // Pre-calcular qué scopes cuelgan de ese root (incluido él mismo)
            var scopesEnRaiz = parentByScope.Keys
                .Where(scopeId => RootOf(scopeId, parentByScope) == rootId)
                .ToHashSet();

            var gerentes = await (
                from w in ctx.Worker.AsNoTracking()
                join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId
                where w.AreaScopeId.HasValue
                      && w.Id != excludeWorkerId
                      && c.Name == "Gerente"
                      && w.EmailPersonal != null
                      && w.EmailPersonal.EndsWith(EmailDomainCorp)
                select new { w.Id, w.AreaScopeId, w.EmailPersonal }
            ).ToListAsync();

            var gerente = gerentes.FirstOrDefault(g => g.AreaScopeId.HasValue && scopesEnRaiz.Contains(g.AreaScopeId.Value));
            return gerente == null ? null : new ApproverResolution(gerente.Id, gerente.EmailPersonal!.Trim());
        }

        /// <summary>Camina hacia arriba devolviendo el id de la raíz de un scope.</summary>
        private static int RootOf(int scopeId, IDictionary<int, int?> parentByScope)
        {
            var seen = new HashSet<int>();
            int curr = scopeId;
            while (seen.Add(curr) && parentByScope.TryGetValue(curr, out var parent) && parent.HasValue)
            {
                curr = parent.Value;
            }
            return curr;
        }

        private static int CategoriaPriority(string? categoria) => categoria switch
        {
            "Jefe"        => 1,
            "Sub Gerente" => 2,
            "Coordinador" => 3,
            _             => 99,
        };
    }
}
