using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Auth;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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
        private readonly IEmailService _emailService;
        private readonly ILogger<ClinicaAuthController> _logger;

        public ClinicaAuthController(
            IDbContextFactory<AppDbContext> factory,
            IConfiguration configuration,
            IEmailService emailService,
            ILogger<ClinicaAuthController> logger)
        {
            _factory = factory;
            _configuration = configuration;
            _emailService = emailService;
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

        [HttpPost("solicitar-activacion")]
        public async Task<IActionResult> SolicitarActivacion([FromBody] ClinicaEmailDto dto)
        {
            try
            {
                using var ctx = _factory.CreateDbContext();

                var email = dto.Email.Trim();
                var clinica = await ctx.SsClinica
                    .FirstOrDefaultAsync(c => c.Email == email);

                if (clinica is null)
                    throw new AbrilException("Clínica no encontrada.", 404);

                var tokenRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

                ctx.SsClinicaResetToken.Add(new SsClinicaResetToken
                {
                    ClinicaId = clinica.Id,
                    Token = tokenRaw,
                    ExpiraEn = DateTime.UtcNow.AddHours(24),
                    Usado = false,
                    CreatedAt = DateTime.UtcNow
                });
                await ctx.SaveChangesAsync();

                var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/');
                var link = $"{frontendUrl}/clinica/activar?token={Uri.EscapeDataString(tokenRaw)}";

                var html = $@"<h2>Bienvenido a Abril Grupo Inmobiliario</h2>
<p>La clínica <strong>{clinica.Nombre}</strong> ha sido registrada en el sistema.</p>
<p>Haz clic en el siguiente enlace para establecer tu contraseña:</p>
<a href='{link}' style='background:#64bc04;color:white;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block;margin:16px 0'>Activar cuenta</a>
<p>Este enlace expira en 24 horas.</p>
<p>Si no solicitaste este registro, ignora este correo.</p>";

                await _emailService.SendAsync(
                    to: new List<string> { email },
                    subject: "Activa tu cuenta de clínica en Abril Grupo Inmobiliario",
                    body: html,
                    isHtml: true);

                return Ok(new { message = "Se envió el enlace de activación al correo registrado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ClinicaAuthController.SolicitarActivacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("activar")]
        public async Task<IActionResult> Activar([FromBody] ClinicaActivarDto dto)
        {
            try
            {
                using var ctx = _factory.CreateDbContext();

                var resetToken = await ctx.SsClinicaResetToken
                    .FirstOrDefaultAsync(t => t.Token == dto.Token && !t.Usado && t.ExpiraEn > DateTime.UtcNow);

                if (resetToken is null)
                    throw new AbrilException("Token inválido o expirado.", 400);

                var clinica = await ctx.SsClinica.FirstOrDefaultAsync(c => c.Id == resetToken.ClinicaId)
                    ?? throw new AbrilException("Clínica no encontrada.", 404);

                clinica.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                resetToken.Usado = true;

                await ctx.SaveChangesAsync();

                return Ok(new { message = "Contraseña establecida correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ClinicaAuthController.Activar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
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
