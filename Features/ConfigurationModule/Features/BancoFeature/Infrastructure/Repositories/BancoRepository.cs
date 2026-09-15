using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Application.Dtos;
using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Infrastructure.Repositories
{
    public class BancoRepository : IBancoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public BancoRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<List<BancoDto>> List()
        {
            using var ctx = _factory.CreateDbContext();

            // El conteo de razones sociales va como subconsulta correlacionada (un solo SELECT con
            // su LATERAL) en vez de traer los contributors y contarlos acá: son 300+ filas que la
            // pantalla no usa para nada más.
            return await ctx.Banco
                .Where(b => b.State)
                .OrderBy(b => b.Orden).ThenBy(b => b.Nombre)
                .Select(b => new BancoDto
                {
                    Id     = b.BancoId,
                    Codigo = b.Codigo,
                    Nombre = b.Nombre,
                    Orden  = b.Orden,
                    Activo = b.Active,
                    RazonesSociales = ctx.Contributor.Count(c => c.BancoId == b.BancoId && c.State),
                })
                .ToListAsync();
        }

        public async Task<BancoDto> Create(BancoUpsertDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var codigo = dto.Codigo!.Trim().ToUpperInvariant();
            var nombre = dto.Nombre!.Trim();

            await ValidarDuplicados(ctx, codigo, nombre, bancoId: null);

            var entity = new Banco
            {
                Codigo          = codigo,
                Nombre          = nombre,
                Orden           = dto.Orden,
                Active          = dto.Activo,
                State           = true,
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId   = userId,
            };

            ctx.Banco.Add(entity);
            await ctx.SaveChangesAsync();

            return new BancoDto
            {
                Id = entity.BancoId, Codigo = entity.Codigo, Nombre = entity.Nombre,
                Orden = entity.Orden, Activo = entity.Active, RazonesSociales = 0,
            };
        }

        public async Task<BancoDto> Update(int bancoId, BancoUpsertDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.Banco.FirstOrDefaultAsync(b => b.BancoId == bancoId && b.State)
                ?? throw new AbrilException("El banco indicado no existe o fue eliminado.", 404);

            var nombre = dto.Nombre!.Trim();
            await ValidarDuplicados(ctx, codigo: null, nombre: nombre, bancoId: bancoId);

            // El código NO se edita: es la clave con la que la migración y cualquier script futuro
            // reconocen al banco. Lo que se corrige es el nombre, que es lo que se ve.
            entity.Nombre            = nombre;
            entity.Orden             = dto.Orden;
            entity.Active            = dto.Activo;
            entity.UpdatedDateTime   = DateTimeOffset.UtcNow;
            entity.UpdatedUserId     = userId;

            await ctx.SaveChangesAsync();

            return new BancoDto
            {
                Id = entity.BancoId, Codigo = entity.Codigo, Nombre = entity.Nombre,
                Orden = entity.Orden, Activo = entity.Active,
                RazonesSociales = await ctx.Contributor.CountAsync(c => c.BancoId == bancoId && c.State),
            };
        }

        public async Task Delete(int bancoId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.Banco.FirstOrDefaultAsync(b => b.BancoId == bancoId && b.State)
                ?? throw new AbrilException("El banco indicado no existe o fue eliminado.", 404);

            // Un banco en uso no se elimina: el formulario de bienvenida de cualquier colaborador
            // de esa razón social se quedaría sin la pregunta de la cuenta sueldo.
            var enUso = await ctx.Contributor.CountAsync(c => c.BancoId == bancoId && c.State);
            if (enUso > 0)
                throw new AbrilException(
                    $"No se puede eliminar: {enUso} razón(es) social(es) trabajan con este banco. "
                    + "Cámbiales el banco o desactívalo en vez de eliminarlo.", 409);

            entity.State           = false;
            entity.Active          = false;
            entity.UpdatedDateTime = DateTimeOffset.UtcNow;
            entity.UpdatedUserId   = userId;

            await ctx.SaveChangesAsync();
        }

        /// <summary>
        /// Código y nombre son únicos entre los bancos vivos. La base lo garantiza con dos índices
        /// parciales; acá se revisa antes para devolver un mensaje que se entienda en vez de un 500
        /// con la violación del índice.
        /// </summary>
        private static async Task ValidarDuplicados(AppDbContext ctx, string? codigo, string nombre, int? bancoId)
        {
            if (codigo != null && await ctx.Banco.AnyAsync(b => b.State && b.Codigo == codigo))
                throw new AbrilException($"Ya existe un banco con el código «{codigo}».", 409);

            var nombreNormalizado = nombre.Trim().ToLower();
            var repetido = await ctx.Banco.AnyAsync(b =>
                b.State && b.BancoId != (bancoId ?? 0) && b.Nombre.Trim().ToLower() == nombreNormalizado);

            if (repetido)
                throw new AbrilException($"Ya existe un banco llamado «{nombre}».", 409);
        }
    }
}
