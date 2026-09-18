using Abril_Backend.Features.SsomaModule.EppFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Repositories
{
    public class EppPedidoRepository : IEppPedidoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public EppPedidoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<EppPedidoListDto>> GetPedidosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            var pedidos = await ctx.SsEppPedido
                .Include(p => p.Lineas)
                .OrderByDescending(p => p.Fecha)
                .ToListAsync();

            var projectIds = pedidos.Select(p => p.ProjectId).Distinct().ToList();
            var proyectos = await ctx.Project
                .Where(p => projectIds.Contains(p.ProjectId))
                .ToDictionaryAsync(p => p.ProjectId, p => p.ProjectDescription);

            var userIds = pedidos.Where(p => p.GeneradoPorId.HasValue).Select(p => p.GeneradoPorId!.Value).Distinct().ToList();
            var usuarios = await ctx.User
                .Where(u => userIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.Person.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            return pedidos.Select(p => new EppPedidoListDto
            {
                Id = p.Id,
                Codigo = p.Codigo,
                ProjectId = p.ProjectId,
                ProjectDescription = proyectos.TryGetValue(p.ProjectId, out var nombre) ? nombre : "",
                GeneradoPorNombre = p.GeneradoPorId.HasValue && usuarios.TryGetValue(p.GeneradoPorId.Value, out var u) ? u : null,
                Fecha = p.Fecha,
                Estado = p.Estado,
                TotalLineas = p.Lineas.Count,
                TotalUnidades = p.Lineas.Sum(l => l.Cantidad)
            }).ToList();
        }

        public async Task<EppPedidoDetalleDto?> GetPedidoDetalleAsync(int pedidoId)
        {
            using var ctx = _factory.CreateDbContext();
            var p = await ctx.SsEppPedido
                .Include(x => x.Lineas)
                .FirstOrDefaultAsync(x => x.Id == pedidoId);

            if (p == null) return null;

            var proyecto = await ctx.Project.Where(x => x.ProjectId == p.ProjectId)
                .Select(x => x.ProjectDescription).FirstOrDefaultAsync() ?? "";

            var generadoPor = p.GeneradoPorId.HasValue
                ? await ctx.User.Where(u => u.UserId == p.GeneradoPorId.Value).Select(u => u.Person.FullName).FirstOrDefaultAsync()
                : null;

            return new EppPedidoDetalleDto
            {
                Id = p.Id,
                Codigo = p.Codigo,
                ProjectId = p.ProjectId,
                ProjectDescription = proyecto,
                GeneradoPorNombre = generadoPor,
                Fecha = p.Fecha,
                Estado = p.Estado,
                Observaciones = p.Observaciones,
                TotalLineas = p.Lineas.Count,
                TotalUnidades = p.Lineas.Sum(l => l.Cantidad),
                Lineas = p.Lineas.Select(l => new EppPedidoLineaDto
                {
                    NombreTecnico = l.NombreTecnico,
                    NombreComercial = l.NombreComercial,
                    Marca = l.Marca,
                    Modelo = l.Modelo,
                    Talla = l.Talla,
                    Cantidad = l.Cantidad
                }).ToList()
            };
        }

        public async Task<EppPedidoDetalleDto> CreatePedidoAsync(EppPedidoCreateDto dto, int? userId)
        {
            if (dto.Lineas.Count == 0)
                throw new InvalidOperationException("El pedido no tiene líneas.");

            using var ctx = _factory.CreateDbContext();
            if (!await ctx.Project.AnyAsync(p => p.ProjectId == dto.ProjectId))
                throw new KeyNotFoundException($"Proyecto {dto.ProjectId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            var entity = new SsEppPedido
            {
                // Código provisional, se reemplaza abajo con el Id ya generado.
                Codigo = "PED-EPP-TEMP",
                ProjectId = dto.ProjectId,
                GeneradoPorId = userId,
                Fecha = now,
                Estado = "Generado",
                Observaciones = dto.Observaciones,
                CreatedAt = now,
                Lineas = dto.Lineas.Select(l => new SsEppPedidoLinea
                {
                    EppItemId = l.EppItemId,
                    EppModeloId = l.EppModeloId,
                    NombreTecnico = l.NombreTecnico,
                    NombreComercial = l.NombreComercial,
                    Marca = l.Marca,
                    Modelo = l.Modelo,
                    Talla = l.Talla,
                    Cantidad = l.Cantidad
                }).ToList()
            };

            ctx.SsEppPedido.Add(entity);
            await ctx.SaveChangesAsync();

            entity.Codigo = $"PED-EPP-{now.Year}-{entity.Id:D4}";
            await ctx.SaveChangesAsync();

            return await GetPedidoDetalleAsync(entity.Id)
                ?? throw new InvalidOperationException("No se pudo recuperar el pedido recién creado.");
        }
    }
}
