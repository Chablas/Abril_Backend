using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
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
            var hoy = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

            var trabajadores = await ctx.SsHabTrabajador
                .Where(h => (h.Estado == "Aprobado" || h.Estado == "En plazo") && h.Vigencia < hoy)
                .ToListAsync();

            foreach (var h in trabajadores)
            {
                h.Estado = "Falta";
                h.UpdatedAt = DateTime.UtcNow;
            }

            var empresas = await ctx.SsHabEmpresa
                .Where(h => (h.Estado == "Aprobado" || h.Estado == "En plazo") && h.Vigencia < hoy)
                .ToListAsync();

            foreach (var h in empresas)
            {
                h.Estado = "Falta";
                h.UpdatedAt = DateTime.UtcNow;
            }

            var equipos = await ctx.SsHabEquipo
                .Where(h => (h.Estado == "Aprobado" || h.Estado == "En plazo") && h.Vigencia < hoy)
                .ToListAsync();

            foreach (var h in equipos)
            {
                h.Estado = "Falta";
                h.UpdatedAt = DateTime.UtcNow;
            }

            var hoyDate = DateOnly.FromDateTime(DateTime.Today);
            var emos = await ctx.WorkerEmo
                .Where(e => e.Activo && e.Estado == "Vigente" && e.FechaVencimiento < hoyDate)
                .ToListAsync();

            foreach (var e in emos)
            {
                e.Estado = "Vencido";
                e.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await ctx.SaveChangesAsync();

            return new VigenciaRevisionResultDto
            {
                Trabajadores = trabajadores.Count,
                Empresas = empresas.Count,
                Equipos = equipos.Count,
                Emos = emos.Count
            };
        }
    }
}
