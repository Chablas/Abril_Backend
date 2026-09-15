using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Repositories
{
    public class EvPeriodoRepository : IEvPeriodoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public EvPeriodoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<EvPeriodo?> GetActivoAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvPeriodos.FirstOrDefaultAsync(p => p.Activo);
        }

        public async Task<EvPeriodo?> GetUltimoAsync()
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var mesAnterior = hoy.AddMonths(-1);

            // El resumen siempre muestra el mes calendario anterior al actual (el último que ya
            // cerró por completo) — no el registro con mayor (anio, mes) de la tabla, que puede
            // incluir períodos futuros o de prueba sembrados de antemano (p. ej. para poblar la
            // tendencia histórica de gráficos) y que aún no tienen evaluaciones reales.
            var periodo = await ctx.EvPeriodos
                .FirstOrDefaultAsync(p => p.Mes == mesAnterior.Month && p.Anio == mesAnterior.Year);
            if (periodo != null) return periodo;

            // Si por algún motivo no existe el período del mes anterior, cae al más reciente
            // que ya haya empezado (nunca uno futuro).
            return await ctx.EvPeriodos
                .Where(p => p.Anio < hoy.Year || (p.Anio == hoy.Year && p.Mes <= hoy.Month))
                .OrderByDescending(p => p.Anio)
                .ThenByDescending(p => p.Mes)
                .FirstOrDefaultAsync();
        }

        public async Task<List<EvPeriodo>> GetAllAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvPeriodos
                .OrderByDescending(p => p.Anio)
                .ThenByDescending(p => p.Mes)
                .ToListAsync();
        }

        public async Task<EvPeriodo?> GetByIdAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvPeriodos.FindAsync(id);
        }

        public async Task<EvPeriodo> CreateAsync(EvPeriodo periodo)
        {
            using var ctx = _factory.CreateDbContext();
            ctx.EvPeriodos.Add(periodo);
            await ctx.SaveChangesAsync();
            return periodo;
        }

        public async Task UpdateAsync(EvPeriodo periodo)
        {
            using var ctx = _factory.CreateDbContext();
            // El período llega de un GetByIdAsync anterior (otro DbContext): Npgsql devuelve
            // CreatedAt con Kind=Unspecified, que la columna timestamptz rechaza al reescribirlo
            // aquí. No se está cambiando el valor, solo aclarando que ya es UTC (así se guardó).
            if (periodo.CreatedAt.Kind == DateTimeKind.Unspecified)
                periodo.CreatedAt = DateTime.SpecifyKind(periodo.CreatedAt, DateTimeKind.Utc);
            ctx.EvPeriodos.Update(periodo);
            await ctx.SaveChangesAsync();
        }

        public async Task SincronizarVigenciaAsync()
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

            // Desactivar cualquier período activo cuya ventana ya cerró.
            var vencidos = await ctx.EvPeriodos.Where(p => p.Activo && p.FechaCierre < hoy).ToListAsync();
            foreach (var v in vencidos) v.Activo = false;
            if (vencidos.Count > 0) await ctx.SaveChangesAsync();

            // Determinar a qué ciclo (apertura día 25 -> cierre último día del MISMO mes)
            // pertenece la fecha de hoy. Fuera de esa ventana (día 1-24) no hay nada que
            // gestionar: el período recién cerrado ya quedó desactivado arriba y el
            // siguiente todavía no abre.
            if (hoy.Day < 25) return;

            int cicloMes = hoy.Month;
            int cicloAnio = hoy.Year;

            var apertura = new DateOnly(cicloAnio, cicloMes, 25);
            var ultimoDiaCierre = DateTime.DaysInMonth(cicloAnio, cicloMes);
            var cierre = new DateOnly(cicloAnio, cicloMes, ultimoDiaCierre);

            var vigente = await ctx.EvPeriodos.FirstOrDefaultAsync(p => p.Mes == cicloMes && p.Anio == cicloAnio);
            if (vigente == null)
            {
                vigente = new EvPeriodo
                {
                    Mes = cicloMes,
                    Anio = cicloAnio,
                    FechaApertura = apertura,
                    FechaCierre = cierre,
                    Activo = true
                };
                ctx.EvPeriodos.Add(vigente);
                await ctx.SaveChangesAsync();
            }
            else if (!vigente.Activo)
            {
                vigente.Activo = true;
                await ctx.SaveChangesAsync();
            }

            // Garantizar que no quede ningún otro período activo por errores manuales previos.
            var otrosActivos = await ctx.EvPeriodos.Where(p => p.Activo && p.Id != vigente.Id).ToListAsync();
            foreach (var o in otrosActivos) o.Activo = false;
            if (otrosActivos.Count > 0) await ctx.SaveChangesAsync();
        }
    }
}
