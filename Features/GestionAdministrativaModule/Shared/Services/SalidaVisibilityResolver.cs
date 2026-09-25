using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Jerarquia;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Implementa la resolución de visibilidad. Ver <see cref="ISalidaVisibilityResolver"/>.
    ///
    /// Piso obligatorio: si el usuario está asignado a mano en Revisores de Áreas
    /// (<c>area_actor_asignacion</c>) para un actor que actúa sobre la bandeja —aprobar la salida en
    /// SALIDAS; además revisar, consolidar o firmar en RENDICIONES y CONSOLIDADOS— ve ese nodo y todo
    /// su subárbol SIEMPRE, sin importar su categoría de trabajador. Y si está personalizado en la
    /// ficha de un trabajador (<c>workers_actor_asignacion</c>), ve lo de ese trabajador. No es un caso
    /// más del algoritmo: se suma tanto al override manual como al algoritmo, porque a esa persona le
    /// toca hacer un trabajo sobre eso y tiene que poder verlo.
    ///
    /// Override (ga_visibilidad_area, filtrado POR ÁMBITO): si el usuario (a través de su/sus
    /// workers) tiene filas vivas en ese ámbito, esas definen su visibilidad — cada fila aporta su
    /// nodo y, si <c>incluye_descendientes</c>, todo el subárbol. El algoritmo NO se aplica en ese
    /// caso (el piso obligatorio sí se suma igual).
    ///
    /// Algoritmo (fallback, cuando no hay override):
    ///   • GTH (área "Gestión del Talento Humano" en su cadena)      → ve todo.
    ///   • Gerente (<see cref="CategoriaIds.Gerente"/>)                → su gerencia (raíz Área
    ///                                                                  de Gerencia) + descendientes.
    ///   • Jefatura del área
    ///     (<see cref="CategoriaIds.ConVistaDeSuArea"/>)              → su propia área +
    ///                                                                  descendientes.
    ///   • Administración de Obra ("Administración de Obra" en cadena)→ las áreas donde hay
    ///                                                                  personal de Obra o Staff
    ///                                                                  (workers.obra_oficina_staff_id).
    /// Las áreas se resuelven por texto (el árbol es administrable por UI); la categoría, por
    /// id, para que renombrarla desde Configuración no apague la regla.
    ///
    /// Piso por OBRA (se suma a todo lo anterior, override incluido, en los tres ámbitos): el
    /// residente y el administrador de obra de un proyecto ven a TODOS los trabajadores cuya obra
    /// vigente es ese proyecto, sea cual sea su área. No se expresa en áreas —el árbol no tiene
    /// dimensión de obra y dar el área entera les abriría las demás obras— sino como lista de
    /// trabajadores (<see cref="SalidaVisibility.TrabajadoresDeSusObras"/>). Sale de
    /// <see cref="ObrasLoader"/>, la misma regla con la que el algoritmo les pide aprobar y firmar,
    /// así que el que hoy es residente ve exactamente lo que se le va a pedir, y el anterior deja de
    /// verlo (salvo lo que él mismo aprobó, que se ve por fila).
    /// </summary>
    public class SalidaVisibilityResolver : ISalidaVisibilityResolver
    {
        private const string AreaGth          = "Gestión del Talento Humano";
        private const string AreaAdminObra    = "Administración de Obra";

        private readonly IDbContextFactory<AppDbContext> _factory;

        public SalidaVisibilityResolver(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<SalidaVisibility> ResolveAsync(int userId, int ambitoId)
        {
            using var ctx = _factory.CreateDbContext();
            var obras = ObrasLoader.Obras(ctx);

            // 1. Worker(s) del usuario (un user puede mapear a más de un worker).
            var workers = await (
                from w in ctx.Worker
                join p in ctx.Person on w.PersonId equals p.PersonId
                where p.UserId == userId
                select new WorkerContexto
                {
                    // El área y la categoría salen las dos del puesto: workers ya no las guarda.
                    Id = w.Id,
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                    CategoriaId = w.PuestoCatalogo != null ? w.PuestoCatalogo.CategoriaId : (int?)null,
                    ACargoDeObra = obras.Any(o => o.ResidenteWorkersId == w.Id || o.WorkersCoordAdminId == w.Id),
                }
            ).ToListAsync();

            return await ResolveAsync(ctx, workers, ambitoId);
        }

        public async Task<SalidaVisibility> ResolveByWorkerAsync(int workerId, int ambitoId)
        {
            using var ctx = _factory.CreateDbContext();
            var obras = ObrasLoader.Obras(ctx);

            var workers = await (
                from w in ctx.Worker
                where w.Id == workerId
                select new WorkerContexto
                {
                    Id = w.Id,
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                    CategoriaId = w.PuestoCatalogo != null ? w.PuestoCatalogo.CategoriaId : (int?)null,
                    ACargoDeObra = obras.Any(o => o.ResidenteWorkersId == w.Id || o.WorkersCoordAdminId == w.Id),
                }
            ).ToListAsync();

            return await ResolveAsync(ctx, workers, ambitoId);
        }

        /// <summary>Lo único que cambia entre las dos entradas es de qué fichas se parte.</summary>
        private async Task<SalidaVisibility> ResolveAsync(
            AppDbContext ctx, List<WorkerContexto> workers, int ambitoId)
        {
            if (workers.Count == 0) return new SalidaVisibility(false, new HashSet<int>());

            var porArea = await ResolverAreasAsync(ctx, workers, ambitoId);

            // Quien ya ve todo no necesita listas de trabajadores: no hay nada que recortar.
            if (porArea.SeesAll) return porArea;

            var workerIds = workers.Select(w => w.Id).ToList();

            // Piso por TRABAJADOR: si en la ficha de alguien se personalizó a este usuario para un
            // actor que actúa sobre esta bandeja, ve lo de ese trabajador. Es la contraparte del piso
            // por área de Revisores de Áreas: sin esto, el aprobador o el consolidador elegido a mano
            // tendría que decidir sobre algo que no ve.
            var actores = ActoresDelAmbito(ambitoId);
            var personalizados = (await ctx.WorkersActorAsignacion
                    .Where(r => r.State && r.Active
                             && workerIds.Contains(r.AsignadoId)
                             && actores.Contains(r.GaActorId))
                    .Select(r => r.WorkerId)
                    .Distinct()
                    .ToListAsync())
                .ToHashSet();

            // Piso por OBRA: el residente y el administrador de obra ven todo lo de su obra. Se
            // suma también sobre el override: aprobar y firmar por la obra no depende de que alguien
            // se acuerde de darle visibilidad a quien hoy ocupa el puesto. Quien no está a cargo de
            // ninguna (casi todos) no paga ninguna consulta más: lo dijo ya la de sus fichas.
            var obras = workers.Any(w => w.ACargoDeObra)
                ? await ObrasLoader.ObrasACargoAsync(ctx, workerIds)
                : new List<ObrasLoader.ObraACargo>();

            if (obras.Count == 0 && personalizados.Count == 0) return porArea;

            var deSusObras = obras.Count == 0
                ? new HashSet<int>()
                : await ObrasLoader.TrabajadoresDeLasObrasAsync(ctx, obras.Select(o => o.ProjectId).ToList());
            deSusObras.UnionWith(personalizados);

            return porArea with
            {
                Obras = obras,
                // Los dos pisos por trabajador van en la misma lista: el filtro los suma igual.
                TrabajadoresDeSusObras = deSusObras,
            };
        }

        /// <summary>
        /// Los actores (<c>ActorIds</c>) que actúan sobre cada bandeja: en Gestión de Salidas solo el
        /// que aprueba la salida; en Gestión de Rendiciones y en Consolidados también los que revisan
        /// la planilla, la consolidan y firman el consolidado. El jefe notificado no entra: se entera
        /// por correo, con el detalle completo, y no decide nada.
        /// </summary>
        private static int[] ActoresDelAmbito(int ambitoId) => ambitoId == VisibilidadAmbitoIds.Salidas
            ? new[] { ActorIds.AprobadorSalida }
            : new[]
            {
                ActorIds.AprobadorSalida, ActorIds.AprobadorPrimeraRevision,
                ActorIds.Consolidador, ActorIds.AprobadorConsolidado,
            };

        /// <summary>
        /// El alcance por ÁREA: piso de revisor/consolidador, override del ámbito y, si no hay
        /// override, el algoritmo de jerarquía.
        /// </summary>
        private async Task<SalidaVisibility> ResolverAreasAsync(
            AppDbContext ctx, List<WorkerContexto> workers, int ambitoId)
        {
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

            // 3. Piso obligatorio: nodos donde el usuario está asignado a mano en Revisores de Áreas
            //    (area_actor_asignacion) para algún actor que actúa sobre ESTA bandeja → ese nodo +
            //    su subárbol, sin importar categoría ni override. Se exige Active además de State:
            //    alguien desactivado no recibe nada, así que tampoco gana visibilidad. Las filas por
            //    obra cuentan igual que las de área: la visibilidad se expresa por area_scope.
            var actores = ActoresDelAmbito(ambitoId);
            var nodosAsignados = await ctx.AreaActorAsignacion
                .Where(a => a.State && a.Active
                         && workerIds.Contains(a.WorkerId)
                         && actores.Contains(a.GaActorId))
                .Select(a => a.AreaScopeId)
                .Distinct()
                .ToListAsync();

            var comoRevisor = new HashSet<int>();
            foreach (var nodo in nodosAsignados)
            {
                // parentById solo tiene los nodos vivos; se ignoran los de áreas dadas de baja.
                if (!parentById.ContainsKey(nodo)) continue;
                comoRevisor.Add(nodo);
                AddDescendants(nodo, childrenByParent, comoRevisor);
            }

            // 4. Override manual DE ESTE ÁMBITO: si existe, define la visibilidad y el algoritmo
            //    NO corre (el piso de revisor/consolidador se suma de todas formas).
            var overrides = await ctx.GaVisibilidadArea
                .Where(v => v.State && v.AmbitoId == ambitoId && workerIds.Contains(v.WorkerId))
                .Select(v => new { v.AreaScopeId, v.IncluyeDescendientes })
                .ToListAsync();

            if (overrides.Count > 0)
            {
                var set = new HashSet<int>(comoRevisor);
                foreach (var o in overrides)
                {
                    set.Add(o.AreaScopeId);
                    if (o.IncluyeDescendientes)
                        AddDescendants(o.AreaScopeId, childrenByParent, set);
                }
                // Si lo acumulado cubre TODOS los nodos del árbol, equivale a "ver todo"
                // (así también se ven las solicitudes de trabajadores sin area_scope asignado).
                if (nodos.Count > 0 && set.Count >= nodos.Count)
                    return new SalidaVisibility(true, set);
                return new SalidaVisibility(false, set);
            }

            // 5. Algoritmo (fallback), partiendo del piso de revisor de área.
            var visible = new HashSet<int>(comoRevisor);
            // Se carga una sola vez y solo si algún worker del usuario es de Administración de Obra.
            List<int>? areasConPersonalDeObra = null;
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
                if (w.CategoriaId == CategoriaIds.Gerente && cadena.Count > 0)
                {
                    var root = cadena[^1];
                    visible.Add(root);
                    AddDescendants(root, childrenByParent, visible);
                }

                // Jefatura del área → su propia área + descendientes.
                //
                // El piso obligatorio de más arriba solo ve lo asignado a mano, pero el algoritmo
                // de los actores (IActoresResolver: la jefatura del área estándar, el Gerente de
                // la gerencia) hace a una jefatura aprobadora y consolidadora de su área SIN tener
                // nada asignado. Sin esta regla esa persona quedaba en cero áreas: le tocaba
                // revisar una rama que no podía ver, y la bandeja le salía vacía. Se decide por
                // categoría, igual que el otro algoritmo, para que los dos deduzcan lo mismo de
                // la misma estructura.
                if (w.CategoriaId.HasValue
                    && CategoriaIds.ConVistaDeSuArea.Contains(w.CategoriaId.Value)
                    && w.AreaScopeId.HasValue
                    && parentById.ContainsKey(w.AreaScopeId.Value))
                {
                    visible.Add(w.AreaScopeId.Value);
                    AddDescendants(w.AreaScopeId.Value, childrenByParent, visible);
                }

                // Administración de Obra → las áreas con personal de Obra o Staff.
                //
                // Antes esto se resolvía listando los nodos de tipo "Área Obra_Oficina" del
                // árbol; ese tipo de área se eliminó y la distinción Obra / Staff / Oficina
                // Central pasó a workers.obra_oficina_staff_id, así que ahora el conjunto se
                // deriva de dónde está asignado ese personal.
                if (cadena.Any(id => itemNameById.TryGetValue(id, out var name) &&
                                     string.Equals(name, AreaAdminObra, StringComparison.OrdinalIgnoreCase)))
                {
                    areasConPersonalDeObra ??= await ctx.Worker
                        .Where(x => x.PuestoCatalogo!.AreaDestinoScopeId != null
                                    && (x.ObraOficinaStaffId == ObraOficinaStaffIds.Obra
                                        || x.ObraOficinaStaffId == ObraOficinaStaffIds.Staff))
                        .Select(x => x.PuestoCatalogo!.AreaDestinoScopeId!.Value)
                        .Distinct()
                        .ToListAsync();

                    foreach (var id in areasConPersonalDeObra)
                        if (parentById.ContainsKey(id)) visible.Add(id);
                }
            }

            return new SalidaVisibility(false, visible);
        }

        /// <summary>
        /// Lo que el algoritmo necesita saber de una ficha: dónde está, qué categoría tiene y si hoy
        /// es residente o administrador de alguna obra.
        /// </summary>
        private class WorkerContexto
        {
            public int Id { get; set; }
            public int? AreaScopeId { get; set; }
            public int? CategoriaId { get; set; }
            public bool ACargoDeObra { get; set; }
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
