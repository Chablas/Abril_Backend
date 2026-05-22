using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Auth;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Empresa;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
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
        private readonly ILogger<ContratistaAuthService> _logger;

        public ContratistaAuthService(
            IDbContextFactory<AppDbContext> factory,
            IConfiguration configuration,
            IEmailService emailService,
            ILogger<ContratistaAuthService> logger)
        {
            _factory = factory;
            _configuration = configuration;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<ContratistaTokenDto> LoginAsync(ContratistaLoginDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var email = dto.Email.Trim().ToLower();

            var user = await ctx.User
                .FirstOrDefaultAsync(u => u.Email == email && u.Active && u.State);

            if (user is null || string.IsNullOrEmpty(user.Password))
                throw new AbrilException("Credenciales incorrectas.", 401);

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
                throw new AbrilException("Credenciales incorrectas.", 401);

            var contractorEmail = await ctx.ContractorEmail
                .Include(ce => ce.Contractor)
                    .ThenInclude(c => c.Contributor)
                .FirstOrDefaultAsync(ce => ce.UserId == user.UserId && ce.Active && ce.State);

            if (contractorEmail is null)
                throw new AbrilException("El usuario no tiene empresa contratista asociada.", 403);

            var allowedFeatures = await GetContratistasFeatureKeysAsync(ctx);

            var contractor = contractorEmail.Contractor;
            var contributor = contractor.Contributor;

            return GenerarTokenDto(user, contractor, contributor, allowedFeatures);
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

            var destinatario = (empresa.EmailAdmin ?? empresa.EmailSsoma ?? empresa.EmailGerente)?.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(destinatario))
                throw new AbrilException("La empresa no tiene email registrado.", 400);

            var user = await ctx.User.FirstOrDefaultAsync(u => u.Email == destinatario)
                ?? throw new AbrilException("No existe un usuario registrado para este email.", 400);

            var tokensPrevios = await ctx.SsResetToken
                .Where(t => t.UserId == user.UserId && !t.Usado)
                .ToListAsync();
            foreach (var t in tokensPrevios) t.Usado = true;
            if (tokensPrevios.Count > 0) await ctx.SaveChangesAsync();

            var token = await CrearTokenAsync(ctx, user.UserId, TimeSpan.FromHours(48));

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

            var emailBuscar = (empresa.EmailAdmin ?? empresa.EmailSsoma ?? empresa.EmailGerente)!.Trim().ToLower();
            var user = await ctx.User.FirstOrDefaultAsync(u => u.Email == emailBuscar && u.Active && u.State)
                ?? throw new AbrilException("Usuario no encontrado en el sistema.", 404);

            // Si el registro se creó antes de que UserId existiera, enlazarlo ahora.
            var huerfano = await ctx.ContractorEmail
                .FirstOrDefaultAsync(ce => ce.Email.ToLower() == emailBuscar && ce.UserId == null && ce.Active && ce.State);
            if (huerfano != null)
            {
                huerfano.UserId = user.UserId;
                await ctx.SaveChangesAsync();
            }

            var contractorEmail = await ctx.ContractorEmail
                .Include(ce => ce.Contractor)
                    .ThenInclude(c => c.Contributor)
                .FirstOrDefaultAsync(ce => ce.UserId == user.UserId && ce.Active && ce.State)
                ?? throw new AbrilException("El usuario no tiene empresa contratista asociada.", 403);

            var allowedFeatures = await GetContratistasFeatureKeysAsync(ctx);

            return GenerarTokenDto(user, contractorEmail.Contractor, contractorEmail.Contractor.Contributor, allowedFeatures);
        }

        public async Task SolicitarResetPasswordAsync(SolicitarResetDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var email = dto.Email.Trim().ToLower();
            var user = await ctx.User.FirstOrDefaultAsync(u => u.Email == email && u.Active && u.State);
            if (user is null) return;

            var esContratista = await ctx.ContractorEmail
                .AnyAsync(ce => ce.UserId == user.UserId && ce.Active && ce.State);
            if (!esContratista) return;

            var tokensPrevios = await ctx.SsResetToken
                .Where(t => t.UserId == user.UserId && !t.Usado)
                .ToListAsync();
            foreach (var t in tokensPrevios) t.Usado = true;
            if (tokensPrevios.Count > 0) await ctx.SaveChangesAsync();

            var token = await CrearTokenAsync(ctx, user.UserId, TimeSpan.FromHours(2));

            var baseUrl = _configuration["FrontendSettings:SetPasswordUrl"];
            var link = $"{baseUrl}?token={token}&tipo=reset-contratista";

            var html = $@"<h2>Restablece tu contraseña</h2>
<p>Hola, recibimos una solicitud para restablecer tu contraseña.</p>
<p>Haz clic en el siguiente enlace para crear una nueva contraseña:</p>
<a href='{link}' style='background:#64bc04;color:white;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block;margin:16px 0'>Restablecer contraseña</a>
<p>Este enlace expira en 2 horas.</p>
<p>Si no solicitaste este cambio, ignora este correo.</p>";

            await _emailService.SendAsync(
                to: new List<string> { email },
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

            var user = await ctx.User.FirstOrDefaultAsync(u => u.UserId == token.UserId)
                ?? throw new AbrilException("Usuario no encontrado.", 404);

            user.Password = BCrypt.Net.BCrypt.HashPassword(dto.NuevaPassword);
            user.UpdatedDateTime = DateTime.UtcNow;
            token.Usado = true;

            await ctx.SaveChangesAsync();
        }

        public async Task CambiarPasswordAsync(int userId, CambiarPasswordDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var user = await ctx.User.FirstOrDefaultAsync(u => u.UserId == userId)
                ?? throw new AbrilException("Usuario no encontrado.", 404);

            if (!BCrypt.Net.BCrypt.Verify(dto.PasswordActual, user.Password))
                throw new AbrilException("Contraseña actual incorrecta.", 400);

            user.Password = BCrypt.Net.BCrypt.HashPassword(dto.PasswordNuevo);
            user.UpdatedDateTime = DateTime.UtcNow;

            await ctx.SaveChangesAsync();
        }


        public async Task<ValidarMigracionResultDto> ValidarMigracionAsync(ValidarMigracionDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var contributor = await ctx.Contributor
                .FirstOrDefaultAsync(c => c.ContributorRuc == dto.Ruc
                    && c.SpPasswordTemp == dto.SpPassword
                    && c.Active);

            if (contributor is null)
                throw new AbrilException("RUC o contraseña temporal incorrectos.", 401);

            return new ValidarMigracionResultDto
            {
                NombreComercial = contributor.ContributorNombreComercial ?? contributor.ContributorName,
                RazonSocial = contributor.ContributorName
            };
        }

        public async Task ActivarMigracionAsync(ActivarMigracionDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var contributor = await ctx.Contributor
                .FirstOrDefaultAsync(c => c.ContributorRuc == dto.Ruc
                    && c.SpPasswordTemp == dto.SpPassword
                    && c.Active)
                ?? throw new AbrilException("RUC o contraseña temporal incorrectos.", 401);

            var contractor = await ctx.Contractor
                .FirstOrDefaultAsync(c => c.ContributorId == contributor.ContributorId && c.Active)
                ?? throw new AbrilException("No se encontró empresa contratista para este RUC.", 404);

            var existingUser = await ctx.User.FirstOrDefaultAsync(u => u.Email == dto.Email);
            User user;
            var hasher = new PasswordHasher<User>();

            if (existingUser != null)
            {
                user = existingUser;
                user.Password = hasher.HashPassword(user, dto.Password);
                user.UpdatedDateTime = DateTime.UtcNow;
            }
            else
            {
                user = new User
                {
                    Email = dto.Email,
                    EmailConfirmed = true,
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTime.UtcNow
                };
                user.Password = hasher.HashPassword(user, dto.Password);
                ctx.User.Add(user);
            }

            await ctx.SaveChangesAsync();

            var contractorUserExists = await ctx.ContractorUser
                .AnyAsync(cu => cu.ContractorId == contractor.ContractorId && cu.UserId == user.UserId && cu.Active);
            if (!contractorUserExists)
                ctx.ContractorUser.Add(new ContractorUser
                {
                    ContractorId = contractor.ContractorId,
                    UserId = user.UserId,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    Active = true,
                    State = true
                });

            var roleExists = await ctx.UserRole
                .AnyAsync(ur => ur.UserId == user.UserId && ur.RoleId == 11 && ur.Active);
            if (!roleExists)
                ctx.UserRole.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = 11,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = user.UserId,
                    Active = true,
                    State = true
                });

            contributor.SpPasswordTemp = null;

            await ctx.SaveChangesAsync();
        }

        private static async Task<string> CrearTokenAsync(AppDbContext ctx, int userId, TimeSpan duracion)
        {
            var raw = Guid.NewGuid().ToString("N");
            ctx.SsResetToken.Add(new SsResetToken
            {
                UserId = userId,
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

        private static Task<List<string>> GetContratistasFeatureKeysAsync(AppDbContext ctx)
            => ctx.Database.SqlQuery<string>($"""
                SELECT f.feature_key
                FROM feature f
                JOIN role_feature rf ON rf.feature_id = f.feature_id
                JOIN role r ON r.role_id = rf.role_id
                WHERE r.role_description = 'CONTRATISTA'
                """)
                .ToListAsync();

        private ContratistaTokenDto GenerarTokenDto(User user, Contractor contractor, Contributor contributor, List<string> allowedFeatures)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, contributor.ContributorName),
                new Claim(ClaimTypes.Role, "CONTRATISTA"),
                new Claim("empresaId", contractor.ContributorId.ToString()),
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
                EmpresaId = contractor.ContributorId,
                RazonSocial = contributor.ContributorName,
                Tipo = "CONTRATISTA",
                AllowedFeatures = allowedFeatures
            };
        }
    }
}
