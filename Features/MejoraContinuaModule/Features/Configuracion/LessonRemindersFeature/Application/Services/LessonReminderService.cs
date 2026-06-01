using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Application.Interfaces;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Application.Services
{
    public class LessonReminderService : ILessonReminderService
    {
        private readonly ILessonReminderRepository _repository;

        public LessonReminderService(ILessonReminderRepository repository)
        {
            _repository = repository;
        }

        public Task<object> GetPaged(int page, int pageSize) => _repository.GetPaged(page, pageSize);
        public Task<LessonReminderCreateDataDTO> GetCreateData() => _repository.GetCreateData();
        public Task Create(LessonReminderCreateDTO dto, int userId) => _repository.Create(dto, userId);
        public Task<bool> DeleteSoftAsync(int userProjectId, int userId) => _repository.DeleteSoftAsync(userProjectId, userId);
        public Task<ToggleLessonReminderResultDTO> ToggleActiveAsync(int userProjectId, int userId) => _repository.ToggleActiveAsync(userProjectId, userId);

        public Task<List<ProjectStaffReminderConfigItemDTO>> GetAllProjectStaffAsync() => _repository.GetAllProjectStaffAsync();
        public Task<ToggleProjectStaffReminderResultDTO> ToggleProjectStaffAsync(int projectId) => _repository.ToggleProjectStaffAsync(projectId);
        public Task<List<ActiveProjectStaffEmailDTO>> GetActiveStaffEmailsAsync() => _repository.GetActiveStaffEmailsAsync();
    }
}
