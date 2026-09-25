using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.DelegacionRevision.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.DelegacionRevision.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.DelegacionRevision.Infrastructure.Repositories
{
    /// <summary>
    /// "Delegación de Revisión" (funcionalidad principal, usuario final). Deja que un revisor
    /// autogestione la lista de aprobadores de la salida de las áreas/proyectos donde figura
    /// asignado a mano en Revisores de Áreas: designar suplentes de su área (delegar), y
    /// activarse/desactivarse para tomar/soltar el puesto. Trabaja sobre las filas del actor
    /// "aprobador de la salida" de area_actor_asignacion (las mismas que edita Configuración →
    /// Revisores de Áreas), una asignación por área, obra y tipo de trabajador, con alcance y
    /// autorización acotados al propio usuario.
    ///
    /// El acceso a la funcionalidad se controla por el rol ADMINISTRADOR DE SOLICITUD DE SALIDAS;
    /// una vez dentro, cada quien solo administra las asignaciones en las que ya es revisor.
    /// </summary>
    public class DelegacionRevisionRepository : IDelegacionRevisionRepository
    {
        private const string EmailDomainCorp = "@abril.pe";

        private readonly IDbContextFactory<AppDbContext> _factory;

        public DelegacionRevisionRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<DelegacionInicialDto> GetInitialDataAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var currentWorkerId = await GetCurrentWorkerIdAsync(ctx, userId);
            if (currentWorkerId == 0)
                return new DelegacionInicialDto { CurrentWorkerId = 0 };

            // Asignaciones (área[, proyecto], caso) donde el usuario aprueba salidas (cualquier active).
            var asignacionesKeys = await ctx.AreaActorAsignacion
                .Where(r => r.State && r.GaActorId == ActorIds.AprobadorSalida && r.WorkerId == currentWorkerId)
                .Select(r => new { r.AreaScopeId, r.ProjectId, CasoId = r.GaActorCasoId })
                .Distinct()
                .ToListAsync();

            if (asignacionesKeys.Count == 0)
                return new DelegacionInicialDto { CurrentWorkerId = currentWorkerId };

            // Árbol de áreas vivo para nombres, padres y subárboles.
            var nodos = await (
                from s in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                where s.State && ai.State
                select new { s.AreaScopeId, s.AreaScopeParentId, ai.AreaItemName }
            ).ToListAsync();
            var nombreById = nodos.ToDictionary(n => n.AreaScopeId, n => n.AreaItemName);
            var parentById = nodos.ToDictionary(n => n.AreaScopeId, n => n.AreaScopeParentId);
            var childrenByParent = nodos
                .Where(n => n.AreaScopeParentId != null)
                .GroupBy(n => n.AreaScopeParentId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.AreaScopeId).ToList());

            var areaIds = asignacionesKeys.Select(a => a.AreaScopeId).Distinct().ToList();
            var projectIds = asignacionesKeys.Where(a => a.ProjectId != null).Select(a => a.ProjectId!.Value).Distinct().ToList();

            // Aprobadores vivos de las áreas involucradas (una query), con datos de cada uno.
            var revisores = await (
                from r in ctx.AreaActorAsignacion
                where r.State && r.GaActorId == ActorIds.AprobadorSalida && areaIds.Contains(r.AreaScopeId)
                join w in ctx.Worker on r.WorkerId equals w.Id
                join p in ctx.Person on w.PersonId equals p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                join pu in ctx.Puesto on w.PuestoId equals pu.PuestoId into puj
                from pu in puj.DefaultIfEmpty()
                join c in ctx.Categoria on pu.CategoriaId equals c.CategoriaId into cj
                from c in cj.DefaultIfEmpty()
                orderby r.OrdenPrioridad, r.AreaActorAsignacionId
                select new
                {
                    r.AreaScopeId,
                    r.ProjectId,
                    CasoId = r.GaActorCasoId,
                    Dto = new DelegacionRevisorAsignadoDto
                    {
                        Id = r.AreaActorAsignacionId,
                        RevisorWorkerId = r.WorkerId,
                        RevisorFullName = p != null ? p.FullName : null,
                        RevisorEmail = w.EmailCorporativo,
                        RevisorCategory = c != null ? c.Nombre : null,
                        OrdenPrioridad = r.OrdenPrioridad,
                        Active = r.Active,
                    }
                }
            ).ToListAsync();

            // Nombres de proyectos involucrados.
            var projectNames = projectIds.Count == 0
                ? new Dictionary<int, string>()
                : await ctx.Project
                    .Where(p => projectIds.Contains(p.ProjectId))
                    .ToDictionaryAsync(p => p.ProjectId, p => p.ProjectDescription);

            var casos = await ctx.GaActorCaso.AsNoTracking()
                .ToDictionaryAsync(c => c.GaActorCasoId, c => c.Nombre);

            var asignaciones = new List<DelegacionAsignacionItemDto>();
            foreach (var key in asignacionesKeys)
            {
                var item = new DelegacionAsignacionItemDto
                {
                    AreaScopeId = key.AreaScopeId,
                    AreaName = nombreById.TryGetValue(key.AreaScopeId, out var an) ? an : string.Empty,
                    ParentName = parentById.TryGetValue(key.AreaScopeId, out var pid) && pid != null && nombreById.TryGetValue(pid.Value, out var pn)
                        ? pn : null,
                    ProjectId = key.ProjectId,
                    ProjectName = key.ProjectId != null && projectNames.TryGetValue(key.ProjectId.Value, out var prn) ? prn : null,
                    CasoId = key.CasoId,
                    CasoNombre = casos.TryGetValue(key.CasoId, out var cn) ? cn : string.Empty,
                    Revisores = revisores
                        .Where(r => r.AreaScopeId == key.AreaScopeId && r.ProjectId == key.ProjectId && r.CasoId == key.CasoId)
                        .Select(r => r.Dto)
                        .ToList(),
                    Options = await GetOptionsAsync(ctx, key.AreaScopeId, key.ProjectId, childrenByParent),
                };
                asignaciones.Add(item);
            }

            return new DelegacionInicialDto
            {
                CurrentWorkerId = currentWorkerId,
                Asignaciones = asignaciones
                    .OrderBy(a => a.AreaName)
                    .ThenBy(a => a.ProjectName)
                    .ThenBy(a => a.CasoId)
                    .ToList(),
            };
        }

        public async Task UpdateAsync(int userId, int areaScopeId, int? projectId, int casoId, List<DelegacionAsignacionDto> revisores)
        {
            using var ctx = _factory.CreateDbContext();

            var currentWorkerId = await GetCurrentWorkerIdAsync(ctx, userId);
            if (currentWorkerId == 0)
                throw new AbrilException("No se encontró el trabajador del usuario.", 403);

            // El usuario debe ser aprobador vivo de esa asignación (área o área+proyecto, y caso).
            var esRevisor = await ctx.AreaActorAsignacion.AnyAsync(r =>
                r.State && r.GaActorId == ActorIds.AprobadorSalida && r.WorkerId == currentWorkerId
                && r.AreaScopeId == areaScopeId && r.ProjectId == projectId && r.GaActorCasoId == casoId);
            if (!esRevisor)
                throw new AbrilException("No tienes permiso para administrar los revisores de esta área.", 403);

            var deseados = revisores ?? new List<DelegacionAsignacionDto>();

            // ── Validaciones base (mismo criterio que Revisores de Áreas) ────
            if (deseados.GroupBy(r => r.RevisorWorkerId).Any(g => g.Count() > 1))
                throw new AbrilException("No se puede asignar dos veces al mismo revisor.", 400);
            if (deseados.Any(r => r.OrdenPrioridad < 1))
                throw new AbrilException("La prioridad debe ser 1 o mayor.", 400);
            if (deseados.GroupBy(r => r.OrdenPrioridad).Any(g => g.Count() > 1))
                throw new AbrilException("No puede haber dos revisores con la misma prioridad.", 400);

            // No puedes quitarte a ti mismo: solo puedes desactivarte (para no perder el acceso
            // y poder retomar el puesto cuando quieras).
            if (!deseados.Any(d => d.RevisorWorkerId == currentWorkerId))
                throw new AbrilException("No puedes quitarte como revisor; solo puedes desactivarte.", 400);

            if (deseados.Count > 0)
            {
                var ids = deseados.Select(r => r.RevisorWorkerId).ToList();
                var validos = await ctx.Worker
                    .Where(w => ids.Contains(w.Id)
                                && w.EmailCorporativo != null
                                && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp))
                    .Select(w => w.Id)
                    .ToListAsync();
                var faltantes = ids.Except(validos).ToList();
                if (faltantes.Count > 0)
                    throw new AbrilException("Uno o más revisores no existen o no tienen correo corporativo @abril.pe.", 400);

                // Los designados (excepto uno mismo) deben pertenecer al área/proyecto.
                var nodos = await (
                    from s in ctx.AreaScope
                    where s.State
                    select new { s.AreaScopeId, s.AreaScopeParentId }
                ).ToListAsync();
                var childrenByParent = nodos
                    .Where(n => n.AreaScopeParentId != null)
                    .GroupBy(n => n.AreaScopeParentId!.Value)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.AreaScopeId).ToList());

                var designables = (await GetOptionsAsync(ctx, areaScopeId, projectId, childrenByParent))
                    .Select(o => o.WorkerId)
                    .ToHashSet();
                var fueraDeArea = ids
                    .Where(id => id != currentWorkerId && !designables.Contains(id))
                    .ToList();
                if (fueraDeArea.Count > 0)
                    throw new AbrilException("Solo puedes designar revisores que pertenezcan a tu área.", 400);
            }

            // ── Diff con las filas vivas del mismo alcance (área o área+proyecto, y caso) ─
            var now = DateTimeOffset.UtcNow;
            var vivos = await ctx.AreaActorAsignacion
                .Where(r => r.State && r.GaActorId == ActorIds.AprobadorSalida
                         && r.AreaScopeId == areaScopeId && r.ProjectId == projectId
                         && r.GaActorCasoId == casoId)
                .ToListAsync();
            var vivosByRevisor = vivos.ToDictionary(r => r.WorkerId);

            foreach (var d in deseados)
            {
                if (vivosByRevisor.TryGetValue(d.RevisorWorkerId, out var row))
                {
                    if (row.OrdenPrioridad != d.OrdenPrioridad || row.Active != d.Active)
                    {
                        row.OrdenPrioridad = d.OrdenPrioridad;
                        row.Active = d.Active;
                        row.UpdatedAt = now;
                    }
                }
                else
                {
                    ctx.AreaActorAsignacion.Add(new AreaActorAsignacion
                    {
                        AreaScopeId = areaScopeId,
                        ProjectId = projectId,
                        GaActorId = ActorIds.AprobadorSalida,
                        GaActorCasoId = casoId,
                        WorkerId = d.RevisorWorkerId,
                        OrdenPrioridad = d.OrdenPrioridad,
                        Active = d.Active,
                        State = true,
                        CreatedAt = now,
                    });
                }
            }

            var deseadosIds = deseados.Select(d => d.RevisorWorkerId).ToHashSet();
            foreach (var row in vivos)
            {
                if (!deseadosIds.Contains(row.WorkerId))
                {
                    row.State = false;
                    row.UpdatedAt = now;
                }
            }

            await ctx.SaveChangesAsync();
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static async Task<int> GetCurrentWorkerIdAsync(AppDbContext ctx, int userId)
        {
            return await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .Select(w => w.Id)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Trabajadores designables de una asignación: para una asignación por proyecto son los
        /// trabajadores cuya vinculación vigente (worker_vinculaciones con fecha_fin NULL) apunta a
        /// ese proyecto — la misma obra que mantiene GTH y con la que JefeRevisorResolver elige al
        /// revisor por proyecto; para una asignación de área son los trabajadores cuya área (la de
        /// destino de su puesto) cae en el subárbol del nodo.
        /// En ambos casos con correo @abril.pe y ordenados por nombre.
        /// </summary>
        private static async Task<List<DelegacionOptionDto>> GetOptionsAsync(
            AppDbContext ctx, int areaScopeId, int? projectId, Dictionary<int, List<int>> childrenByParent)
        {
            if (projectId != null)
            {
                // EXISTS y no un join contra las vinculaciones: si una ficha llegara a tener más de
                // una vinculación abierta a la misma obra, el join la repetiría en el desplegable.
                return await (
                    from w in ctx.Worker
                    where w.EmailCorporativo != null && w.EmailCorporativo.ToLower().Contains(EmailDomainCorp)
                          && ctx.WorkerVinculacion.Any(v =>
                                 v.WorkerId == w.Id && v.FechaFin == null && v.ProyectoId == projectId.Value)
                    join p in ctx.Person on w.PersonId equals p.PersonId
                    where p.State == true
                    orderby p.FullName
                    select new DelegacionOptionDto { WorkerId = w.Id, FullName = p.FullName, Email = w.EmailCorporativo }
                ).ToListAsync();
            }

            var subarbol = DescendantsInclusive(areaScopeId, childrenByParent);
            return await (
                from w in ctx.Worker
                where w.PuestoCatalogo!.AreaDestinoScopeId != null
                      && subarbol.Contains(w.PuestoCatalogo.AreaDestinoScopeId!.Value)
                      && w.EmailCorporativo != null && w.EmailCorporativo.ToLower().Contains(EmailDomainCorp)
                join p in ctx.Person on w.PersonId equals p.PersonId
                where p.State == true
                orderby p.FullName
                select new DelegacionOptionDto { WorkerId = w.Id, FullName = p.FullName, Email = w.EmailCorporativo }
            ).ToListAsync();
        }

        /// <summary>Nodo + todos sus descendientes vivos (BFS por el árbol area_scope).</summary>
        private static HashSet<int> DescendantsInclusive(int rootId, Dictionary<int, List<int>> childrenByParent)
        {
            var result = new HashSet<int> { rootId };
            var queue = new Queue<int>();
            queue.Enqueue(rootId);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (!childrenByParent.TryGetValue(cur, out var hijos)) continue;
                foreach (var h in hijos)
                    if (result.Add(h)) queue.Enqueue(h);
            }
            return result;
        }
    }
}
