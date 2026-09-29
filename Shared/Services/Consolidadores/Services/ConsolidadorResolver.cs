using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Actores.Interfaces;
using Abril_Backend.Shared.Services.Consolidadores.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services.Consolidadores.Services
{
    /// <summary>
    /// Fachada de <see cref="IActoresResolver"/> sobre el actor <c>ActorIds.Consolidador</c>. Ver
    /// <see cref="IConsolidadorResolver"/>. La regla vive en el núcleo de los actores, igual que la de
    /// los aprobadores: acá solo se traduce y se cruza con las fichas del usuario.
    /// </summary>
    public class ConsolidadorResolver : IConsolidadorResolver
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IActoresResolver _actores;

        public ConsolidadorResolver(IDbContextFactory<AppDbContext> factory, IActoresResolver actores)
        {
            _factory = factory;
            _actores = actores;
        }

        public async Task<Dictionary<int, List<ConsolidadorElegido>>> ResolveManyAsync(
            IReadOnlyCollection<int> workerIds)
        {
            var resultado = new Dictionary<int, List<ConsolidadorElegido>>();

            var resueltos = await _actores.ResolverTrabajadoresAsync(workerIds, new[] { ActorIds.Consolidador });
            foreach (var (workerId, actores) in resueltos)
            {
                var r = actores.Actores.GetValueOrDefault(ActorIds.Consolidador);
                resultado[workerId] = r == null
                    ? new List<ConsolidadorElegido>()
                    : r.Personas
                        .Where(p => p.WorkerId != null)
                        .Select(p => new ConsolidadorElegido(
                            p.WorkerId!.Value, p.PersonId, p.Email, p.Nombre,
                            r.Origen is ActorOrigen.Trabajador or ActorOrigen.Area
                                ? ConsolidadorOrigen.Personalizado
                                : ConsolidadorOrigen.Algoritmo))
                        .ToList();
            }

            return resultado;
        }

        public async Task<HashSet<int>> FiltrarQuePuedeConsolidarAsync(int userId, IReadOnlyCollection<int> workerIds)
        {
            var porUsuario = await FiltrarQuePuedenConsolidarAsync(new[] { userId }, workerIds);
            return porUsuario.GetValueOrDefault(userId) ?? new HashSet<int>();
        }

        public async Task<Dictionary<int, HashSet<int>>> FiltrarQuePuedenConsolidarAsync(
            IReadOnlyCollection<int> userIds, IReadOnlyCollection<int> workerIds)
        {
            var usuarios = userIds.Where(id => id > 0).Distinct().ToList();
            var resultado = usuarios.ToDictionary(u => u, _ => new HashSet<int>());

            var ids = workerIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0 || usuarios.Count == 0) return resultado;

            // Las fichas de cada usuario: un user puede mapear a más de un worker (reingresos).
            List<(int UserId, int WorkerId, int? PersonId)> fichas;
            using (var ctx = _factory.CreateDbContext())
            {
                fichas = (await ctx.Worker.AsNoTracking()
                        .Where(w => w.Person != null && w.Person.UserId != null && usuarios.Contains(w.Person.UserId.Value))
                        .Select(w => new { UserId = w.Person!.UserId!.Value, w.Id, w.PersonId })
                        .ToListAsync())
                    .Select(w => (w.UserId, w.Id, w.PersonId))
                    .ToList();
            }
            if (fichas.Count == 0) return resultado;

            var consolidadores = await ResolveManyAsync(ids);

            foreach (var porUsuario in fichas.GroupBy(f => f.UserId))
            {
                var misWorkers  = porUsuario.Select(f => f.WorkerId).ToHashSet();
                var misPersonas = porUsuario.Where(f => f.PersonId != null).Select(f => f.PersonId!.Value).ToHashSet();

                foreach (var (workerId, lista) in consolidadores)
                    if (lista.Any(c => misWorkers.Contains(c.WorkerId)
                                    || (c.PersonId != null && misPersonas.Contains(c.PersonId.Value))))
                        resultado[porUsuario.Key].Add(workerId);
            }

            return resultado;
        }
    }
}
