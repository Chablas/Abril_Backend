using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Infrastructure.Repositories
{
    /// <summary>
    /// La bandeja de Tesorería. A diferencia de las otras pantallas de salidas no hay recorte por
    /// área: Tesorería paga a toda la organización, y su recorte es por ESTADO — lo que la
    /// jefatura ya firmó, lo que ella misma confirmó y lo que ya pagó.
    /// </summary>
    public class ReembolsoRepository : IReembolsoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ReembolsoRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<List<ReembolsoListItemDto>> GetAll(ReembolsoFiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();

            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, SalidasDeTesoreria(ctx, filters));
            if (planillas.Count == 0) return new();

            // Los nombres de todos los usuarios que firmaron, confirmaron o pagaron, de una vez:
            // resolverlos planilla por planilla sería un N+1 sobre la tabla más chica del flujo.
            var nombres = await NombresDeUsuariosAsync(ctx, planillas);
            var items = planillas.Select(p => Armar(p, nombres)).ToList();

            if (filters.PeriodoAnio.HasValue && filters.PeriodoMes.HasValue)
                items = items
                    .Where(x => x.PeriodoAnio == filters.PeriodoAnio.Value
                             && x.PeriodoMes  == filters.PeriodoMes.Value)
                    .ToList();

            // El texto se filtra acá, sobre las filas ya armadas, porque busca contra cosas que no
            // son columnas: el número de planilla formateado, los nombres agrupados y la guía del
            // consolidado. Va antes de que el servicio cuente las tarjetas, así la tabla y los
            // números del encabezado siempre hablan del mismo conjunto.
            var texto = filters.Texto?.Trim();
            if (!string.IsNullOrEmpty(texto))
                items = items.Where(x => Coincide(x, texto)).ToList();

            return items;
        }

        /// <summary>Busca el texto en lo que la fila muestra: planilla, código, gente, guía y periodo.</summary>
        private static bool Coincide(ReembolsoListItemDto x, string texto)
        {
            bool Tiene(string? valor) =>
                !string.IsNullOrEmpty(valor)
                && valor.Contains(texto, StringComparison.OrdinalIgnoreCase);

            return Tiene(x.NumeroPlanilla)
                || Tiene(x.Codigo)
                || Tiene(x.Periodo)
                || Tiene(x.ConsolidadoS10?.NumeroGuia)
                || x.Trabajadores.Any(Tiene);
        }

        public async Task<ReembolsoDetalleDto?> GetDetalle(int rendicionId)
        {
            using var ctx = _factory.CreateDbContext();

            var query = SalidasDeTesoreria(ctx, new ReembolsoFiltersDto())
                .Where(s => s.RendicionId == rendicionId);

            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, query, conDetalle: true);
            if (planillas.Count == 0) return null;

            var planilla = planillas[0];
            var nombres  = await NombresDeUsuariosAsync(ctx, planillas);
            var cabecera = Armar(planilla, nombres);
            var detalle  = new ReembolsoDetalleDto();
            CopiarCabecera(cabecera, detalle);

            // Los tramos con sus vouchers: es lo que Tesorería revisa antes de confirmar (RF-TES-05).
            var tramosPorSalida = await CargarTramosAsync(
                ctx, planilla.Salidas.Select(s => s.Id).ToList());

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
                    Tramos          = tramosPorSalida.TryGetValue(s.Id, out var t) ? t : new(),
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

        public Task<List<int>> ResolverSolicitudIds(
            IEnumerable<int> rendicionIds, IEnumerable<int> solicitudIds, int estadoId) =>
            ResolverSolicitudIds(rendicionIds, solicitudIds, new[] { estadoId });

        public async Task<List<int>> ResolverSolicitudIds(
            IEnumerable<int> rendicionIds, IEnumerable<int> solicitudIds, int[] estadoIds)
        {
            var rIds = rendicionIds?.Distinct().ToList() ?? new List<int>();
            var sIds = solicitudIds?.Distinct().ToList() ?? new List<int>();
            if (rIds.Count == 0 && sIds.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            return await SalidasDeTesoreria(ctx, new ReembolsoFiltersDto())
                .Where(s => estadoIds.Contains(s.EstadoReembolsoId))
                .Where(s => rIds.Contains(s.RendicionId!.Value) || sIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync();
        }

        public async Task<List<int>> ConfirmarRevision(IEnumerable<int> ids, int tesoreroUserId) =>
            await CambiarEstadoAsync(
                ids,
                desde: EstadosSalida.Reembolso.Firmado,
                hasta: EstadosSalida.Reembolso.PorPagar,
                tesoreroUserId,
                "Ninguna de las salidas seleccionadas está firmada: la revisión de Tesorería solo se "
                + "confirma sobre lo que la jefatura ya firmó.");

        public async Task<List<int>> MarcarPagadas(IEnumerable<int> ids, int tesoreroUserId) =>
            await CambiarEstadoAsync(
                ids,
                desde: EstadosSalida.Reembolso.PorPagar,
                hasta: EstadosSalida.Reembolso.Pagado,
                tesoreroUserId,
                "Ninguna de las salidas seleccionadas tiene la revisión de Tesorería confirmada: hay "
                + "que confirmar la revisión antes de pagar.");

        /// <summary>
        /// Tesorería devuelve el consolidado con un motivo (RG-49). No es un estado nuevo: deja la
        /// salida en el MISMO <see cref="EstadosSalida.Reembolso.Observado"/> que usa la jefatura,
        /// porque la subsanación también es la misma —recargar el Consolidado del S10, o pedirle la
        /// corrección al Coordinador ERP— y así todo lo que ya existe aguas abajo funciona sin
        /// tocarse. Lo único que distingue las dos es el ORIGEN.
        ///
        /// La confirmación de la revisión se borra: lo que Tesorería revisó dejó de ser válido, y
        /// cuando la planilla vuelva firmada de nuevo tiene que volver a confirmarse. El rastro del
        /// pago no se toca porque una salida pagada nunca llega acá.
        /// </summary>
        public async Task<List<int>> Observar(IEnumerable<int> ids, string observacion, int tesoreroUserId)
        {
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            if (string.IsNullOrWhiteSpace(observacion))
                throw new AbrilException("Para observar un reembolso hay que escribir el motivo.", 400);

            using var ctx = _factory.CreateDbContext();

            var observables = EstadosSalida.Reembolso.ObservablesPorTesoreria;
            var solicitudes = await ctx.GaSolicitudSalida
                .Where(s => idsList.Contains(s.Id) && observables.Contains(s.EstadoReembolsoId))
                .ToListAsync();

            if (solicitudes.Count == 0)
                throw new AbrilException(
                    "Ninguna de las salidas seleccionadas se puede observar: solo se devuelve lo que "
                    + "está firmado o listo para pagar. Lo ya pagado no vuelve.", 400);

            var now = DateTimeOffset.UtcNow;
            var obs = observacion.Trim();

            foreach (var s in solicitudes)
            {
                s.EstadoReembolsoId            = EstadosSalida.Reembolso.Observado;
                s.ObservacionReembolso         = obs;
                s.ObservacionReembolsoOrigenId = EstadosSalida.OrigenObservacionReembolso.Tesoreria;
                s.ReembolsoDecididoPorId       = tesoreroUserId;
                s.ReembolsoDecididoAt          = now;
                s.RevisionTesoreriaPorId       = null;
                s.RevisionTesoreriaAt          = null;
                s.UpdatedAt                    = now;
            }

            await ctx.SaveChangesAsync();
            return solicitudes.Select(s => s.Id).ToList();
        }

        public async Task<List<ReembolsoPlanillaCorreoDatos>> GetPlanillaCorreoInfo(IEnumerable<int> solicitudIds)
        {
            var idsList = solicitudIds?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            var filas = await (
                from s   in ctx.GaSolicitudSalida.Where(x => idsList.Contains(x.Id) && x.RendicionId != null)
                join w   in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                join u   in ctx.User on per.UserId equals (int?)u.UserId into uGroup
                from u   in uGroup.DefaultIfEmpty()
                select new
                {
                    s.Id,
                    RendicionId = s.RendicionId!.Value,
                    WorkerId    = w.Id,
                    w.Subarea,
                    Trabajador  = per != null ? (per.FullName ?? "Trabajador") : "Trabajador",
                    Email       = u != null ? u.Email : null,
                    Area        = w.Area,
                    s.FechaSalida,
                    s.PagadoPorId,
                }
            ).ToListAsync();

            if (filas.Count == 0) return new();

            var rendicionIds = filas.Select(f => f.RendicionId).Distinct().ToList();

            var planillas = await ctx.GaRendicion
                .Where(r => rendicionIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Codigo, r.NumeroPlanilla })
                .ToDictionaryAsync(r => r.Id, r => r);

            var consolidados = await ConsolidadoS10Loader.LoadPorRendicionAsync(ctx, rendicionIds);

            var montoPorSolicitud = await MontoPorSolicitudAsync(
                ctx, filas.ToDictionary(f => f.Id, f => f.Subarea));

            var nombresUsuario = await NombrePorUserIdAsync(
                ctx, filas.Where(f => f.PagadoPorId.HasValue).Select(f => f.PagadoPorId!.Value));

            return filas
                .GroupBy(f => new { f.RendicionId, f.WorkerId })
                .Select(g =>
                {
                    var primera = g.First();
                    var desde   = g.Min(x => x.FechaSalida);
                    var hasta   = g.Max(x => x.FechaSalida);
                    planillas.TryGetValue(g.Key.RendicionId, out var planilla);
                    consolidados.TryGetValue(g.Key.RendicionId, out var consolidado);

                    var pagadoPorId = g.Select(x => x.PagadoPorId).FirstOrDefault(x => x.HasValue);

                    return new ReembolsoPlanillaCorreoDatos
                    {
                        RendicionId     = g.Key.RendicionId,
                        Codigo          = PlanillaRendicionHelper.CodigoRendicion(planilla?.Codigo, g.Key.RendicionId),
                        Trabajador      = primera.Trabajador,
                        TrabajadorEmail = primera.Email,
                        Area            = primera.Area,
                        NumeroPlanilla  = PlanillaRendicionHelper.NumeroPlanilla(planilla?.NumeroPlanilla),
                        Periodo         = PlanillaRendicionHelper.EtiquetaPeriodo(desde, hasta),
                        SalidasCount    = g.Count(),
                        MontoTotal      = g.Sum(x => montoPorSolicitud.TryGetValue(x.Id, out var m) ? m : 0m),
                        NumeroGuia      = consolidado?.NumeroGuia,
                        PagadoPor       = pagadoPorId.HasValue && nombresUsuario.TryGetValue(pagadoPorId.Value, out var n)
                                            ? n : null,
                    };
                })
                .ToList();
        }

        public async Task<ReembolsoSeguimientoDto> GetSeguimiento(ReembolsoFiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();

            // El seguimiento es de PAGOS: el estado no se toma del filtro de la bandeja.
            var soloPagadas = new ReembolsoFiltersDto
            {
                WorkerId           = filters.WorkerId,
                FilterAreaScopeIds = filters.FilterAreaScopeIds,
                EstadoReembolso    = EstadosSalida.Reembolso.NombrePagado,
            };

            var filas = await (
                from s   in SalidasDeTesoreria(ctx, soloPagadas)
                join w   in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                select new
                {
                    s.Id,
                    RendicionId = s.RendicionId!.Value,
                    WorkerId    = w.Id,
                    w.Subarea,
                    Trabajador  = per != null ? (per.FullName ?? "Trabajador") : "Trabajador",
                    Area        = w.Area,
                    s.FechaSalida,
                    s.PagadoAt,
                    s.PagadoPorId,
                }
            ).ToListAsync();

            if (filas.Count == 0) return new ReembolsoSeguimientoDto();

            var rendicionIds = filas.Select(f => f.RendicionId).Distinct().ToList();

            var planillas = await ctx.GaRendicion
                .Where(r => rendicionIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Codigo, r.NumeroPlanilla, r.PdfFirmadoUrl })
                .ToDictionaryAsync(r => r.Id, r => r);

            var consolidados      = await ConsolidadoS10Loader.LoadPorRendicionAsync(ctx, rendicionIds);
            var montoPorSolicitud = await MontoPorSolicitudAsync(ctx, filas.ToDictionary(f => f.Id, f => f.Subarea));
            var nombresUsuario    = await NombrePorUserIdAsync(
                ctx, filas.Where(f => f.PagadoPorId.HasValue).Select(f => f.PagadoPorId!.Value));

            var colaboradores = filas
                .GroupBy(f => f.WorkerId)
                .Select(porTrabajador =>
                {
                    var rendiciones = porTrabajador
                        .GroupBy(x => x.RendicionId)
                        .Select(g =>
                        {
                            var desde = g.Min(x => x.FechaSalida);
                            var hasta = g.Max(x => x.FechaSalida);
                            planillas.TryGetValue(g.Key, out var planilla);
                            consolidados.TryGetValue(g.Key, out var consolidado);

                            var pagadoPorId = g.Select(x => x.PagadoPorId).FirstOrDefault(x => x.HasValue);

                            return new SeguimientoRendicionDto
                            {
                                RendicionId       = g.Key,
                                Codigo            = PlanillaRendicionHelper.CodigoRendicion(planilla?.Codigo, g.Key),
                                NumeroPlanilla    = PlanillaRendicionHelper.NumeroPlanilla(planilla?.NumeroPlanilla),
                                Periodo           = PlanillaRendicionHelper.EtiquetaPeriodo(desde, hasta),
                                PeriodoAnio       = desde.Year,
                                PeriodoMes        = desde.Month,
                                NumeroGuia        = consolidado?.NumeroGuia,
                                SalidasCount      = g.Count(),
                                MontoAbonado      = g.Sum(x => montoPorSolicitud.TryGetValue(x.Id, out var m) ? m : 0m),
                                ActualizadoAt     = g.Max(x => x.PagadoAt),
                                PagadoPor         = pagadoPorId.HasValue
                                                        && nombresUsuario.TryGetValue(pagadoPorId.Value, out var n)
                                                    ? n : null,
                                PdfFirmadoUrl     = planilla?.PdfFirmadoUrl,
                                ConsolidadoS10Url = consolidado?.PdfFirmadoUrl ?? consolidado?.PdfUrl,
                            };
                        })
                        .OrderByDescending(r => r.ActualizadoAt ?? DateTimeOffset.MinValue)
                        .ThenByDescending(r => r.PeriodoAnio).ThenByDescending(r => r.PeriodoMes)
                        .ToList();

                    var primera = porTrabajador.First();
                    return new SeguimientoColaboradorDto
                    {
                        WorkerId           = porTrabajador.Key,
                        Trabajador         = primera.Trabajador,
                        Area               = primera.Area,
                        TotalAbonado       = rendiciones.Sum(r => r.MontoAbonado),
                        RendicionesPagadas = rendiciones.Count,
                        UltimoPagoAt       = rendiciones.Max(r => r.ActualizadoAt),
                        Rendiciones        = rendiciones,
                    };
                })
                .OrderByDescending(c => c.UltimoPagoAt ?? DateTimeOffset.MinValue)
                .ThenBy(c => c.Trabajador)
                .ToList();

            // Se filtra por periodo acá y no en la consulta porque el periodo es el de la planilla
            // (el mes de sus salidas), no una columna: se conoce recién con las filas agrupadas.
            if (filters.PeriodoAnio.HasValue && filters.PeriodoMes.HasValue)
            {
                foreach (var c in colaboradores)
                {
                    c.Rendiciones = c.Rendiciones
                        .Where(r => r.PeriodoAnio == filters.PeriodoAnio.Value
                                 && r.PeriodoMes  == filters.PeriodoMes.Value)
                        .ToList();
                    c.TotalAbonado       = c.Rendiciones.Sum(r => r.MontoAbonado);
                    c.RendicionesPagadas = c.Rendiciones.Count;
                    c.UltimoPagoAt       = c.Rendiciones.Count == 0 ? null : c.Rendiciones.Max(r => r.ActualizadoAt);
                }
                colaboradores = colaboradores.Where(c => c.Rendiciones.Count > 0).ToList();
            }

            // Misma búsqueda libre que la bandeja, contra lo que esta vista muestra.
            var texto = filters.Texto?.Trim();
            if (!string.IsNullOrEmpty(texto))
                colaboradores = colaboradores
                    .Where(c =>
                        c.Trabajador.Contains(texto, StringComparison.OrdinalIgnoreCase)
                        || (c.Area ?? string.Empty).Contains(texto, StringComparison.OrdinalIgnoreCase)
                        || c.Rendiciones.Any(r =>
                            r.Codigo.Contains(texto, StringComparison.OrdinalIgnoreCase)
                            || (r.NumeroPlanilla ?? string.Empty).Contains(texto, StringComparison.OrdinalIgnoreCase)
                            || (r.NumeroGuia ?? string.Empty).Contains(texto, StringComparison.OrdinalIgnoreCase)
                            || r.Periodo.Contains(texto, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

            return new ReembolsoSeguimientoDto
            {
                Colaboradores      = colaboradores,
                TotalAbonado       = colaboradores.Sum(c => c.TotalAbonado),
                RendicionesPagadas = colaboradores.Sum(c => c.RendicionesPagadas),
                ColaboradoresCount = colaboradores.Count,
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// El universo de Tesorería: salidas rendidas cuyo reembolso ya está Firmado, confirmado
        /// para pago o Pagado. El filtro de estado del desplegable solo puede recortar ESE
        /// conjunto, nunca ampliarlo.
        /// </summary>
        /// <summary>
        /// El universo de Tesorería: lo que la jefatura ya firmó, lo que ella misma confirmó, lo
        /// que ya pagó y —desde RG-49— lo que ella misma devolvió con una observación.
        ///
        /// Lo observado entra por el PAR (estado + origen) y no por el estado solo: una planilla
        /// que devolvió la jefatura en la segunda revisión también está Observada, pero nunca
        /// llegó a Tesorería y no tiene por qué aparecer en su bandeja. Y lo que Tesorería devolvió
        /// sí tiene que seguir viéndose, o observar haría desaparecer la fila y nadie podría
        /// seguirle el rastro.
        /// </summary>
        private static IQueryable<GaSolicitudSalida> SalidasDeTesoreria(
            AppDbContext ctx, ReembolsoFiltersDto filters)
        {
            var visibles  = EstadosSalida.Reembolso.VisiblesParaTesoreria;
            const int observado = EstadosSalida.Reembolso.Observado;
            const int porTesoreria = EstadosSalida.OrigenObservacionReembolso.Tesoreria;

            var query = ctx.GaSolicitudSalida
                .Where(s => s.RendicionId != null
                         && (visibles.Contains(s.EstadoReembolsoId)
                          || (s.EstadoReembolsoId == observado
                           && s.ObservacionReembolsoOrigenId == porTesoreria)));

            var estadoId = EstadosSalida.Reembolso.IdFromNombre(filters.EstadoReembolso);
            if (estadoId == observado)
                query = query.Where(s => s.EstadoReembolsoId == observado
                                      && s.ObservacionReembolsoOrigenId == porTesoreria);
            else if (estadoId.HasValue && visibles.Contains(estadoId.Value))
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

        /// <summary>
        /// Mueve las salidas de un estado del reembolso al siguiente, dejando el rastro de quién y
        /// cuándo en la columna que corresponde al paso. Las que no estén en el estado de partida
        /// se ignoran: la selección viene de una pantalla que pudo quedar desactualizada.
        /// </summary>
        private async Task<List<int>> CambiarEstadoAsync(
            IEnumerable<int> ids, int desde, int hasta, int tesoreroUserId, string mensajeSiNada)
        {
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            var solicitudes = await ctx.GaSolicitudSalida
                .Where(s => idsList.Contains(s.Id) && s.EstadoReembolsoId == desde)
                .ToListAsync();

            if (solicitudes.Count == 0) throw new AbrilException(mensajeSiNada, 400);

            var now = DateTimeOffset.UtcNow;
            foreach (var s in solicitudes)
            {
                s.EstadoReembolsoId = hasta;
                s.UpdatedAt         = now;

                if (hasta == EstadosSalida.Reembolso.PorPagar)
                {
                    s.RevisionTesoreriaPorId = tesoreroUserId;
                    s.RevisionTesoreriaAt    = now;
                }
                else
                {
                    s.PagadoPorId = tesoreroUserId;
                    s.PagadoAt    = now;
                }
            }

            await ctx.SaveChangesAsync();
            return solicitudes.Select(s => s.Id).ToList();
        }

        /// <summary>
        /// Los tramos de cada salida con su importe y sus sustentos. Es el mismo criterio de
        /// importe que imprime la planilla (<see cref="ImporteRendidoLoader"/>) para que Tesorería
        /// no vea un número distinto del que está firmado en el papel.
        /// </summary>
        private static async Task<Dictionary<int, List<ReembolsoTramoDto>>> CargarTramosAsync(
            AppDbContext ctx, List<int> solicitudIds)
        {
            var result = new Dictionary<int, List<ReembolsoTramoDto>>();
            if (solicitudIds.Count == 0) return result;

            var trayectos = await (
                from t  in ctx.GaSolicitudTrayecto
                join m  in ctx.GaMotivoSalida on t.MotivoId equals m.Id into mGroup
                from m  in mGroup.DefaultIfEmpty()
                join lo in ctx.GaLugar on t.LugarOrigenId equals lo.Id into loGroup
                from lo in loGroup.DefaultIfEmpty()
                join po in ctx.Project on lo.ProjectId equals (int?)po.ProjectId into poGroup
                from po in poGroup.DefaultIfEmpty()
                join ld in ctx.GaLugar on t.LugarDestinoId equals ld.Id into ldGroup
                from ld in ldGroup.DefaultIfEmpty()
                join pd in ctx.Project on ld.ProjectId equals (int?)pd.ProjectId into pdGroup
                from pd in pdGroup.DefaultIfEmpty()
                where solicitudIds.Contains(t.SolicitudId)
                orderby t.SolicitudId, t.Orden
                select new
                {
                    t.Id,
                    t.SolicitudId,
                    t.Orden,
                    t.HoraSalida,
                    t.HoraRetorno,
                    t.LugarOrigenId,
                    t.LugarDestinoId,
                    Motivo       = m != null ? m.Descripcion : (t.MotivoLibre ?? string.Empty),
                    LugarOrigen  = lo == null ? t.LugarOrigenLibre
                                 : lo.Tipo == "proyecto" ? (po != null ? po.ProjectDescription : "[Sin proyecto]")
                                 : lo.Nombre,
                    LugarDestino = ld == null ? t.LugarDestinoLibre
                                 : ld.Tipo == "proyecto" ? (pd != null ? pd.ProjectDescription : "[Sin proyecto]")
                                 : ld.Nombre,
                    t.AdjuntoUrl,
                    t.AdjuntoFilename,
                }
            ).ToListAsync();

            if (trayectos.Count == 0) return result;

            var trayectoIds = trayectos.Select(t => t.Id).ToList();

            var capturas = await ctx.GaSolicitudCaptura
                .Where(c => trayectoIds.Contains(c.TrayectoId))
                .OrderBy(c => c.UploadedAt).ThenBy(c => c.Id)
                .Select(c => new
                {
                    c.TrayectoId,
                    Dto = new ReembolsoCapturaDto
                    {
                        Id       = c.Id,
                        ImageUrl = c.ImageUrl,
                        Filename = c.Filename,
                        Monto    = c.Monto,
                    },
                })
                .ToListAsync();

            var capturasPorTrayecto = capturas
                .GroupBy(x => x.TrayectoId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Dto).ToList());

            var adjuntos = await ctx.GaSolicitudTrayectoAdjunto
                .Where(a => trayectoIds.Contains(a.TrayectoId))
                .OrderBy(a => a.UploadedAt).ThenBy(a => a.Id)
                .Select(a => new { a.TrayectoId, a.AdjuntoUrl, a.AdjuntoFilename })
                .ToListAsync();

            var adjuntosPorTrayecto = adjuntos
                .GroupBy(a => a.TrayectoId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(a => new ReembolsoAdjuntoDto
                    {
                        Url = a.AdjuntoUrl,
                        Filename = a.AdjuntoFilename,
                    }).ToList());

            // La subárea decide si el importe puede salir del tarifario (solo TI).
            var subareaPorSolicitud = await ctx.GaSolicitudSalida
                .Where(s => solicitudIds.Contains(s.Id))
                .Join(ctx.Worker, s => s.WorkerId, w => w.Id, (s, w) => new { s.Id, w.Subarea })
                .ToDictionaryAsync(x => x.Id, x => x.Subarea);

            var importes = await ImporteRendidoLoader.LoadAsync(
                ctx,
                trayectos
                    .Select(t => new ImporteRendidoLoader.TrayectoParaImporte(
                        t.Id,
                        subareaPorSolicitud.TryGetValue(t.SolicitudId, out var sub) ? sub : null,
                        t.LugarOrigenId,
                        t.LugarDestinoId))
                    .ToList());

            foreach (var grupo in trayectos.GroupBy(t => t.SolicitudId))
            {
                result[grupo.Key] = grupo
                    .OrderBy(t => t.Orden)
                    .Select(t =>
                    {
                        var lista = new List<ReembolsoAdjuntoDto>();
                        // Adjunto legacy embebido (modelo 1:1 anterior) + los de la tabla nueva.
                        if (!string.IsNullOrWhiteSpace(t.AdjuntoUrl))
                            lista.Add(new ReembolsoAdjuntoDto
                            {
                                Url      = t.AdjuntoUrl,
                                Filename = t.AdjuntoFilename ?? "Ver documento",
                            });
                        if (adjuntosPorTrayecto.TryGetValue(t.Id, out var nuevos)) lista.AddRange(nuevos);

                        importes.TryGetValue(t.Id, out var importe);

                        return new ReembolsoTramoDto
                        {
                            Id              = t.Id,
                            Orden           = t.Orden,
                            HoraSalida      = t.HoraSalida?.ToString("HH:mm"),
                            HoraRetorno     = t.HoraRetorno?.ToString("HH:mm"),
                            Motivo          = t.Motivo,
                            LugarOrigen     = t.LugarOrigen,
                            LugarDestino    = t.LugarDestino,
                            Monto           = importe.Importe,
                            MontoDeCatalogo = importe.EsCatalogo,
                            Capturas        = capturasPorTrayecto.TryGetValue(t.Id, out var caps) ? caps : new(),
                            Adjuntos        = lista,
                        };
                    })
                    .ToList();
            }

            return result;
        }

        /// <summary>Importe rendido por salida, con la misma regla que imprime la planilla.</summary>
        private static async Task<Dictionary<int, decimal>> MontoPorSolicitudAsync(
            AppDbContext ctx, Dictionary<int, string?> subareaPorSolicitud)
        {
            var solicitudIds = subareaPorSolicitud.Keys.ToList();
            if (solicitudIds.Count == 0) return new();

            var trayectos = await ctx.GaSolicitudTrayecto
                .Where(t => solicitudIds.Contains(t.SolicitudId))
                .Select(t => new { t.Id, t.SolicitudId, t.LugarOrigenId, t.LugarDestinoId })
                .ToListAsync();

            var importes = await ImporteRendidoLoader.LoadAsync(
                ctx,
                trayectos
                    .Select(t => new ImporteRendidoLoader.TrayectoParaImporte(
                        t.Id,
                        subareaPorSolicitud.TryGetValue(t.SolicitudId, out var sub) ? sub : null,
                        t.LugarOrigenId,
                        t.LugarDestinoId))
                    .ToList());

            return trayectos
                .GroupBy(t => t.SolicitudId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(t => importes.TryGetValue(t.Id, out var imp) ? imp.Importe : 0m));
        }

        /// <summary>app_user.user_id → nombre completo, para los tres rastros que muestra la bandeja.</summary>
        private static async Task<Dictionary<int, string>> NombrePorUserIdAsync(
            AppDbContext ctx, IEnumerable<int> userIds)
        {
            var ids = userIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return new();

            var filas = await ctx.Person
                .Where(p => p.UserId != null && ids.Contains(p.UserId!.Value) && p.FullName != null)
                .Select(p => new { UserId = p.UserId!.Value, p.FullName })
                .ToListAsync();

            return filas
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.First().FullName!);
        }

        /// <summary>Nombres de quienes firmaron, confirmaron o pagaron las planillas del listado.</summary>
        private static Task<Dictionary<int, string>> NombresDeUsuariosAsync(
            AppDbContext ctx, List<PlanillaRendicionLoader.PlanillaFila> planillas)
        {
            var ids = new List<int>();
            foreach (var p in planillas)
            {
                if (p.FirmadoPorId.HasValue)           ids.Add(p.FirmadoPorId.Value);
                if (p.ReembolsoDecididoPorId.HasValue) ids.Add(p.ReembolsoDecididoPorId.Value);
                foreach (var s in p.Salidas)
                {
                    if (s.RevisionTesoreriaPorId.HasValue) ids.Add(s.RevisionTesoreriaPorId.Value);
                    if (s.PagadoPorId.HasValue)            ids.Add(s.PagadoPorId.Value);
                }
            }
            return NombrePorUserIdAsync(ctx, ids);
        }

        private static ReembolsoListItemDto Armar(
            PlanillaRendicionLoader.PlanillaFila p, Dictionary<int, string> nombres)
        {
            // El rastro de Tesorería es por salida, pero la pantalla es por planilla: se toma el
            // más reciente, que es el que responde "¿cuándo se movió esto por última vez?".
            var revisionAt   = p.Salidas.Max(s => s.RevisionTesoreriaAt);
            var pagadoAt     = p.Salidas.Max(s => s.PagadoAt);
            var revisionPor  = p.Salidas.OrderByDescending(s => s.RevisionTesoreriaAt)
                                        .Select(s => s.RevisionTesoreriaPorId).FirstOrDefault(x => x.HasValue);
            var pagadoPor    = p.Salidas.OrderByDescending(s => s.PagadoAt)
                                        .Select(s => s.PagadoPorId).FirstOrDefault(x => x.HasValue);

            return new ReembolsoListItemDto
            {
                Id                 = p.Id,
                Codigo             = p.Codigo,
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
                FirmadoPor         = p.FirmadoPorId.HasValue && nombres.TryGetValue(p.FirmadoPorId.Value, out var f)
                                        ? f : null,
                ConsolidadoS10     = p.ConsolidadoS10,
                EstadoReembolso    = p.EstadoReembolso,
                ReembolsoMixto     = p.ReembolsoMixto,
                PorConfirmarCount  = p.Salidas.Count(s => s.EstadoReembolsoId == EstadosSalida.Reembolso.Firmado),
                PorPagarCount      = p.Salidas.Count(s => s.EstadoReembolsoId == EstadosSalida.Reembolso.PorPagar),
                // Solo las que devolvió TESORERÍA: es lo único que la consulta trae observado, pero
                // se repite acá porque este conteo decide si la fila se puede seleccionar.
                ObservadasCount    = p.Salidas.Count(
                    s => s.EstadoReembolsoId == EstadosSalida.Reembolso.Observado
                      && s.ObservacionReembolsoOrigenId == EstadosSalida.OrigenObservacionReembolso.Tesoreria),
                ObservacionReembolso = p.ObservacionReembolso,
                ObservadoAt          = p.ReembolsoDecididoAt,
                ObservadoPor         = p.ReembolsoDecididoPorId.HasValue
                                    && nombres.TryGetValue(p.ReembolsoDecididoPorId.Value, out var obs)
                                        ? obs : null,
                RevisionTesoreriaAt  = revisionAt,
                RevisionTesoreriaPor = revisionPor.HasValue && nombres.TryGetValue(revisionPor.Value, out var r)
                                        ? r : null,
                PagadoAt             = pagadoAt,
                PagadoPor            = pagadoPor.HasValue && nombres.TryGetValue(pagadoPor.Value, out var pg)
                                        ? pg : null,
            };
        }

        private static void CopiarCabecera(ReembolsoListItemDto o, ReembolsoDetalleDto d)
        {
            d.Id = o.Id; d.Codigo = o.Codigo; d.NumeroPlanilla = o.NumeroPlanilla; d.RendidoAt = o.RendidoAt;
            d.Periodo = o.Periodo; d.PeriodoAnio = o.PeriodoAnio; d.PeriodoMes = o.PeriodoMes;
            d.Trabajadores = o.Trabajadores; d.SalidasCount = o.SalidasCount; d.MontoTotal = o.MontoTotal;
            d.PdfUrl = o.PdfUrl; d.PdfFilename = o.PdfFilename;
            d.PdfFirmadoUrl = o.PdfFirmadoUrl; d.PdfFirmadoFilename = o.PdfFirmadoFilename;
            d.FirmadoAt = o.FirmadoAt; d.FirmadoPor = o.FirmadoPor; d.ConsolidadoS10 = o.ConsolidadoS10;
            d.EstadoReembolso = o.EstadoReembolso; d.ReembolsoMixto = o.ReembolsoMixto;
            d.PorConfirmarCount = o.PorConfirmarCount; d.PorPagarCount = o.PorPagarCount;
            d.ObservadasCount = o.ObservadasCount; d.ObservacionReembolso = o.ObservacionReembolso;
            d.ObservadoAt = o.ObservadoAt; d.ObservadoPor = o.ObservadoPor;
            d.RevisionTesoreriaAt = o.RevisionTesoreriaAt; d.RevisionTesoreriaPor = o.RevisionTesoreriaPor;
            d.PagadoAt = o.PagadoAt; d.PagadoPor = o.PagadoPor;
        }
    }
}
