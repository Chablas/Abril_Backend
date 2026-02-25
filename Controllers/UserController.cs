using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Application.DTOs;
using System.Security.Cryptography;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Abril_Backend.Controllers
{

    [ApiController]
    [Route("api/v1/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserRegistrationTokenRepository _tokenRepository;
        private readonly FrontendSettings _frontendSettings;
        private readonly IEmailService _emailService;
        private readonly UserRepository _userRepository;
        public UserController(UserRegistrationTokenRepository tokenRepository, UserRepository userRepository,
            IEmailService emailService, IOptions<FrontendSettings> frontendSettings)
        {
            _tokenRepository = tokenRepository;
            _userRepository = userRepository;
            _emailService = emailService;
            _frontendSettings = frontendSettings.Value;
        }

        [Authorize]
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var result = await _userRepository.GetPagedFactory(page, pageSize);

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create(UserCreateDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                var user = await _userRepository.Create(dto);

                if (user == null)
                    return BadRequest(new { message = "La persona ya tiene un usuario registrado." });

                var token = GenerateToken();

                await _tokenRepository.CreateAsync(new UserRegistrationToken
                {
                    UserId = user.UserId,
                    Token = token,
                    CreatedDateTime = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    Used = false
                });

                var link = $"https://abril-frontend.onrender.com/auth/complete-registration?token={token}";

                await _emailService.SendAsync(
                    to: new List<string> { user.Person.Email },
                    subject: "Completa tu registro",
                    body: $"Hola,\n\nCompleta tu registro aquí:\n{link}\n\nEste enlace expirará en 24 horas.",
                    isHtml: false
                );

                return Ok(new { message = "Usuario creado y correo enviado." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost("complete-registration")]
        public async Task<IActionResult> CompleteRegistration(CompleteRegistrationDTO dto)
        {
            try
            {
                var tokenEntity = await _tokenRepository.GetValidTokenAsync(dto.Token);

                if (tokenEntity == null)
                    return BadRequest(new { message = "Token inválido o expirado." });

                await _userRepository.SetPassword(
                    tokenEntity.UserId,
                    dto.Password
                );

                tokenEntity.Used = true;
                await _tokenRepository.SaveAsync();

                return Ok(new { message = "Cuenta activada correctamente." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        private string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}