using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.ContratistaUsuarios;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Application.Services
{
    public class ContratistaUsuarioService : IContratistaUsuarioService
    {
        private readonly IContratistaUsuarioRepository _repo;
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ContratistaUsuarioService(
            IContratistaUsuarioRepository repo,
            IDbContextFactory<AppDbContext> factory)
        {
            _repo = repo;
            _factory = factory;
        }

        public Task<List<ContratistaUsuarioListDto>> GetUsuariosAsync(int contractorId)
            => _repo.GetUsuariosAsync(contractorId);

        public async Task InvitarUsuarioAsync(int contractorId, ContratistaUsuarioCreateDto dto, int creadoPor)
        {
            using var ctx = _factory.CreateDbContext();

            var email = dto.Email.Trim().ToLower();

            var user = await ctx.User.FirstOrDefaultAsync(u => u.Email == email)
                ?? throw new AbrilException("No existe un usuario registrado con ese email.", 404);

            var yaInvitado = await ctx.SsContratistaUsuarios
                .AnyAsync(u => u.ContractorId == contractorId && u.UserId == user.UserId);
            if (yaInvitado)
                throw new AbrilException("Este usuario ya fue invitado a esta empresa.", 400);

            var rolNombre = dto.RolNombre.Trim().ToUpper();
            if (rolNombre != "ADMIN" && rolNombre != "GESTOR")
                throw new AbrilException("Rol inválido. Use ADMIN o GESTOR.", 400);

            var rol = await ctx.SsContratistaRoles.FirstOrDefaultAsync(r => r.Nombre == rolNombre)
                ?? throw new AbrilException("Rol no encontrado en el sistema.", 500);

            var tieneRolContratista = await ctx.UserRole
                .AnyAsync(ur => ur.UserId == user.UserId && ur.RoleId == 11 && ur.Active);
            if (!tieneRolContratista)
                throw new AbrilException("El usuario debe tener rol CONTRATISTA en el sistema.", 400);

            var scope = dto.Scope?.Trim().ToUpper() ?? "TODOS";
            if (scope == "POR_PROYECTO" && (dto.ProyectoIds is null || dto.ProyectoIds.Count == 0))
                throw new AbrilException("Debe seleccionar al menos un proyecto.", 400);

            var entity = new SsContratistaUsuario
            {
                ContractorId = contractorId,
                UserId = user.UserId,
                RolId = rol.Id,
                Scope = scope,
                Activo = true,
                CreadoEn = DateTime.UtcNow,
                CreadoPor = creadoPor
            };

            await _repo.InvitarUsuarioAsync(entity, scope == "POR_PROYECTO" ? dto.ProyectoIds : null);
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
    }
}
