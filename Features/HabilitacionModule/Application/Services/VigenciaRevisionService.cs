using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Application.Services
{
    public class VigenciaRevisionService : IVigenciaRevisionService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public VigenciaRevisionService(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<VigenciaRevisionResultDto> RevisarVigencias()
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateTime.Today;
            var estados = new[] { "Aprobado", "En plazo" };

            var trabajadores = await ctx.SsHabTrabajador
                .Where(h => estados.Contains(h.Estado) && h.Vigencia < hoy)
                .ToListAsync();

            foreach (var h in trabajadores)
            {
                h.Estado = "Falta";
                h.UpdatedAt = DateTime.UtcNow;
            }

            var empresas = await ctx.SsHabEmpresa
                .Where(h => estados.Contains(h.Estado) && h.Vigencia < hoy)
                .ToListAsync();

            foreach (var h in empresas)
            {
                h.Estado = "Falta";
                h.UpdatedAt = DateTime.UtcNow;
            }

            var equipos = await ctx.SsHabEquipo
                .Where(h => estados.Contains(h.Estado) && h.Vigencia < hoy)
                .ToListAsync();

            foreach (var h in equipos)
            {
                h.Estado = "Falta";
                h.UpdatedAt = DateTime.UtcNow;
            }

            await ctx.SaveChangesAsync();

            return new VigenciaRevisionResultDto
            {
                Trabajadores = trabajadores.Count,
                Empresas = empresas.Count,
                Equipos = equipos.Count
            };
        }
    }
}
