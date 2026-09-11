using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.Jerarquia;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Microsoft.EntityFrameworkCore;
using AreaRevisoresModel = Abril_Backend.Features.GestionAdministrativa.Shared.Models.AreaRevisores;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Infrastructure.Repositories
{
    /// <summary>
    /// Lectura/escritura de los revisores de salidas por área (area_revisores):
    /// n revisores por nodo area_scope, ordenados por prioridad (1 = primero).
    ///
    /// Los nodos configurables los define <see cref="AreaAsignacionNodos"/> (los mismos que lista
    /// Consolidadores de Áreas). Estos revisores aplican a los trabajadores del subárbol del nodo
    /// que no tengan jefe personalizado; sin revisores de área resuelve el algoritmo (el Jefe del
    /// área o el Gerente de la gerencia) y, en última instancia, GTH.
    ///
    /// Visibilidad: los roles ADMINISTRADOR DE SOLICITUD DE SALIDAS y USUARIO DE GTH ven todas las
    /// áreas y pueden editarlas; un trabajador de las categorías
    /// <c>CategoriaIds.ConVistaDeSuArea</c> (Jefe, Coordinador o Gerente) ve solo el área listada a
    /// la que pertenece y sin poder editarla; el resto no ve ninguna.
    /// </summary>
    public class AreaRevisorRepository : IAreaRevisorRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IJefeRevisorResolver _revisorResolver;

        public AreaRevisorRepository(
            IDbContextFactory<AppDbContext> factory,
            IJefeRevisorResolver revisorResolver)
        {
            _factory = factory;
            _revisorResolver = revisorResolver;
        }

        public async Task<AreaAsignacionInicialDto> GetInitialDataAsync(int userId, bool verTodas)
        {
            // Tabla + opciones en una sola conexión.
            using var ctx = _factory.CreateDbContext();

            var nodos = await AreaAsignacionNodos.LoadNodosAsync(ctx);
            var elegibles = AreaAsignacionNodos.Configurables(nodos);

            if (!verTodas)
            {
                // Jefe/Coordinador/Gerente: solo el área listada a la que pertenece su worker.
                // Cualquier otro usuario (sin rol de admin ni de GTH): ninguna.
                var areaVisible = await AreaAsignacionNodos.AreaVisibleDelUsuarioAsync(ctx, userId, nodos, elegibles);
                if (areaVisible == null) return new AreaAsignacionInicialDto();
                elegibles = elegibles.Where(n => n.AreaScopeId == areaVisible.Value).ToList();
            }

            var areas = AreaAsignacionArmador.ArmarAreas(elegibles, nodos);
            var areaIds = areas.Select(a => a.AreaScopeId).ToList();

            // Revisores vivos de las áreas listadas, con los datos de cada uno (una sola query).
            // project_id NULL = revisor a nivel de área; con valor = revisor de ese proyecto.
            var asignaciones = await (
                from r in ctx.AreaRevisores
                where r.State && areaIds.Contains(r.AreaScopeId)
                join w in ctx.Worker on r.RevisorId equals w.Id
                join p in ctx.Person on w.PersonId equals p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                join pu in ctx.Puesto on w.PuestoId equals pu.PuestoId into puj
                from pu in puj.DefaultIfEmpty()
                join c in ctx.Categoria on pu.CategoriaId equals c.CategoriaId into cj
                from c in cj.DefaultIfEmpty()
                orderby r.AreaScopeId, r.OrdenPrioridad, r.AreaRevisoresId
                select new AreaAsignacionArmador.AsignacionCruda
                {
                    AreaScopeId = r.AreaScopeId,
                    ProjectId = r.ProjectId,
                    Asignado = new AreaAsignadoDto
                    {
                        Id = r.AreaRevisoresId,
                        WorkerId = r.RevisorId,
                        FullName = p != null ? p.FullName : null,
                        Email = w.EmailCorporativo,
                        Category = c != null ? c.Nombre : null,
                        OrdenPrioridad = r.OrdenPrioridad,
                        Active = r.Active,
                    },
                }
            ).ToListAsync();

            var proyectos = await AreaAsignacionArmador.ProyectosActivosAsync(ctx);
            var flags = await AreaAsignacionArmador.FiltranPorProyectoAsync(ctx, areaIds);

            // El revisor que realmente le toca hoy a cada área/proyecto. Sale del MISMO resolver que
            // decide a quién se le manda a aprobar una salida, así que la columna no puede mostrar a
            // alguien distinto de quien va a recibir el correo. Va sin workerId: acá no hay
            // trabajador del que descartarse, la pregunta es por el área.
            var efectivos = await _revisorResolver.ResolveByAreaScopeManyAsync(areaIds);

            AreaAsignacionArmador.Completar(
                areas, asignaciones, flags, proyectos,
                efectivosDeArea: id => efectivos.TryGetValue(id, out var e) ? Describir(e.Area) : new List<AreaEfectivoDto>(),
                efectivosDeProyecto: (id, projectId) =>
                    efectivos.TryGetValue(id, out var e) && e.PorProyecto.TryGetValue(projectId, out var p)
                        ? Describir(p)
                        : new List<AreaEfectivoDto>());

            return new AreaAsignacionInicialDto
            {
                Areas = areas,
                // Solo quien ve todas las áreas puede editarlas, así que solo esos necesitan el selector.
                Options = verTodas ? await AreaAsignacionArmador.OpcionesAsync(ctx) : new List<AreaWorkerOptionDto>(),
            };
        }

        /// <summary>
        /// El revisor efectivo como lo muestra la columna. Es siempre uno solo (o ninguno): el
        /// algoritmo de revisores elige un ganador. El fallback de GTH es un área y no una persona,
        /// y se etiqueta como tal.
        /// </summary>
        private static List<AreaEfectivoDto> Describir(RevisorElegido? elegido)
            => elegido?.Revisor == null
                ? new List<AreaEfectivoDto>()
                : new List<AreaEfectivoDto>
                {
                    new()
                    {
                        WorkerId = elegido.Revisor.WorkerId,
                        Nombre = elegido.Revisor.Nombre,
                        Origen = elegido.Revisor.Origen.ToString(),
                    },
                };

        public async Task UpdateAreaRevisoresAsync(
            int areaScopeId, int? projectId, List<AreaAsignacionInputDto> revisores)
        {
            using var ctx = _factory.CreateDbContext();

            await AreaAsignacionArmador.ValidarAsync(ctx, areaScopeId, projectId, revisores, "revisor");

            var deseados = revisores ?? new List<AreaAsignacionInputDto>();
            var now = DateTimeOffset.UtcNow;
            var vivos = await ctx.AreaRevisores
                .Where(r => r.State && r.AreaScopeId == areaScopeId && r.ProjectId == projectId)
                .ToListAsync();
            var vivosByWorker = vivos.ToDictionary(r => r.RevisorId);

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
                    ctx.AreaRevisores.Add(new AreaRevisoresModel
                    {
                        AreaScopeId = areaScopeId,
                        ProjectId = projectId,
                        RevisorId = d.WorkerId,
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
                if (!deseadosIds.Contains(row.RevisorId))
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
