using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Qué nodos del árbol de áreas se pueden configurar en las pantallas que asignan personas a
    /// áreas (Revisores de Áreas y Consolidadores de Áreas) y cuál de ellos ve un usuario que no
    /// las administra.
    ///
    /// Las dos listan exactamente los mismos nodos: si una ofreciera configurar un área que la otra
    /// no, el algoritmo —que es el mismo— resolvería distinto según la pantalla.
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

        /// <summary>
        /// area_scope_id (de los nodos configurables) que el usuario puede ver, o null si no puede
        /// ver ninguno. Solo ven su área los trabajadores de las categorías
        /// <see cref="CategoriaIds.ConVistaDeSuArea"/> (Jefe, Coordinador o Gerente): se parte del
        /// área del trabajador (la de destino de su puesto) y se sube el árbol hasta el primer nodo
        /// listado (un gerente colgado directamente de su gerencia resuelve a esa gerencia).
        /// </summary>
        public static async Task<int?> AreaVisibleDelUsuarioAsync(
            AppDbContext ctx, int userId, List<NodoArea> nodos, List<NodoArea> configurables)
        {
            var areaScopeWorker = await (
                from w in ctx.Worker
                where w.Person != null && w.Person.UserId == userId
                    && w.PuestoCatalogo != null
                    && w.PuestoCatalogo.AreaDestinoScopeId != null
                    && CategoriaIds.ConVistaDeSuArea.Contains(w.PuestoCatalogo.CategoriaId)
                select w.PuestoCatalogo.AreaDestinoScopeId
            ).FirstOrDefaultAsync();
            if (areaScopeWorker == null) return null;

            var byId = nodos.ToDictionary(n => n.AreaScopeId);
            var configurablesIds = configurables.Select(n => n.AreaScopeId).ToHashSet();
            var visitados = new HashSet<int>();
            int? actual = areaScopeWorker;
            while (actual != null && visitados.Add(actual.Value))
            {
                if (configurablesIds.Contains(actual.Value)) return actual.Value;
                actual = byId.TryGetValue(actual.Value, out var nodo) ? nodo.AreaScopeParentId : null;
            }
            return null;
        }

        /// <summary>
        /// Marca/desmarca "filtrar por proyecto" para un nodo configurable. La bandera vive en
        /// <c>ga_salidas_area_config</c> y la comparten las dos pantallas: es una propiedad del
        /// área, no de quién se le asigna.
        /// </summary>
        public static async Task SetFiltroProyectoAsync(
            AppDbContext ctx, int areaScopeId, bool filtraPorProyecto)
        {
            var now = DateTimeOffset.UtcNow;
            var config = await ctx.GaSalidasAreaConfig
                .FirstOrDefaultAsync(f => f.State && f.AreaScopeId == areaScopeId);

            if (config == null)
            {
                ctx.GaSalidasAreaConfig.Add(new Models.GaSalidasAreaConfig
                {
                    AreaScopeId = areaScopeId,
                    FiltraPorProyecto = filtraPorProyecto,
                    State = true,
                    Active = true,
                    CreatedAt = now,
                });
            }
            else if (config.FiltraPorProyecto != filtraPorProyecto)
            {
                config.FiltraPorProyecto = filtraPorProyecto;
                config.UpdatedAt = now;
            }

            await ctx.SaveChangesAsync();
        }
    }
}
