using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Services
{
    /// <summary>
    /// Implementa la resolución de visibilidad. Ver <see cref="ISalidaVisibilityResolver"/>.
    ///
    /// Override (ga_salida_visibilidad_area): si el usuario (a través de su/sus workers)
    /// tiene filas vivas, esas definen su visibilidad — cada fila aporta su nodo y, si
    /// <c>incluye_descendientes</c>, todo el subárbol. El algoritmo NO se aplica en ese caso.
    ///
    /// Algoritmo (fallback, cuando no hay override):
    ///   • GTH (área "Gestión del Talento Humano" en su cadena)      → ve todo.
    ///   • Gerente (workers_category "Gerente")                       → su gerencia (raíz Área
    ///                                                                  de Gerencia) + descendientes.
    ///   • Administración de Obra ("Administración de Obra" en cadena)→ todos los nodos de tipo
    ///                                                                  "Área Obra_Oficina".
    /// Los nombres de área/categoría se resuelven por texto (el árbol y el catálogo son
    /// administrables por UI), no por IDs fijos.
    /// </summary>
    public class SalidaVisibilityResolver : ISalidaVisibilityResolver
    {
        private const string AreaGth          = "Gestión del Talento Humano";
        private const string AreaAdminObra    = "Administración de Obra";
        private const string TipoObraOficina  = "Área Obra_Oficina";
        private const string CategoriaGerente = "Gerente";

        private readonly IDbContextFactory<AppDbContext> _factory;

        public SalidaVisibilityResolver(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<SalidaVisibility> ResolveAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // 1. Worker(s) del usuario (un user puede mapear a más de un worker).
            var workers = await (
                from w in ctx.Worker
                join p in ctx.Person on w.PersonId equals p.PersonId
                where p.UserId == userId
                join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId into cj
                from c in cj.DefaultIfEmpty()
                select new { w.Id, w.AreaScopeId, Categoria = c != null ? c.Name : null }
            ).ToListAsync();

            if (workers.Count == 0) return new SalidaVisibility(false, new HashSet<int>());

            var workerIds = workers.Select(w => w.Id).ToList();

            // 2. Topología del árbol (tabla chica) para expandir descendientes y correr el algoritmo.
            var nodos = await (
                from s in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State
                select new { s.AreaScopeId, s.AreaScopeParentId, ItemName = ai.AreaItemName, TypeName = at.AreaTypeName }
            ).ToListAsync();

            var parentById = nodos.ToDictionary(n => n.AreaScopeId, n => n.AreaScopeParentId);
            var itemNameById = nodos.ToDictionary(n => n.AreaScopeId, n => n.ItemName);
            var typeNameById = nodos.ToDictionary(n => n.AreaScopeId, n => n.TypeName);
            var childrenByParent = nodos
                .Where(n => n.AreaScopeParentId.HasValue)
                .GroupBy(n => n.AreaScopeParentId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.AreaScopeId).ToList());

            // 3. Override manual: si existe, define la visibilidad y el algoritmo NO corre.
            var overrides = await ctx.GaSalidaVisibilidadArea
                .Where(v => v.State && workerIds.Contains(v.WorkerId))
                .Select(v => new { v.AreaScopeId, v.IncluyeDescendientes })
                .ToListAsync();

            if (overrides.Count > 0)
            {
                var set = new HashSet<int>();
                foreach (var o in overrides)
                {
                    set.Add(o.AreaScopeId);
                    if (o.IncluyeDescendientes)
                        AddDescendants(o.AreaScopeId, childrenByParent, set);
                }
                // Si el override cubre TODOS los nodos del árbol, equivale a "ver todo"
                // (así también se ven las solicitudes de trabajadores sin area_scope asignado).
                if (nodos.Count > 0 && set.Count >= nodos.Count)
                    return new SalidaVisibility(true, set);
                return new SalidaVisibility(false, set);
            }

            // 4. Algoritmo (fallback).
            var visible = new HashSet<int>();
            var todosLosNodos = new Lazy<HashSet<int>>(() => nodos.Select(n => n.AreaScopeId).ToHashSet());

            foreach (var w in workers)
            {
                var cadena = w.AreaScopeId.HasValue ? AncestorsChain(w.AreaScopeId.Value, parentById) : new List<int>();

                // GTH → ve todo.
                if (cadena.Any(id => itemNameById.TryGetValue(id, out var name) &&
                                     string.Equals(name, AreaGth, StringComparison.OrdinalIgnoreCase)))
                {
                    return new SalidaVisibility(true, todosLosNodos.Value);
                }

                // Gerente → su gerencia (raíz) + descendientes.
                if (string.Equals(w.Categoria, CategoriaGerente, StringComparison.OrdinalIgnoreCase) && cadena.Count > 0)
                {
                    var root = cadena[^1];
                    visible.Add(root);
                    AddDescendants(root, childrenByParent, visible);
                }

                // Administración de Obra → todos los nodos de tipo "Área Obra_Oficina".
                if (cadena.Any(id => itemNameById.TryGetValue(id, out var name) &&
                                     string.Equals(name, AreaAdminObra, StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var n in nodos)
                        if (string.Equals(n.TypeName, TipoObraOficina, StringComparison.OrdinalIgnoreCase))
                            visible.Add(n.AreaScopeId);
                }
            }

            return new SalidaVisibility(false, visible);
        }

        /// <summary>Cadena (self, padre, abuelo, …, raíz) caminando hacia arriba. Corta ciclos.</summary>
        private static List<int> AncestorsChain(int startScopeId, IDictionary<int, int?> parentById)
        {
            var chain = new List<int>();
            var seen = new HashSet<int>();
            int? curr = startScopeId;
            while (curr.HasValue && seen.Add(curr.Value))
            {
                chain.Add(curr.Value);
                parentById.TryGetValue(curr.Value, out var parent);
                curr = parent;
            }
            return chain;
        }

        /// <summary>Agrega recursivamente todos los descendientes de un nodo al conjunto.</summary>
        private static void AddDescendants(int scopeId, IDictionary<int, List<int>> childrenByParent, HashSet<int> set)
        {
            if (!childrenByParent.TryGetValue(scopeId, out var children)) return;
            foreach (var child in children)
            {
                if (set.Add(child))
                    AddDescendants(child, childrenByParent, set);
            }
        }
    }
}
