namespace Abril_Backend.Application.DTOs
{
    public class UserWithoutLessonsDTO
    {
        public int UserId { get; set; }
        public string? UserFullName { get; set; }
        public string? Email {get;set;}
        public List<ProjectFilterDTO>? Projects { get; set; }
    }
}