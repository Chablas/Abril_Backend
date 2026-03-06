namespace Abril_Backend.Application.DTOs
{
    public class UserWithoutMilestoneDTO
    {
        public int UserId { get; set; }
        public string? UserFullName { get; set; }
        public string? Email {get;set;}
        public List<ProjectSimpleDTO>? Projects { get; set; }
    }
}