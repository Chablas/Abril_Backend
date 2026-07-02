using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Restringidos;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class TrabajadorRestringidoRepository : ITrabajadorRestringidoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public TrabajadorRestringidoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<bool> EstaRestringidoPorDniAsync(string? dni)
        {
            if (string.IsNullOrWhiteSpace(dni)) return false;
            var dniNorm = dni.Trim().ToUpper();
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsTrabajadorRestringido
                .AnyAsync(r => r.Dni != null && r.Dni.ToUpper() == dniNorm && r.Activo);
        }

        public async Task<List<TrabajadorRestringidoListDto>> GetAllAsync(bool soloActivos = true, string? dni = null)
        {
            using var ctx = _factory.CreateDbContext();
            var query = ctx.SsTrabajadorRestringido.AsQueryable();
            if (soloActivos)
                query = query.Where(r => r.Activo);
            if (!string.IsNullOrWhiteSpace(dni))
            {
                var dniNorm = dni.Trim().ToUpper();
                query = query.Where(r => r.Dni != null && r.Dni.ToUpper() == dniNorm);
            }

            return await query
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new TrabajadorRestringidoListDto
                {
                    Id = r.Id,
                    Dni = r.Dni,
                    ApellidoNombre = r.ApellidoNombre,
                    Motivo = r.Motivo,
                    ProyectoOrigen = r.ProyectoOrigen,
                    RestringidoPor = r.RestringidoPor,
                    FechaRestriccion = r.FechaRestriccion,
                    Activo = r.Activo,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<TrabajadorRestringidoListDto> CreateAsync(TrabajadorRestringidoCreateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var dniNorm = dto.Dni?.Trim().ToUpper();
            var dniOriginal = dto.Dni?.Trim();

            // Si ya existe un registro con el mismo WorkerId o DNI pero inactivo, reactivarlo
            SsTrabajadorRestringido? existente = null;

            if (dto.WorkerId.HasValue)
            {
                existente = await ctx.SsTrabajadorRestringido
                    .FirstOrDefaultAsync(r => r.WorkerId == dto.WorkerId.Value && !r.Activo);
            }

            if (existente is null && !string.IsNullOrWhiteSpace(dniNorm))
            {
                existente = await ctx.SsTrabajadorRestringido
                    .FirstOrDefaultAsync(r => r.Dni != null && r.Dni.ToUpper() == dniNorm && !r.Activo);
            }

            if (existente is not null)
            {
                existente.Activo = true;
                existente.WorkerId = dto.WorkerId ?? existente.WorkerId;
                existente.Motivo = dto.Motivo;
                existente.ApellidoNombre = dto.ApellidoNombre ?? existente.ApellidoNombre;
                existente.ProyectoOrigen = dto.ProyectoOrigen;
                existente.RestringidoPor = dto.RestringidoPor;
                existente.FechaRestriccion = dto.FechaRestriccion;
                existente.UpdatedAt = DateTime.UtcNow;
                await ctx.SaveChangesAsync();
                return ToDto(existente);
            }

            var nuevo = new SsTrabajadorRestringido
            {
                WorkerId = dto.WorkerId,
                Dni = dniOriginal,
                ApellidoNombre = dto.ApellidoNombre,
                Motivo = dto.Motivo,
                ProyectoOrigen = dto.ProyectoOrigen,
                RestringidoPor = dto.RestringidoPor,
                FechaRestriccion = dto.FechaRestriccion,
                Activo = true,
                CreatedAt = DateTime.UtcNow
            };

            ctx.SsTrabajadorRestringido.Add(nuevo);
            await ctx.SaveChangesAsync();
            return ToDto(nuevo);
        }

        public async Task DesactivarAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            var registro = await ctx.SsTrabajadorRestringido.FirstOrDefaultAsync(r => r.Id == id)
                ?? throw new AbrilException("Registro no encontrado.", 404);

            registro.Activo = false;
            registro.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task DesactivarPorWorkerIdAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.SsTrabajadorRestringido
                .Where(r => r.WorkerId == workerId && r.Activo)
                .ToList();
            foreach (var r in registros)
            {
                r.Activo = false;
                r.UpdatedAt = DateTime.UtcNow;
            }
            if (registros.Count > 0)
                await ctx.SaveChangesAsync();
        }

        private static TrabajadorRestringidoListDto ToDto(SsTrabajadorRestringido r) => new()
        {
            Id = r.Id,
            Dni = r.Dni,
            ApellidoNombre = r.ApellidoNombre,
            Motivo = r.Motivo,
            ProyectoOrigen = r.ProyectoOrigen,
            RestringidoPor = r.RestringidoPor,
            FechaRestriccion = r.FechaRestriccion,
            Activo = r.Activo,
            CreatedAt = r.CreatedAt
        };
    }
}
