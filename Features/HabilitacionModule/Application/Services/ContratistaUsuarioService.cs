using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.ContratistaUsuarios;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Abril_Backend.Features.Habilitacion.Application.Services
{
    public class ContratistaUsuarioService : IContratistaUsuarioService
    {
        private readonly IContratistaUsuarioRepository _repo;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IEmailService _emailService;

        public ContratistaUsuarioService(
            IContratistaUsuarioRepository repo,
            IDbContextFactory<AppDbContext> factory,
            IEmailService emailService)
        {
            _repo = repo;
            _factory = factory;
            _emailService = emailService;
        }

        public Task<List<ContratistaUsuarioListDto>> GetUsuariosAsync(int contractorId)
            => _repo.GetUsuariosAsync(contractorId);

        public async Task InvitarUsuarioAsync(int contractorId, ContratistaUsuarioCreateDto dto, int creadoPor)
        {
            if (dto.SystemRoleId != 11 && dto.SystemRoleId != 49)
                throw new AbrilException("SystemRoleId inválido. Los valores aceptados son 11 (CONTRATISTA) o 49 (SERVICIO DE VIGILANCIA).", 400);

            var rolNombre = dto.RolNombre.Trim().ToUpper();
            if (rolNombre != "ADMIN" && rolNombre != "GESTOR")
                throw new AbrilException("Rol inválido. Use ADMIN o GESTOR.", 400);

            var scope = dto.Scope?.Trim().ToUpper() ?? "TODOS";
            if (scope == "POR_PROYECTO" && (dto.ProyectoIds is null || dto.ProyectoIds.Count == 0))
                throw new AbrilException("Debe seleccionar al menos un proyecto.", 400);

            using var ctx = _factory.CreateDbContext();

            var email = dto.Email.Trim().ToLower();

            var rol = await ctx.SsContratistaRoles.FirstOrDefaultAsync(r => r.Nombre == rolNombre)
                ?? throw new AbrilException("Rol no encontrado en el sistema.", 500);

            string? passwordTemporal = null;
            var user = await ctx.User.FirstOrDefaultAsync(u => u.Email == email);

            if (user is null)
            {
                passwordTemporal = GenerarPasswordTemporal();
                user = new User
                {
                    Email = email,
                    Password = BCrypt.Net.BCrypt.HashPassword(passwordTemporal),
                    EmailConfirmed = true,
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTime.UtcNow
                };
                ctx.User.Add(user);
                await ctx.SaveChangesAsync();
            }

            var yaInvitado = await ctx.SsContratistaUsuarios
                .AnyAsync(u => u.ContractorId == contractorId && u.UserId == user.UserId);
            if (yaInvitado)
                throw new AbrilException("Este usuario ya fue invitado a esta empresa.", 400);

            var tieneRol = await ctx.UserRole
                .AnyAsync(ur => ur.UserId == user.UserId && ur.RoleId == dto.SystemRoleId && ur.Active && ur.State);
            if (!tieneRol)
            {
                ctx.UserRole.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = dto.SystemRoleId,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = creadoPor,
                    Active = true,
                    State = true
                });
                await ctx.SaveChangesAsync();
            }

            var existeContractorEmail = await ctx.ContractorEmail
                .AnyAsync(ce => ce.UserId == user.UserId && ce.ContractorId == contractorId);
            if (!existeContractorEmail)
            {
                ctx.ContractorEmail.Add(new ContractorEmail
                {
                    Email = dto.Email.Trim().ToLower(),
                    ContractorId = contractorId,
                    UserId = user.UserId,
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    UpdatedDateTime = DateTimeOffset.UtcNow,
                    CreatedUserId = creadoPor,
                    UpdatedUserId = creadoPor
                });
                await ctx.SaveChangesAsync();
            }

            var entity = new SsContratistaUsuario
            {
                ContractorId = contractorId,
                UserId = user.UserId,
                RolId = rol.Id,
                SystemRoleId = dto.SystemRoleId,
                Scope = scope,
                Activo = true,
                CreadoEn = DateTime.UtcNow,
                CreadoPor = creadoPor
            };

            await _repo.InvitarUsuarioAsync(entity, scope == "POR_PROYECTO" ? dto.ProyectoIds : null);

            if (passwordTemporal is not null)
            {
                var html = $@"<p>Has sido invitado a la plataforma <strong>Abril - CASEVIP</strong>.</p>
<p>Tu usuario es: <strong>{email}</strong></p>
<p>Tu contraseña temporal es: <strong>{passwordTemporal}</strong></p>
<p>Ingresa en <a href='https://abril.com'>https://abril.com</a></p>
<p>Te recomendamos cambiar tu contraseña después del primer ingreso.</p>";

                await _emailService.SendAsync(
                    to: new List<string> { email },
                    subject: "Invitación a plataforma Abril - CASEVIP",
                    body: html,
                    isHtml: true);
            }
        }

        public async Task ActualizarUsuarioAsync(int id, int contractorId, ContratistaUsuarioUpdateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var existing = await ctx.SsContratistaUsuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Id == id && u.ContractorId == contractorId)
                ?? throw new AbrilException("Usuario no encontrado.", 404);

            int rolId = existing.RolId;
            if (!string.IsNullOrWhiteSpace(dto.RolNombre))
            {
                var rolNombre = dto.RolNombre.Trim().ToUpper();
                if (rolNombre != "ADMIN" && rolNombre != "GESTOR")
                    throw new AbrilException("Rol inválido. Use ADMIN o GESTOR.", 400);

                var rol = await ctx.SsContratistaRoles.FirstOrDefaultAsync(r => r.Nombre == rolNombre)
                    ?? throw new AbrilException("Rol no encontrado en el sistema.", 500);
                rolId = rol.Id;
            }

            var scope = dto.Scope?.Trim().ToUpper() ?? existing.Scope;
            if (scope == "POR_PROYECTO" && (dto.ProyectoIds is null || dto.ProyectoIds.Count == 0))
                throw new AbrilException("Debe seleccionar al menos un proyecto.", 400);

            var entityUpdate = new SsContratistaUsuario
            {
                ContractorId = contractorId,
                RolId = rolId,
                Scope = scope,
                Activo = dto.Activo ?? existing.Activo
            };

            var proyectosActualizar = scope == "POR_PROYECTO" ? dto.ProyectoIds : (scope == "TODOS" ? new List<int>() : null);
            await _repo.ActualizarUsuarioAsync(id, entityUpdate, proyectosActualizar);
        }

        public Task DesactivarUsuarioAsync(int id, int contractorId)
            => _repo.DesactivarUsuarioAsync(id, contractorId);

        private static string GenerarPasswordTemporal()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
            return RandomNumberGenerator.GetString(chars, 8);
        }
    }
}
