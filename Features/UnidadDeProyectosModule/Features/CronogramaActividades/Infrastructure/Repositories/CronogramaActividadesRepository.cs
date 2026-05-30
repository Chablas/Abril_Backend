using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mpxj = MPXJ.Net;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Repositories
{
    public class CronogramaActividadesRepository : ICronogramaActividadesRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CronogramaActividadesRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<ProyectoSimpleCronogramaDto>> GetProyectosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Project
                .Where(p => p.State && p.TieneUnidadDeProyectos)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new ProyectoSimpleCronogramaDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    ResponsableUdp = p.ResponsableUdp,
                    TotalActividades = ctx.ProjectActivity
                        .Count(a => a.ProjectId == p.ProjectId && a.State && a.Active)
                })
                .ToListAsync();
        }

        public async Task<List<ActividadDto>> GetActividadesAsync(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State && a.Active)
                .OrderBy(a => a.Order)
                .Select(a => new ActividadDto
                {
                    ProjectActivityId = a.ProjectActivityId,
                    ProjectId = a.ProjectId,
                    ActivityDescription = a.ActivityDescription,
                    PlannedStartDate = a.PlannedStartDate,
                    PlannedEndDate = a.PlannedEndDate,
                    ActualEndDate = a.ActualEndDate,
                    ProgressPercentage = a.ProgressPercentage,
                    Order = a.Order,
                    HierarchyLevel = a.HierarchyLevel,
                    ParentId = a.ParentId
                })
                .ToListAsync();
        }

        public async Task<ActividadDto> CrearActividadAsync(int proyectoId, CrearActividadRequest request, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var maxOrder = await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State)
                .Select(a => (int?)a.Order)
                .MaxAsync() ?? 0;

            var activity = new ProjectActivity
            {
                ProjectId = proyectoId,
                ActivityDescription = request.ActivityDescription,
                PlannedStartDate = request.PlannedStartDate,
                PlannedEndDate = request.PlannedEndDate,
                ActualEndDate = null,
                ProgressPercentage = request.ProgressPercentage,
                Order = maxOrder + 1,
                HierarchyLevel = request.HierarchyLevel,
                ParentId = request.ParentId,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };
            ctx.ProjectActivity.Add(activity);
            await ctx.SaveChangesAsync();

            return new ActividadDto
            {
                ProjectActivityId = activity.ProjectActivityId,
                ProjectId = activity.ProjectId,
                ActivityDescription = activity.ActivityDescription,
                PlannedStartDate = activity.PlannedStartDate,
                PlannedEndDate = activity.PlannedEndDate,
                ActualEndDate = activity.ActualEndDate,
                ProgressPercentage = activity.ProgressPercentage,
                Order = activity.Order,
                HierarchyLevel = activity.HierarchyLevel,
                ParentId = activity.ParentId
            };
        }

        public async Task<ActividadDto> EditarActividadAsync(int projectActivityId, EditarActividadRequest request, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var activity = await ctx.ProjectActivity
                .FirstOrDefaultAsync(a => a.ProjectActivityId == projectActivityId && a.State);
            if (activity == null)
                throw new AbrilException("Actividad no encontrada.", 404);

            activity.ActivityDescription = request.ActivityDescription;
            activity.PlannedStartDate = request.PlannedStartDate;
            activity.PlannedEndDate = request.PlannedEndDate;
            activity.ActualEndDate = request.ActualEndDate;
            activity.ProgressPercentage = request.ProgressPercentage;
            activity.UpdatedDateTime = DateTime.UtcNow;
            activity.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();

            return new ActividadDto
            {
                ProjectActivityId = activity.ProjectActivityId,
                ProjectId = activity.ProjectId,
                ActivityDescription = activity.ActivityDescription,
                PlannedStartDate = activity.PlannedStartDate,
                PlannedEndDate = activity.PlannedEndDate,
                ActualEndDate = activity.ActualEndDate,
                ProgressPercentage = activity.ProgressPercentage,
                Order = activity.Order,
                HierarchyLevel = activity.HierarchyLevel,
                ParentId = activity.ParentId
            };
        }

        public async Task<CulminarActividadDto> CulminarActividadAsync(int projectActivityId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var activity = await ctx.ProjectActivity
                .FirstOrDefaultAsync(a => a.ProjectActivityId == projectActivityId && a.State);
            if (activity == null)
                throw new AbrilException("Actividad no encontrada.", 404);

            if (activity.ActualEndDate.HasValue)
            {
                activity.ActualEndDate = null;
                activity.ProgressPercentage = 0;
            }
            else
            {
                activity.ActualEndDate = DateOnly.FromDateTime(DateTime.UtcNow);
                activity.ProgressPercentage = 100;
            }
            activity.UpdatedDateTime = DateTime.UtcNow;
            activity.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();

            return new CulminarActividadDto
            {
                ProjectActivityId = activity.ProjectActivityId,
                ActualEndDate = activity.ActualEndDate,
                ProgressPercentage = activity.ProgressPercentage
            };
        }

        public async Task<List<DebugProyectoDto>> GetDebugProyectosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Project
                .OrderBy(p => p.ProjectId)
                .Select(p => new DebugProyectoDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    TieneUnidadDeProyectos = p.TieneUnidadDeProyectos,
                    State = p.State
                })
                .ToListAsync();
        }

        public async Task EliminarActividadAsync(int projectActivityId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var activity = await ctx.ProjectActivity
                .FirstOrDefaultAsync(a => a.ProjectActivityId == projectActivityId && a.State);
            if (activity == null)
                throw new AbrilException("Actividad no encontrada.", 404);

            activity.State = false;
            activity.Active = false;
            activity.UpdatedDateTime = DateTime.UtcNow;
            activity.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
        }

        public async Task<ImportarMppResultDto> ImportarMppAsync(int proyectoId, IFormFile archivo, int userId)
        {
            if (archivo == null || archivo.Length == 0)
                throw new AbrilException("El archivo .mpp está vacío o no fue enviado.", 400);

            using var ctx = _factory.CreateDbContext();

            var proyecto = await ctx.Project.FirstOrDefaultAsync(p => p.ProjectId == proyectoId && p.State);
            if (proyecto == null)
                throw new AbrilException("Proyecto no encontrado.", 404);

            // Guardar el archivo en un path temporal para que MPXJ pueda leerlo
            var tempPath = Path.Combine(Path.GetTempPath(), $"mpp_{Guid.NewGuid()}.mpp");
            try
            {
                using (var fs = File.Create(tempPath))
                    await archivo.CopyToAsync(fs);

                var projectFile = new Mpxj.UniversalProjectReader().Read(tempPath);

                // Fecha de inicio del proyecto en el .mpp (DateTime? nativo en MPXJ.Net)
                DateTime? mppStartDt = projectFile.ProjectProperties.StartDate;
                DateOnly? mppStartDate = mppStartDt.HasValue ? DateOnly.FromDateTime(mppStartDt.Value) : null;

                // Calcular offset en días entre el inicio del .mpp y la fecha real del proyecto en BD
                int offsetDias = 0;
                if (mppStartDate.HasValue && proyecto.FechaInicio.HasValue)
                    offsetDias = proyecto.FechaInicio.Value.DayNumber - mppStartDate.Value.DayNumber;

                // Eliminar todas las actividades existentes del proyecto (eliminación física)
                var existentes = await ctx.ProjectActivity
                    .Where(a => a.ProjectId == proyectoId)
                    .ToListAsync();
                int eliminadas = existentes.Count;
                ctx.ProjectActivity.RemoveRange(existentes);
                await ctx.SaveChangesAsync();

                // Mapeo: UniqueID del .mpp → ProjectActivityId generado en BD
                var uniqueIdToDbId = new Dictionary<int, int>();

                int orden = 1;
                foreach (var tarea in projectFile.Tasks)
                {
                    // Omitir la tarea raíz nula que MPP genera como contenedor
                    if (tarea.Null || string.IsNullOrWhiteSpace(tarea.Name)) continue;

                    int level = tarea.OutlineLevel ?? 0;
                    int uniqueId = tarea.UniqueID ?? 0;

                    // Resolver parent_id en BD a partir del UniqueID del padre en el .mpp
                    int? parentDbId = null;
                    var parentMpxj = tarea.ParentTask;
                    if (parentMpxj != null)
                    {
                        int parentUniqueId = parentMpxj.UniqueID ?? 0;
                        if (parentUniqueId > 0 && uniqueIdToDbId.TryGetValue(parentUniqueId, out var pid))
                            parentDbId = pid;
                    }

                    // Aplicar offset de fechas
                    DateOnly? inicio = AplicarOffset(tarea.Start, offsetDias);
                    DateOnly? fin = AplicarOffset(tarea.Finish, offsetDias);

                    var nueva = new ProjectActivity
                    {
                        ProjectId = proyectoId,
                        ActivityDescription = tarea.Name.Trim(),
                        PlannedStartDate = inicio,
                        PlannedEndDate = fin,
                        ActualEndDate = null,
                        ProgressPercentage = 0,
                        Order = orden++,
                        ParentId = parentDbId,
                        HierarchyLevel = level,
                        CreatedDateTime = DateTime.UtcNow,
                        CreatedUserId = userId,
                        Active = true,
                        State = true
                    };
                    ctx.ProjectActivity.Add(nueva);
                    await ctx.SaveChangesAsync();

                    if (uniqueId > 0)
                        uniqueIdToDbId[uniqueId] = nueva.ProjectActivityId;
                }

                return new ImportarMppResultDto
                {
                    ActividadesImportadas = orden - 1,
                    ActividadesEliminadas = eliminadas
                };
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        public async Task<List<ActividadDto>> ReordenarActividadesAsync(int proyectoId, List<ReordenarItem> items)
        {
            if (items == null || items.Count == 0)
                throw new AbrilException("La lista de actividades a reordenar está vacía.", 400);

            Console.WriteLine($"[Reordenar] proyectoId={proyectoId} items recibidos={items.Count}");

            using var ctx = _factory.CreateDbContext();

            var ids = items.Select(i => i.ProjectActivityId).ToList();
            var activities = await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State && ids.Contains(a.ProjectActivityId))
                .ToListAsync();

            if (activities.Count != ids.Count)
                throw new AbrilException("Una o más actividades no pertenecen al proyecto o no existen.", 400);

            foreach (var item in items)
            {
                var activity = activities.First(a => a.ProjectActivityId == item.ProjectActivityId);
                Console.WriteLine($"[Reordenar]   ID={item.ProjectActivityId} parentId={activity.ParentId} orderAnterior={activity.Order} → nuevoOrder={item.Order}");
                activity.Order = item.Order;
            }

            await ctx.SaveChangesAsync();
            Console.WriteLine("[Reordenar] Reordenamiento completado");

            return await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State && a.Active)
                .OrderBy(a => a.Order)
                .Select(a => new ActividadDto
                {
                    ProjectActivityId = a.ProjectActivityId,
                    ProjectId = a.ProjectId,
                    ActivityDescription = a.ActivityDescription,
                    PlannedStartDate = a.PlannedStartDate,
                    PlannedEndDate = a.PlannedEndDate,
                    ActualEndDate = a.ActualEndDate,
                    ProgressPercentage = a.ProgressPercentage,
                    Order = a.Order,
                    HierarchyLevel = a.HierarchyLevel,
                    ParentId = a.ParentId
                })
                .ToListAsync();
        }

        public async Task<List<DebugActividadOrdenDto>> GetDebugOrderAsync(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State && a.Active)
                .OrderBy(a => a.Order)
                .Select(a => new DebugActividadOrdenDto
                {
                    ProjectActivityId = a.ProjectActivityId,
                    Description = a.ActivityDescription,
                    Order = a.Order,
                    ParentId = a.ParentId,
                    HierarchyLevel = a.HierarchyLevel
                })
                .ToListAsync();
        }

        public async Task<List<ActividadDto>> CambiarJerarquiaAsync(int proyectoId, CambiarJerarquiaRequest request)
        {
            using var ctx = _factory.CreateDbContext();

            var activity = await ctx.ProjectActivity
                .FirstOrDefaultAsync(a => a.ProjectActivityId == request.ProjectActivityId && a.State);
            if (activity == null)
                throw new AbrilException("Actividad no encontrada.", 404);
            if (activity.ProjectId != proyectoId)
                throw new AbrilException("La actividad no pertenece al proyecto indicado.", 400);

            int levelDelta = request.NuevoHierarchyLevel - activity.HierarchyLevel;
            activity.HierarchyLevel = request.NuevoHierarchyLevel;
            activity.ParentId = request.NuevoParentId;

            if (levelDelta != 0)
            {
                var todas = await ctx.ProjectActivity
                    .Where(a => a.ProjectId == proyectoId && a.State)
                    .ToListAsync();
                ActualizarHijosRecursivo(activity.ProjectActivityId, levelDelta, todas);
            }

            await ctx.SaveChangesAsync();

            return await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State && a.Active)
                .OrderBy(a => a.Order)
                .Select(a => new ActividadDto
                {
                    ProjectActivityId = a.ProjectActivityId,
                    ProjectId = a.ProjectId,
                    ActivityDescription = a.ActivityDescription,
                    PlannedStartDate = a.PlannedStartDate,
                    PlannedEndDate = a.PlannedEndDate,
                    ActualEndDate = a.ActualEndDate,
                    ProgressPercentage = a.ProgressPercentage,
                    Order = a.Order,
                    HierarchyLevel = a.HierarchyLevel,
                    ParentId = a.ParentId
                })
                .ToListAsync();
        }

        public async Task<List<ActividadDto>> SubirNivelAsync(int proyectoId, int actividadId)
        {
            using var ctx = _factory.CreateDbContext();

            var todas = await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State)
                .ToListAsync();

            var actividad = todas.FirstOrDefault(a => a.ProjectActivityId == actividadId);
            if (actividad == null)
                throw new AbrilException("Actividad no encontrada.", 404);

            if (actividad.HierarchyLevel == 0)
                throw new AbrilException("La actividad ya está en el nivel más alto.", 400);

            // Nuevo parentId = abuelo (parentId del padre actual)
            int? nuevoParentId = null;
            if (actividad.ParentId.HasValue)
            {
                var padre = todas.FirstOrDefault(a => a.ProjectActivityId == actividad.ParentId.Value);
                nuevoParentId = padre?.ParentId;
            }

            actividad.HierarchyLevel -= 1;
            actividad.ParentId = nuevoParentId;
            ActualizarHijosRecursivo(actividadId, -1, todas);

            await ctx.SaveChangesAsync();

            return await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State && a.Active)
                .OrderBy(a => a.Order)
                .Select(a => new ActividadDto
                {
                    ProjectActivityId = a.ProjectActivityId,
                    ProjectId = a.ProjectId,
                    ActivityDescription = a.ActivityDescription,
                    PlannedStartDate = a.PlannedStartDate,
                    PlannedEndDate = a.PlannedEndDate,
                    ActualEndDate = a.ActualEndDate,
                    ProgressPercentage = a.ProgressPercentage,
                    Order = a.Order,
                    HierarchyLevel = a.HierarchyLevel,
                    ParentId = a.ParentId
                })
                .ToListAsync();
        }

        public async Task<List<ActividadDto>> BajarNivelAsync(int proyectoId, int actividadId)
        {
            using var ctx = _factory.CreateDbContext();

            var todas = await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State)
                .ToListAsync();

            var actividad = todas.FirstOrDefault(a => a.ProjectActivityId == actividadId);
            if (actividad == null)
                throw new AbrilException("Actividad no encontrada.", 404);

            // Hermano inmediatamente anterior: mismo parentId, mayor order menor que el actual
            var hermanoAnterior = todas
                .Where(a => a.ParentId == actividad.ParentId
                         && a.ProjectActivityId != actividadId
                         && a.Order < actividad.Order)
                .OrderByDescending(a => a.Order)
                .FirstOrDefault();

            if (hermanoAnterior == null)
                throw new AbrilException("No hay un padre disponible para asignar esta actividad.", 400);

            actividad.HierarchyLevel += 1;
            actividad.ParentId = hermanoAnterior.ProjectActivityId;
            ActualizarHijosRecursivo(actividadId, 1, todas);

            await ctx.SaveChangesAsync();

            return await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State && a.Active)
                .OrderBy(a => a.Order)
                .Select(a => new ActividadDto
                {
                    ProjectActivityId = a.ProjectActivityId,
                    ProjectId = a.ProjectId,
                    ActivityDescription = a.ActivityDescription,
                    PlannedStartDate = a.PlannedStartDate,
                    PlannedEndDate = a.PlannedEndDate,
                    ActualEndDate = a.ActualEndDate,
                    ProgressPercentage = a.ProgressPercentage,
                    Order = a.Order,
                    HierarchyLevel = a.HierarchyLevel,
                    ParentId = a.ParentId
                })
                .ToListAsync();
        }

        private static void ActualizarHijosRecursivo(int parentId, int levelDelta, List<ProjectActivity> todas)
        {
            foreach (var hijo in todas.Where(a => a.ParentId == parentId))
            {
                hijo.HierarchyLevel += levelDelta;
                ActualizarHijosRecursivo(hijo.ProjectActivityId, levelDelta, todas);
            }
        }

        private static DateOnly? AplicarOffset(DateTime? fecha, int offsetDias)
        {
            if (!fecha.HasValue) return null;
            return DateOnly.FromDateTime(fecha.Value).AddDays(offsetDias);
        }
    }
}
