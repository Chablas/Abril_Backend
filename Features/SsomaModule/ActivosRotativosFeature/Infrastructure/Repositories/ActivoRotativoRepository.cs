using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Repositories
{
    public class ActivoRotativoRepository : IActivoRotativoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ActivoRotativoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // ─────────────────────────────────────────────────────────────────
        // CATEGORÍAS
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<ActivoRotativoCategoriaDto>> GetCategoriasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsActivoRotativoCategoria
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Nombre)
                .Select(c => new ActivoRotativoCategoriaDto
                {
                    Id           = c.Id,
                    Nombre       = c.Nombre,
                    Orden        = c.Orden,
                    Activo       = c.Activo,
                    TotalActivos = c.Activos.Count(a => a.Activo)
                })
                .ToListAsync();
        }

        public async Task<SsActivoRotativoCategoria> CreateCategoriaAsync(ActivoRotativoCategoriaUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;
            var entity = new SsActivoRotativoCategoria
            {
                Nombre    = dto.Nombre,
                Orden     = dto.Orden,
                Activo    = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            ctx.SsActivoRotativoCategoria.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateCategoriaAsync(int categoriaId, ActivoRotativoCategoriaUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsActivoRotativoCategoria.FindAsync(categoriaId)
                ?? throw new KeyNotFoundException($"Categoría {categoriaId} no encontrada.");

            entity.Nombre    = dto.Nombre;
            entity.Orden     = dto.Orden;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // ACTIVOS
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<ActivoRotativoListDto>> GetActivosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsActivoRotativo
                .Include(a => a.Categoria)
                .Include(a => a.ProyectoActual)
                .Where(a => a.Activo)
                .OrderBy(a => a.Categoria!.Orden)
                .ThenBy(a => a.Nombre)
                .Select(a => new ActivoRotativoListDto
                {
                    Id                   = a.Id,
                    Nombre               = a.Nombre,
                    CategoriaId          = a.CategoriaId,
                    CategoriaNombre      = a.Categoria != null ? a.Categoria.Nombre : "",
                    Codigo               = a.Codigo,
                    Estado               = a.Estado,
                    ProyectoActualId     = a.ProyectoActualId,
                    ProyectoActualNombre = a.ProyectoActual != null ? a.ProyectoActual.ProjectDescription : null,
                    ResponsableNombre    = a.ResponsableNombre,
                    ResponsableTelefono  = a.ResponsableTelefono,
                    Activo               = a.Activo
                })
                .ToListAsync();
        }

        public async Task<ActivoRotativoDetalleDto?> GetActivoDetalleAsync(int activoId)
        {
            using var ctx = _factory.CreateDbContext();
            var a = await ctx.SsActivoRotativo
                .Include(x => x.Categoria)
                .Include(x => x.ProyectoActual)
                .Include(x => x.Movimientos)
                    .ThenInclude(m => m.ProyectoOrigen)
                .Include(x => x.Movimientos)
                    .ThenInclude(m => m.ProyectoDestino)
                .FirstOrDefaultAsync(x => x.Id == activoId);

            if (a == null) return null;

            var movidoPorIds = a.Movimientos
                .Where(m => m.MovidoPorId.HasValue)
                .Select(m => m.MovidoPorId!.Value)
                .Distinct()
                .ToList();

            var usuarios = await ctx.User
                .Where(u => movidoPorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.Person.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            return new ActivoRotativoDetalleDto
            {
                Id                   = a.Id,
                Nombre               = a.Nombre,
                CategoriaId          = a.CategoriaId,
                CategoriaNombre      = a.Categoria?.Nombre ?? "",
                Codigo               = a.Codigo,
                Estado               = a.Estado,
                ProyectoActualId     = a.ProyectoActualId,
                ProyectoActualNombre = a.ProyectoActual?.ProjectDescription,
                ResponsableNombre    = a.ResponsableNombre,
                ResponsableTelefono  = a.ResponsableTelefono,
                Activo               = a.Activo,
                Observaciones        = a.Observaciones,
                Historial = a.Movimientos
                    .OrderByDescending(m => m.FechaMovimiento)
                    .Select(m => new ActivoRotativoMovimientoDto
                    {
                        Id                   = m.Id,
                        ProyectoOrigenNombre = m.ProyectoOrigen?.ProjectDescription,
                        ProyectoDestinoNombre = m.ProyectoDestino?.ProjectDescription,
                        FechaMovimiento      = m.FechaMovimiento,
                        MovidoPor            = m.MovidoPorId.HasValue && usuarios.TryGetValue(m.MovidoPorId.Value, out var nombre) ? nombre : null,
                        Observacion          = m.Observacion
                    }).ToList()
            };
        }

        public async Task<SsActivoRotativo> CreateActivoAsync(ActivoRotativoUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;
            var entity = new SsActivoRotativo
            {
                Nombre              = dto.Nombre,
                CategoriaId         = dto.CategoriaId,
                Codigo              = dto.Codigo,
                Estado              = dto.Estado,
                ProyectoActualId    = dto.ProyectoActualId,
                ResponsableNombre   = dto.ResponsableNombre,
                ResponsableTelefono = dto.ResponsableTelefono,
                Observaciones       = dto.Observaciones,
                Activo              = true,
                CreatedAt           = now,
                UpdatedAt           = now
            };
            ctx.SsActivoRotativo.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateActivoAsync(int activoId, ActivoRotativoUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsActivoRotativo.FindAsync(activoId)
                ?? throw new KeyNotFoundException($"Activo {activoId} no encontrado.");

            entity.Nombre              = dto.Nombre;
            entity.CategoriaId         = dto.CategoriaId;
            entity.Codigo              = dto.Codigo;
            entity.Estado              = dto.Estado;
            entity.ProyectoActualId    = dto.ProyectoActualId;
            entity.ResponsableNombre   = dto.ResponsableNombre;
            entity.ResponsableTelefono = dto.ResponsableTelefono;
            entity.Observaciones       = dto.Observaciones;
            entity.UpdatedAt           = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task<SsActivoRotativo> MoverActivoAsync(int activoId, ActivoRotativoMoverDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsActivoRotativo.FindAsync(activoId)
                ?? throw new KeyNotFoundException($"Activo {activoId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            var proyectoOrigen = entity.ProyectoActualId;

            var movimiento = new SsActivoRotativoMovimiento
            {
                ActivoId          = activoId,
                ProyectoOrigenId  = proyectoOrigen,
                ProyectoDestinoId = dto.NuevoProyectoId,
                FechaMovimiento   = now,
                MovidoPorId       = userId,
                Observacion       = dto.Observacion,
                CreatedAt         = now
            };
            ctx.SsActivoRotativoMovimiento.Add(movimiento);

            entity.ProyectoActualId = dto.NuevoProyectoId;
            entity.UpdatedAt        = now;

            await ctx.SaveChangesAsync();
            return entity;
        }
    }
}
