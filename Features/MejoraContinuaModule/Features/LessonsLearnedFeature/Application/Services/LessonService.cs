using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Interfaces;
using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Services
{
    public class LessonService : ILessonService
    {
        private const string PlatformUrl = "https://abril-frontend-m21l.onrender.com/auth/login";

        private readonly ILessonRepository _lessonRepository;
        private readonly IEmailService _emailService;

        public LessonService(ILessonRepository lessonRepository, IEmailService emailService)
        {
            _lessonRepository = lessonRepository;
            _emailService = emailService;
        }

        public Task<LessonDetailDTO?> GetByIdAsync(int id, int currentUserId)
            => _lessonRepository.GetByIdAsync(id, currentUserId);

        public Task<PagedResult<LessonListDTO>> GetLessonsFilterPaged(
            DateTimeOffset? periodDate, int? stateId, int? projectId,
            int? areaId, int? userId, int page, int pageSize)
            => _lessonRepository.GetLessonsFilterPaged(periodDate, stateId, projectId, areaId, userId, page, pageSize);

        public Task<LessonsPagedWithFiltersDTO> GetPagedWithFilters(LessonFilterDTO filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return _lessonRepository.GetPagedWithFiltersAsync(filter);
        }

        public Task<List<LessonListDTO>> GetLessonsFilterAsync(
            string? period, int? stateId, int? projectId, int? areaId, int? userId)
            => _lessonRepository.GetLessonsFilterAsync(period, stateId, projectId, areaId, userId);

        public Task<object?> CreateAsync(LessonCreateDTO dto, int userId)
            => _lessonRepository.CreateAsync(dto, userId);

        public Task<bool> UpdateAsync(int lessonId, LessonUpdateDTO dto, int currentUserId)
            => _lessonRepository.UpdateAsync(lessonId, dto, currentUserId);

        public Task<bool> DeleteSoftAsync(int lessonId, int userId)
            => _lessonRepository.DeleteSoftAsync(lessonId, userId);

        public async Task ApproveAsync(int lessonId, int currentUserId)
        {
            var result = await _lessonRepository.ApproveAsync(lessonId, currentUserId);
            await NotifyAuthorAsync(result, approved: true, comment: null);
        }

        public async Task RejectAsync(int lessonId, int currentUserId, string? comment)
        {
            var result = await _lessonRepository.RejectAsync(lessonId, currentUserId, comment);
            await NotifyAuthorAsync(result, approved: false, comment: comment);
        }

        // ── Correos al autor ─────────────────────────────────────────────────

        private async Task NotifyAuthorAsync(LessonReviewResultDTO result, bool approved, string? comment)
        {
            if (string.IsNullOrWhiteSpace(result.CreatorEmail)) return;

            var saludo = !string.IsNullOrWhiteSpace(result.CreatorFullName)
                ? $"Estimado(a) <strong>{result.CreatorFullName}</strong>,"
                : "Estimado(a),";

            string subject;
            string body;

            if (approved)
            {
                subject = "✅ Tu lección aprendida fue aprobada";
                body = $@"
                <p>{saludo}</p>
                <p>Tu <strong>lección aprendida</strong> fue revisada y <strong>aprobada</strong> por tu jefatura.</p>
                <p>No necesitas hacer nada más. ¡Gracias por tu aporte a la mejora continua!</p>
                <p>👉 <a href='{PlatformUrl}' target='_blank'>Acceder a la plataforma</a></p>";
            }
            else
            {
                var comentarioHtml = string.IsNullOrWhiteSpace(comment)
                    ? ""
                    : $"<p><strong>Comentario de tu jefatura:</strong><br/>{comment}</p>";
                subject = "⚠️ Tu lección aprendida fue rechazada";
                body = $@"
                <p>{saludo}</p>
                <p>Tu <strong>lección aprendida</strong> fue <strong>rechazada</strong> por tu jefatura.</p>
                {comentarioHtml}
                <p>Por favor ingresa a la plataforma, <strong>edítala</strong> y vuelve a enviarla para una nueva revisión.</p>
                <p>👉 <a href='{PlatformUrl}' target='_blank'>Acceder a la plataforma</a></p>";
            }

            await _emailService.SendAsync(
                to: new List<string> { result.CreatorEmail! },
                subject: subject,
                body: body,
                isHtml: true,
                bcc: new List<string> { "calvarez@abril.pe" });
        }
    }
}
