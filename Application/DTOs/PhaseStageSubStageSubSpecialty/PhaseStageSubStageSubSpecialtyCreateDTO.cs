namespace Abril_Backend.Application.DTOs
{
    public class PhaseStageSubStageSubSpecialtyCreateDTO
    {
        public int PhaseId { get; set; }
        public int StageId { get; set; }
        public int? LayerId { get; set; }
        public int? SubStageId { get; set; }
        public int? SubSpecialtyId { get; set; }
        public int CreatedUserId { get; set; }
        public bool Active { get; set; }
    }
}