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
    /// jefatura ya firmó, lo que ella misma confirmó, lo que ya pagó y lo que ella misma devolvió.
    ///
    /// La fila NO se arma desde la planilla sino desde la SALIDA: el consolidado de cada una se
    /// resuelve con la precedencia de <see cref="ConsolidadoS10Loader"/> (el propio de la salida si
    /// lo tiene —solo en registros antiguos—, y si no el de su planilla) y después se agrupan las
    /// salidas por documento. Es el mismo armado de la pantalla Consolidados, que es donde la
    /// jefatura decide sobre ese mismo documento: lo que se firma y lo que se paga tienen que ser
    /// la misma unidad o el importe del S10 no cuadra nunca con la fila.
    /// </summary>
    public class ReembolsoRepository : IReembolsoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ReembolsoRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<List<ReembolsoListItemDto>> GetAll(ReembolsoFiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();

            var armado = await ArmarAsync(ctx, SalidasDeTesoreria(ctx, filters));
            return Filtrar(armado.Items, filters);
        }

        public async Task<ReembolsoDetalleDto?> GetDetalle(int consolidadoId)
        {
            using var ctx = _factory.CreateDbContext();

            var rendicionIds = await RendicionesCubiertasAsync(ctx, consolidadoId);
            if (rendicionIds.Count == 0) return null;

            var query = SalidasDeTesoreria(ctx, new ReembolsoFiltersDto())
                .Where(s => s.RendicionId != null && rendicionIds.Contains(s.RendicionId!.Value));

            var armado = await ArmarAsync(ctx, query, conDetalle: true);

            var cabecera = armado.Items.FirstOrDefault(x => x.Id == consolidadoId);
            if (cabecera == null) return null;

            var detalle = new ReembolsoDetalleDto();
            CopiarCabecera(cabecera, detalle);
            detalle.Salidas = armado.Salidas.TryGetValue(consolidadoId, out var salidas)
                ? salidas
                : new List<ReembolsoSalidaDto>();
            return detalle;
        }

        public async Task<SolicitudSalidaDetalleDto?> GetSalidaDetalle(int solicitudId)
        {
            using var ctx = _factory.CreateDbContext();

            // Solo una salida de la bandeja: mandar un id cualquiera no abre una salida que todavía no
            // llegó a Tesorería.
            var enBandeja = await SalidasDeTesoreria(ctx, new ReembolsoFiltersDto())
                .AnyAsync(s => s.Id == solicitudId);
            if (!enBandeja) return null;

            return await SalidaDetalleLoader.LoadAsync(ctx, solicitudId, conAptitudParaRendir: false);
        }

        public async Task<List<ConsolidadoCorreoDatos>> GetConsolidadoCorreoDatos(IReadOnlyCollection<int> solicitudIds)
        {
            using var ctx = _factory.CreateDbContext();
            return await ConsolidadoCorreoLoader.LoadAsync(ctx, solicitudIds);
        }

        public async Task<List<string>> GetCorreosTesoreria()
        {
            using var ctx = _factory.CreateDbContext();
            return await CorreosTesoreriaLoader.LoadAsync(ctx);
        }

        public async Task<ReembolsoPorPagarCorreoInfoDto> GetPorPagarCorreoInfo(IReadOnlyCollection<int> solicitudIds)
        {
            var info = new ReembolsoPorPagarCorreoInfoDto();
            var ids  = solicitudIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return info;

            using var ctx = _factory.CreateDbContext();

            // El documento de cada salida confirmada, con la precedencia del módulo: el aviso es por
            // consolidado, no por salida ni por planilla.
            var confirmadas = await ctx.GaSolicitudSalida
                .Where(s => ids.Contains(s.Id))
                .Select(s => new { s.Id, s.RendicionId })
                .ToListAsync();

            var consolidadoIds = (await ConsolidadoS10Loader.LoadAsync(
                    ctx, confirmadas.ToDictionary(x => x.Id, x => x.RendicionId)))
                .Values
                .Select(c => c.Id)
                .Distinct()
                .ToList();
            if (consolidadoIds.Count == 0) return info;

            // Lo que cada uno tiene HOY por pagar —lo recién confirmado y lo que se hubiera
            // confirmado antes—, que es exactamente lo que va a desembolsar «Marcar como pagado».
            var porPagar = await SalidasPorConsolidadoAsync(
                ctx, consolidadoIds, new[] { EstadosSalida.Reembolso.PorPagar });
            if (porPagar.Count == 0) return info;

            var porPagarIds = porPagar.Keys.ToList();
            var subareaPorSolicitud = await (
                from s in ctx.GaSolicitudSalida.Where(x => porPagarIds.Contains(x.Id))
                join w in ctx.Worker on s.WorkerId equals w.Id
                select new { s.Id, Subarea = (string?)w.Subarea }
            ).ToDictionaryAsync(x => x.Id, x => x.Subarea);

            var montoPorSolicitud = await MontoPorSolicitudAsync(ctx, subareaPorSolicitud);

            // El área es la del consolidador, la misma con la que se armó el código CONS-SIGLA y la
            // que lleva el aviso de consolidado firmado.
            var conPorPagar = porPagar.Values.Distinct().ToList();
            var cabeceras = await (
                from c  in ctx.GaConsolidadoS10.AsNoTracking()
                join s  in ctx.AreaScope on c.AreaScopeId equals (int?)s.AreaScopeId into sGroup
                from s  in sGroup.DefaultIfEmpty()
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId into aiGroup
                from ai in aiGroup.DefaultIfEmpty()
                where conPorPagar.Contains(c.Id)
                select new { c.Id, c.Codigo, c.NumeroReembolso, Area = ai != null ? ai.AreaItemName : null }
            ).ToListAsync();

            info.Destinatarios = await CorreosTesoreriaLoader.LoadAsync(ctx);
            info.Consolidados  = cabeceras
                .OrderBy(c => c.Id)
                .Select(c => new ConsolidadoPorPagarCorreoDatos
                {
                    ConsolidadoId   = c.Id,
                    Codigo          = c.Codigo,
                    Area            = c.Area,
                    NumeroReembolso = c.NumeroReembolso,
                    MontoTotal      = porPagar
                        .Where(kv => kv.Value == c.Id)
                        .Sum(kv => montoPorSolicitud.TryGetValue(kv.Key, out var m) ? m : 0m),
                })
                .ToList();

            return info;
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

            // El periodo de un consolidado es el mes de su salida más antigua — el mismo criterio
            // que la tabla, si no el filtro dejaría fuera consolidados que sí muestra. Se agrupa
            // por documento (y no por planilla) porque uno puede cubrir varias.
            var salidas = await SalidasDeTesoreria(ctx, new ReembolsoFiltersDto())
                .Select(s => new { s.Id, s.RendicionId, s.FechaSalida })
                .ToListAsync();

            var consolidados = await ConsolidadoS10Loader.LoadAsync(
                ctx, salidas.ToDictionary(x => x.Id, x => x.RendicionId));

            var periodos = salidas
                .Where(x => consolidados.ContainsKey(x.Id))
                .GroupBy(x => consolidados[x.Id].Id)
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

        public Task<List<int>> ResolverSolicitudIds(IEnumerable<int> consolidadoIds, int estadoId) =>
            ResolverSolicitudIds(consolidadoIds, new[] { estadoId });

        public async Task<List<int>> ResolverSolicitudIds(IEnumerable<int> consolidadoIds, int[] estadoIds)
        {
            var ids = consolidadoIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();
            return (await SalidasPorConsolidadoAsync(ctx, ids, estadoIds)).Keys.ToList();
        }

        /// <summary>
        /// solicitudId → consolidadoId de las salidas de la bandeja que están en
        /// <paramref name="estadoIds"/> y cuyo consolidado vigente es uno de <paramref name="ids"/>.
        /// Es la traducción de <see cref="ResolverSolicitudIds(IEnumerable{int}, int[])"/>,
        /// conservando a qué documento pertenece cada salida para quien tiene que agrupar por él.
        /// </summary>
        private static async Task<Dictionary<int, int>> SalidasPorConsolidadoAsync(
            AppDbContext ctx, List<int> ids, int[] estadoIds)
        {
            // Las planillas que cubren esos consolidados y, de ellas, solo las salidas que están en
            // la bandeja y en el estado que la acción admite: la selección viene de una pantalla
            // que pudo quedar desactualizada.
            var rendicionIds = await ctx.GaConsolidadoS10Rendicion
                .Where(v => v.State && ids.Contains(v.ConsolidadoS10Id))
                .Select(v => v.RendicionId)
                .Distinct()
                .ToListAsync();

            var candidatas = await SalidasDeTesoreria(ctx, new ReembolsoFiltersDto())
                .Where(s => estadoIds.Contains(s.EstadoReembolsoId))
                .Where(s => s.RendicionId != null
                         && (rendicionIds.Contains(s.RendicionId!.Value)
                          || ctx.GaConsolidadoS10.Any(
                                 c => c.State && c.SolicitudId == s.Id && ids.Contains(c.Id))))
                .Select(s => new { s.Id, s.RendicionId })
                .ToListAsync();

            if (candidatas.Count == 0) return new();

            // Y se descartan las salidas de esas planillas cuyo consolidado vigente es OTRO (pasa
            // solo con los registros antiguos): el documento sobre el que se actúa es el elegido.
            var consolidados = await ConsolidadoS10Loader.LoadAsync(
                ctx, candidatas.ToDictionary(x => x.Id, x => x.RendicionId));

            return candidatas
                .Where(x => consolidados.TryGetValue(x.Id, out var c) && ids.Contains(c.Id))
                .ToDictionary(x => x.Id, x => consolidados[x.Id].Id);
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
        /// Solo se observa lo que Tesorería todavía no confirmó
        /// (<see cref="EstadosSalida.Reembolso.ObservablesPorTesoreria"/>): con la revisión
        /// confirmada el consolidado sigue al pago. Así que no hay una revisión que deshacer; sus
        /// columnas se limpian igual, para que una salida observada nunca arrastre un visto bueno.
        /// El rastro del pago no se toca porque una salida pagada nunca llega acá.
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
                    + "está firmado y todavía sin la revisión confirmada. Lo confirmado o pagado no vuelve.", 400);

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

            // El consolidador de cada planilla es quien subió su consolidado: el aviso de pago lo
            // nombra debajo del trabajador.
            var subidoPor = await SubidoPorAsync(
                ctx, consolidados.Values.Select(c => c.Id).Distinct().ToList());

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

                    var consolidador = consolidado != null && subidoPor.TryGetValue(consolidado.Id, out var quien)
                        ? quien.Nombre
                        : null;

                    return new ReembolsoPlanillaCorreoDatos
                    {
                        RendicionId     = g.Key.RendicionId,
                        Codigo          = PlanillaRendicionHelper.CodigoRendicion(planilla?.Codigo, g.Key.RendicionId),
                        Trabajador      = primera.Trabajador,
                        TrabajadorEmail = primera.Email,
                        Consolidador    = consolidador,
                        Area            = primera.Area,
                        NumeroPlanilla  = PlanillaRendicionHelper.NumeroPlanilla(planilla?.NumeroPlanilla),
                        Periodo         = PlanillaRendicionHelper.EtiquetaPeriodo(desde, hasta),
                        SalidasCount    = g.Count(),
                        MontoTotal      = g.Sum(x => montoPorSolicitud.TryGetValue(x.Id, out var m) ? m : 0m),
                        NumeroReembolso = consolidado?.NumeroReembolso,
                        PagadoPor       = pagadoPorId.HasValue && nombresUsuario.TryGetValue(pagadoPorId.Value, out var n)
                                            ? n : null,
                    };
                })
                .ToList();
        }

        public async Task<ReembolsoSeguimientoDto> GetSeguimiento(ReembolsoFiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();

            // El seguimiento es de PAGOS: el estado no sale del filtro de la bandeja sino que se
            // fija acá. Va explícito porque SalidasDeTesoreria ya no recorta por estado —la
            // bandeja lo hace sobre la fila armada— y sin esto el histórico traería todo.
            var alcance = new ReembolsoFiltersDto
            {
                WorkerId           = filters.WorkerId,
                FilterAreaScopeIds = filters.FilterAreaScopeIds,
            };

            var filas = await (
                from s   in SalidasDeTesoreria(ctx, alcance)
                             .Where(x => x.EstadoReembolsoId == EstadosSalida.Reembolso.Pagado)
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
                                NumeroReembolso   = consolidado?.NumeroReembolso,
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
                            || (r.NumeroReembolso ?? string.Empty).Contains(texto, StringComparison.OrdinalIgnoreCase)
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

        // ── Armado de las filas ──────────────────────────────────────────────

        /// <summary>
        /// Lo que devuelve <see cref="ArmarAsync"/>: las filas y, si se pidió con detalle, las
        /// salidas de cada consolidado. Van juntas porque ya se recorrieron para armar la cabecera:
        /// devolverlas aparte obligaría a volver a la base por lo mismo.
        /// </summary>
        private sealed record Armado(
            List<ReembolsoListItemDto> Items,
            Dictionary<int, List<ReembolsoSalidaDto>> Salidas);

        private static async Task<Armado> ArmarAsync(
            AppDbContext ctx,
            IQueryable<GaSolicitudSalida> salidasDeTesoreria,
            bool conDetalle = false)
        {
            var vacio = new Armado(new(), new());

            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, salidasDeTesoreria, conDetalle);
            if (planillas.Count == 0) return vacio;

            // Consolidado de cada salida, con la precedencia del módulo. Una salida sin consolidado
            // no puede estar acá —firmar el reembolso ES firmar su consolidado—, pero si alguna
            // quedara suelta se ignora en vez de inventarle una fila sin documento que revisar.
            var rendicionPorSolicitud = planillas
                .SelectMany(p => p.Salidas.Select(s => (SolicitudId: s.Id, RendicionId: (int?)p.Id)))
                .ToDictionary(x => x.SolicitudId, x => x.RendicionId);

            var consolidadoPorSolicitud = await ConsolidadoS10Loader.LoadAsync(ctx, rendicionPorSolicitud);
            if (consolidadoPorSolicitud.Count == 0) return vacio;

            // Un grupo por documento: sus planillas en la bandeja y, dentro de cada una, las salidas
            // que ese documento cubre (en los consolidados por salida es solo una de ellas).
            var grupos = new Dictionary<int, List<(PlanillaRendicionLoader.PlanillaFila Planilla,
                                                  List<PlanillaRendicionLoader.SalidaFila> Salidas)>>();
            var dtoPorConsolidado = new Dictionary<int, ConsolidadoS10Dto>();

            foreach (var planilla in planillas)
            {
                foreach (var grupo in planilla.Salidas
                             .Where(s => consolidadoPorSolicitud.ContainsKey(s.Id))
                             .GroupBy(s => consolidadoPorSolicitud[s.Id].Id))
                {
                    dtoPorConsolidado[grupo.Key] = consolidadoPorSolicitud[grupo.First().Id];

                    if (!grupos.TryGetValue(grupo.Key, out var lista))
                        grupos[grupo.Key] = lista = new();

                    lista.Add((planilla, grupo.ToList()));
                }
            }

            if (grupos.Count == 0) return vacio;

            // Lo que hay que resolver mirando el documento entero y no solo lo que llegó a la
            // bandeja: las planillas que cubre y todavía esperan a su jefatura, con su monto
            // completo, para que el importe declarado en el S10 se pueda contrastar.
            var cubiertas = dtoPorConsolidado.Values
                .SelectMany(c => c.Rendiciones.Select(r => r.Id))
                .Concat(grupos.SelectMany(g => g.Value.Select(x => x.Planilla.Id)))
                .Distinct()
                .ToList();

            var enBandeja     = planillas.ToDictionary(p => p.Id);
            var totalesFuera  = await TotalPlanillaLoader.LoadAsync(
                ctx, cubiertas.Where(id => !enBandeja.ContainsKey(id)).ToList());

            // Quién adjuntó cada consolidado, con qué área quedó y bajo qué razón social: las del
            // consolidador, no las de los trabajadores, que pueden ser de varias.
            var subidoPor = await SubidoPorAsync(ctx, grupos.Keys.ToList());
            var razones   = await RazonSocialConsolidador.LoadPorUsuarioAsync(
                ctx, subidoPor.Values.Select(x => x.UserId).Distinct().ToList());

            // Los nombres de todos los usuarios que firmaron, observaron, confirmaron o pagaron, de
            // una vez: resolverlos fila por fila sería un N+1 sobre la tabla más chica del flujo.
            var nombres = await NombresDeUsuariosAsync(ctx, planillas);

            decimal MontoCompletoDe(int rendicionId) => enBandeja.TryGetValue(rendicionId, out var fila)
                ? fila.MontoTotalPlanilla
                : totalesFuera.GetValueOrDefault(rendicionId);

            string? NombreDe(int? userId) =>
                userId.HasValue && nombres.TryGetValue(userId.Value, out var n) ? n : null;

            var salidasDelDetalle = new Dictionary<int, List<ReembolsoSalidaDto>>();
            var items = new List<ReembolsoListItemDto>(grupos.Count);

            foreach (var (consolidadoId, lista) in grupos)
            {
                var dto     = dtoPorConsolidado[consolidadoId];
                var salidas = lista.SelectMany(x => x.Salidas).ToList();

                // Los códigos del documento salen de sus vínculos vigentes; un consolidado por
                // salida no tiene ninguno, así que su única planilla es la de esa salida.
                var codigoCubierta = dto.Rendiciones.ToDictionary(r => r.Id, r => r.Codigo);
                foreach (var (planilla, _) in lista) codigoCubierta.TryAdd(planilla.Id, planilla.Codigo);

                var rendiciones = codigoCubierta
                    .Select(kv =>
                    {
                        var deLaBandeja = lista.FirstOrDefault(x => x.Planilla.Id == kv.Key);
                        if (deLaBandeja.Planilla == null)
                        {
                            return new ReembolsoPlanillaDto
                            {
                                Id                 = kv.Key,
                                Codigo             = kv.Value,
                                EnBandeja          = false,
                                MontoTotalPlanilla = MontoCompletoDe(kv.Key),
                            };
                        }

                        var p     = deLaBandeja.Planilla;
                        var suyas = deLaBandeja.Salidas;
                        return new ReembolsoPlanillaDto
                        {
                            Id                 = p.Id,
                            Codigo             = p.Codigo,
                            EnBandeja          = true,
                            MontoTotalPlanilla = p.MontoTotalPlanilla,
                            NumeroPlanilla     = p.NumeroPlanilla,
                            Periodo            = PlanillaRendicionHelper.EtiquetaPeriodo(
                                                     suyas.Min(s => s.FechaSalida),
                                                     suyas.Max(s => s.FechaSalida)),
                            Trabajadores       = suyas.Select(s => s.Trabajador).Distinct().ToList(),
                            SalidasCount       = suyas.Count,
                            Monto              = suyas.Sum(s => s.Monto),
                            EstadoReembolso    = PlanillaRendicionHelper.ResumirEstadoReembolso(
                                                     suyas.Select(s => s.EstadoReembolsoId)),
                            PdfUrl             = p.PdfUrl,
                            PdfFilename        = p.PdfFilename,
                            PdfFirmadoUrl      = p.PdfFirmadoUrl,
                            PdfFirmadoFilename = p.PdfFirmadoFilename,
                            FirmadoAt          = p.FirmadoAt,
                            FirmadoPor         = NombreDe(p.FirmadoPorId),
                        };
                    })
                    .OrderBy(r => r.Codigo, StringComparer.Ordinal)
                    .ToList();

                var desde = salidas.Min(s => s.FechaSalida);
                var hasta = salidas.Max(s => s.FechaSalida);

                // La observación vigente y quién la escribió salen de la MISMA salida: un
                // consolidado devuelto no puede mostrar el motivo de una y la fecha de otra.
                var observada = salidas
                    .Where(s => s.EstadoReembolsoId == EstadosSalida.Reembolso.Observado
                             && !string.IsNullOrWhiteSpace(s.ObservacionReembolso))
                    .OrderByDescending(s => s.ReembolsoDecididoAt)
                    .FirstOrDefault();

                // El rastro de Tesorería es por salida, pero la fila es por documento: se toma el
                // más reciente, que es el que responde "¿cuándo se movió esto por última vez?".
                var revisionAt  = salidas.Max(s => s.RevisionTesoreriaAt);
                var pagadoAt    = salidas.Max(s => s.PagadoAt);
                var revisionPor = salidas.OrderByDescending(s => s.RevisionTesoreriaAt)
                                         .Select(s => s.RevisionTesoreriaPorId).FirstOrDefault(x => x.HasValue);
                var pagadoPor   = salidas.OrderByDescending(s => s.PagadoAt)
                                         .Select(s => s.PagadoPorId).FirstOrDefault(x => x.HasValue);

                subidoPor.TryGetValue(dto.Id, out var quienSubio);
                var razon = quienSubio.UserId > 0 ? razones.GetValueOrDefault(quienSubio.UserId) : null;

                items.Add(new ReembolsoListItemDto
                {
                    Id              = dto.Id,
                    Codigo          = dto.Codigo,
                    NumeroReembolso = dto.NumeroReembolso,
                    PlanillaGrupalUrl      = dto.PlanillaGrupalUrl,
                    PlanillaGrupalFilename = dto.PlanillaGrupalFilename,
                    PlanillaGrupalFirmadoUrl      = dto.PlanillaGrupalFirmadoUrl,
                    PlanillaGrupalFirmadoFilename = dto.PlanillaGrupalFirmadoFilename,
                    MontoS10        = dto.MontoTotal,
                    MontoPlanillas  = rendiciones.Sum(r => r.MontoTotalPlanilla),
                    MontoTotal      = salidas.Sum(s => s.Monto),

                    PdfUrl             = dto.PdfUrl,
                    PdfFilename        = dto.PdfFilename,
                    PdfFirmadoUrl      = dto.PdfFirmadoUrl,
                    PdfFirmadoFilename = dto.PdfFirmadoFilename,
                    FirmadoAt          = dto.FirmadoAt,
                    UploadedAt         = dto.UploadedAt,
                    SubidoPor          = quienSubio.Nombre,
                    RazonSocial        = razon?.Nombre,
                    Area               = quienSubio.Area,

                    Rendiciones  = rendiciones,
                    Trabajadores = salidas.Select(s => s.Trabajador).Distinct().ToList(),
                    SalidasCount = salidas.Count,

                    Periodo     = PlanillaRendicionHelper.EtiquetaPeriodo(desde, hasta),
                    PeriodoAnio = desde.Year,
                    PeriodoMes  = desde.Month,

                    // Un consolidado compartido lo firman los jefes de cada planilla: la evidencia
                    // que Tesorería revisa son todas esas firmas, no una sola.
                    // Se agrupa por firmante y no se exige el nombre: si no se pudo resolver, la
                    // firma igual existe y esconderla dejaría a Tesorería sin la evidencia que
                    // tiene que mirar (la pantalla la rotula "Jefatura").
                    Firmas = rendiciones
                        .Where(r => r.FirmadoAt != null)
                        .GroupBy(r => r.FirmadoPor ?? string.Empty)
                        .Select(g => new FirmaJefaturaDto
                        {
                            Nombre    = g.Key,
                            FirmadoAt = g.Max(r => r.FirmadoAt),
                        })
                        .OrderBy(f => f.FirmadoAt)
                        .ToList(),

                    EstadoReembolso = PlanillaRendicionHelper.ResumirEstadoReembolso(
                                          salidas.Select(s => s.EstadoReembolsoId)),
                    ReembolsoMixto  = salidas.Select(s => s.EstadoReembolsoId).Distinct().Count() > 1,

                    PorConfirmarCount = salidas.Count(s => s.EstadoReembolsoId == EstadosSalida.Reembolso.Firmado),
                    PorPagarCount     = salidas.Count(s => s.EstadoReembolsoId == EstadosSalida.Reembolso.PorPagar),
                    // Solo las que devolvió TESORERÍA: es lo único que la consulta trae observado,
                    // pero se repite acá porque este conteo decide si la fila se puede seleccionar.
                    ObservadasCount   = salidas.Count(
                        s => s.EstadoReembolsoId == EstadosSalida.Reembolso.Observado
                          && s.ObservacionReembolsoOrigenId == EstadosSalida.OrigenObservacionReembolso.Tesoreria),

                    ObservacionReembolso = observada?.ObservacionReembolso,
                    ObservadoAt          = observada?.ReembolsoDecididoAt,
                    ObservadoPor         = NombreDe(observada?.ReembolsoDecididoPorId),

                    RevisionTesoreriaAt  = revisionAt,
                    RevisionTesoreriaPor = NombreDe(revisionPor),
                    PagadoAt             = pagadoAt,
                    PagadoPor            = NombreDe(pagadoPor),
                });

                if (conDetalle)
                {
                    salidasDelDetalle[dto.Id] = lista
                        .SelectMany(x => x.Salidas.Select(s => new ReembolsoSalidaDto
                        {
                            Id              = s.Id,
                            Codigo          = s.Codigo,
                            RendicionId     = x.Planilla.Id,
                            Trabajador      = s.Trabajador,
                            Area            = s.Area,
                            FechaSalida     = s.FechaSalida,
                            Motivo          = s.Motivo,
                            LugarOrigen     = s.LugarOrigen,
                            LugarDestino    = s.LugarDestino,
                            TrayectosCount  = s.TrayectosCount,
                            Monto           = s.Monto,
                            EstadoReembolso = s.EstadoReembolso,
                        }))
                        .ToList();
                }
            }

            // Lo último adjuntado primero: es lo que está esperando a Tesorería.
            return new Armado(
                items.OrderByDescending(x => x.UploadedAt).ThenByDescending(x => x.Id).ToList(),
                salidasDelDetalle);
        }

        /// <summary>
        /// Quién subió cada consolidado —el consolidador—: su usuario (para resolver la razón
        /// social bajo la que quedó el registro del S10), su nombre y el área con la que quedó el
        /// consolidado, que es la suya (la de la sigla del código y la de la planilla grupal).
        /// </summary>
        private static async Task<Dictionary<int, (int UserId, string? Nombre, string? Area)>> SubidoPorAsync(
            AppDbContext ctx, List<int> consolidadoIds)
        {
            if (consolidadoIds.Count == 0) return new();

            var filas = await (
                from c   in ctx.GaConsolidadoS10
                join per in ctx.Person on c.UploadedById equals per.UserId into perGroup
                from per in perGroup.DefaultIfEmpty()
                join s   in ctx.AreaScope on c.AreaScopeId equals (int?)s.AreaScopeId into sGroup
                from s   in sGroup.DefaultIfEmpty()
                join ai  in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId into aiGroup
                from ai  in aiGroup.DefaultIfEmpty()
                where consolidadoIds.Contains(c.Id)
                select new
                {
                    c.Id,
                    c.UploadedById,
                    Nombre = per != null ? per.FullName : null,
                    Area   = ai != null ? ai.AreaItemName : null,
                }
            ).ToListAsync();

            return filas
                .GroupBy(x => x.Id)
                .ToDictionary(
                    g => g.Key,
                    g => (g.First().UploadedById,
                          g.Select(x => x.Nombre).FirstOrDefault(n => n != null),
                          g.First().Area));
        }

        /// <summary>
        /// Planillas que cubre un consolidado: sus vínculos vigentes y, en los registros antiguos
        /// atados a una sola salida, la planilla de esa salida.
        /// </summary>
        private static async Task<List<int>> RendicionesCubiertasAsync(AppDbContext ctx, int consolidadoId)
        {
            var ids = await ctx.GaConsolidadoS10Rendicion
                .Where(v => v.State && v.ConsolidadoS10Id == consolidadoId)
                .Select(v => v.RendicionId)
                .ToListAsync();

            var solicitudId = await ctx.GaConsolidadoS10
                .Where(c => c.Id == consolidadoId && c.State)
                .Select(c => c.SolicitudId)
                .FirstOrDefaultAsync();

            if (solicitudId != null)
            {
                var deLaSalida = await ctx.GaSolicitudSalida
                    .Where(s => s.Id == solicitudId.Value)
                    .Select(s => s.RendicionId)
                    .FirstOrDefaultAsync();
                if (deLaSalida != null) ids.Add(deLaSalida.Value);
            }

            return ids.Distinct().ToList();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// El universo de Tesorería: lo que la jefatura ya firmó, lo que ella misma confirmó, lo
        /// que ya pagó y —desde RG-49— lo que ella misma devolvió con una observación.
        ///
        /// Lo observado entra por el PAR (estado + origen) y no por el estado solo: un consolidado
        /// que devolvió la jefatura en la segunda revisión también está Observado, pero nunca
        /// llegó a Tesorería y no tiene por qué aparecer en su bandeja. Y lo que Tesorería devolvió
        /// sí tiene que seguir viéndose, o observar haría desaparecer la fila y nadie podría
        /// seguirle el rastro.
        ///
        /// El estado del desplegable NO se filtra acá sino sobre la fila ya armada
        /// (<see cref="Filtrar"/>): el estado que la pantalla muestra es el resumen del documento,
        /// y recortar las salidas antes de agruparlas dejaría filas con montos y conteos a medias.
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
        /// Filtros que se resuelven sobre la fila ya armada: el estado y el periodo de un
        /// consolidado salen de sus salidas, y el texto busca contra cosas que no son columnas —el
        /// número de reembolso, los códigos de sus planillas y los nombres agrupados—. Va antes de
        /// que el servicio cuente las tarjetas, así la tabla y los números del encabezado siempre
        /// hablan del mismo conjunto.
        /// </summary>
        private static List<ReembolsoListItemDto> Filtrar(
            List<ReembolsoListItemDto> items, ReembolsoFiltersDto filters)
        {
            IEnumerable<ReembolsoListItemDto> q = items;

            var estado = filters.EstadoReembolso?.Trim();
            if (!string.IsNullOrEmpty(estado))
                q = q.Where(x => x.EstadoReembolso == estado);

            if (filters.PeriodoAnio.HasValue && filters.PeriodoMes.HasValue)
                q = q.Where(x => x.PeriodoAnio == filters.PeriodoAnio.Value
                              && x.PeriodoMes  == filters.PeriodoMes.Value);

            var texto = filters.Texto?.Trim();
            if (!string.IsNullOrEmpty(texto))
                q = q.Where(x => Coincide(x, texto));

            return q.ToList();
        }

        /// <summary>
        /// Busca el texto en lo que la fila muestra —reembolso, área, gente y periodo— y en los
        /// códigos de las planillas que cubre, que ya no son columna pero son como las nombran los
        /// correos.
        /// </summary>
        private static bool Coincide(ReembolsoListItemDto x, string texto)
        {
            bool Tiene(string? valor) =>
                !string.IsNullOrEmpty(valor)
                && valor.Contains(texto, StringComparison.OrdinalIgnoreCase);

            return Tiene(x.Codigo)
                || Tiene(x.NumeroReembolso)
                || Tiene(x.Periodo)
                || Tiene(x.RazonSocial)
                || Tiene(x.Area)
                || x.Trabajadores.Any(Tiene)
                || x.Rendiciones.Any(r => Tiene(r.Codigo) || Tiene(r.NumeroPlanilla));
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

        /// <summary>app_user.user_id → nombre completo, para los rastros que muestra la bandeja.</summary>
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

        /// <summary>Nombres de quienes firmaron, observaron, confirmaron o pagaron lo del listado.</summary>
        private static Task<Dictionary<int, string>> NombresDeUsuariosAsync(
            AppDbContext ctx, List<PlanillaRendicionLoader.PlanillaFila> planillas)
        {
            var ids = new List<int>();
            foreach (var p in planillas)
            {
                if (p.FirmadoPorId.HasValue) ids.Add(p.FirmadoPorId.Value);
                foreach (var s in p.Salidas)
                {
                    if (s.ReembolsoDecididoPorId.HasValue) ids.Add(s.ReembolsoDecididoPorId.Value);
                    if (s.RevisionTesoreriaPorId.HasValue) ids.Add(s.RevisionTesoreriaPorId.Value);
                    if (s.PagadoPorId.HasValue)            ids.Add(s.PagadoPorId.Value);
                }
            }
            return NombrePorUserIdAsync(ctx, ids);
        }

        private static void CopiarCabecera(ReembolsoListItemDto o, ReembolsoDetalleDto d)
        {
            d.Id = o.Id; d.Codigo = o.Codigo; d.NumeroReembolso = o.NumeroReembolso;
            d.MontoS10 = o.MontoS10; d.MontoPlanillas = o.MontoPlanillas; d.MontoTotal = o.MontoTotal;
            d.PdfUrl = o.PdfUrl; d.PdfFilename = o.PdfFilename;
            d.PlanillaGrupalUrl = o.PlanillaGrupalUrl; d.PlanillaGrupalFilename = o.PlanillaGrupalFilename;
            d.PlanillaGrupalFirmadoUrl = o.PlanillaGrupalFirmadoUrl;
            d.PlanillaGrupalFirmadoFilename = o.PlanillaGrupalFirmadoFilename;
            d.PdfFirmadoUrl = o.PdfFirmadoUrl; d.PdfFirmadoFilename = o.PdfFirmadoFilename;
            d.FirmadoAt = o.FirmadoAt; d.UploadedAt = o.UploadedAt; d.SubidoPor = o.SubidoPor;
            d.RazonSocial = o.RazonSocial; d.Area = o.Area;
            d.Rendiciones = o.Rendiciones; d.Trabajadores = o.Trabajadores; d.SalidasCount = o.SalidasCount;
            d.Periodo = o.Periodo; d.PeriodoAnio = o.PeriodoAnio; d.PeriodoMes = o.PeriodoMes;
            d.Firmas = o.Firmas;
            d.EstadoReembolso = o.EstadoReembolso; d.ReembolsoMixto = o.ReembolsoMixto;
            d.PorConfirmarCount = o.PorConfirmarCount; d.PorPagarCount = o.PorPagarCount;
            d.ObservadasCount = o.ObservadasCount; d.ObservacionReembolso = o.ObservacionReembolso;
            d.ObservadoAt = o.ObservadoAt; d.ObservadoPor = o.ObservadoPor;
            d.RevisionTesoreriaAt = o.RevisionTesoreriaAt; d.RevisionTesoreriaPor = o.RevisionTesoreriaPor;
            d.PagadoAt = o.PagadoAt; d.PagadoPor = o.PagadoPor;
        }
    }
}
