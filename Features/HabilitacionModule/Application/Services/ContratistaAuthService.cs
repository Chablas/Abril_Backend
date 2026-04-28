using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Auth;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Empresa;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Abril_Backend.Features.Habilitacion.Application.Services
{
    public class ContratistaAuthService : IContratistaAuthService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public ContratistaAuthService(
            IDbContextFactory<AppDbContext> factory,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _factory = factory;
            _configuration = configuration;
            _emailService = emailService;
        }

        public async Task<ContratistaTokenDto> LoginAsync(ContratistaLoginDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var email = dto.Email.Trim();
            var empresa = await ctx.SsEmpresaContratista
                .FirstOrDefaultAsync(e =>
                    (e.EmailAdmin == email || e.EmailSsoma == email || e.EmailGerente == email)
                    && e.Activo);

            if (empresa is null)
                throw new AbrilException("Credenciales incorrectas.", 401);

            if (empresa.PasswordHash == "PENDIENTE_RESET")
                throw new AbrilException(
                    "Tu cuenta no ha sido activada. Revisa tu correo electrónico.", 401);

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, empresa.PasswordHash))
                throw new AbrilException("Credenciales incorrectas.", 401);

            return GenerarTokenDto(empresa);
        }

        public async Task<List<EmpresaSimpleDto>> GetEmpresasParaLoginAsync()
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.SsEmpresaContratista
                .Where(e => e.Activo)
                .OrderBy(e => e.RazonSocial)
                .Select(e => new EmpresaSimpleDto
                {
                    Id = e.Id,
                    RazonSocial = e.RazonSocial,
                    NombreComercial = e.NombreComercial,
                    LogoUrl = e.LogoUrl
                })
                .ToListAsync();
        }

        public async Task SolicitarActivacionAsync(int empresaId)
        {
            using var ctx = _factory.CreateDbContext();

            var empresa = await ctx.SsEmpresaContratista.FirstOrDefaultAsync(e => e.Id == empresaId)
                ?? throw new AbrilException("Empresa no encontrada.", 404);

            var destinatario = empresa.EmailAdmin ?? empresa.EmailSsoma ?? empresa.EmailGerente;
            if (string.IsNullOrWhiteSpace(destinatario))
                throw new AbrilException("La empresa no tiene email registrado.", 400);

            var tokensPrevios = await ctx.SsResetToken
                .Where(t => t.EmpresaId == empresa.Id && !t.Usado)
                .ToListAsync();
            foreach (var t in tokensPrevios) t.Usado = true;
            if (tokensPrevios.Count > 0) await ctx.SaveChangesAsync();

            var token = await CrearTokenAsync(ctx, empresa.Id, TimeSpan.FromHours(48));

            var baseUrl = _configuration["FrontendSettings:SetPasswordUrl"];
            var link = $"{baseUrl}?token={token}&tipo=activacion-contratista";

            var html = $@"<h2>Bienvenido a Abril Grupo Inmobiliario</h2>
<p>Tu empresa <strong>{empresa.RazonSocial}</strong> ha sido registrada.</p>
<p>Haz clic en el siguiente enlace para activar tu cuenta y crear tu contraseña:</p>
<a href='{link}' style='background:#64bc04;color:white;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block;margin:16px 0'>Activar mi cuenta</a>
<p>Este enlace expira en 48 horas.</p>
<p>Si no solicitaste este registro, ignora este correo.</p>";

            await _emailService.SendAsync(
                to: new List<string> { destinatario },
                subject: "Activa tu cuenta en Abril Grupo Inmobiliario",
                body: html,
                isHtml: true);
        }

        public async Task<ContratistaTokenDto> ActivarCuentaAsync(ActivarCuentaDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var token = await BuscarTokenVigenteAsync(ctx, dto.Token)
                ?? throw new AbrilException("Enlace inválido o expirado.", 400);

            if (string.IsNullOrEmpty(dto.Password) || dto.Password.Length < 6)
                throw new AbrilException("La contraseña debe tener al menos 6 caracteres.", 400);

            var empresa = await ctx.SsEmpresaContratista.FirstOrDefaultAsync(e => e.Id == token.EmpresaId)
                ?? throw new AbrilException("Empresa no encontrada.", 404);

            empresa.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            empresa.Activo = true;
            empresa.UpdatedAt = DateTime.UtcNow;

            token.Usado = true;

            await ctx.SaveChangesAsync();

            return GenerarTokenDto(empresa);
        }

        public async Task SolicitarResetPasswordAsync(SolicitarResetDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var email = dto.Email.Trim();
            var empresa = await ctx.SsEmpresaContratista.FirstOrDefaultAsync(e =>
                (e.EmailAdmin == email || e.EmailSsoma == email || e.EmailGerente == email)
                && e.Activo);

            if (empresa is null) return;

            var destinatario = empresa.EmailAdmin ?? empresa.EmailGerente ?? empresa.EmailSsoma;
            if (string.IsNullOrWhiteSpace(destinatario)) return;

            var token = await CrearTokenAsync(ctx, empresa.Id, TimeSpan.FromHours(2));

            var baseUrl = _configuration["FrontendSettings:SetPasswordUrl"];
            var link = $"{baseUrl}?token={token}&tipo=reset-contratista";

            var html = $@"<h2>Restablece tu contraseña</h2>
<p>Hola, recibimos una solicitud para restablecer la contraseña de la cuenta de <strong>{empresa.RazonSocial}</strong>.</p>
<p>Haz clic en el siguiente enlace para crear una nueva contraseña:</p>
<a href='{link}' style='background:#64bc04;color:white;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block;margin:16px 0'>Restablecer contraseña</a>
<p>Este enlace expira en 2 horas.</p>
<p>Si no solicitaste este cambio, ignora este correo.</p>";

            await _emailService.SendAsync(
                to: new List<string> { destinatario },
                subject: "Restablece tu contraseña - Abril Grupo Inmobiliario",
                body: html,
                isHtml: true);
        }

        public async Task ResetPasswordAsync(ResetPasswordDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var token = await BuscarTokenVigenteAsync(ctx, dto.Token)
                ?? throw new AbrilException("Enlace inválido o expirado.", 400);

            if (string.IsNullOrEmpty(dto.NuevaPassword) || dto.NuevaPassword.Length < 6)
                throw new AbrilException("La contraseña debe tener al menos 6 caracteres.", 400);

            var empresa = await ctx.SsEmpresaContratista.FirstOrDefaultAsync(e => e.Id == token.EmpresaId)
                ?? throw new AbrilException("Empresa no encontrada.", 404);

            empresa.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NuevaPassword);
            empresa.UpdatedAt = DateTime.UtcNow;

            token.Usado = true;

            await ctx.SaveChangesAsync();
        }

        public async Task CambiarPasswordAsync(int empresaId, CambiarPasswordDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var empresa = await ctx.SsEmpresaContratista.FirstOrDefaultAsync(e => e.Id == empresaId)
                ?? throw new AbrilException("Empresa no encontrada.", 404);

            if (!BCrypt.Net.BCrypt.Verify(dto.PasswordActual, empresa.PasswordHash))
                throw new AbrilException("Contraseña actual incorrecta.", 400);

            empresa.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.PasswordNuevo);
            empresa.UpdatedAt = DateTime.UtcNow;

            await ctx.SaveChangesAsync();
        }

        private static async Task<string> CrearTokenAsync(AppDbContext ctx, int empresaId, TimeSpan duracion)
        {
            var raw = Guid.NewGuid().ToString("N");
            ctx.SsResetToken.Add(new SsResetToken
            {
                EmpresaId = empresaId,
                Token = raw,
                ExpiraAt = DateTime.UtcNow.Add(duracion),
                Usado = false,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
            return raw;
        }

        private static Task<SsResetToken?> BuscarTokenVigenteAsync(AppDbContext ctx, string token)
            => ctx.SsResetToken.FirstOrDefaultAsync(t =>
                t.Token == token && !t.Usado && t.ExpiraAt > DateTime.UtcNow);

        private ContratistaTokenDto GenerarTokenDto(SsEmpresaContratista empresa)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, empresa.Id.ToString()),
                new Claim(ClaimTypes.Name, empresa.RazonSocial),
                new Claim(ClaimTypes.Role, "CONTRATISTA"),
                new Claim("empresaId", empresa.Id.ToString()),
                new Claim("tipo", "CONTRATISTA"),
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

            return new ContratistaTokenDto
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                EmpresaId = empresa.Id,
                RazonSocial = empresa.RazonSocial,
                Tipo = empresa.Tipo
            };
        }
    }
}
