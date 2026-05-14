namespace Abril_Backend.Infrastructure.Models
{
    public class PsssTemplate
    {
        public int PsssTemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool State { get; set; }
        public bool Active { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
    }
}
