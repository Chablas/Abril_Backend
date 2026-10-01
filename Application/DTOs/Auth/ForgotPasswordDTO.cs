namespace Abril_Backend.Application.DTOs
{
    public class ForgotPasswordDTO
    {
        public int UserId { get; set; }
    }

    public class ForgotPasswordByEmailDTO
    {
        public string Email { get; set; } = string.Empty;
    }
}
