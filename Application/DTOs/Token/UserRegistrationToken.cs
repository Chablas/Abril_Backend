namespace Abril_Backend.Application.DTOs
{
    public class UserRegistrationTokenDTO
    {
        public int UserId { get; set; }
        public string Token { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool Used { get; set; }
    }
}