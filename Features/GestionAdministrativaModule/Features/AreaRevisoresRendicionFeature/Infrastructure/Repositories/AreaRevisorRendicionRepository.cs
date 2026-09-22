using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisoresRendicion.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Microsoft.EntityFrameworkCore;
using AreaRevisoresRendicionModel =
    Abril_Backend.Features.GestionAdministrativa.Shared.Models.AreaRevisoresRendicion;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisoresRendicion.Infrastructure.Repositories
{
    /// <summary>
    /// Lectura/escritura de los aprobadores de la PRIMERA REVISIÓN y firmantes del CONSOLIDADO por
    /// área (<c>area_revisores_rendicion</c>).
    ///
    /// Es el gemelo de <c>AreaRevisorRepository</c> y reusa sus mismos helpers —los nodos
    /// configurables, el armador de la tabla y la bandera "filtrar por proyecto" son del ÁREA y no
    /// de la pantalla—. Lo único propio son las dos casillas por persona: en un área de obra
    /// intervienen varios y no todos en los dos pasos, así que acá no se elige un ganador como en
    /// Revisores de Salidas sino que se guarda el conjunto.
    ///
    /// Visibilidad y edición: exactamente las mismas reglas que su gemelo.
    /// </summary>
    public class AreaRevisorRendicionRepository : IAreaRevisorRendicionRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IJefeRevisorResolver _revisorResolver;

        public AreaRevisorRendicionRepository(
            IDbContextFactory<AppDbContext> factory,
            IJefeRevisorResolver revisorResolver)
        {
            _factory = factory;
            _revisorResolver = revisorResolver;
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
                from r in ctx.AreaRevisoresRendicion
                where r.State && areaIds.Contains(r.AreaScopeId)
                join w in ctx.Worker on r.RevisorId equals w.Id
                join p in ctx.Person on w.PersonId equals p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                join pu in ctx.Puesto on w.PuestoId equals pu.PuestoId into puj
                from pu in puj.DefaultIfEmpty()
                join c in ctx.Categoria on pu.CategoriaId equals c.CategoriaId into cj
                from c in cj.DefaultIfEmpty()
                orderby r.AreaScopeId, r.OrdenPrioridad, r.AreaRevisoresRendicionId
                select new AreaAsignacionArmador.AsignacionCruda
                {
                    AreaScopeId = r.AreaScopeId,
                    ProjectId = r.ProjectId,
                    Asignado = new AreaAsignadoDto
                    {
                        Id = r.AreaRevisoresRendicionId,
                        WorkerId = r.RevisorId,
                        FullName = p != null ? p.FullName : null,
                        Email = w.EmailCorporativo,
                        Category = c != null ? c.Nombre : null,
                        OrdenPrioridad = r.OrdenPrioridad,
                        Active = r.Active,
                        ApruebaPrimeraRevision = r.ApruebaPrimeraRevision,
                        ApruebaConsolidado = r.ApruebaConsolidado,
                    },
                }
            ).ToListAsync();

            var proyectos = await AreaAsignacionArmador.ProyectosActivosAsync(ctx);
            var flags = await AreaAsignacionArmador.FiltranPorProyectoAsync(ctx, areaIds);

            // Quiénes quedan vigentes hoy, con el MISMO recorrido que después aprueba y firma: lee
            // esta tabla y, en las áreas de obra, devuelve la PAREJA que el algoritmo propone —el
            // administrador de obra (revisa y firma) y el residente (solo firma)—, cada uno con el
            // paso que le toca. No se recorta a uno: la pantalla responde "quiénes pueden aprobar
            // esta área", y con un solo ganador el residente no aparecía por ningún lado.
            var efectivos = await _revisorResolver.ResolveAprobadoresByAreaScopeManyAsync(areaIds);

            AreaAsignacionArmador.Completar(
                areas, asignaciones, flags, proyectos,
                efectivosDeArea: id => efectivos.TryGetValue(id, out var e) ? Describir(e.Area) : new List<AreaEfectivoDto>(),
                efectivosDeProyecto: (id, projectId) =>
                    efectivos.TryGetValue(id, out var e) && e.PorProyecto.TryGetValue(projectId, out var p)
                        ? Describir(p)
                        : new List<AreaEfectivoDto>());

            await AreaAsignacionArmador.CompletarCategoriasAsync(ctx, areas);

            return new AreaAsignacionInicialDto
            {
                Areas = areas,
                Options = verTodas ? await AreaAsignacionArmador.OpcionesAsync(ctx) : new List<AreaWorkerOptionDto>(),
            };
        }

        /// <summary>
        /// Los aprobadores vigentes como los muestra la pantalla: la columna pinta al primero y
        /// cuenta al resto, y el modal Ver los lista a todos con las dos casillas en solo lectura.
        /// Igual que en su gemelo, el fallback de GTH es un área y no una persona, y se etiqueta
        /// como tal.
        /// </summary>
        private static List<AreaEfectivoDto> Describir(List<AprobadorDeArea> aprobadores)
            => aprobadores
                .Select(a => new AreaEfectivoDto
                {
                    WorkerId = a.Persona.WorkerId,
                    Nombre = a.Persona.Nombre,
                    Email = a.Persona.Email,
                    Origen = a.Persona.Origen.ToString(),
                    ApruebaPrimeraRevision = a.ApruebaPrimeraRevision,
                    ApruebaConsolidado = a.ApruebaConsolidado,
                })
                .ToList();

        public async Task UpdateAreaRevisoresAsync(
            int areaScopeId, int? projectId, List<AreaAsignacionInputDto> revisores)
        {
            using var ctx = _factory.CreateDbContext();

            await AreaAsignacionArmador.ValidarAsync(ctx, areaScopeId, projectId, revisores, "aprobador");

            var deseados = revisores ?? new List<AreaAsignacionInputDto>();
            var now = DateTimeOffset.UtcNow;
            var vivos = await ctx.AreaRevisoresRendicion
                .Where(r => r.State && r.AreaScopeId == areaScopeId && r.ProjectId == projectId)
                .ToListAsync();
            var vivosByWorker = vivos.ToDictionary(r => r.RevisorId);

            foreach (var d in deseados)
            {
                if (vivosByWorker.TryGetValue(d.WorkerId, out var row))
                {
                    if (row.OrdenPrioridad != d.OrdenPrioridad
                        || row.Active != d.Active
                        || row.ApruebaPrimeraRevision != d.ApruebaPrimeraRevision
                        || row.ApruebaConsolidado != d.ApruebaConsolidado)
                    {
                        row.OrdenPrioridad = d.OrdenPrioridad;
                        row.Active = d.Active;
                        row.ApruebaPrimeraRevision = d.ApruebaPrimeraRevision;
                        row.ApruebaConsolidado = d.ApruebaConsolidado;
                        row.UpdatedAt = now;
                    }
                }
                else
                {
                    ctx.AreaRevisoresRendicion.Add(new AreaRevisoresRendicionModel
                    {
                        AreaScopeId = areaScopeId,
                        ProjectId = projectId,
                        RevisorId = d.WorkerId,
                        OrdenPrioridad = d.OrdenPrioridad,
                        ApruebaPrimeraRevision = d.ApruebaPrimeraRevision,
                        ApruebaConsolidado = d.ApruebaConsolidado,
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

            // La bandera es del ÁREA y la comparten las tres pantallas: tocarla desde acá la mueve
            // también en Revisores de Salidas y en Consolidadores, que es lo correcto.
            await AreaAsignacionNodos.SetFiltroProyectoAsync(ctx, areaScopeId, filtraPorProyecto);
        }
    }
}
