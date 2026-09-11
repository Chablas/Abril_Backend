using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.AreaConsolidadores.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.Consolidadores.Interfaces;
using Microsoft.EntityFrameworkCore;
using AreaConsolidadoresModel = Abril_Backend.Features.GestionAdministrativa.Shared.Models.AreaConsolidadores;

namespace Abril_Backend.Features.GestionAdministrativa.AreaConsolidadores.Infrastructure.Repositories
{
    /// <summary>
    /// Lectura/escritura de los consolidadores del S10 por área (area_consolidadores).
    ///
    /// Es el espejo de <c>AreaRevisorRepository</c> —mismos nodos configurables, mismo modal, misma
    /// posibilidad de asignar por proyecto— con UNA diferencia: la columna de vigentes no muestra
    /// un ganador sino la lista completa. Consolidar no es decidir: es hacerle un trámite al
    /// trabajador, así que todos los activos del nodo que resuelve quedan habilitados.
    ///
    /// Sin nada asignado el área igual resuelve: el algoritmo deduce al Jefe del área (o al Gerente
    /// de la gerencia, o al residente de la obra si el área filtra por proyecto).
    /// </summary>
    public class AreaConsolidadorRepository : IAreaConsolidadorRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IConsolidadorResolver _consolidadorResolver;

        public AreaConsolidadorRepository(
            IDbContextFactory<AppDbContext> factory,
            IConsolidadorResolver consolidadorResolver)
        {
            _factory = factory;
            _consolidadorResolver = consolidadorResolver;
        }

        public async Task<AreaAsignacionInicialDto> GetInitialDataAsync(int userId, bool verTodas)
        {
            using var ctx = _factory.CreateDbContext();

            var nodos = await AreaAsignacionNodos.LoadNodosAsync(ctx);
            var elegibles = AreaAsignacionNodos.Configurables(nodos);

            if (!verTodas)
            {
                var areaVisible = await AreaAsignacionNodos.AreaVisibleDelUsuarioAsync(ctx, userId, nodos, elegibles);
                if (areaVisible == null) return new AreaAsignacionInicialDto();
                elegibles = elegibles.Where(n => n.AreaScopeId == areaVisible.Value).ToList();
            }

            var areas = AreaAsignacionArmador.ArmarAreas(elegibles, nodos);
            var areaIds = areas.Select(a => a.AreaScopeId).ToList();

            var asignaciones = await (
                from a in ctx.AreaConsolidadores
                where a.State && areaIds.Contains(a.AreaScopeId)
                join w in ctx.Worker on a.ConsolidadorId equals w.Id
                join p in ctx.Person on w.PersonId equals p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                join pu in ctx.Puesto on w.PuestoId equals pu.PuestoId into puj
                from pu in puj.DefaultIfEmpty()
                join c in ctx.Categoria on pu.CategoriaId equals c.CategoriaId into cj
                from c in cj.DefaultIfEmpty()
                orderby a.AreaScopeId, a.OrdenPrioridad, a.AreaConsolidadoresId
                select new AreaAsignacionArmador.AsignacionCruda
                {
                    AreaScopeId = a.AreaScopeId,
                    ProjectId = a.ProjectId,
                    Asignado = new AreaAsignadoDto
                    {
                        Id = a.AreaConsolidadoresId,
                        WorkerId = a.ConsolidadorId,
                        FullName = p != null ? p.FullName : null,
                        Email = w.EmailCorporativo,
                        Category = c != null ? c.Nombre : null,
                        OrdenPrioridad = a.OrdenPrioridad,
                        Active = a.Active,
                    },
                }
            ).ToListAsync();

            var proyectos = await AreaAsignacionArmador.ProyectosActivosAsync(ctx);
            var flags = await AreaAsignacionArmador.FiltranPorProyectoAsync(ctx, areaIds);

            // Los vigentes salen del MISMO resolver que habilita el botón "Consolidado S10" de cada
            // planilla, así que la pantalla no puede prometer a alguien que después no puede.
            var efectivos = await _consolidadorResolver.ResolveByAreaScopeManyAsync(areaIds);

            AreaAsignacionArmador.Completar(
                areas, asignaciones, flags, proyectos,
                efectivosDeArea: id => efectivos.TryGetValue(id, out var e)
                    ? Describir(e.Area)
                    : new List<AreaEfectivoDto>(),
                efectivosDeProyecto: (id, projectId) =>
                    efectivos.TryGetValue(id, out var e) && e.PorProyecto.TryGetValue(projectId, out var p)
                        ? Describir(p)
                        : new List<AreaEfectivoDto>());

            return new AreaAsignacionInicialDto
            {
                Areas = areas,
                Options = verTodas ? await AreaAsignacionArmador.OpcionesAsync(ctx) : new List<AreaWorkerOptionDto>(),
            };
        }

        /// <summary>
        /// Todos los vigentes del área/proyecto. El propio trabajador no aparece: acá no hay
        /// trabajador, la pregunta es por el área (en la planilla sí se le suma siempre).
        /// </summary>
        private static List<AreaEfectivoDto> Describir(List<ConsolidadorElegido> elegidos)
            => elegidos
                .Select(c => new AreaEfectivoDto
                {
                    WorkerId = c.WorkerId,
                    Nombre = c.Nombre,
                    Origen = c.Origen.ToString(),
                })
                .ToList();

        public async Task UpdateAreaConsolidadoresAsync(
            int areaScopeId, int? projectId, List<AreaAsignacionInputDto> consolidadores)
        {
            using var ctx = _factory.CreateDbContext();

            await AreaAsignacionArmador.ValidarAsync(ctx, areaScopeId, projectId, consolidadores, "consolidador");

            var deseados = consolidadores ?? new List<AreaAsignacionInputDto>();
            var now = DateTimeOffset.UtcNow;
            var vivos = await ctx.AreaConsolidadores
                .Where(a => a.State && a.AreaScopeId == areaScopeId && a.ProjectId == projectId)
                .ToListAsync();
            var vivosByWorker = vivos.ToDictionary(a => a.ConsolidadorId);

            foreach (var d in deseados)
            {
                if (vivosByWorker.TryGetValue(d.WorkerId, out var row))
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
                    ctx.AreaConsolidadores.Add(new AreaConsolidadoresModel
                    {
                        AreaScopeId = areaScopeId,
                        ProjectId = projectId,
                        ConsolidadorId = d.WorkerId,
                        OrdenPrioridad = d.OrdenPrioridad,
                        Active = d.Active,
                        State = true,
                        CreatedAt = now,
                    });
                }
            }

            var deseadosIds = deseados.Select(d => d.WorkerId).ToHashSet();
            foreach (var row in vivos)
            {
                if (!deseadosIds.Contains(row.ConsolidadorId))
                {
                    row.State = false;
                    row.UpdatedAt = now;
                }
            }

            await ctx.SaveChangesAsync();
        }

        public async Task SetFiltroProyectoAsync(int areaScopeId, bool filtraPorProyecto)
        {
            using var ctx = _factory.CreateDbContext();

            var nodos = await AreaAsignacionNodos.LoadNodosAsync(ctx);
            if (AreaAsignacionNodos.Configurables(nodos).All(n => n.AreaScopeId != areaScopeId))
                throw new AbrilException(
                    "El área no existe o no admite configuración (solo áreas de tipo Área de Gerencia o Área Estándar).", 404);

            await AreaAsignacionNodos.SetFiltroProyectoAsync(ctx, areaScopeId, filtraPorProyecto);
        }
    }
}
