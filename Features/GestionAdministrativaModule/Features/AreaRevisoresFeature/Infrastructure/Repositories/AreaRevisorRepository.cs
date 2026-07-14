using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using AreaRevisoresModel = Abril_Backend.Features.GestionAdministrativa.Shared.Models.AreaRevisores;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Infrastructure.Repositories
{
    /// <summary>
    /// Lectura/escritura de los revisores de salidas por área (area_revisores):
    /// n revisores por nodo area_scope, ordenados por prioridad (1 = primero).
    /// Solo se configuran las áreas de tipo "Área Estándar" que son el primer nodo
    /// estándar de su rama (si un Área Estándar cuelga de otro Área Estándar, el hijo
    /// no se lista, para no confundir a los usuarios con subáreas). Estos revisores
    /// aplican a los trabajadores del subárbol del nodo que no tengan revisores
    /// propios en workers_revisores; sin revisores de área, el fallback es GTH.
    ///
    /// Visibilidad: el rol ADMINISTRADOR DE SOLICITUD DE SALIDAS ve todas las áreas;
    /// un trabajador con categoría (workers_category) Jefe, Coordinador o Gerente ve
    /// solo el área estándar a la que pertenece (subiendo el árbol desde su
    /// workers.area_scope_id); el resto no ve ninguna.
    /// </summary>
    public class AreaRevisorRepository : IAreaRevisorRepository
    {
        private const string EmailDomainCorp = "@abril.pe";
        private const string AreaTypeEstandar = "Área Estándar";

        /// <summary>Categorías de workers_category cuyo trabajador puede ver su propia área.</summary>
        private static readonly string[] CategoriasConVista = { "jefe", "coordinador", "gerente" };

        private readonly IDbContextFactory<AppDbContext> _factory;

        public AreaRevisorRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<AreaRevisorInicialDto> GetInitialDataAsync(int userId, bool verTodas)
        {
            // Tabla + opciones en una sola conexión.
            using var ctx = _factory.CreateDbContext();

            // 1) Árbol completo de áreas vivas (lista plana) para resolver en memoria
            //    qué nodos son el primer "Área Estándar" de su rama.
            var nodos = await LoadNodosAsync(ctx);
            var elegibles = FirstStandardNodes(nodos);

            if (!verTodas)
            {
                // Jefe/Coordinador/Gerente: solo el área estándar a la que pertenece
                // su worker. Cualquier otro usuario: ninguna.
                var areaVisible = await GetAreaVisibleDelUsuarioAsync(ctx, userId, nodos, elegibles);
                if (areaVisible == null)
                    return new AreaRevisorInicialDto();
                elegibles = elegibles.Where(n => n.AreaScopeId == areaVisible.Value).ToList();
            }

            var areas = elegibles
                .OrderBy(n => n.AreaItemName)
                .Select(n => new AreaRevisorItemDto
                {
                    AreaScopeId = n.AreaScopeId,
                    AreaName = n.AreaItemName,
                    ParentName = n.AreaScopeParentId != null
                        ? nodos.FirstOrDefault(p => p.AreaScopeId == n.AreaScopeParentId)?.AreaItemName
                        : null,
                })
                .ToList();

            // 2) Revisores vivos de las áreas listadas, con los datos del revisor resueltos (una sola query).
            var areaIds = areas.Select(a => a.AreaScopeId).ToList();
            var asignaciones = await (
                from r in ctx.AreaRevisores
                where r.State && areaIds.Contains(r.AreaScopeId)
                join w in ctx.Worker on r.RevisorId equals w.Id
                join p in ctx.Person on w.PersonId equals p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId into cj
                from c in cj.DefaultIfEmpty()
                orderby r.AreaScopeId, r.OrdenPrioridad, r.AreaRevisoresId
                select new
                {
                    r.AreaScopeId,
                    Dto = new AreaRevisorAsignadoDto
                    {
                        Id = r.AreaRevisoresId,
                        RevisorWorkerId = r.RevisorId,
                        RevisorFullName = p != null ? p.FullName : null,
                        RevisorEmail = w.EmailCorporativo,
                        RevisorCategory = c != null ? c.Name : null,
                        OrdenPrioridad = r.OrdenPrioridad,
                        Active = r.Active,
                    }
                }
            ).ToListAsync();

            var porArea = asignaciones
                .GroupBy(a => a.AreaScopeId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Dto).ToList());

            foreach (var a in areas)
                if (porArea.TryGetValue(a.AreaScopeId, out var revs)) a.Revisores = revs;

            // 3) Opciones del selector (mismo criterio que Revisores de Trabajadores).
            //    Solo el administrador puede editar revisores, así que solo él las necesita.
            var options = !verTodas
                ? new List<AreaRevisorOptionDto>()
                : await (
                    from w in ctx.Worker
                    where w.EmailCorporativo != null && w.EmailCorporativo.ToLower().Contains(EmailDomainCorp)
                    join p in ctx.Person on w.PersonId equals p.PersonId
                    where p.State == true
                    orderby p.FullName
                    select new AreaRevisorOptionDto
                    {
                        WorkerId = w.Id,
                        FullName = p.FullName,
                        Email = w.EmailCorporativo
                    }
                ).ToListAsync();

            return new AreaRevisorInicialDto
            {
                Areas = areas,
                Options = options,
            };
        }

        public async Task UpdateAreaRevisoresAsync(int areaScopeId, List<AreaRevisorAsignacionDto> revisores)
        {
            using var ctx = _factory.CreateDbContext();

            // El área debe existir, estar viva y ser el primer nodo "Área Estándar" de su rama
            // (los mismos nodos que lista la pantalla).
            var nodos = await LoadNodosAsync(ctx);
            var area = FirstStandardNodes(nodos).FirstOrDefault(n => n.AreaScopeId == areaScopeId);
            if (area == null)
                throw new AbrilException("El área no existe o no admite revisores (solo áreas de tipo Área Estándar).", 404);

            var deseados = revisores ?? new List<AreaRevisorAsignacionDto>();

            // ── Validaciones (mismo criterio que workers_revisores) ─────────
            if (deseados.GroupBy(r => r.RevisorWorkerId).Any(g => g.Count() > 1))
                throw new AbrilException("No se puede asignar dos veces al mismo revisor.", 400);

            if (deseados.Any(r => r.OrdenPrioridad < 1))
                throw new AbrilException("La prioridad debe ser 1 o mayor.", 400);

            if (deseados.GroupBy(r => r.OrdenPrioridad).Any(g => g.Count() > 1))
                throw new AbrilException("No puede haber dos revisores con la misma prioridad.", 400);

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
            }

            // ── Diff con las filas vivas (mismo patrón que workers_revisores) ─
            var now = DateTimeOffset.UtcNow;
            var vivos = await ctx.AreaRevisores
                .Where(r => r.State && r.AreaScopeId == areaScopeId)
                .ToListAsync();
            var vivosByRevisor = vivos.ToDictionary(r => r.RevisorId);

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
                    ctx.AreaRevisores.Add(new AreaRevisoresModel
                    {
                        AreaScopeId = areaScopeId,
                        RevisorId = d.RevisorWorkerId,
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
                if (!deseadosIds.Contains(row.RevisorId))
                {
                    row.State = false;
                    row.UpdatedAt = now;
                }
            }

            await ctx.SaveChangesAsync();
        }

        // ── Visibilidad por usuario ─────────────────────────────────────────

        /// <summary>
        /// area_scope_id (de los nodos elegibles) que el usuario puede ver, o null si
        /// no puede ver ninguno. Solo ven su área los trabajadores cuya categoría
        /// (workers_category) es Jefe, Coordinador o Gerente: se parte del
        /// workers.area_scope_id del trabajador y se sube el árbol hasta el primer
        /// nodo estándar listado en la pantalla.
        /// </summary>
        private static async Task<int?> GetAreaVisibleDelUsuarioAsync(
            AppDbContext ctx, int userId, List<NodoArea> nodos, List<NodoArea> elegibles)
        {
            var areaScopeWorker = await (
                from w in ctx.Worker
                where w.Person != null && w.Person.UserId == userId && w.AreaScopeId != null
                join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId
                where CategoriasConVista.Contains(c.Name.ToLower())
                select w.AreaScopeId
            ).FirstOrDefaultAsync();
            if (areaScopeWorker == null) return null;

            var byId = nodos.ToDictionary(n => n.AreaScopeId);
            var elegiblesIds = elegibles.Select(n => n.AreaScopeId).ToHashSet();
            var visitados = new HashSet<int>();
            int? actual = areaScopeWorker;
            while (actual != null && visitados.Add(actual.Value))
            {
                if (elegiblesIds.Contains(actual.Value)) return actual.Value;
                actual = byId.TryGetValue(actual.Value, out var nodo) ? nodo.AreaScopeParentId : null;
            }
            return null;
        }

        // ── Árbol de áreas ──────────────────────────────────────────────────

        private sealed record NodoArea(int AreaScopeId, int? AreaScopeParentId, string AreaItemName, string AreaTypeName);

        private static async Task<List<NodoArea>> LoadNodosAsync(AppDbContext ctx)
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
        /// Nodos de tipo "Área Estándar" que son el primero de su rama: ningún ancestro
        /// es también "Área Estándar" (ej. de "Unidad de Proyectos" → "Ingeniería BIM",
        /// ambos estándar, solo se devuelve "Unidad de Proyectos").
        /// </summary>
        private static List<NodoArea> FirstStandardNodes(List<NodoArea> nodos)
        {
            var byId = nodos.ToDictionary(n => n.AreaScopeId);
            return nodos
                .Where(n => n.AreaTypeName == AreaTypeEstandar && !TieneAncestroEstandar(n, byId))
                .ToList();
        }

        private static bool TieneAncestroEstandar(NodoArea nodo, Dictionary<int, NodoArea> byId)
        {
            var visitados = new HashSet<int>();
            var parentId = nodo.AreaScopeParentId;
            while (parentId != null && visitados.Add(parentId.Value))
            {
                if (!byId.TryGetValue(parentId.Value, out var parent)) break;
                if (parent.AreaTypeName == AreaTypeEstandar) return true;
                parentId = parent.AreaScopeParentId;
            }
            return false;
        }
    }
}
