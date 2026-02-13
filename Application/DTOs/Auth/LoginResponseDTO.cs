namespace Abril_Backend.Application.DTOs
{
    public class LoginResponseDTO
    {
        public string AccessToken { get; set; } = null!;
        public string SessionToken { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
    }
}