using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Auth;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional/auth")]
    [AllowAnonymous]
    public class ClinicaAuthController : ControllerBase
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ClinicaAuthController> _logger;

        public ClinicaAuthController(
            IDbContextFactory<AppDbContext> factory,
            IConfiguration configuration,
            ILogger<ClinicaAuthController> logger)
        {
            _factory = factory;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ClinicaLoginDto dto)
        {
            try
            {
                using var ctx = _factory.CreateDbContext();

                var email = dto.Email.Trim();
                var clinica = await ctx.SsClinica
                    .FirstOrDefaultAsync(c => c.Email == email && c.Activo);

                if (clinica is null)
                    throw new AbrilException("Credenciales inválidas.", 401);

                if (clinica.PasswordHash == "PENDIENTE_RESET")
                    throw new AbrilException("Credenciales inválidas.", 401);

                if (!BCrypt.Net.BCrypt.Verify(dto.Password, clinica.PasswordHash))
                    throw new AbrilException("Credenciales inválidas.", 401);

                return Ok(GenerarTokenDto(clinica));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ClinicaAuthController.Login"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        private ClinicaTokenDto GenerarTokenDto(SsClinica clinica)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, clinica.Id.ToString()),
                new Claim(ClaimTypes.Name, clinica.Nombre),
                new Claim(ClaimTypes.Role, "CLINICA"),
                new Claim("clinicaId", clinica.Id.ToString()),
                new Claim("tipo", "CLINICA"),
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);

            return new ClinicaTokenDto
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ClinicaId = clinica.Id,
                Nombre = clinica.Nombre,
                Tipo = "CLINICA"
            };
        }
    }
}
