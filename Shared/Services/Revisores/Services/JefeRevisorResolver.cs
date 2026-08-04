using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services.Revisores.Services
{
    /// <summary>
    /// Resolución del jefe/revisor de un trabajador en tres pasos:
    ///   1) <c>workers_revisores</c>: n revisores por trabajador, por prioridad.
    ///   2) <c>area_revisores</c>: n revisores por área, por prioridad, partiendo del
    ///      nodo area_scope del trabajador (workers.area_scope_id) y subiendo por el
    ///      árbol hasta el primer nodo con revisores (los revisores se configuran solo
    ///      en el primer nodo "Área de Gerencia" y el primer "Área Estándar" de cada
    ///      rama, así que un trabajador cae en su área estándar y, si esa no tiene
    ///      revisores, en la gerencia de la que cuelga).
    ///   3) Fallback: el área de GTH (area_scope.email).
    ///
    /// Todo se resuelve por lotes: <see cref="ResolveManyAsync"/> hace un número FIJO de
    /// consultas sea para 1 o para 500 trabajadores, y <see cref="ResolveAsync"/> es un
    /// atajo sobre ella (una sola ruta de código, sin lógica duplicada).
    /// </summary>
    public class JefeRevisorResolver : IJefeRevisorResolver
    {
        private const string EmailDomainCorp = "@abril.pe";
        /// <summary>Nombre exacto del área en area_item cuyo area_scope.email es el fallback.</summary>
        private const string AreaGthNombre = "Gestión del Talento Humano";

        private readonly IDbContextFactory<AppDbContext> _factory;

        public JefeRevisorResolver(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<JefeRevisorResolution?> ResolveAsync(int workerId)
        {
            var resueltos = await ResolveManyAsync(new[] { workerId });
            return resueltos.TryGetValue(workerId, out var jefe) ? jefe : null;
        }

        public async Task<Dictionary<int, JefeRevisorResolution>> ResolveManyAsync(IReadOnlyCollection<int> workerIds)
        {
            var resultado = new Dictionary<int, JefeRevisorResolution>();

            var ids = workerIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();

            // ── Paso 1: revisor directo (workers_revisores) ────────────────────────
            var directos = await (
                from r in ctx.WorkersRevisores.AsNoTracking()
                where r.State && r.Active && ids.Contains(r.SolicitanteId)
                join w in ctx.Worker.AsNoTracking() on r.RevisorId equals w.Id
                where w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                select new
                {
                    r.SolicitanteId,
                    r.OrdenPrioridad,
                    r.WorkersRevisoresId,
                    RevisorWorkerId = w.Id,
                    w.EmailCorporativo,
                    Nombre = w.Person != null ? w.Person.FullName : null,
                }
            ).ToListAsync();

            foreach (var grupo in directos.GroupBy(d => d.SolicitanteId))
            {
                // El propio trabajador no puede ser su revisor.
                var elegido = grupo
                    .Where(d => d.RevisorWorkerId != grupo.Key)
                    .OrderBy(d => d.OrdenPrioridad)
                    .ThenBy(d => d.WorkersRevisoresId)
                    .FirstOrDefault();

                if (elegido != null)
                    resultado[grupo.Key] = new JefeRevisorResolution(
                        elegido.RevisorWorkerId, null, elegido.EmailCorporativo!.Trim(), elegido.Nombre);
            }

            var pendientes = ids.Where(id => !resultado.ContainsKey(id)).ToList();
            if (pendientes.Count == 0) return resultado;

            // ── Paso 2: revisores del área (area_revisores, subiendo por el árbol) ──
            await ResolveByAreaAsync(ctx, pendientes, resultado);

            pendientes = ids.Where(id => !resultado.ContainsKey(id)).ToList();
            if (pendientes.Count == 0) return resultado;

            // ── Paso 3: fallback al área de GTH ───────────────────────────────────
            var gth = await (
                from s in ctx.AreaScope.AsNoTracking()
                join ai in ctx.AreaItem.AsNoTracking() on s.AreaItemId equals ai.AreaItemId
                where s.State && ai.State
                      && ai.AreaItemName == AreaGthNombre
                      && s.Email != null && s.Email != ""
                orderby s.AreaScopeId
                select new { s.AreaScopeId, s.Email, ai.AreaItemName }
            ).FirstOrDefaultAsync();

            if (gth != null)
                foreach (var id in pendientes)
                    resultado[id] = new JefeRevisorResolution(
                        null, gth.AreaScopeId, gth.Email!.Trim(), gth.AreaItemName);

            return resultado;
        }

        /// <summary>
        /// Busca revisores de área para los trabajadores indicados: para cada uno arma la
        /// cadena de nodos desde su area_scope hacia la raíz y toma, del primer nodo con
        /// revisores válidos, el de mayor prioridad. El propio trabajador no puede ser su
        /// revisor. Escribe en <paramref name="resultado"/> solo los que resuelve.
        ///
        /// Si un nodo está marcado como "filtrar por proyecto" (ga_salidas_area_config),
        /// se usa el revisor del proyecto al que pertenece el trabajador
        /// (ga_salidas_workers_project → area_revisores.project_id); si ese nodo no tiene
        /// revisor para ese proyecto (o el trabajador no tiene proyecto), se cae al
        /// revisor a nivel de área del mismo nodo (project_id NULL). Los nodos no filtrados
        /// usan siempre el revisor a nivel de área. Todo se generaliza por configuración,
        /// sin reglas especiales por nombre de área.
        /// </summary>
        private static async Task ResolveByAreaAsync(
            AppDbContext ctx,
            List<int> workerIds,
            Dictionary<int, JefeRevisorResolution> resultado)
        {
            var areaScopePorWorker = await ctx.Worker.AsNoTracking()
                .Where(w => workerIds.Contains(w.Id) && w.AreaScopeId != null)
                .Select(w => new { w.Id, AreaScopeId = w.AreaScopeId!.Value })
                .ToDictionaryAsync(w => w.Id, w => w.AreaScopeId);
            if (areaScopePorWorker.Count == 0) return;

            // Árbol vivo (tabla pequeña) para armar las cadenas trabajador → raíz en memoria.
            var scopes = await ctx.AreaScope.AsNoTracking()
                .Where(s => s.State)
                .Select(s => new { s.AreaScopeId, s.AreaScopeParentId })
                .ToListAsync();
            var parentById = scopes.ToDictionary(s => s.AreaScopeId, s => s.AreaScopeParentId);

            var cadenaPorWorker = new Dictionary<int, List<int>>();
            foreach (var (workerId, areaScopeId) in areaScopePorWorker)
            {
                var cadena = new List<int>();
                var visitados = new HashSet<int>();
                int? actual = areaScopeId;
                while (actual != null && visitados.Add(actual.Value))
                {
                    cadena.Add(actual.Value);
                    parentById.TryGetValue(actual.Value, out actual);
                }
                if (cadena.Count > 0) cadenaPorWorker[workerId] = cadena;
            }
            if (cadenaPorWorker.Count == 0) return;

            var nodos = cadenaPorWorker.Values.SelectMany(c => c).Distinct().ToList();

            // Proyecto de cada trabajador (si pertenece a alguno) y nodos que filtran por proyecto.
            var proyectoPorWorker = await ctx.GaSalidasWorkersProject.AsNoTracking()
                .Where(wp => wp.State && workerIds.Contains(wp.WorkerId))
                .Select(wp => new { wp.WorkerId, wp.ProjectId })
                .ToListAsync();
            var proyectoDe = proyectoPorWorker
                .GroupBy(wp => wp.WorkerId)
                .ToDictionary(g => g.Key, g => (int?)g.First().ProjectId);

            var nodosFiltranProyecto = (await ctx.GaSalidasAreaConfig.AsNoTracking()
                .Where(f => f.State && f.FiltraPorProyecto && nodos.Contains(f.AreaScopeId))
                .Select(f => f.AreaScopeId)
                .ToListAsync()).ToHashSet();

            // Revisores vivos + activos con correo válido de cualquier nodo involucrado.
            var candidatos = await (
                from r in ctx.AreaRevisores.AsNoTracking()
                where r.State && r.Active && nodos.Contains(r.AreaScopeId)
                join w in ctx.Worker.AsNoTracking() on r.RevisorId equals w.Id
                where w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                select new
                {
                    r.AreaScopeId,
                    r.ProjectId,
                    r.OrdenPrioridad,
                    r.AreaRevisoresId,
                    RevisorWorkerId = w.Id,
                    w.EmailCorporativo,
                    Nombre = w.Person != null ? w.Person.FullName : null,
                }
            ).ToListAsync();
            if (candidatos.Count == 0) return;

            var porNodo = candidatos.ToLookup(c => c.AreaScopeId);

            foreach (var (workerId, cadena) in cadenaPorWorker)
            {
                proyectoDe.TryGetValue(workerId, out var proyectoTrabajador);

                // Conjunto efectivo de candidatos por nodo según el filtro por proyecto.
                var efectivos = cadena
                    .SelectMany(nodo =>
                    {
                        var delNodo = porNodo[nodo].Where(c => c.RevisorWorkerId != workerId).ToList();

                        // Nodo no filtrado: revisor a nivel de área (project_id NULL).
                        if (!nodosFiltranProyecto.Contains(nodo))
                            return delNodo.Where(c => c.ProjectId == null);

                        // Nodo filtrado: preferir revisor del proyecto del trabajador; si no hay, área (NULL).
                        var porProyecto = delNodo
                            .Where(c => proyectoTrabajador != null && c.ProjectId == proyectoTrabajador)
                            .ToList();
                        return porProyecto.Count > 0
                            ? porProyecto.AsEnumerable()
                            : delNodo.Where(c => c.ProjectId == null);
                    })
                    .ToList();

                var elegido = efectivos
                    .OrderBy(c => cadena.IndexOf(c.AreaScopeId)) // nodo más cercano al trabajador primero
                    .ThenBy(c => c.OrdenPrioridad)
                    .ThenBy(c => c.AreaRevisoresId)
                    .FirstOrDefault();

                if (elegido != null)
                    resultado[workerId] = new JefeRevisorResolution(
                        elegido.RevisorWorkerId, null, elegido.EmailCorporativo!.Trim(), elegido.Nombre);
            }
        }
    }
}
