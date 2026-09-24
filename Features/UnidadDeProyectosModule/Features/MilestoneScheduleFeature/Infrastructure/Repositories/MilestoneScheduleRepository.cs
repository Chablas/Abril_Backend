using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Repositories
{
    public class MilestoneScheduleRepository : IMilestoneScheduleRepository
    {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;

        public MilestoneScheduleRepository(AppDbContext context, IDbContextFactory<AppDbContext> factory)
        {
            _context = context;
            _factory = factory;
        }

        public async Task<List<ScheduleChangeInfoDTO>> GetSchedulesWithChangesThisMonthAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var startOfNextMonth = startOfMonth.AddMonths(1);

            var data = await (
                from msh in _context.MilestoneScheduleHistory
                join p in _context.Project on msh.ProjectId equals p.ProjectId
                join u in _context.User on msh.CreatedUserId equals u.UserId
                join person in _context.Person on u.UserId equals person.UserId
                where msh.CreatedDateTime >= startOfMonth
                    && msh.CreatedDateTime < startOfNextMonth
                    && msh.Active && msh.State
                select new
                {
                    ProjectDescription = p.ProjectDescription ?? string.Empty,
                    ChangedBy = person.FullName,
                    ChangeDate = msh.CreatedDateTime
                }
            ).ToListAsync();

            return data
                .GroupBy(x => new { x.ProjectDescription, x.ChangedBy })
                .Select(g => new ScheduleChangeInfoDTO
                {
                    ProjectDescription = g.Key.ProjectDescription,
                    ChangedBy = g.Key.ChangedBy,
                    ChangeDate = g.Select(x => x.ChangeDate).Distinct().OrderBy(d => d).ToList()
                })
                .ToList();
        }

        public async Task<List<MilestoneScheduleDTO>> GetAllByMilestoneScheduleHistoryIdFactory(int milestoneScheduleHistoryId)
        {
            using var ctx = _factory.CreateDbContext();

            var registros = from ms in ctx.MilestoneSchedule
                            join m in ctx.Milestone on ms.MilestoneId equals m.MilestoneId into gj
                            from m in gj.DefaultIfEmpty()
                            where ms.State && ms.MilestoneScheduleHistoryId == milestoneScheduleHistoryId
                            orderby ms.Order
                            select new MilestoneScheduleDTO
                            {
                                MilestoneScheduleId = ms.MilestoneScheduleId,
                                MilestoneId = ms.MilestoneId,
                                MilestoneDescription = ms.MilestoneId != null ? m.MilestoneDescription : ms.CustomDescription,
                                MilestoneScheduleHistoryId = ms.MilestoneScheduleHistoryId,
                                Order = ms.Order,
                                PlannedStartDate = ms.PlannedStartDate,
                                PlannedEndDate = ms.PlannedEndDate,
                                FechaRealFin = ms.FechaRealFin,
                                CreatedDateTime = ms.CreatedDateTime,
                                CreatedUserId = ms.CreatedUserId,
                                UpdatedDateTime = ms.UpdatedDateTime,
                                UpdatedUserId = ms.UpdatedUserId,
                                Active = ms.Active,
                                EsHitoCritico = ms.EsHitoCritico,
                                EsObligatorio = ms.MilestoneId != null ? m.EsObligatorio : false,
                                EsPuntual = ms.MilestoneId != null ? m.EsPuntual : false
                            };

            return await registros.ToListAsync();
        }

        /// <summary>Resuelve el proyecto dueño de un hito, para validar que quien edita
        /// (Culminar/MarcarCritico) es el residente asignado a ese proyecto.</summary>
        public async Task<int?> GetProjectIdByMilestoneScheduleId(int milestoneScheduleId)
        {
            using var ctx = _factory.CreateDbContext();

            return await (
                from ms in ctx.MilestoneSchedule
                join msh in ctx.MilestoneScheduleHistory on ms.MilestoneScheduleHistoryId equals msh.MilestoneScheduleHistoryId
                where ms.MilestoneScheduleId == milestoneScheduleId && ms.State
                select (int?)msh.ProjectId
            ).FirstOrDefaultAsync();
        }

        public async Task CulminarAsync(int milestoneScheduleId, DateOnly? fechaRealFin, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var ms = await ctx.MilestoneSchedule
                .FirstOrDefaultAsync(x => x.MilestoneScheduleId == milestoneScheduleId && x.State);
            if (ms == null)
                throw new AbrilException("Hito no encontrado.", 404);

            ms.FechaRealFin = fechaRealFin;
            ms.UpdatedDateTime = DateTime.UtcNow;
            ms.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
        }

        public async Task MarcarCriticoAsync(int milestoneScheduleId, bool esHitoCritico, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var ms = await ctx.MilestoneSchedule
                .FirstOrDefaultAsync(x => x.MilestoneScheduleId == milestoneScheduleId && x.State);
            if (ms == null)
                throw new AbrilException("Hito no encontrado.", 404);

            ms.EsHitoCritico = esHitoCritico;
            ms.UpdatedDateTime = DateTime.UtcNow;
            ms.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
        }

        /// <summary>"Inicio de obra" es el único hito obligatorio/puntual cuya fecha única va en
        /// PlannedStartDate en vez de PlannedEndDate. En MilestoneScheduleCreateDTO (usado por el
        /// POST de crear versión completa) esto queda garantizado porque PlannedStartDate no admite
        /// null en el tipo del DTO. MilestoneScheduleEditDTO y MilestoneScheduleAddDTO sí lo admiten
        /// (para poder mover la fecha entre campos / agregar hitos sin fecha aún), así que acá la
        /// garantía se valida a mano.</summary>
        private const string DescripcionInicioDeObra = "Inicio de obra";

        /// <summary>Edita un hito ya guardado sin necesidad de subir una versión nueva completa del
        /// cronograma (eso es Create, en MilestoneScheduleHistoryRepository) — solo llamado para
        /// ADMINISTRADOR DE RESIDENTES, ver el [Authorize(Roles=...)] en el controller.</summary>
        public async Task EditAsync(int milestoneScheduleId, MilestoneScheduleEditDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var ms = await ctx.MilestoneSchedule
                .FirstOrDefaultAsync(x => x.MilestoneScheduleId == milestoneScheduleId && x.State);
            if (ms == null)
                throw new AbrilException("Hito no encontrado.", 404);

            if (dto.MilestoneId.HasValue)
            {
                var milestone = await ctx.Milestone
                    .Where(m => m.MilestoneId == dto.MilestoneId.Value)
                    .Select(m => new { m.EsObligatorio, m.MilestoneDescription })
                    .FirstOrDefaultAsync();

                if (milestone != null && milestone.MilestoneDescription == DescripcionInicioDeObra
                    && dto.PlannedStartDate == null)
                {
                    throw new AbrilException(
                        $"El hito \"{DescripcionInicioDeObra}\" debe tener una fecha.");
                }

                // Misma regla que ValidarHitosObligatoriosAsync en MilestoneScheduleHistoryRepository:
                // un hito de catálogo marcado es_obligatorio=true debe traer PlannedEndDate, salvo
                // "Inicio de obra" (su fecha única va en PlannedStartDate, ya validada arriba).
                if (milestone != null && milestone.EsObligatorio
                    && milestone.MilestoneDescription != DescripcionInicioDeObra
                    && dto.PlannedEndDate == null)
                {
                    throw new AbrilException(
                        $"El hito \"{milestone.MilestoneDescription}\" es obligatorio y debe tener una fecha.");
                }
            }

            ms.MilestoneId = dto.MilestoneId;
            ms.CustomDescription = dto.CustomDescription;
            ms.Order = dto.Order;
            ms.PlannedStartDate = dto.PlannedStartDate;
            ms.PlannedEndDate = dto.PlannedEndDate;
            ms.EsHitoCritico = dto.EsHitoCritico;
            ms.UpdatedDateTime = DateTime.UtcNow;
            ms.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
        }

        /// <summary>Agrega un único hito nuevo a una MilestoneScheduleHistory ya existente, sin subir
        /// una versión completa nueva — solo ADMINISTRADOR DE RESIDENTES (ver el
        /// [Authorize(Roles=...)] en el controller, mismo alcance que EditAsync). Devuelve el hito
        /// ya resuelto (mismo shape que GetAllByMilestoneScheduleHistoryIdFactory) para que el
        /// frontend actualice el Gantt en memoria sin un segundo GET.</summary>
        public async Task<MilestoneScheduleDTO> AddHitoAsync(int milestoneScheduleHistoryId, MilestoneScheduleAddDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var history = await ctx.MilestoneScheduleHistory
                .FirstOrDefaultAsync(h => h.MilestoneScheduleHistoryId == milestoneScheduleHistoryId && h.State);
            if (history == null)
                throw new AbrilException("Cronograma no encontrado.", 404);

            if (!dto.MilestoneId.HasValue && string.IsNullOrWhiteSpace(dto.CustomDescription))
                throw new AbrilException("Debe indicar un hito del catálogo o una descripción personalizada.");

            bool esObligatorio = false;
            bool esPuntual = false;
            string? milestoneDescription = null;

            if (dto.MilestoneId.HasValue)
            {
                var milestone = await ctx.Milestone
                    .Where(m => m.MilestoneId == dto.MilestoneId.Value && m.State)
                    .Select(m => new { m.EsObligatorio, m.EsPuntual, m.MilestoneDescription })
                    .FirstOrDefaultAsync();
                if (milestone == null)
                    throw new AbrilException("El hito del catálogo indicado no existe.", 404);

                var yaExiste = await ctx.MilestoneSchedule.AnyAsync(ms =>
                    ms.MilestoneScheduleHistoryId == milestoneScheduleHistoryId && ms.State
                    && ms.MilestoneId == dto.MilestoneId.Value);
                if (yaExiste)
                    throw new AbrilException($"El hito \"{milestone.MilestoneDescription}\" ya está en este cronograma.");

                if (milestone.MilestoneDescription == DescripcionInicioDeObra && dto.PlannedStartDate == null)
                    throw new AbrilException($"El hito \"{DescripcionInicioDeObra}\" debe tener una fecha.");

                if (milestone.EsObligatorio && milestone.MilestoneDescription != DescripcionInicioDeObra
                    && dto.PlannedEndDate == null)
                    throw new AbrilException(
                        $"El hito \"{milestone.MilestoneDescription}\" es obligatorio y debe tener una fecha.");

                esObligatorio = milestone.EsObligatorio;
                esPuntual = milestone.EsPuntual;
                milestoneDescription = milestone.MilestoneDescription;
            }

            var maxOrder = await ctx.MilestoneSchedule
                .Where(ms => ms.MilestoneScheduleHistoryId == milestoneScheduleHistoryId && ms.State)
                .Select(ms => (int?)ms.Order)
                .MaxAsync() ?? 0;

            var nuevo = new MilestoneSchedule
            {
                MilestoneId = dto.MilestoneId,
                CustomDescription = dto.MilestoneId.HasValue ? null : dto.CustomDescription,
                MilestoneScheduleHistoryId = milestoneScheduleHistoryId,
                Order = maxOrder + 1,
                PlannedStartDate = dto.PlannedStartDate,
                PlannedEndDate = dto.PlannedEndDate,
                EsHitoCritico = dto.EsHitoCritico,
                Active = true,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            ctx.MilestoneSchedule.Add(nuevo);
            await ctx.SaveChangesAsync();

            return new MilestoneScheduleDTO
            {
                MilestoneScheduleId = nuevo.MilestoneScheduleId,
                MilestoneId = nuevo.MilestoneId,
                MilestoneDescription = nuevo.MilestoneId.HasValue ? milestoneDescription! : nuevo.CustomDescription!,
                MilestoneScheduleHistoryId = nuevo.MilestoneScheduleHistoryId,
                Order = nuevo.Order,
                PlannedStartDate = nuevo.PlannedStartDate,
                PlannedEndDate = nuevo.PlannedEndDate,
                FechaRealFin = nuevo.FechaRealFin,
                CreatedDateTime = nuevo.CreatedDateTime,
                CreatedUserId = nuevo.CreatedUserId,
                UpdatedDateTime = nuevo.UpdatedDateTime,
                UpdatedUserId = nuevo.UpdatedUserId,
                Active = nuevo.Active,
                EsHitoCritico = nuevo.EsHitoCritico,
                EsObligatorio = esObligatorio,
                EsPuntual = esPuntual
            };
        }

        /// <summary>Hitos del catálogo (activos) que todavía no están en el cronograma vigente del
        /// proyecto — para que el frontend arme el selector de "hitos faltantes" al agregar uno
        /// nuevo (AddHitoAsync). Si el proyecto no tiene ninguna history activa, devuelve el
        /// catálogo completo (todos "faltan" en ese caso).</summary>
        public async Task<List<MilestoneSimpleDTO>> GetFaltantesAsync(int projectId)
        {
            using var ctx = _factory.CreateDbContext();

            var lastHistory = await ctx.MilestoneScheduleHistory
                .Where(h => h.ProjectId == projectId && h.Active && h.State)
                .OrderByDescending(h => h.CreatedDateTime)
                .FirstOrDefaultAsync();

            var existentesIds = lastHistory == null
                ? new List<int>()
                : await ctx.MilestoneSchedule
                    .Where(ms => ms.MilestoneScheduleHistoryId == lastHistory.MilestoneScheduleHistoryId
                                 && ms.State && ms.MilestoneId != null)
                    .Select(ms => ms.MilestoneId!.Value)
                    .ToListAsync();

            return await ctx.Milestone
                .Where(m => m.State && !existentesIds.Contains(m.MilestoneId))
                .OrderBy(m => m.MilestoneDescription)
                .Select(m => new MilestoneSimpleDTO
                {
                    MilestoneId = m.MilestoneId,
                    MilestoneDescription = m.MilestoneDescription,
                    EsObligatorio = m.EsObligatorio,
                    EsPuntual = m.EsPuntual
                })
                .ToListAsync();
        }
    }
}
