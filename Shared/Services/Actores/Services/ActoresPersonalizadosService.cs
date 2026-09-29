using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Models;
using Abril_Backend.Shared.Services.Actores.Interfaces;
using Abril_Backend.Shared.Services.Jerarquia;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services.Actores.Services
{
    /// <summary>
    /// Implementación de <see cref="IActoresPersonalizadosService"/> sobre
    /// <c>workers_actor_asignacion</c>. Ver la interfaz.
    /// </summary>
    public class ActoresPersonalizadosService : IActoresPersonalizadosService
    {
        private const string EmailDomainCorp = EstructuraAreaLoader.EmailDomainCorp;

        private readonly IDbContextFactory<AppDbContext> _factory;

        public ActoresPersonalizadosService(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task SetAsync(int workerId, IReadOnlyDictionary<int, IReadOnlyList<int>> porActor)
        {
            if (workerId <= 0) return;

            // Lo que se pidió, limpio: solo actores conocidos, sin repetir persona y en el orden en que
            // vinieron. Un actor de uno solo no admite una lista.
            var deseado = new Dictionary<int, List<int>>();
            foreach (var (actorId, workerIds) in porActor ?? new Dictionary<int, IReadOnlyList<int>>())
            {
                if (!ActorIds.Todos.Contains(actorId))
                    throw new AbrilException("Uno de los actores personalizados no existe.", 400);

                var lista = (workerIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToList();
                if (lista.Count == 0) continue;

                if (!ActorIds.EsMultiple(actorId) && lista.Count > 1)
                    throw new AbrilException("Ese actor admite una sola persona.", 400);

                deseado[actorId] = lista;
            }

            using var ctx = _factory.CreateDbContext();

            // Quien se elija tiene que poder recibir el correo que ese papel implica.
            var elegidos = deseado.Values.SelectMany(l => l).Distinct().ToList();
            if (elegidos.Count > 0)
            {
                var validos = await ctx.Worker.AsNoTracking()
                    .Where(w => elegidos.Contains(w.Id)
                                && w.EmailCorporativo != null
                                && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp))
                    .Select(w => w.Id)
                    .ToListAsync();

                if (elegidos.Except(validos).Any())
                    throw new AbrilException(
                        $"Una o más personas elegidas no existen o no tienen correo corporativo {EmailDomainCorp}.", 400);
            }

            var now = DateTimeOffset.UtcNow;
            var vivas = await ctx.WorkersActorAsignacion
                .Where(r => r.State && r.WorkerId == workerId)
                .ToListAsync();

            foreach (var actorId in ActorIds.Todos)
            {
                var delActor = vivas.Where(r => r.GaActorId == actorId).ToList();
                var lista = deseado.TryGetValue(actorId, out var l) ? l : new List<int>();

                // Las que ya no van: baja lógica, quedan como historial.
                foreach (var r in delActor.Where(r => !lista.Contains(r.AsignadoId)))
                {
                    r.State = false;
                    r.UpdatedAt = now;
                }

                // Las que van, en su lugar: la posición en la lista es la prioridad.
                for (var i = 0; i < lista.Count; i++)
                {
                    var orden = i + 1;
                    var fila = delActor.FirstOrDefault(r => r.AsignadoId == lista[i]);
                    if (fila == null)
                    {
                        ctx.WorkersActorAsignacion.Add(new WorkersActorAsignacion
                        {
                            WorkerId       = workerId,
                            GaActorId      = actorId,
                            AsignadoId     = lista[i],
                            OrdenPrioridad = orden,
                            Active         = true,
                            State          = true,
                            CreatedAt      = now,
                        });
                    }
                    else if (fila.OrdenPrioridad != orden || !fila.Active)
                    {
                        fila.OrdenPrioridad = orden;
                        fila.Active = true;
                        fila.UpdatedAt = now;
                    }
                }
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<List<ActorCandidatoDto>> GetCandidatosAsync()
        {
            using var ctx = _factory.CreateDbContext();

            return await (
                from w in ctx.Worker.AsNoTracking()
                where w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                join p in ctx.Person.AsNoTracking() on w.PersonId equals p.PersonId
                where p.State == true
                orderby p.FullName
                select new ActorCandidatoDto
                {
                    WorkerId = w.Id,
                    PersonId = w.PersonId,
                    FullName = p.FullName,
                    Email    = w.EmailCorporativo,
                }
            ).ToListAsync();
        }
    }
}
