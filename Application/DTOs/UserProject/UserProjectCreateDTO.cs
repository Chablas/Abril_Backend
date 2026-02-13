namespace Abril_Backend.Application.DTOs {
    public class UserProjectCreateDTO {
        public int UserId { get; set; }
        public int ProjectId { get; set; }
        public bool Active { get; set; }
    }
}