using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Qué nodos del árbol de áreas se pueden configurar en Revisores de Áreas y cuáles ve (y edita,
    /// si es jefe) un usuario que no administra la pantalla.
    /// </summary>
    public static class AreaAsignacionNodos
    {
        public const string AreaTypeEstandar = "Área Estándar";
        public const string AreaTypeGerencia = "Área de Gerencia";

        /// <summary>Tipos de área que admiten asignaciones, en el orden en que se listan.</summary>
        public static readonly string[] TiposConfigurables = { AreaTypeGerencia, AreaTypeEstandar };

        public sealed record NodoArea(
            int AreaScopeId, int? AreaScopeParentId, string AreaItemName, string AreaTypeName);

        /// <summary>Árbol completo de áreas vivas, como lista plana.</summary>
        public static async Task<List<NodoArea>> LoadNodosAsync(AppDbContext ctx)
        {
            return await (
                from s in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State && at.State
                select new NodoArea(s.AreaScopeId, s.AreaScopeParentId, ai.AreaItemName, at.AreaTypeName)
            ).ToListAsync();
        }

        /// <summary>
        /// Nodos configurables: los de un tipo de <see cref="TiposConfigurables"/> que son el
        /// primero de SU MISMO tipo en su rama, es decir que ningún ancestro comparte su tipo. Así,
        /// de "Gerencia de Proyectos" → "Unidad de Proyectos" → "Ingeniería BIM" se devuelven la
        /// gerencia (única de su tipo en la rama) y "Unidad de Proyectos" (primera estándar), pero
        /// no "Ingeniería BIM", que cuelga de otra estándar.
        ///
        /// Una gerencia y las áreas estándar que cuelgan de ella coexisten en la lista aunque una
        /// sea padre de la otra: el algoritmo toma siempre el nodo más cercano al trabajador, así
        /// que la gerencia es el área propia de los gerentes y el respaldo del resto de su rama.
        /// </summary>
        public static List<NodoArea> Configurables(List<NodoArea> nodos)
        {
            var byId = nodos.ToDictionary(n => n.AreaScopeId);
            return nodos
                .Where(n => TiposConfigurables.Contains(n.AreaTypeName) && !TieneAncestroDelMismoTipo(n, byId))
                .ToList();
        }

        private static bool TieneAncestroDelMismoTipo(NodoArea nodo, Dictionary<int, NodoArea> byId)
        {
            var visitados = new HashSet<int>();
            var parentId = nodo.AreaScopeParentId;
            while (parentId != null && visitados.Add(parentId.Value))
            {
                if (!byId.TryGetValue(parentId.Value, out var parent)) break;
                if (parent.AreaTypeName == nodo.AreaTypeName) return true;
                parentId = parent.AreaScopeParentId;
            }
            return false;
        }

        /// <summary>Nodos configurables (area_scope_id) que ve un usuario y, de esos, los de su jefatura.</summary>
        public sealed record AlcanceUsuario(HashSet<int> Visibles, HashSet<int> DelJefe);

        /// <summary>
        /// Qué nodos configurables ve y cuáles edita un usuario que NO administra la pantalla. Se parte
        /// del área de cada ficha viva y adentro (la de destino de su puesto) y se sube el árbol hasta
        /// el primer nodo listado (un gerente colgado directamente de su gerencia resuelve a esa
        /// gerencia):
        ///   • <c>Visibles</c>: las fichas de <see cref="CategoriaIds.ConVistaDeSuArea"/> (Sub Gerente,
        ///     Jefe, Coordinador o Gerente) ven su área, en lectura.
        ///   • <c>DelJefe</c>: las de categoría JEFE, además, eligen a los consolidadores de oficina
        ///     central de su área. Solo áreas estándar: la gerencia es de su gerente.
        /// </summary>
        public static async Task<AlcanceUsuario> AlcanceDelUsuarioAsync(
            AppDbContext ctx, int userId, List<NodoArea> nodos, List<NodoArea> configurables)
        {
            var fichas = await (
                from w in ctx.Worker
                where w.Person != null && w.Person.UserId == userId
                    && w.State
                    && WorkersEstadoIds.EstanAdentro.Contains(w.WorkersEstadoId)
                    && w.PuestoCatalogo != null
                    && w.PuestoCatalogo.AreaDestinoScopeId != null
                    && CategoriaIds.ConVistaDeSuArea.Contains(w.PuestoCatalogo.CategoriaId)
                select new { Area = w.PuestoCatalogo.AreaDestinoScopeId!.Value, w.PuestoCatalogo.CategoriaId }
            ).ToListAsync();

            var byId = nodos.ToDictionary(n => n.AreaScopeId);
            var configurablesIds = configurables.Select(n => n.AreaScopeId).ToHashSet();
            var alcance = new AlcanceUsuario(new HashSet<int>(), new HashSet<int>());

            foreach (var ficha in fichas)
            {
                var nodo = PrimerConfigurable(ficha.Area, byId, configurablesIds);
                if (nodo == null) continue;

                alcance.Visibles.Add(nodo.Value);
                if (ficha.CategoriaId == CategoriaIds.Jefe && byId[nodo.Value].AreaTypeName == AreaTypeEstandar)
                    alcance.DelJefe.Add(nodo.Value);
            }
            return alcance;
        }

        /// <summary>El propio nodo y todos sus descendientes, de cada nodo pedido.</summary>
        public static List<int> Subarbol(List<NodoArea> nodos, IEnumerable<int> raices)
        {
            var hijos = nodos
                .Where(n => n.AreaScopeParentId != null)
                .GroupBy(n => n.AreaScopeParentId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(n => n.AreaScopeId).ToList());

            var resultado = new HashSet<int>();
            var pendientes = new Stack<int>(raices);
            while (pendientes.Count > 0)
            {
                var actual = pendientes.Pop();
                if (!resultado.Add(actual)) continue;
                if (hijos.TryGetValue(actual, out var deEste))
                    foreach (var h in deEste) pendientes.Push(h);
            }
            return resultado.ToList();
        }

        private static int? PrimerConfigurable(int desde, Dictionary<int, NodoArea> byId, HashSet<int> configurablesIds)
        {
            var visitados = new HashSet<int>();
            int? actual = desde;
            while (actual != null && visitados.Add(actual.Value))
            {
                if (configurablesIds.Contains(actual.Value)) return actual.Value;
                actual = byId.TryGetValue(actual.Value, out var nodo) ? nodo.AreaScopeParentId : null;
            }
            return null;
        }
    }
}
