namespace Abril_Backend.Application.DTOs
{
    public class PhaseStageSubStageSubSpecialtyFlatDTO
    {
        public int LinkId { get; set; }

        public int PhaseId { get; set; }
        public string PhaseDescription { get; set; } = null!;

        public int? StageId { get; set; }
        public string? StageDescription { get; set; }

        public int? LayerId { get; set; }
        public string? LayerDescription { get; set; }

        public int? SubStageId { get; set; }
        public string? SubStageDescription { get; set; }

        public int? SubSpecialtyId { get; set; }
        public string? SubSpecialtyDescription { get; set; }
    }
}