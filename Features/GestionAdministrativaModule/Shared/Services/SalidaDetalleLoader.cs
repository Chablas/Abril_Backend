using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Arma el detalle de UNA solicitud de salida —cabecera, trayectos con sus capturas, adjuntos y
    /// montos, planilla y Consolidado del S10— tal como lo muestra el modal de detalle.
    ///
    /// Vive en el Shared del módulo porque ese mismo modal lo abren cuatro pantallas: Solicitud de
    /// Salidas (el propio trabajador) y, en consulta, Gestión de Rendiciones, Consolidados y
    /// Reembolsos (la jefatura, el consolidador y Tesorería mirando la salida de otro). Con una copia
    /// del armado por pantalla, la misma salida podría mostrar montos distintos según quién la mire.
    ///
    /// No aplica permisos: cada pantalla valida ANTES que la salida esté en su alcance y recién
    /// entonces pide el detalle. Todo lo que depende del trabajador (la regla de TI del catálogo de
    /// trayectos, el área que decide si las capturas son obligatorias) sale del dueño de la salida,
    /// nunca de quien la está mirando.
    /// </summary>
    public static class SalidaDetalleLoader
    {
        private const string SubareaTi = "Tecnología de la Información";

        /// <param name="conAptitudParaRendir">
        /// true = calcula <see cref="SolicitudSalidaDetalleDto.AptaParaRendir"/>. Solo lo pide el
        /// detalle del propio trabajador: en las pantallas de consulta nadie rinde por otro y el
        /// cálculo (plazo, feriados, capturas obligatorias del área) sería trabajo tirado.
        /// </param>
        /// <returns>Null si la solicitud no existe.</returns>
        public static async Task<SolicitudSalidaDetalleDto?> LoadAsync(
            AppDbContext ctx, int solicitudId, bool conAptitudParaRendir)
        {
            var solicitud = await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                join r in ctx.GaRendicion on s.RendicionId equals (int?)r.Id into rGroup
                from r in rGroup.DefaultIfEmpty()
                where s.Id == solicitudId
                select new
                {
                    s.Id, s.Codigo, s.FechaSalida, s.EstadoAprobacionId, s.EstadoRendicionId,
                    s.CreatedAt, s.MotivoRechazo, s.RendicionId,
                    Trabajador = per != null ? per.FullName : null,
                    // Regla TI ("Tecnología de la Información") y área del dueño de la salida: el área
                    // sale del puesto (workers ya no la guarda) y decide si las capturas le son
                    // obligatorias.
                    w.Subarea,
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                    Rendicion = r == null ? null : new SolicitudSalidaRendicionDto
                    {
                        Id          = r.Id,
                        PdfUrl      = r.PdfUrl,
                        PdfFilename = r.PdfFilename,
                        RendidoAt   = r.RendidoAt,
                    },
                    // Tope de movilidad por trayecto. Viaja en esta misma consulta —es un escalar
                    // de la fila única de config— para no gastar un viaje aparte por un número.
                    LimiteMovilidad = ctx.GaRendicionConfig
                        .Where(c => c.State)
                        .OrderBy(c => c.Id)
                        .Select(c => (decimal?)c.LimiteDiarioMovilidad)
                        .FirstOrDefault(),
                })
                .FirstOrDefaultAsync();
            if (solicitud == null) return null;

            // Trayectos con su info resuelta. Cargamos LugarOrigenId/LugarDestinoId crudos
            // para poder hacer el match contra ga_trayecto después.
            var trayectosRaw = await (
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
                where t.SolicitudId == solicitudId
                orderby t.Orden
                select new
                {
                    Dto = new TrayectoDetalleDto
                    {
                        Id          = t.Id,
                        Orden       = t.Orden,
                        HoraSalida  = t.HoraSalida,
                        HoraRetorno = t.HoraRetorno,
                        Motivo      = m == null || m.EsMotivoLibre ? (t.MotivoLibre ?? string.Empty) : m.Descripcion,
                        MotivoAdicional = t.MotivoAdicional,
                        LugarOrigen = lo == null ? t.LugarOrigenLibre
                                    : lo.Tipo == "proyecto" ? (po != null ? po.ProjectDescription : "[Sin proyecto]")
                                    : lo.Nombre,
                        LugarDestino = ld == null ? t.LugarDestinoLibre
                                    : ld.Tipo == "proyecto" ? (pd != null ? pd.ProjectDescription : "[Sin proyecto]")
                                    : ld.Nombre,
                    },
                    t.LugarOrigenId,
                    t.LugarDestinoId,
                    // Las dos mitades de la regla de reembolso. Sin fila de motivo (solicitudes
                    // anteriores a la fila de "Otro motivo") no hay flag, y por eso ese caso se
                    // distingue del motivo que lo tiene en false.
                    EsMotivoDeCatalogo   = m != null,
                    MotivoEsReembolsable = m != null && m.EsReembolsable,
                    // Adjunto legacy embebido (modelo anterior 1:1). Se combina con la tabla nueva.
                    t.AdjuntoUrl,
                    t.AdjuntoFilename,
                }
            ).ToListAsync();

            var trayectosListado = trayectosRaw.Select(x => x.Dto).ToList();
            var trayectoIds = trayectosListado.Select(t => t.Id).ToList();

            // Adjuntos (tabla nueva ga_solicitud_trayecto_adjunto, N por trayecto).
            var adjuntosByTrayecto = new Dictionary<int, List<TrayectoAdjuntoDto>>();
            if (trayectoIds.Count > 0)
            {
                var adjRaw = await ctx.GaSolicitudTrayectoAdjunto
                    .Where(a => trayectoIds.Contains(a.TrayectoId))
                    .OrderBy(a => a.UploadedAt).ThenBy(a => a.Id)
                    .Select(a => new { a.TrayectoId, a.AdjuntoUrl, a.AdjuntoFilename })
                    .ToListAsync();

                adjuntosByTrayecto = adjRaw.GroupBy(a => a.TrayectoId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(a => new TrayectoAdjuntoDto { Url = a.AdjuntoUrl, Filename = a.AdjuntoFilename }).ToList());
            }

            // Combinar: adjunto legacy embebido (si existe) + adjuntos de la tabla nueva.
            foreach (var raw in trayectosRaw)
            {
                var lista = new List<TrayectoAdjuntoDto>();
                if (!string.IsNullOrWhiteSpace(raw.AdjuntoUrl))
                    lista.Add(new TrayectoAdjuntoDto { Url = raw.AdjuntoUrl, Filename = raw.AdjuntoFilename ?? "Ver documento" });
                if (adjuntosByTrayecto.TryGetValue(raw.Dto.Id, out var nuevos))
                    lista.AddRange(nuevos);
                raw.Dto.Adjuntos = lista;
            }

            // Capturas
            var capsByTrayecto = new Dictionary<int, List<SolicitudSalidaCapturaDto>>();
            if (trayectoIds.Count > 0)
            {
                var capsRaw = await ctx.GaSolicitudCaptura
                    .Where(c => trayectoIds.Contains(c.TrayectoId))
                    .OrderBy(c => c.UploadedAt)
                    .Select(c => new
                    {
                        c.TrayectoId,
                        Dto = new SolicitudSalidaCapturaDto
                        {
                            Id         = c.Id,
                            ImageUrl   = c.ImageUrl,
                            Filename   = c.Filename,
                            Monto      = c.Monto,
                            UploadedAt = c.UploadedAt,
                        }
                    })
                    .ToListAsync();

                capsByTrayecto = capsRaw.GroupBy(x => x.TrayectoId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Dto).ToList());
            }

            // Catálogo de trayectos (solo si TI). Cargamos todo el catálogo activo y mapeamos por (origen, destino).
            var esTI = string.Equals(solicitud.Subarea, SubareaTi, StringComparison.OrdinalIgnoreCase);
            var catalogoMap = esTI ? await CargarCatalogoTrayectosAsync(ctx) : new();

            // Excepciones que anulan el reembolso del motivo. Van aparte del catálogo de montos:
            // ese solo aplica a TI y esta regla es para todos.
            var excluidosReembolso = await ReembolsoTrayectoRule.CargarExcluidosAsync(ctx);

            foreach (var raw in trayectosRaw)
            {
                if (capsByTrayecto.TryGetValue(raw.Dto.Id, out var list))
                    raw.Dto.Capturas = list;

                raw.Dto.EsReembolsable = ReembolsoTrayectoRule.Resolver(
                    raw.EsMotivoDeCatalogo, raw.MotivoEsReembolsable,
                    raw.LugarOrigenId, raw.LugarDestinoId, excluidosReembolso);

                var sumCapturas = raw.Dto.Capturas.Sum(c => c.Monto);

                if (esTI && raw.LugarOrigenId.HasValue && raw.LugarDestinoId.HasValue &&
                    catalogoMap.TryGetValue((raw.LugarOrigenId.Value, raw.LugarDestinoId.Value), out var montoCat))
                {
                    raw.Dto.MontoCatalogo = montoCat;
                }

                raw.Dto.MontoTotal = sumCapturas > 0
                    ? sumCapturas
                    : (raw.Dto.MontoCatalogo ?? 0m);
            }

            // ── ¿Se puede rendir desde el detalle? ──────────────────────────────────────────
            // La MISMA definición que la columna de acciones del listado (GetByUserId →
            // AptaParaRendir): aprobada, no rendida, con todos sus trayectos cubiertos, con motivo
            // reembolsable y dentro del plazo. Se resuelve acá para que el botón del modal no pueda
            // discrepar de la fila que lo abrió ni ofrecer algo que RendirYGenerarPlanilla rechace.
            //
            // Va en cascada y de lo barato a lo caro: los dos primeros cortes salen de lo que ya
            // está cargado, así que el detalle de una salida pendiente, rendida o sin motivo
            // reembolsable —el caso normal— no gasta ni un viaje extra a la base.
            var aptaParaRendir = false;
            if (conAptitudParaRendir
                && trayectosRaw.Count > 0
                && solicitud.EstadoAprobacionId == EstadosSalida.Aprobacion.Aprobado
                && solicitud.EstadoRendicionId  == EstadosSalida.Rendicion.NoRendido
                // Basta un trayecto que deje gasto que rendir: es la misma regla que aplica
                // GetIdsNoReembolsables al rendir. Lo que el pill marca SIN REEMBOLSO no entra en
                // la planilla, y tampoco el que resuelve a S/ 0.00 —el tarifario de TI en cero—:
                // ninguno de los dos vuelve apta a la salida porque no dejan ni una fila impresa.
                && trayectosRaw.Any(t => t.Dto.EsReembolsable == true && t.Dto.MontoTotal > 0m))
            {
                var calendario   = await CalendarioNoLaborable.CargarAsync(ctx);
                var plazoVencido = MesAnteriorPeru.HoyPeru()
                                 > calendario.LimiteDeRendicion(solicitud.FechaSalida.Year, solicitud.FechaSalida.Month);

                // Cobertura de los trayectos: captura propia o, para TI, match contra el catálogo
                // (que es justo lo que dejó puesto MontoCatalogo unas líneas más arriba). Solo se
                // le exige sustento a lo que se va a rendir: el trayecto sin reembolso no entra en
                // la planilla, así que no necesita captura. El área con las capturas en OPCIONAL
                // solo se consulta si quedó alguno sin cubrir.
                var todosCubiertos = trayectosRaw
                    .Where(t => t.Dto.EsReembolsable == true)
                    .All(t => t.Dto.Capturas.Count > 0 || t.Dto.MontoCatalogo != null);

                aptaParaRendir = !plazoVencido
                    && (todosCubiertos
                        || await CapturasObligatoriasLoader.SonOpcionalesAsync(ctx, solicitud.AreaScopeId));
            }

            return new SolicitudSalidaDetalleDto
            {
                Id               = solicitud.Id,
                Codigo           = solicitud.Codigo,
                Trabajador       = solicitud.Trabajador,
                FechaSalida      = solicitud.FechaSalida,
                EstadoAprobacion = EstadosSalida.Aprobacion.Nombre(solicitud.EstadoAprobacionId),
                EstadoRendicion  = EstadosSalida.Rendicion.Nombre(solicitud.EstadoRendicionId),
                CreatedAt        = solicitud.CreatedAt,
                MotivoRechazo    = solicitud.MotivoRechazo,
                Rendicion        = solicitud.Rendicion,
                // Tope de CADA trayecto. Ya no hace falta mirar las otras salidas del día: lo que
                // un día no aguanta se reparte al imprimir la planilla, no se corta acá.
                LimiteMovilidadTrayecto = TopeMovilidad.Acotar(solicitud.LimiteMovilidad),
                AptaParaRendir   = aptaParaRendir,
                ConsolidadoS10   = (await ConsolidadoS10Loader.LoadAsync(
                                        ctx, new Dictionary<int, int?> { [solicitud.Id] = solicitud.RendicionId }))
                                    .GetValueOrDefault(solicitud.Id),
                Trayectos        = trayectosListado,
            };
        }

        /// <summary>
        /// Carga el catálogo de trayectos activos en memoria. Llave: (lugar_origen_id, lugar_destino_id).
        /// </summary>
        private static async Task<Dictionary<(int, int), decimal>> CargarCatalogoTrayectosAsync(AppDbContext ctx)
        {
            var rows = await ctx.GaTrayecto
                .Where(g => g.Activo)
                .Select(g => new { g.LugarOrigenId, g.LugarDestinoId, g.Monto })
                .ToListAsync();
            return rows.ToDictionary(r => (r.LugarOrigenId, r.LugarDestinoId), r => r.Monto);
        }
    }
}
