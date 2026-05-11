using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Dashboard;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private static readonly string[] AbrilCategorias = { "Casa", "Staff" };
        private const string ContratistaCategoria = "Contratista";

        private readonly IDbContextFactory<AppDbContext> _factory;

        public DashboardRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<DashboardSaludOcupacionalDto> GetDashboard()
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var hoy7 = hoy.AddDays(7);
            var hoy15 = hoy.AddDays(15);
            var hoy30 = hoy.AddDays(30);

            var totalTrabajadores = await ctx.Worker.CountAsync();
            var totalAbril = await ctx.Worker
                .CountAsync(w => w.ContrataCasa != null && AbrilCategorias.Contains(w.ContrataCasa));
            var totalContratistas = await ctx.Worker
                .CountAsync(w => w.ContrataCasa == ContratistaCategoria);

            var emosActivos = ctx.WorkerEmo.Where(e => e.Activo);

            var ultimosEmos = await emosActivos
                .Where(e => !emosActivos.Any(e2 =>
                    e2.WorkerId == e.WorkerId &&
                    (e2.FechaEmo > e.FechaEmo || (e2.FechaEmo == e.FechaEmo && e2.Id > e.Id))))
                .Select(e => new { e.WorkerId, e.Aptitud })
                .ToListAsync();

            int GetCount(string aptitud) => ultimosEmos.Count(e => e.Aptitud == aptitud);

            var workersConEmo = ultimosEmos.Select(e => e.WorkerId).Distinct().Count();

            var aptitud = new AptitudResumenDto
            {
                Apto = GetCount("Apto"),
                AptoConRestricciones = GetCount("Apto con Restricciones"),
                NoApto = GetCount("No Apto"),
                Observado = GetCount("Observado"),
                SinEmo = Math.Max(0, totalTrabajadores - workersConEmo)
            };

            var emosVencidos = await emosActivos
                .CountAsync(e => (e.FechaVencimientoCalculada ?? e.FechaVencimiento) != null
                              && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) < hoy);

            var vencer = new VencimientoResumenDto
            {
                Dias30 = await emosActivos.CountAsync(e =>
                    (e.FechaVencimientoCalculada ?? e.FechaVencimiento) != null
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) >= hoy
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) <= hoy30),
                Dias15 = await emosActivos.CountAsync(e =>
                    (e.FechaVencimientoCalculada ?? e.FechaVencimiento) != null
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) >= hoy
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) <= hoy15),
                Dias7 = await emosActivos.CountAsync(e =>
                    (e.FechaVencimientoCalculada ?? e.FechaVencimiento) != null
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) >= hoy
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) <= hoy7)
            };

            var interconsultasPendientes = await ctx.SsInterconsulta
                .CountAsync(i => i.Estado == "Pendiente");

            var programacionesSemana = await ctx.SsProgramacionEmo
                .CountAsync(p => p.FechaProgramada >= hoy && p.FechaProgramada <= hoy7
                              && p.Estado != "Cancelado");

            var trabajadoresInhabilitados = await ctx.Worker.CountAsync(w => w.HabilitadoObra == false);

            var proximos = await (
                from e in emosActivos
                join w in ctx.Worker on e.WorkerId equals w.Id
                join em in ctx.Contributor on e.EmpresaOrigenId equals em.ContributorId into ej
                from em in ej.DefaultIfEmpty()
                let fv = e.FechaVencimientoCalculada ?? e.FechaVencimiento
                where fv != null && fv >= hoy
                orderby fv
                select new
                {
                    e.WorkerId,
                    Nombre = (w.Person != null ? w.Person.FullName : null) ?? string.Empty,
                    Dni = (w.Person != null ? w.Person.DocumentIdentityCode : null) ?? string.Empty,
                    FechaVencimiento = fv!.Value,
                    Empresa = em != null ? (em.ContributorName ?? string.Empty) : string.Empty
                })
                .Take(10)
                .ToListAsync();

            var proximosDto = proximos.Select(p => new ProximoVencerDto
            {
                WorkerId = p.WorkerId,
                Nombre = p.Nombre,
                Dni = p.Dni,
                FechaVencimiento = p.FechaVencimiento,
                DiasParaVencer = p.FechaVencimiento.DayNumber - hoy.DayNumber,
                Empresa = p.Empresa
            }).ToList();

            return new DashboardSaludOcupacionalDto
            {
                TotalTrabajadores = totalTrabajadores,
                TotalAbril = totalAbril,
                TotalContratistas = totalContratistas,
                EmosPorAptitud = aptitud,
                EmosPorVencer = vencer,
                EmosVencidos = emosVencidos,
                InterconsultasPendientes = interconsultasPendientes,
                ProgramacionesSemana = programacionesSemana,
                TrabajadoresInhabilitados = trabajadoresInhabilitados,
                ProximosVencer = proximosDto
            };
        }
    }
}
