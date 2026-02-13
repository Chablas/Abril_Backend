using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthRepository _authRepository;
        private readonly JwtService _jwtService;

        public AuthController(
            AuthRepository authRepository,
            JwtService jwtService
        )
        {
            _authRepository = authRepository;
            _jwtService = jwtService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDTO dto)
        {
            try
            {
                var user = await _authRepository.ValidateUserAsync(
                dto.Email,
                dto.Password
            );

                if (user == null)
                    return Unauthorized(new { message = "Credenciales inválidas." });

                var accessToken = _jwtService.GenerateToken(user);
                var session = await _authRepository.CreateSessionAsync(user.UserId);

                return Ok(new LoginResponseDTO
                {
                    AccessToken = accessToken,
                    SessionToken = session.Token,
                    ExpiresAt = session.ExpiresAt
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }

        }
    }
}