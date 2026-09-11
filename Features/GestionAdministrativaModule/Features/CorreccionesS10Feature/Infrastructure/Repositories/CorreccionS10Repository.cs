using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Infrastructure.Repositories
{
    /// <summary>
    /// La bandeja del Coordinador ERP. A diferencia de las pantallas de la jefatura no hay recorte
    /// por área: el responsable ERP es uno para toda la organización (§6.1 del requerimiento), así
    /// que ve todas las correcciones vivas. Su recorte es por ESTADO — lo que está por atender y
    /// lo que ya devolvió al colaborador.
    ///
    /// Nada de esto toca el S10: Abril One registra el pedido y la confirmación, y la corrección se
    /// ejecuta afuera (§2.1).
    /// </summary>
    public class CorreccionS10Repository : ICorreccionS10Repository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CorreccionS10Repository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<List<CorreccionS10ListItemDto>> GetAll(CorreccionS10FiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();
            return await ArmarAsync(ctx, filters, correccionId: null);
        }

        public async Task<CorreccionS10ListItemDto?> GetDetalle(int correccionId)
        {
            using var ctx = _factory.CreateDbContext();
            var items = await ArmarAsync(ctx, new CorreccionS10FiltersDto(), correccionId);
            return items.FirstOrDefault();
        }

        /// <summary>
        /// El armado que comparten el listado y el detalle: la fila del ERP necesita datos de tres
        /// tablas distintas (la corrección, su planilla y el colaborador dueño de las salidas), y
        /// resolverlas por separado en cada camino haría que las dos vistas se pudieran desalinear.
        ///
        /// Cinco consultas fijas, sin N+1: correcciones, planillas, dueños, consolidados y totales.
        /// </summary>
        private static async Task<List<CorreccionS10ListItemDto>> ArmarAsync(
            AppDbContext ctx, CorreccionS10FiltersDto filters, int? correccionId)
        {
            // 1) Las correcciones vivas que pasan los filtros de columna.
            var query = ctx.GaCorreccionS10.Where(c => c.State);

            if (correccionId.HasValue)
                query = query.Where(c => c.Id == correccionId.Value);

            var estadoId = EstadosSalida.CorreccionS10.IdFromNombre(filters.Estado);
            if (estadoId.HasValue)
                query = query.Where(c => c.EstadoId == estadoId.Value);

            var correcciones = await query
                .OrderBy(c => c.SolicitadaAt)   // lo más viejo primero: es una cola de trabajo
                .ToListAsync();

            if (correcciones.Count == 0) return new();

            var rendicionIds = correcciones.Select(c => c.RendicionId).Distinct().ToList();

            // 2) Las planillas de esas correcciones.
            var planillas = await ctx.GaRendicion
                .Where(r => rendicionIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Codigo, r.NumeroPlanilla, r.PdfUrl, r.PdfFilename })
                .ToDictionaryAsync(r => r.Id, r => r);

            // 3) El colaborador dueño de cada planilla y el periodo que cubre. La planilla puede
            //    agrupar a varias personas (cuando la generó el revisor desde Gestión de Salidas):
            //    se toma la que tiene más salidas, que es de quien es la rendición en la práctica.
            var salidas = await (
                from s   in ctx.GaSolicitudSalida
                join w   in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                where s.RendicionId != null && rendicionIds.Contains(s.RendicionId.Value)
                select new
                {
                    RendicionId = s.RendicionId!.Value,
                    WorkerId    = w.Id,
                    Trabajador  = per.FullName ?? "[Sin nombre]",
                    s.FechaSalida,
                    // El área del trabajador sale del puesto, no de workers.
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                }
            ).ToListAsync();

            var duenoPorRendicion = salidas
                .GroupBy(s => s.RendicionId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Dueno   = g.GroupBy(x => new { x.WorkerId, x.Trabajador, x.AreaScopeId })
                                   .OrderByDescending(x => x.Count())
                                   .ThenBy(x => x.Key.Trabajador)
                                   .First().Key,
                        Desde   = g.Min(x => x.FechaSalida),
                        Hasta   = g.Max(x => x.FechaSalida),
                    });

            // 4) Nombres de las áreas involucradas, de una vez.
            var areaScopeIds = duenoPorRendicion.Values
                .Where(x => x.Dueno.AreaScopeId != null)
                .Select(x => x.Dueno.AreaScopeId!.Value)
                .Distinct()
                .ToList();

            var areaPorScope = areaScopeIds.Count == 0
                ? new Dictionary<int, string>()
                : await (
                    from sc in ctx.AreaScope
                    join it in ctx.AreaItem on sc.AreaItemId equals it.AreaItemId
                    where areaScopeIds.Contains(sc.AreaScopeId)
                    select new { sc.AreaScopeId, it.AreaItemName }
                ).ToDictionaryAsync(x => x.AreaScopeId, x => x.AreaItemName);

            // 5) El consolidado OBSERVADO de cada corrección (el que hay que corregir) y el total
            //    de la planilla completa, que es el importe que el registro del S10 debería tener.
            var consolidadoIds = correcciones
                .Where(c => c.ConsolidadoS10Id != null)
                .Select(c => c.ConsolidadoS10Id!.Value)
                .Distinct()
                .ToList();

            var consolidados = consolidadoIds.Count == 0
                ? new Dictionary<int, GaConsolidadoS10>()
                : await ctx.GaConsolidadoS10
                    .Where(x => consolidadoIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, x => x);

            var totales = await TotalPlanillaLoader.LoadAsync(ctx, rendicionIds);
            var nombres = await CorreccionS10Loader.NombresAsync(ctx, correcciones);

            // ── Armado de las filas ──────────────────────────────────────────
            var items = new List<CorreccionS10ListItemDto>(correcciones.Count);

            foreach (var c in correcciones)
            {
                if (!planillas.TryGetValue(c.RendicionId, out var p)) continue;

                duenoPorRendicion.TryGetValue(c.RendicionId, out var info);

                var areaScopeId = info?.Dueno.AreaScopeId;

                items.Add(new CorreccionS10ListItemDto
                {
                    Id             = c.Id,
                    RendicionId    = c.RendicionId,
                    Codigo         = PlanillaRendicionHelper.CodigoRendicion(p.Codigo, p.Id),
                    NumeroPlanilla = PlanillaRendicionHelper.NumeroPlanilla(p.NumeroPlanilla),

                    Estado     = EstadosSalida.CorreccionS10.Nombre(c.EstadoId),
                    PorAtender = c.EstadoId == EstadosSalida.CorreccionS10.Solicitada,

                    Trabajador    = info?.Dueno.Trabajador ?? "[Sin trabajador]",
                    Area          = areaScopeId != null && areaPorScope.TryGetValue(areaScopeId.Value, out var a)
                                        ? a : null,
                    SolicitadaPor = nombres.TryGetValue(c.SolicitadaPorId, out var quien) ? quien : string.Empty,
                    SolicitadaAt  = c.SolicitadaAt,

                    Motivo         = c.Motivo,
                    MotivoJefatura = c.MotivoJefatura,
                    MotivoOrigen   = EstadosSalida.OrigenObservacionReembolso.Nombre(c.MotivoOrigenId),
                    NumeroGuia     = c.NumeroGuia,

                    Periodo     = info != null
                                    ? PlanillaRendicionHelper.EtiquetaPeriodo(info.Desde, info.Hasta)
                                    : string.Empty,
                    PeriodoAnio = info?.Desde.Year  ?? 0,
                    PeriodoMes  = info?.Desde.Month ?? 0,

                    MontoTotalPlanilla = totales.TryGetValue(c.RendicionId, out var t) ? t : 0m,

                    PdfUrl      = p.PdfUrl,
                    PdfFilename = p.PdfFilename,
                    ConsolidadoS10 = c.ConsolidadoS10Id != null
                                     && consolidados.TryGetValue(c.ConsolidadoS10Id.Value, out var doc)
                                        ? ConsolidadoS10Loader.ToDto(doc)
                                        : null,

                    AtendidaPor        = c.AtendidaPorId != null && nombres.TryGetValue(c.AtendidaPorId.Value, out var erp)
                                            ? erp : null,
                    AtendidaAt         = c.AtendidaAt,
                    ComentarioAtencion = c.ComentarioAtencion,
                    GuiaAnulada        = c.GuiaAnulada,

                    // WorkerId no viaja en el DTO: la pantalla filtra por el desplegable y el
                    // backend resuelve el recorte, así que exponerlo no agregaría nada.
                });
            }

            // Los filtros que se resuelven sobre la fila ya armada, porque miran cosas que no son
            // columnas de ga_correccion_s10 (el dueño y el periodo de la planilla). Van acá y no en
            // el servicio para que la tabla y las tarjetas siempre cuenten el mismo conjunto.
            if (filters.WorkerId.HasValue)
            {
                var deEse = duenoPorRendicion
                    .Where(kv => kv.Value.Dueno.WorkerId == filters.WorkerId.Value)
                    .Select(kv => kv.Key)
                    .ToHashSet();
                items = items.Where(x => deEse.Contains(x.RendicionId)).ToList();
            }

            if (filters.PeriodoAnio.HasValue && filters.PeriodoMes.HasValue)
                items = items
                    .Where(x => x.PeriodoAnio == filters.PeriodoAnio.Value
                             && x.PeriodoMes  == filters.PeriodoMes.Value)
                    .ToList();

            var texto = filters.Q?.Trim();
            if (!string.IsNullOrEmpty(texto))
                items = items.Where(x => Coincide(x, texto)).ToList();

            return items;
        }

        /// <summary>Busca el texto en lo que la fila muestra: código, planilla, guía y colaborador.</summary>
        private static bool Coincide(CorreccionS10ListItemDto x, string texto)
        {
            bool Tiene(string? valor) =>
                !string.IsNullOrEmpty(valor)
                && valor.Contains(texto, StringComparison.OrdinalIgnoreCase);

            return Tiene(x.Codigo)
                || Tiene(x.NumeroPlanilla)
                || Tiene(x.NumeroGuia)
                || Tiene(x.Trabajador)
                || Tiene(x.Periodo);
        }

        public async Task<CorreccionS10FilterDataDto> GetFilterData()
        {
            using var ctx = _factory.CreateDbContext();

            // Se arma sobre las filas de la bandeja (sin filtros) por la misma razón que en
            // Tesorería: ofrecer colaboradores o periodos que no están acá sería ofrecer un
            // resultado vacío. Reutiliza el armado, así que no puede desalinearse de la tabla.
            var items = await ArmarAsync(ctx, new CorreccionS10FiltersDto(), correccionId: null);
            if (items.Count == 0) return new();

            var rendicionIds = items.Select(x => x.RendicionId).Distinct().ToList();

            // El Distinct va sobre los IDS y no sobre la proyección: EF no sabe construir un
            // comparador para un tipo propio como TrabajadorOptionDto, así que un
            // `.Select(new TrabajadorOptionDto{...}).Distinct()` compila en verde y revienta en
            // runtime. Mismo camino en dos pasos que usa la bandeja de Tesorería.
            var workerIds = await ctx.GaSolicitudSalida
                .Where(s => s.RendicionId != null && rendicionIds.Contains(s.RendicionId.Value))
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

            var periodos = items
                .Where(x => x.PeriodoAnio > 0)
                .Select(x => (x.PeriodoAnio, x.PeriodoMes))
                .Distinct()
                .OrderByDescending(p => p.PeriodoAnio).ThenByDescending(p => p.PeriodoMes)
                .Select(p => new PeriodoCorreccionOptionDto
                {
                    Anio  = p.PeriodoAnio,
                    Mes   = p.PeriodoMes,
                    Label = PlanillaRendicionHelper.EtiquetaMes(p.PeriodoAnio, p.PeriodoMes),
                })
                .ToList();

            return new CorreccionS10FilterDataDto
            {
                Trabajadores = trabajadores,
                Periodos     = periodos,
            };
        }

        public async Task<List<int>> Atender(
            IEnumerable<int> correccionIds, string? comentario, bool guiaAnulada, int erpUserId)
        {
            var ids = correccionIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            // Solo las que están por atender: en una acción masiva la selección puede traer filas
            // que otro Coordinador ya resolvió, y volver a marcarlas pisaría su rastro.
            var filas = await ctx.GaCorreccionS10
                .Where(c => c.State
                         && ids.Contains(c.Id)
                         && c.EstadoId == EstadosSalida.CorreccionS10.Solicitada)
                .ToListAsync();

            if (filas.Count == 0)
                throw new AbrilException(
                    "Ninguna de las correcciones seleccionadas está pendiente de atención.", 400);

            var now = DateTimeOffset.UtcNow;
            var obs = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();

            foreach (var c in filas)
            {
                c.EstadoId           = EstadosSalida.CorreccionS10.Atendida;
                c.AtendidaPorId      = erpUserId;
                c.AtendidaAt         = now;
                c.ComentarioAtencion = obs;
                c.GuiaAnulada        = guiaAnulada;
                c.UpdatedDateTime    = now;
            }

            await ctx.SaveChangesAsync();
            return filas.Select(c => c.Id).ToList();
        }

        public async Task<CorreccionS10CorreoDatos?> GetCorreoDatos(int correccionId)
        {
            using var ctx = _factory.CreateDbContext();

            var c = await ctx.GaCorreccionS10.FirstOrDefaultAsync(x => x.Id == correccionId);
            if (c == null) return null;

            var p = await ctx.GaRendicion
                .Where(r => r.Id == c.RendicionId)
                .Select(r => new { r.Id, r.Codigo, r.NumeroPlanilla })
                .FirstOrDefaultAsync();
            if (p == null) return null;

            var quien = await GetSolicitanteDeRendicion(ctx, c.RendicionId);

            var fechas = await ctx.GaSolicitudSalida
                .Where(s => s.RendicionId == c.RendicionId)
                .Select(s => s.FechaSalida)
                .ToListAsync();

            var nombres = await CorreccionS10Loader.NombresAsync(ctx, new[] { c });

            return new CorreccionS10CorreoDatos
            {
                CorreccionId   = c.Id,
                RendicionId    = c.RendicionId,
                Codigo         = PlanillaRendicionHelper.CodigoRendicion(p.Codigo, p.Id),
                NumeroPlanilla = PlanillaRendicionHelper.NumeroPlanilla(p.NumeroPlanilla),
                Trabajador     = quien?.Trabajador ?? "Colaborador",
                TrabajadorEmail = quien?.Email,
                Area           = quien?.Area,
                Periodo        = fechas.Count == 0
                                    ? null
                                    : PlanillaRendicionHelper.EtiquetaPeriodo(fechas.Min(), fechas.Max()),
                NumeroGuia     = c.NumeroGuia,
                MontoTotal     = await TotalPlanillaLoader.LoadOneAsync(ctx, c.RendicionId),
                Motivo         = c.Motivo,
                MotivoJefatura = c.MotivoJefatura,
                MotivoOrigen   = EstadosSalida.OrigenObservacionReembolso.Nombre(c.MotivoOrigenId),
                AtendidaPor    = c.AtendidaPorId != null && nombres.TryGetValue(c.AtendidaPorId.Value, out var erp)
                                    ? erp : null,
                ComentarioAtencion = c.ComentarioAtencion,
                GuiaAnulada        = c.GuiaAnulada,
            };
        }

        public async Task<CorreccionS10SolicitanteDto?> GetSolicitante(int correccionId)
        {
            using var ctx = _factory.CreateDbContext();

            var rendicionId = await ctx.GaCorreccionS10
                .Where(c => c.Id == correccionId)
                .Select(c => (int?)c.RendicionId)
                .FirstOrDefaultAsync();

            return rendicionId == null ? null : await GetSolicitanteDeRendicion(ctx, rendicionId.Value);
        }

        /// <summary>
        /// El colaborador dueño de una planilla, con su correo y su área. Cuando la planilla agrupa
        /// a varias personas gana la que tiene más salidas — la misma regla que usa el listado, así
        /// que el correo le llega a quien la bandeja muestra como dueño.
        /// </summary>
        private static async Task<CorreccionS10SolicitanteDto?> GetSolicitanteDeRendicion(
            AppDbContext ctx, int rendicionId)
        {
            var candidatos = await (
                from s   in ctx.GaSolicitudSalida
                join w   in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                join u   in ctx.User on (int?)per.UserId equals (int?)u.UserId into uGroup
                from u   in uGroup.DefaultIfEmpty()
                where s.RendicionId == rendicionId
                select new
                {
                    WorkerId    = w.Id,
                    Trabajador  = per.FullName ?? "Colaborador",
                    Email       = u != null ? u.Email : null,
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                }
            ).ToListAsync();

            if (candidatos.Count == 0) return null;

            var dueno = candidatos
                .GroupBy(x => new { x.WorkerId, x.Trabajador, x.Email, x.AreaScopeId })
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key.Trabajador)
                .First().Key;

            string? area = null;
            if (dueno.AreaScopeId != null)
                area = await (
                    from sc in ctx.AreaScope
                    join it in ctx.AreaItem on sc.AreaItemId equals it.AreaItemId
                    where sc.AreaScopeId == dueno.AreaScopeId.Value
                    select it.AreaItemName
                ).FirstOrDefaultAsync();

            return new CorreccionS10SolicitanteDto
            {
                WorkerId   = dueno.WorkerId,
                Trabajador = dueno.Trabajador,
                Email      = dueno.Email,
                Area       = area,
            };
        }
    }
}
