using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Services
{
    public class MilestoneScheduleService : IMilestoneScheduleService
    {
        private readonly IMilestoneScheduleRepository _repository;
        private readonly MilestoneRepository _milestoneRepository;
        private readonly IProjectResidentRepository _projectResidentRepository;

        public MilestoneScheduleService(
            IMilestoneScheduleRepository repository,
            MilestoneRepository milestoneRepository,
            IProjectResidentRepository projectResidentRepository)
        {
            _repository = repository;
            _milestoneRepository = milestoneRepository;
            _projectResidentRepository = projectResidentRepository;
        }

        /// <summary>Solo el residente asignado al proyecto dueño del hito puede editarlo — el
        /// featureKey "mejora-continua.milestone-schedule.editar" es por rol, no por proyecto,
        /// así que cualquier RESIDENTE podía editar el cronograma de cualquier obra sin esto.
        /// El rol ADMINISTRADOR DE RESIDENTES está exento de este chequeo (ver llamadores).</summary>
        private async Task ValidarResidenteDelProyectoAsync(int milestoneScheduleId, int userId)
        {
            var projectId = await _repository.GetProjectIdByMilestoneScheduleId(milestoneScheduleId);
            if (projectId == null)
                throw new AbrilException("Hito no encontrado.", 404);

            var esResidenteAsignado = await _projectResidentRepository.IsUserAssignedToProject(userId, projectId.Value);
            if (!esResidenteAsignado)
                throw new AbrilException("No estás asignado como residente de este proyecto.", 403);
        }

        public Task<List<MilestoneScheduleDTO>> GetAllByMilestoneScheduleHistoryId(int milestoneScheduleHistoryId)
            => _repository.GetAllByMilestoneScheduleHistoryIdFactory(milestoneScheduleHistoryId);

        public async Task<List<MilestoneScheduleFakeDataDTO>> BuildFakeSchedule()
        {
            var milestones = await _milestoneRepository.GetAllFactorySimple();

            var fakeScheduleConfig = new Dictionary<int, (DateTime start, DateTime? end)>
            {
                { 1,  (new DateTime(2025, 12, 22), null) },
                { 2,  (new DateTime(2025, 12, 22), new DateTime(2026, 4, 7)) },
                { 3,  (new DateTime(2026, 3, 28), new DateTime(2026, 5, 26)) },
                { 4,  (new DateTime(2026, 1, 30), null) },
                { 5,  (new DateTime(2026, 2, 20), null) },
                { 6,  (new DateTime(2026, 5, 21), new DateTime(2026, 7, 7)) },
                { 7,  (new DateTime(2026, 7, 2), null) },
                { 8,  (new DateTime(2026, 7, 7), new DateTime(2026, 11, 7)) },
                { 9,  (new DateTime(2026, 11, 10), null) },
                { 10, (new DateTime(2026, 6, 21), new DateTime(2026, 9, 3)) },
                { 11, (new DateTime(2026, 7, 21), new DateTime(2026, 10, 14)) },
                { 12, (new DateTime(2026, 8, 21), new DateTime(2026, 10, 31)) },
                { 13, (new DateTime(2026, 10, 7), new DateTime(2026, 12, 15)) },
                { 14, (new DateTime(2026, 9, 7), new DateTime(2027, 2, 6)) },
                { 15, (new DateTime(2026, 10, 7), new DateTime(2026, 10, 31)) },
                { 16, (new DateTime(2026, 10, 7), new DateTime(2027, 4, 12)) },
                { 17, (new DateTime(2026, 12, 7), new DateTime(2027, 1, 30)) },
                { 18, (new DateTime(2026, 10, 7), new DateTime(2027, 5, 3)) },
                { 19, (new DateTime(2026, 12, 28), new DateTime(2027, 3, 30)) },
                { 20, (new DateTime(2027, 4, 7), new DateTime(2027, 4, 30)) },
                { 21, (new DateTime(2027, 5, 3), null) }
            };

            int order = 1;
            return milestones
                .Where(m => fakeScheduleConfig.ContainsKey(m.MilestoneId))
                .Select(m =>
                {
                    var config = fakeScheduleConfig[m.MilestoneId];
                    // Hitos puntuales (config.end == null en este diccionario): la fecha única va
                    // en PlannedEndDate (así lo valida ValidarHitosObligatoriosAsync para los
                    // obligatorios). PlannedStartDate no admite null en el DTO real, así que se
                    // rellena con la misma fecha en vez de dejarlo vacío.
                    // Excepción: "Inicio de obra" es el único obligatorio/puntual cuya fecha única
                    // va en PlannedStartDate (no en PlannedEndDate) — conceptualmente es una fecha
                    // de inicio, no de fin. ValidarHitosObligatoriosAsync tiene la misma excepción.
                    var fechaUnica = config.end == null;
                    var esInicioDeObra = m.MilestoneDescription == "Inicio de obra";
                    return new MilestoneScheduleFakeDataDTO
                    {
                        MilestoneId = m.MilestoneId,
                        MilestoneDescription = m.MilestoneDescription,
                        PlannedStartDate = config.start,
                        PlannedEndDate = esInicioDeObra ? null : (fechaUnica ? config.start : config.end),
                        Order = order++,
                        EsObligatorio = m.EsObligatorio,
                        EsPuntual = m.EsPuntual
                    };
                })
                .OrderBy(x => x.MilestoneId)
                .ToList();
        }

        public async Task CulminarAsync(int milestoneScheduleId, DateOnly? fechaRealFin, int userId, bool esAdminResidentes)
        {
            if (!esAdminResidentes)
                await ValidarResidenteDelProyectoAsync(milestoneScheduleId, userId);
            await _repository.CulminarAsync(milestoneScheduleId, fechaRealFin, userId);
        }

        public async Task MarcarCriticoAsync(int milestoneScheduleId, bool esHitoCritico, int userId, bool esAdminResidentes)
        {
            if (!esAdminResidentes)
                await ValidarResidenteDelProyectoAsync(milestoneScheduleId, userId);
            await _repository.MarcarCriticoAsync(milestoneScheduleId, esHitoCritico, userId);
        }

        /// <summary>Editar un hito ya guardado es exclusivo de ADMINISTRADOR DE RESIDENTES (igual que
        /// DeleteAsync en MilestoneScheduleHistoryService) — el [Authorize(Roles=...)] en el
        /// controller ya filtra el acceso, no hace falta el chequeo por proyecto de
        /// ValidarResidenteDelProyectoAsync porque un RESIDENTE normal nunca llega hasta acá.</summary>
        public Task EditAsync(int milestoneScheduleId, MilestoneScheduleEditDTO dto, int userId)
            => _repository.EditAsync(milestoneScheduleId, dto, userId);

        /// <summary>Agregar un hito nuevo a una history ya existente es exclusivo de ADMINISTRADOR
        /// DE RESIDENTES, mismo alcance que EditAsync.</summary>
        public Task<MilestoneScheduleDTO> AddHitoAsync(int milestoneScheduleHistoryId, MilestoneScheduleAddDTO dto, int userId)
            => _repository.AddHitoAsync(milestoneScheduleHistoryId, dto, userId);

        public Task<List<MilestoneSimpleDTO>> GetFaltantesAsync(int projectId)
            => _repository.GetFaltantesAsync(projectId);
    }
}
