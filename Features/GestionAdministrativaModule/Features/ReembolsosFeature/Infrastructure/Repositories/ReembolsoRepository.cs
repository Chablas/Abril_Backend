using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Infrastructure.Repositories
{
    /// <summary>
    /// La bandeja de Tesorería. A diferencia de las otras pantallas de salidas no hay recorte por
    /// área: Tesorería paga a toda la organización, y su recorte es por ESTADO — solo lo que la
    /// jefatura ya firmó (y lo que ya se pagó, para poder consultarlo).
    /// </summary>
    public class ReembolsoRepository : IReembolsoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ReembolsoRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<List<ReembolsoListItemDto>> GetAll(ReembolsoFiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();

            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, SalidasDeTesoreria(ctx, filters));
            var items = planillas.Select(Armar).ToList();

            if (filters.PeriodoAnio.HasValue && filters.PeriodoMes.HasValue)
                items = items
                    .Where(x => x.PeriodoAnio == filters.PeriodoAnio.Value
                             && x.PeriodoMes  == filters.PeriodoMes.Value)
                    .ToList();

            return items;
        }

        public async Task<ReembolsoDetalleDto?> GetDetalle(int rendicionId)
        {
            using var ctx = _factory.CreateDbContext();

            var query = SalidasDeTesoreria(ctx, new ReembolsoFiltersDto())
                .Where(s => s.RendicionId == rendicionId);

            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, query, conDetalle: true);
            if (planillas.Count == 0) return null;

            var planilla = planillas[0];
            var cabecera = Armar(planilla);
            var detalle  = new ReembolsoDetalleDto();
            CopiarCabecera(cabecera, detalle);

            detalle.Salidas = planilla.Salidas
                .Select(s => new ReembolsoSalidaDto
                {
                    Id              = s.Id,
                    Codigo          = s.Codigo,
                    Trabajador      = s.Trabajador,
                    Area            = s.Area,
                    FechaSalida     = s.FechaSalida,
                    Motivo          = s.Motivo,
                    LugarOrigen     = s.LugarOrigen,
                    LugarDestino    = s.LugarDestino,
                    TrayectosCount  = s.TrayectosCount,
                    Monto           = s.Monto,
                    EstadoReembolso = s.EstadoReembolso,
                })
                .ToList();

            return detalle;
        }

        public async Task<ReembolsoFilterDataDto> GetFilterData()
        {
            using var ctx = _factory.CreateDbContext();

            // Solo los trabajadores que aparecen en la bandeja: ofrecer al resto sería ofrecer un
            // resultado vacío.
            var workerIds = await SalidasDeTesoreria(ctx, new ReembolsoFiltersDto())
                .Select(s => s.WorkerId)
                .Distinct()
                .ToListAsync();

            var trabajadores = await (
                from w   in ctx.Worker.Where(w => workerIds.Contains(w.Id))
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                orderby per != null ? per.FullName : null
                select new TrabajadorOptionDto
                {
                    WorkerId       = w.Id,
                    NombreCompleto = per != null ? (per.FullName ?? "[Sin nombre]") : "[Sin nombre]",
                }
            ).ToListAsync();

            // El árbol completo: Tesorería filtra sobre toda la organización.
            var areaTree = await (
                from s  in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State && at.State
                orderby s.DisplayOrder
                select new AreaNodeDto
                {
                    AreaScopeId       = s.AreaScopeId,
                    AreaItemId        = s.AreaItemId,
                    AreaItemName      = ai.AreaItemName,
                    AreaTypeId        = ai.AreaTypeId,
                    AreaTypeName      = at.AreaTypeName,
                    AreaScopeParentId = s.AreaScopeParentId,
                    DisplayOrder      = s.DisplayOrder,
                }
            ).ToListAsync();

            var fechas = await SalidasDeTesoreria(ctx, new ReembolsoFiltersDto())
                .Select(s => new { RendicionId = s.RendicionId!.Value, s.FechaSalida })
                .ToListAsync();

            var periodos = fechas
                .GroupBy(x => x.RendicionId)
                .Select(g => g.Min(x => x.FechaSalida))
                .Select(f => (f.Year, f.Month))
                .Distinct()
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .Select(p => new PeriodoReembolsoOptionDto
                {
                    Anio  = p.Year,
                    Mes   = p.Month,
                    Label = PlanillaRendicionHelper.EtiquetaMes(p.Year, p.Month),
                })
                .ToList();

            return new ReembolsoFilterDataDto
            {
                Trabajadores = trabajadores,
                AreaTree     = areaTree,
                Periodos     = periodos,
            };
        }

        public async Task<List<int>> ResolverSolicitudIds(
            IEnumerable<int> rendicionIds, IEnumerable<int> solicitudIds)
        {
            var rIds = rendicionIds?.Distinct().ToList() ?? new List<int>();
            var sIds = solicitudIds?.Distinct().ToList() ?? new List<int>();
            if (rIds.Count == 0 && sIds.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            return await SalidasDeTesoreria(ctx, new ReembolsoFiltersDto())
                .Where(s => rIds.Contains(s.RendicionId!.Value) || sIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync();
        }

        public async Task<List<int>> MarcarPagadas(IEnumerable<int> ids, int tesoreroUserId)
        {
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            var solicitudes = await ctx.GaSolicitudSalida
                .Where(s => idsList.Contains(s.Id)
                         && s.EstadoReembolsoId == EstadosSalida.Reembolso.Firmado)
                .ToListAsync();

            if (solicitudes.Count == 0)
                throw new AbrilException(
                    "Ninguna de las salidas seleccionadas está firmada: solo se puede marcar como pagado " +
                    "lo que la jefatura ya firmó.", 400);

            var now = DateTimeOffset.UtcNow;
            foreach (var s in solicitudes)
            {
                s.EstadoReembolsoId = EstadosSalida.Reembolso.Pagado;
                s.PagadoPorId       = tesoreroUserId;
                s.PagadoAt          = now;
                s.UpdatedAt         = now;
            }

            await ctx.SaveChangesAsync();
            return solicitudes.Select(s => s.Id).ToList();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// El universo de Tesorería: salidas rendidas cuyo reembolso ya está Firmado o Pagado. El
        /// filtro de estado del desplegable solo puede recortar ESE conjunto, nunca ampliarlo.
        /// </summary>
        private static IQueryable<GaSolicitudSalida> SalidasDeTesoreria(
            AppDbContext ctx, ReembolsoFiltersDto filters)
        {
            var visibles = EstadosSalida.Reembolso.VisiblesParaTesoreria;
            var query = ctx.GaSolicitudSalida
                .Where(s => s.RendicionId != null && visibles.Contains(s.EstadoReembolsoId));

            var estadoId = EstadosSalida.Reembolso.IdFromNombre(filters.EstadoReembolso);
            if (estadoId.HasValue && visibles.Contains(estadoId.Value))
                query = query.Where(s => s.EstadoReembolsoId == estadoId.Value);

            if (filters.WorkerId.HasValue)
                query = query.Where(s => s.WorkerId == filters.WorkerId.Value);

            if (filters.FilterAreaScopeIds is { Count: > 0 })
            {
                var areaFilter = filters.FilterAreaScopeIds;
                query = query.Where(s =>
                    ctx.Worker.Any(w => w.Id == s.WorkerId &&
                        w.PuestoCatalogo!.AreaDestinoScopeId != null
                        && areaFilter.Contains(w.PuestoCatalogo.AreaDestinoScopeId!.Value)));
            }

            return query;
        }

        private static ReembolsoListItemDto Armar(PlanillaRendicionLoader.PlanillaFila p) => new()
        {
            Id                 = p.Id,
            NumeroPlanilla     = p.NumeroPlanilla,
            RendidoAt          = p.RendidoAt,
            Periodo            = p.Periodo,
            PeriodoAnio        = p.PeriodoAnio,
            PeriodoMes         = p.PeriodoMes,
            Trabajadores       = p.Trabajadores,
            SalidasCount       = p.SalidasCount,
            MontoTotal         = p.MontoTotal,
            PdfUrl             = p.PdfUrl,
            PdfFilename        = p.PdfFilename,
            PdfFirmadoUrl      = p.PdfFirmadoUrl,
            PdfFirmadoFilename = p.PdfFirmadoFilename,
            FirmadoAt          = p.FirmadoAt,
            ConsolidadoS10     = p.ConsolidadoS10,
            EstadoReembolso    = p.EstadoReembolso,
            ReembolsoMixto     = p.ReembolsoMixto,
            PorPagarCount      = p.Salidas.Count(s => s.EstadoReembolsoId == EstadosSalida.Reembolso.Firmado),
        };

        private static void CopiarCabecera(ReembolsoListItemDto o, ReembolsoDetalleDto d)
        {
            d.Id = o.Id; d.NumeroPlanilla = o.NumeroPlanilla; d.RendidoAt = o.RendidoAt;
            d.Periodo = o.Periodo; d.PeriodoAnio = o.PeriodoAnio; d.PeriodoMes = o.PeriodoMes;
            d.Trabajadores = o.Trabajadores; d.SalidasCount = o.SalidasCount; d.MontoTotal = o.MontoTotal;
            d.PdfUrl = o.PdfUrl; d.PdfFilename = o.PdfFilename;
            d.PdfFirmadoUrl = o.PdfFirmadoUrl; d.PdfFirmadoFilename = o.PdfFirmadoFilename;
            d.FirmadoAt = o.FirmadoAt; d.ConsolidadoS10 = o.ConsolidadoS10;
            d.EstadoReembolso = o.EstadoReembolso; d.ReembolsoMixto = o.ReembolsoMixto;
            d.PorPagarCount = o.PorPagarCount;
        }
    }
}
