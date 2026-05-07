namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemCategoryFeature.Application.Dtos
{
    public class WorkItemCategoryDto
    {
        public int WorkItemCategoryId { get; set; }
        public string WorkItemCategoryDescription { get; set; } = null!;
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public string? InstructivosFolderId { get; set; }
        public string? InstructivosFolderName { get; set; }
        public int? InstructivosSyncStatus { get; set; }
        public DateTime? InstructivosSyncedAt { get; set; }
    }

    public class WorkItemCategorySyncResultDto
    {
        public int Total { get; set; }
        public int Matched { get; set; }
        public int Unmatched { get; set; }
        public int Created { get; set; }
        public List<string> UnmatchedDescriptions { get; set; } = [];
        public List<string> CreatedDescriptions { get; set; } = [];
    }
}
