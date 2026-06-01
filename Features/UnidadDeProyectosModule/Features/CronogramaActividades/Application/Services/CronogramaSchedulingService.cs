using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Services
{
    public class CronogramaSchedulingService : ICronogramaSchedulingService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CronogramaSchedulingService(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // ─────────────────────────── Días hábiles ───────────────────────────

        private static bool EsHabil(DateTime dia, HashSet<DateTime> feriados)
            => dia.DayOfWeek != DayOfWeek.Saturday
               && dia.DayOfWeek != DayOfWeek.Sunday
               && !feriados.Contains(dia.Date);

        public DateTime AddBusinessDays(DateTime start, int days, List<DateTime> feriados)
        {
            var set = feriados.Select(f => f.Date).ToHashSet();
            var current = start.Date;
            if (days <= 0) return current;
            int added = 0;
            while (added < days)
            {
                current = current.AddDays(1);
                if (EsHabil(current, set)) added++;
            }
            return current;
        }

        public DateTime NextBusinessDay(DateTime date, List<DateTime> feriados)
        {
            var set = feriados.Select(f => f.Date).ToHashSet();
            var current = date.Date.AddDays(1);
            while (!EsHabil(current, set)) current = current.AddDays(1);
            return current;
        }

        /// <summary>Días hábiles entre dos fechas, ambos extremos inclusive (mínimo 1).</summary>
        private static int DuracionHabilInclusive(DateOnly inicio, DateOnly fin, HashSet<DateTime> feriados)
        {
            var i = inicio.ToDateTime(TimeOnly.MinValue);
            var f = fin.ToDateTime(TimeOnly.MinValue);
            if (f < i) (i, f) = (f, i);
            int count = 0;
            for (var c = i; c <= f; c = c.AddDays(1))
                if (EsHabil(c, feriados)) count++;
            return Math.Max(1, count);
        }

        // ─────────────────────────── Detección de ciclos ───────────────────────────

        public async Task<bool> DetectCycleAsync(int proyectoId, int activityId, List<int> nuevasPredecesoras)
        {
            // Una actividad no puede ser su propia predecesora
            if (nuevasPredecesoras.Contains(activityId)) return true;

            using var ctx = _factory.CreateDbContext();
            var actividadIds = await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State && a.Active)
                .Select(a => a.ProjectActivityId)
                .ToListAsync();
            var idSet = actividadIds.ToHashSet();

            var relaciones = await ctx.ActivityPredecessors
                .Where(r => idSet.Contains(r.ActivityId))
                .Select(r => new { r.ActivityId, r.PredecessorId })
                .ToListAsync();

            // Grafo predecesora → sucesora, reemplazando las predecesoras de activityId
            var sucesores = new Dictionary<int, List<int>>();
            void AddEdge(int pred, int succ)
            {
                if (!sucesores.TryGetValue(pred, out var list))
                    sucesores[pred] = list = new List<int>();
                list.Add(succ);
            }

            foreach (var r in relaciones)
            {
                if (r.ActivityId == activityId) continue; // se reemplazan
                AddEdge(r.PredecessorId, r.ActivityId);
            }
            foreach (var pred in nuevasPredecesoras.Distinct())
                AddEdge(pred, activityId);

            return TieneCiclo(sucesores);
        }

        /// <summary>Detección de ciclo por DFS con colores (blanco/gris/negro).</summary>
        private static bool TieneCiclo(Dictionary<int, List<int>> sucesores)
        {
            var estado = new Dictionary<int, int>(); // 0=sin visitar, 1=en pila, 2=terminado

            bool Dfs(int nodo)
            {
                estado[nodo] = 1;
                if (sucesores.TryGetValue(nodo, out var hijos))
                {
                    foreach (var h in hijos)
                    {
                        var e = estado.GetValueOrDefault(h, 0);
                        if (e == 1) return true;           // arista hacia nodo en pila → ciclo
                        if (e == 0 && Dfs(h)) return true;
                    }
                }
                estado[nodo] = 2;
                return false;
            }

            foreach (var nodo in sucesores.Keys)
                if (estado.GetValueOrDefault(nodo, 0) == 0 && Dfs(nodo))
                    return true;
            return false;
        }

        // ─────────────────────────── Cascada FS ───────────────────────────

        public async Task<CascadaResultDto> RecalcularCascadaAsync(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            var (cambios, _) = await CalcularCascadaAsync(ctx, proyectoId);
            return new CascadaResultDto { HayCambios = cambios.Count > 0, Cambios = cambios };
        }

        public async Task<CascadaResultDto> AplicarCascadaAsync(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            var (cambios, actividades) = await CalcularCascadaAsync(ctx, proyectoId);

            foreach (var c in cambios)
            {
                var act = actividades[c.ProjectActivityId];
                act.PlannedStartDate = c.InicioNuevo;
                act.PlannedEndDate = c.FinNuevo;
                act.UpdatedDateTime = DateTime.UtcNow;
            }
            await ctx.SaveChangesAsync();

            // Tras mover las hojas, los nodos padre deben reflejar min/max de sus hijos
            await RecalcularFechasPadresInternoAsync(ctx, proyectoId);

            return new CascadaResultDto { HayCambios = cambios.Count > 0, Cambios = cambios };
        }

        /// <summary>
        /// Núcleo del cálculo de cascada. Devuelve los cambios y el diccionario de
        /// entidades (rastreadas por <paramref name="ctx"/>) para permitir persistir.
        /// </summary>
        private async Task<(List<CascadaCambioDto> cambios, Dictionary<int, ProjectActivity> actividades)>
            CalcularCascadaAsync(AppDbContext ctx, int proyectoId)
        {
            var feriados = await ctx.Feriados
                .Select(f => f.Fecha.ToDateTime(TimeOnly.MinValue))
                .ToListAsync();
            var feriadoSet = feriados.Select(f => f.Date).ToHashSet();

            var lista = await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State && a.Active)
                .ToListAsync();
            var actividades = lista.ToDictionary(a => a.ProjectActivityId);
            var idSet = actividades.Keys.ToHashSet();

            var relaciones = await ctx.ActivityPredecessors
                .Where(r => idSet.Contains(r.ActivityId))
                .Select(r => new { r.ActivityId, r.PredecessorId })
                .ToListAsync();

            // Predecesoras por sucesora + adyacencia sucesora (para Kahn) + grado de entrada
            var predecesorasDe = new Dictionary<int, List<int>>();
            var sucesoresDe = new Dictionary<int, List<int>>();
            var inDegree = new Dictionary<int, int>();
            foreach (var id in idSet) inDegree[id] = 0;

            foreach (var r in relaciones)
            {
                if (!idSet.Contains(r.PredecessorId)) continue;
                if (!predecesorasDe.TryGetValue(r.ActivityId, out var ps))
                    predecesorasDe[r.ActivityId] = ps = new List<int>();
                ps.Add(r.PredecessorId);

                if (!sucesoresDe.TryGetValue(r.PredecessorId, out var ss))
                    sucesoresDe[r.PredecessorId] = ss = new List<int>();
                ss.Add(r.ActivityId);

                inDegree[r.ActivityId]++;
            }

            // Duración hábil original y fin "vigente" (se va actualizando en cascada)
            var duracion = new Dictionary<int, int>();
            var finVigente = new Dictionary<int, DateTime?>();
            foreach (var a in lista)
            {
                if (a.PlannedStartDate.HasValue && a.PlannedEndDate.HasValue)
                    duracion[a.ProjectActivityId] = DuracionHabilInclusive(
                        a.PlannedStartDate.Value, a.PlannedEndDate.Value, feriadoSet);
                else
                    duracion[a.ProjectActivityId] = 1;

                finVigente[a.ProjectActivityId] = a.PlannedEndDate.HasValue
                    ? a.PlannedEndDate.Value.ToDateTime(TimeOnly.MinValue)
                    : null;
            }

            var cambios = new List<CascadaCambioDto>();

            // Orden topológico (Kahn)
            var queue = new Queue<int>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();

                // Solo recalculan las actividades que tienen predecesoras
                if (predecesorasDe.TryGetValue(id, out var preds) && preds.Count > 0)
                {
                    var finesPred = preds
                        .Select(p => finVigente.GetValueOrDefault(p))
                        .Where(f => f.HasValue)
                        .Select(f => f!.Value)
                        .ToList();

                    if (finesPred.Count > 0)
                    {
                        var maxFin = finesPred.Max();
                        var nuevoInicio = NextBusinessDay(maxFin, feriados);
                        var nuevoFin = AddBusinessDays(nuevoInicio, duracion[id] - 1, feriados);

                        var inicioNuevoDo = DateOnly.FromDateTime(nuevoInicio);
                        var finNuevoDo = DateOnly.FromDateTime(nuevoFin);

                        var act = actividades[id];
                        if (act.PlannedStartDate != inicioNuevoDo || act.PlannedEndDate != finNuevoDo)
                        {
                            cambios.Add(new CascadaCambioDto
                            {
                                ProjectActivityId = id,
                                ActivityDescription = act.ActivityDescription,
                                InicioAnterior = act.PlannedStartDate,
                                InicioNuevo = inicioNuevoDo,
                                FinAnterior = act.PlannedEndDate,
                                FinNuevo = finNuevoDo
                            });
                        }
                        finVigente[id] = nuevoFin;
                    }
                }

                if (sucesoresDe.TryGetValue(id, out var succs))
                {
                    foreach (var s in succs)
                    {
                        inDegree[s]--;
                        if (inDegree[s] == 0) queue.Enqueue(s);
                    }
                }
            }

            return (cambios, actividades);
        }

        // ─────────────────────────── Fechas de nodos padre ───────────────────────────

        public async Task RecalcularFechasPadresAsync(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            await RecalcularFechasPadresInternoAsync(ctx, proyectoId);
        }

        private static async Task RecalcularFechasPadresInternoAsync(AppDbContext ctx, int proyectoId)
        {
            var todas = await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State && a.Active)
                .ToListAsync();

            var hijosDe = todas
                .Where(a => a.ParentId.HasValue)
                .GroupBy(a => a.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Post-orden con memoización: calcula los hijos antes que el padre, a cualquier nivel.
            // 0 = sin visitar, 1 = en proceso (guarda anti-ciclo), 2 = terminado.
            var estado = new Dictionary<int, int>();

            void Procesar(ProjectActivity nodo)
            {
                var st = estado.GetValueOrDefault(nodo.ProjectActivityId, 0);
                if (st != 0) return; // ya terminado, o en proceso (ciclo) → no reentrar
                estado[nodo.ProjectActivityId] = 1;

                if (hijosDe.TryGetValue(nodo.ProjectActivityId, out var hijos) && hijos.Count > 0)
                {
                    foreach (var h in hijos) Procesar(h);

                    var inicios = hijos.Where(h => h.PlannedStartDate.HasValue)
                                       .Select(h => h.PlannedStartDate!.Value).ToList();
                    var fines = hijos.Where(h => h.PlannedEndDate.HasValue)
                                     .Select(h => h.PlannedEndDate!.Value).ToList();

                    var nuevoInicio = inicios.Count > 0 ? inicios.Min() : (DateOnly?)null;
                    var nuevoFin = fines.Count > 0 ? fines.Max() : (DateOnly?)null;

                    if (nodo.PlannedStartDate != nuevoInicio || nodo.PlannedEndDate != nuevoFin)
                    {
                        nodo.PlannedStartDate = nuevoInicio;
                        nodo.PlannedEndDate = nuevoFin;
                        nodo.UpdatedDateTime = DateTime.UtcNow;
                    }
                }
                // las hojas conservan sus fechas

                estado[nodo.ProjectActivityId] = 2;
            }

            // Procesa TODOS los nodos (no solo raíces), para cubrir subárboles huérfanos.
            // La recursión a hijos + memoización garantizan el orden bottom-up correcto.
            foreach (var nodo in todas)
                Procesar(nodo);

            await ctx.SaveChangesAsync();
        }
    }
}
