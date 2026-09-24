using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.SharePoint.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Implementación de <see cref="IConsolidadoS10Service"/>. Los PDF se guardan en la misma carpeta
    /// de SharePoint que las planillas de rendición (<c>ga_rendicion_folder</c>): el consolidado y la
    /// planilla grupal son la contraparte de las planillas en el S10 y así no hace falta configurar
    /// una carpeta extra por entorno.
    /// </summary>
    public class ConsolidadoS10Service : IConsolidadoS10Service
    {
        /// <summary>
        /// Espacio del <c>pg_advisory_xact_lock</c> con el que se serializa el correlativo
        /// CONS-ÁREA-AAAA-NNN. Va después del de las solicitudes (8472) y el de las planillas (8473):
        /// son tres series independientes y cada una toma su propio candado por año. Lo toman tanto
        /// la planilla grupal (donde nace el código) como el consolidado que tiene que armar uno.
        /// </summary>
        private const int CorrelativoConsolidadoLockNamespace = 8474;

        /// <summary>Perú no tiene horario de verano: el año del correlativo es el de -05:00.</summary>
        private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IGraphSharePointService _sharePointService;
        /// <summary>Quien arma la planilla grupal: el PDF de gasto es de Gestión de Salidas.</summary>
        private readonly IGestionSalidaService _gestionSalidaService;
        private readonly ILogger<ConsolidadoS10Service> _logger;

        public ConsolidadoS10Service(
            IDbContextFactory<AppDbContext> factory,
            IGraphSharePointService sharePointService,
            IGestionSalidaService gestionSalidaService,
            ILogger<ConsolidadoS10Service> logger)
        {
            _factory = factory;
            _sharePointService = sharePointService;
            _gestionSalidaService = gestionSalidaService;
            _logger = logger;
        }

        // ══ La planilla grupal ═══════════════════════════════════════════════

        public async Task<PlanillaGrupalDto> PrepararPlanillaGrupal(
            IReadOnlyCollection<int> rendicionIds, int userId)
        {
            var ids = Normalizar(rendicionIds);
            if (ids.Count == 0)
                throw new AbrilException("Selecciona al menos una rendición.", 400);

            using var ctx = _factory.CreateDbContext();

            var codigo = await CargarPlanillasAprobadasAsync(ctx, ids, "la planilla grupal");

            string Codigos(IEnumerable<int> deIds) => ConsolidadoS10Agrupacion.Enumerar(
                deIds.Select(id => codigo.TryGetValue(id, out var c) ? c : $"#{id}"));

            // Las mismas reglas de agrupación que al rendir y que al subir el S10: el consolidado se
            // sube sobre esta planilla, así que lo que no se pueda consolidar junto tampoco se
            // prepara junto (ver AgrupacionRendicionRule).
            var workersDeLasPlanillas = await ctx.GaSolicitudSalida.AsNoTracking()
                .Where(s => s.RendicionId != null && ids.Contains(s.RendicionId.Value))
                .Select(s => s.WorkerId)
                .Distinct()
                .ToListAsync();

            await AgrupacionRendicionRule.ValidarAsync(ctx, workersDeLasPlanillas, "planillas");

            // Con el S10 ya subido, la planilla grupal es la que quedó registrada en el S10 y la que
            // firma la jefatura: ya no se rehace. Cambiar el consolidado es de Consolidados, que se
            // queda con la misma planilla.
            var consolidadas = await ConsolidadoS10Loader.LoadPorRendicionAsync(ctx, ids);
            if (consolidadas.Count > 0)
            {
                var yaConsolidadas = ids.Where(consolidadas.ContainsKey).ToList();
                throw new AbrilException(
                    $"{Codigos(yaConsolidadas)} ya {(yaConsolidadas.Count == 1 ? "tiene" : "tienen")} "
                    + "su Consolidado del S10: su planilla grupal ya no se puede volver a preparar.", 409);
            }

            // Sin consolidado el reembolso todavía no se pudo decidir: esto solo atrapa registros
            // viejos con un consolidado por salida ya firmado, donde no hay nada que preparar.
            var agrupables = await ConsolidadoS10Agrupacion.LoadPlanillasAsync(ctx, ids);
            var cerradas = ids
                .Where(id => agrupables.TryGetValue(id, out var p) && !p.ReembolsoAbierto)
                .ToList();
            if (cerradas.Count > 0)
                throw new AbrilException(
                    $"{Codigos(cerradas)} ya {(cerradas.Count == 1 ? "tiene" : "tienen")} el reembolso "
                    + "decidido: no hay planilla grupal que preparar.", 409);

            // Una planilla grupal ya preparada NO se rehace ni se reemplaza: es el papel que el
            // consolidador registró en el S10, con el código que quedó anotado ahí. Sus rendiciones
            // tampoco pueden entrar en otra.
            await ExigirSinPlanillaGrupalAsync(ctx, ids, Codigos);

            // ── Área y código de la rendición grupal ─────────────────────────
            // El código nace acá: el consolidado que se suba después sobre esta planilla lo hereda.
            var now = DateTimeOffset.UtcNow;

            var areaScopeId = await AreaDelConsolidadoAsync(ctx, userId, ids);
            var area = await CargarAreaAsync(ctx, areaScopeId);

            var prefijo     = CodigoRendicionGrupal.Prefijo(area?.Abreviatura, area?.Nombre);
            var anio        = now.ToOffset(PeruOffset).Year;
            var numero      = await MenorNumeroLibreAsync(ctx, prefijo, anio);
            var codigoGrupo = CodigoRendicionGrupal.Armar(prefijo, anio, numero);

            var carpeta = await ResolverCarpetaAsync(ctx);

            var archivo = await ArmarYSubirPlanillaGrupalAsync(
                ctx, carpeta, ids, userId, codigoGrupo, area?.Nombre);

            var nueva = new GaPlanillaGrupal
            {
                AreaScopeId    = areaScopeId,
                PdfUrl         = archivo.Url,
                PdfItemId      = archivo.ItemId,
                PdfDriveId     = carpeta.DriveId,
                PdfFilename    = archivo.Filename,
                PreparadaPorId = userId,
                PreparadaAt    = now,
                State          = true,
            };

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await ctx.Database.BeginTransactionAsync();

                // El código ya va impreso: acá solo se confirma, con el candado del correlativo
                // tomado, que nadie lo haya tomado mientras se subía el archivo.
                await VerificarCodigoLibreAsync(ctx, anio, codigoGrupo);

                // Y con el mismo candado —lo toma toda preparación— se vuelve a mirar que ninguna de
                // estas rendiciones haya entrado en otra planilla grupal mientras tanto: dos
                // consolidadores del área pueden haber preparado a la vez.
                await ExigirSinPlanillaGrupalAsync(ctx, ids, Codigos);

                nueva.Codigo = codigoGrupo;
                nueva.Anio   = anio;
                nueva.Numero = numero;

                // Este guardado le da id a la planilla, que es lo que necesitan sus vínculos.
                ctx.GaPlanillaGrupal.Add(nueva);
                await ctx.SaveChangesAsync();

                ctx.GaPlanillaGrupalRendicion.AddRange(ids.Select(id => new GaPlanillaGrupalRendicion
                {
                    PlanillaGrupalId = nueva.Id,
                    RendicionId      = id,
                    State            = true,
                }));

                await ctx.SaveChangesAsync();
                await tx.CommitAsync();
            });

            var dto = PlanillaGrupalLoader.ToDto(nueva);
            dto.Rendiciones = ids
                .Select(id => new ConsolidadoS10RendicionDto { Id = id, Codigo = codigo[id] })
                .OrderBy(r => r.Codigo, StringComparer.Ordinal)
                .ToList();
            return dto;
        }

        // ══ El Consolidado del S10 ══════════════════════════════════════════

        public async Task<ConsolidadoS10Dto> UploadParaRendiciones(
            IReadOnlyCollection<int> rendicionIds,
            IFormFile file,
            decimal montoTotal,
            string numeroReembolso,
            int userId)
        {
            if (file == null || file.Length == 0)
                throw new AbrilException("No se recibió el archivo del consolidado.", 400);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".pdf")
                throw new AbrilException("El Consolidado del S10 debe ser un archivo PDF.", 400);

            var reembolso = (numeroReembolso ?? string.Empty).Trim();
            if (reembolso.Length == 0)
                throw new AbrilException("Falta el número de reembolso del Consolidado del S10.", 400);
            if (reembolso.Length > 60)
                throw new AbrilException("El número de reembolso no puede pasar de 60 caracteres.", 400);

            // La columna es numeric(12,2): se redondea antes de comparar y de guardar, así el
            // monto que se contrasta es el mismo que queda en la base.
            var monto = decimal.Round(montoTotal, 2, MidpointRounding.AwayFromZero);
            if (monto <= 0m)
                throw new AbrilException("El monto total del Consolidado del S10 tiene que ser mayor que 0.", 400);

            var ids = Normalizar(rendicionIds);
            if (ids.Count == 0)
                throw new AbrilException("Selecciona al menos una rendición.", 400);

            using var ctx = _factory.CreateDbContext();

            var codigo = await CargarPlanillasAprobadasAsync(ctx, ids, "el Consolidado del S10");

            string Codigos(IEnumerable<int> deIds) => ConsolidadoS10Agrupacion.Enumerar(
                deIds.Select(id => codigo.TryGetValue(id, out var c) ? c : $"#{id}"));

            // Quiénes pueden compartir un consolidado: no se mezclan jefaturas (JEFE, SUB GERENTE,
            // RESIDENTE) con el resto del equipo —a una jefatura la firma su gerencia, y acá
            // terminaría firmando un documento donde ella misma está incluida— y todos tienen que
            // colgar de un mismo nodo de área por debajo de la gerencia. Es la MISMA regla que se
            // aplica al rendir y al preparar la planilla grupal, así que no se puede consolidar lo
            // que no se pudo agrupar (ni al revés).
            var workersDeLasPlanillas = await ctx.GaSolicitudSalida.AsNoTracking()
                .Where(s => s.RendicionId != null && ids.Contains(s.RendicionId.Value))
                .Select(s => s.WorkerId)
                .Distinct()
                .ToListAsync();

            await AgrupacionRendicionRule.ValidarAsync(ctx, workersDeLasPlanillas, "planillas");

            // ── Qué planillas pueden ir juntas (ver ConsolidadoS10Agrupacion) ──
            var actuales = await ConsolidadoS10Loader.LoadPorRendicionAsync(ctx, ids);
            var agrupables = await ConsolidadoS10Agrupacion.LoadPlanillasAsync(
                ctx,
                ids.Concat(actuales.Values.SelectMany(c => c.Rendiciones).Select(r => r.Id))
                   .Distinct()
                   .ToList());

            // Con el reembolso ya decidido, el consolidado quedó firmado junto con la planilla:
            // cambiarlo dejaría a Tesorería con un respaldo distinto del que aprobó la jefatura.
            var cerradas = ids
                .Where(id => agrupables.TryGetValue(id, out var p) && !p.ReembolsoAbierto)
                .ToList();
            if (cerradas.Count > 0)
                throw new AbrilException(
                    $"{Codigos(cerradas)} ya {(cerradas.Count == 1 ? "tiene" : "tienen")} el consolidado "
                    + "decidido: su Consolidado del S10 ya no se puede cambiar.", 409);

            // El documento se reemplaza entero: las demás planillas abiertas de un consolidado
            // compartido tienen que venir en la misma subida.
            var faltantes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (!actuales.TryGetValue(id, out var actual)) continue;

                foreach (var otra in ConsolidadoS10Agrupacion.Conjunto(id, actual, agrupables))
                    if (!ids.Contains(otra))
                        faltantes.Add(actual.Rendiciones.First(r => r.Id == otra).Codigo);
            }
            if (faltantes.Count > 0)
                throw new AbrilException(
                    $"El Consolidado del S10 actual también cubre {ConsolidadoS10Agrupacion.Enumerar(faltantes)}, "
                    + "que " + (faltantes.Count == 1 ? "sigue" : "siguen") + " por decidir: "
                    + "el documento se reemplaza para todas sus rendiciones a la vez.", 409);

            // El consolidado es UN registro en el S10 y cubre las planillas completas, así que su
            // importe se contrasta contra el total de TODAS sus salidas —no contra el subconjunto
            // que ve quien lo sube—. Se valida acá, antes de tocar SharePoint: un archivo subido con
            // el monto mal solo dejaría basura en la carpeta.
            var totales = await TotalPlanillaLoader.LoadAsync(ctx, ids);
            var totalPlanillas = decimal.Round(totales.Values.Sum(), 2, MidpointRounding.AwayFromZero);

            if (monto != totalPlanillas)
                throw new AbrilException(
                    $"El monto total del Consolidado del S10 (S/ {monto:N2}) no coincide con el monto "
                    + (ids.Count == 1 ? "de la planilla" : $"de las {ids.Count} planillas")
                    + $" (S/ {totalPlanillas:N2}). Corrígelo antes de adjuntarlo.", 400);

            // Las correcciones vivas de estas planillas: se cierran más abajo, en la misma
            // transacción, porque recargar el consolidado ES el final de esa gestión.
            //
            // El número de reembolso NO se valida contra ellas: se reutiliza el mismo o se escribe
            // uno nuevo según lo que diga el S10, y eso lo decide el consolidador al llenar el
            // formulario.
            var correcciones = await ctx.GaCorreccionS10
                .Where(c => c.State && ids.Contains(c.RendicionId))
                .ToListAsync();

            // ── Qué pasa con los consolidados anteriores ──────────────────────
            // Se resuelve ANTES de tocar SharePoint: si este documento reemplaza entero a otro,
            // hereda su código y su planilla grupal.

            // Los vínculos vigentes de estas planillas pasan al consolidado nuevo.
            var vinculosAnteriores = await ctx.GaConsolidadoS10Rendicion
                .Where(v => v.State && ids.Contains(v.RendicionId))
                .ToListAsync();

            // Un consolidado anterior se da de baja cuando se queda sin planillas. Si todavía cubre
            // alguna con el reembolso ya decidido, sigue vivo para ella: es el documento que se firmó.
            var anterioresIds = vinculosAnteriores.Select(v => v.ConsolidadoS10Id).Distinct().ToList();
            var siguenCubriendo = await ctx.GaConsolidadoS10Rendicion
                .Where(v => v.State
                         && anterioresIds.Contains(v.ConsolidadoS10Id)
                         && !ids.Contains(v.RendicionId))
                .Select(v => v.ConsolidadoS10Id)
                .Distinct()
                .ToListAsync();
            var anterioresDeBaja = await ctx.GaConsolidadoS10
                .Where(c => c.State && anterioresIds.Contains(c.Id) && !siguenCubriendo.Contains(c.Id))
                .ToListAsync();

            // El código de la rendición grupal se HEREDA cuando este documento reemplaza a otro
            // que queda entero de baja: es el mismo grupo de planillas y lo único que cambió es el
            // archivo, así que renombrarlo dejaría a Tesorería y al ERP siguiendo un nombre que ya
            // no existe (igual que una planilla conserva su REN al subsanarla). Con el código se
            // hereda también el área, que es la que lo arma.
            //
            // Se mintea uno nuevo cuando no hay de quién heredarlo: varios anteriores que se fusionan
            // en uno, o un anterior que sigue vivo cubriendo planillas con el reembolso ya decidido
            // (ahí el código sigue siendo suyo). El primer consolidado no mintea: toma el de su
            // planilla grupal, que es donde nace.
            var heredado = anterioresIds.Count == 1 && anterioresDeBaja.Count == 1
                        && !string.IsNullOrWhiteSpace(anterioresDeBaja[0].Codigo)
                            ? anterioresDeBaja[0]
                            : null;

            // ── La planilla grupal ───────────────────────────────────────────
            // Acá nunca se arma una: la planilla grupal solo se PREPARA, y una vez preparada no se
            // rehace ni se reemplaza. El PRIMER consolidado se sube sobre la que preparó el
            // consolidador —es el papel con el que registró las planillas en el S10, así que sin ella
            // no hay consolidado— y reemplazarlo se queda con la del consolidado que reemplaza: las
            // rendiciones y sus montos no cambiaron, y es la que se registró. También cuando el
            // reemplazo deja afuera planillas ya decididas y el consolidado nuevo recibe otro código.
            // Un consolidado anterior a la planilla grupal sigue sin tenerla.
            var preparada = actuales.Count == 0
                ? await ExigirPlanillaPreparadaAsync(ctx, ids, Codigos)
                : null;
            var reemplazado = heredado
                ?? (anterioresIds.Count == 1
                    ? await ctx.GaConsolidadoS10.AsNoTracking().FirstOrDefaultAsync(c => c.Id == anterioresIds[0])
                    : null);

            // ── Carpeta destino (la misma de las planillas de rendición) ──────
            var carpeta = await ResolverCarpetaAsync(ctx);

            // ── Área y código de la rendición grupal ─────────────────────────
            var now = DateTimeOffset.UtcNow;

            // El área viaja con el código: la de la planilla grupal o la del consolidado que se
            // reemplaza. Solo se resuelve de nuevo cuando el código también es nuevo.
            var areaScopeId = preparada != null
                ? preparada.AreaScopeId
                : heredado?.AreaScopeId ?? await AreaDelConsolidadoAsync(ctx, userId, ids);
            var area = await CargarAreaAsync(ctx, areaScopeId);

            // El código nuevo se calcula acá, sin candado, para poder imprimirlo; dentro de la
            // transacción se confirma que siga libre (ver VerificarCodigoLibreAsync).
            string  codigoGrupo;
            int?    anio;
            int?    numero;
            string? prefijoNuevo = null;
            if (heredado != null)
            {
                codigoGrupo = heredado.Codigo!;
                anio        = heredado.Anio;
                numero      = heredado.Numero;
            }
            else if (preparada != null)
            {
                codigoGrupo = preparada.Codigo;
                anio        = preparada.Anio;
                numero      = preparada.Numero;
            }
            else
            {
                prefijoNuevo = CodigoRendicionGrupal.Prefijo(area?.Abreviatura, area?.Nombre);
                anio         = now.ToOffset(PeruOffset).Year;
                numero       = await MenorNumeroLibreAsync(ctx, prefijoNuevo, anio.Value);
                codigoGrupo  = CodigoRendicionGrupal.Armar(prefijoNuevo, anio.Value, numero.Value);
            }

            // Los archivos se nombran con el código —solo letras, números y guiones, así que
            // SharePoint lo acepta— y con la hora: un reemplazo hereda el código y no puede pisar
            // el archivo del documento que reemplaza, que queda como historial.
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var filename = $"Consolidado_S10_{codigoGrupo}_{stamp}.pdf";

            string pdfUrl;
            string? pdfItemId;
            try
            {
                using var stream = file.OpenReadStream();
                var result = await _sharePointService.UploadToOneDriveFolderAsync(
                    carpeta.DriveId, carpeta.ItemId, filename, stream,
                    "application/pdf",
                    autoRenameOnLock: true);

                if (result?.WebUrl is null)
                    throw new AbrilException("No se pudo subir el Consolidado del S10 a SharePoint (respuesta vacía).", 502);

                pdfUrl = result.WebUrl;
                pdfItemId = result.ItemId;
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la subida del Consolidado del S10 (rendiciones={RendicionIds})",
                    string.Join(",", ids));
                throw new AbrilException("Error al subir el Consolidado del S10 a SharePoint.", 502);
            }

            // ── Persistir ────────────────────────────────────────────────────
            var nuevo = new GaConsolidadoS10
            {
                SolicitudId  = null,
                AreaScopeId  = areaScopeId,
                PdfUrl       = pdfUrl,
                PdfItemId    = pdfItemId,
                PdfDriveId   = carpeta.DriveId,
                PdfFilename  = filename,
                MontoTotal   = monto,
                NumeroReembolso   = reembolso,
                UploadedById = userId,
                UploadedAt   = now,
                State        = true,
            };

            // La planilla grupal del consolidado es siempre la ORIGINAL, sin firmas: la copia firmada
            // se arma al aprobar, sobre este consolidado.
            if (preparada != null)
            {
                nuevo.PlanillaGrupalId       = preparada.Id;
                nuevo.PlanillaGrupalUrl      = preparada.PdfUrl;
                nuevo.PlanillaGrupalItemId   = preparada.PdfItemId;
                nuevo.PlanillaGrupalDriveId  = preparada.PdfDriveId;
                nuevo.PlanillaGrupalFilename = preparada.PdfFilename;
            }
            else if (reemplazado?.PlanillaGrupalUrl != null)
            {
                nuevo.PlanillaGrupalId       = reemplazado.PlanillaGrupalId;
                nuevo.PlanillaGrupalUrl      = reemplazado.PlanillaGrupalUrl;
                nuevo.PlanillaGrupalItemId   = reemplazado.PlanillaGrupalItemId;
                nuevo.PlanillaGrupalDriveId  = reemplazado.PlanillaGrupalDriveId;
                nuevo.PlanillaGrupalFilename = reemplazado.PlanillaGrupalFilename;
            }

            // Subsanación: si la jefatura (o Tesorería) había OBSERVADO el reembolso, adjuntar otra
            // vez el consolidado es exactamente lo que se le pidió al consolidador, así que el
            // reembolso vuelve a Pendiente y le reaparece a la jefatura. La observación NO se borra: sigue
            // siendo lo que se observó y el jefe la necesita para contrastar.
            //
            // El archivo cubre las planillas enteras, así que reabre todas sus salidas observadas.
            var reabrir = await ctx.GaSolicitudSalida
                .Where(x => x.RendicionId != null && ids.Contains(x.RendicionId.Value)
                         && x.EstadoReembolsoId == EstadosSalida.Reembolso.Observado)
                .ToListAsync();

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await ctx.Database.BeginTransactionAsync();

                // Los índices únicos parciales se validan por sentencia, no al COMMIT: las bajas
                // van en su PROPIO guardado, antes del INSERT. Si no, el vínculo vigente de la
                // misma planilla —y, cuando el código se hereda, el código del consolidado que se
                // está reemplazando— chocarían con la fila nueva.
                foreach (var v in vinculosAnteriores) v.State = false;
                foreach (var c in anterioresDeBaja)   c.State = false;
                await ctx.SaveChangesAsync();

                // El código nuevo ya va impreso en la planilla: acá solo se confirma, con el
                // candado del correlativo tomado, que nadie lo haya tomado mientras se subían los
                // archivos. Se repite en cada intento de la execution strategy.
                if (prefijoNuevo != null)
                    await VerificarCodigoLibreAsync(ctx, anio!.Value, codigoGrupo);

                nuevo.Codigo = codigoGrupo;
                nuevo.Anio   = anio;
                nuevo.Numero = numero;

                // Este guardado le da id al consolidado, que es lo que necesitan sus vínculos.
                ctx.GaConsolidadoS10.Add(nuevo);
                await ctx.SaveChangesAsync();

                ctx.GaConsolidadoS10Rendicion.AddRange(ids.Select(id => new GaConsolidadoS10Rendicion
                {
                    ConsolidadoS10Id = nuevo.Id,
                    RendicionId      = id,
                    State            = true,
                }));

                foreach (var r in reabrir)
                {
                    r.EstadoReembolsoId = EstadosSalida.Reembolso.Pendiente;
                    r.UpdatedAt         = now;
                }

                // Y la gestión con el ERP se cierra: recargar el consolidado ES el final de ese
                // ciclo. Va en la MISMA transacción que reabre el reembolso porque son la misma
                // subsanación — dejar la corrección viva sobre un reembolso ya Pendiente
                // bloquearía la próxima si la jefatura vuelve a observar.
                //
                // Se cierra incluso si el ERP todavía no la había atendido: el consolidador puede
                // haber resuelto el S10 por otro lado, y en ese caso el pedido ya no tiene sentido
                // (desaparece de la bandeja del ERP en vez de quedar ahí sin dueño).
                foreach (var c in correcciones)
                {
                    c.State           = false;
                    c.UpdatedDateTime = now;
                }

                await ctx.SaveChangesAsync();
                await tx.CommitAsync();
            });

            var dto = ConsolidadoS10Loader.ToDto(nuevo);
            dto.Rendiciones = ids
                .Select(id => new ConsolidadoS10RendicionDto { Id = id, Codigo = codigo[id] })
                .OrderBy(r => r.Codigo, StringComparer.Ordinal)
                .ToList();
            return dto;
        }

        /// <summary>
        /// La planilla grupal preparada sobre la que se sube el PRIMER Consolidado del S10. Tiene que
        /// ser una sola y cubrir exactamente las rendiciones que llegan: el S10 registró ese
        /// documento, así que el consolidado cubre lo mismo que él —ni una rendición más, ni una
        /// menos—. Cada caso que no cuadra dice qué hacer, porque desde la pantalla se ve igual.
        /// </summary>
        private static async Task<GaPlanillaGrupal> ExigirPlanillaPreparadaAsync(
            AppDbContext ctx, List<int> ids, Func<IEnumerable<int>, string> codigos)
        {
            // Los vínculos vigentes de las planillas grupales que tocan la selección, con los que
            // cubren fuera de ella: un solo roundtrip para las tres preguntas de abajo.
            var vinculos = await (
                from v in ctx.GaPlanillaGrupalRendicion
                join g in ctx.GaPlanillaGrupal on v.PlanillaGrupalId equals g.Id
                join r in ctx.GaRendicion on v.RendicionId equals r.Id
                where v.State && g.State
                   && ctx.GaPlanillaGrupalRendicion.Any(x => x.State
                                                          && x.PlanillaGrupalId == g.Id
                                                          && ids.Contains(x.RendicionId))
                select new { Planilla = g, v.RendicionId, r.Codigo }
            ).AsNoTracking().ToListAsync();

            var conPlanilla = vinculos.Select(v => v.RendicionId).ToHashSet();
            var sinPlanilla = ids.Where(id => !conPlanilla.Contains(id)).ToList();
            if (sinPlanilla.Count > 0)
                throw new AbrilException(
                    $"{codigos(sinPlanilla)} todavía no {(sinPlanilla.Count == 1 ? "tiene" : "tienen")} "
                    + "planilla grupal: prepárala antes de subir el Consolidado del S10.", 409);

            var planillas = vinculos
                .Select(v => v.Planilla)
                .GroupBy(g => g.Id)
                .Select(grupo => grupo.First())
                .ToList();
            if (planillas.Count > 1)
                throw new AbrilException(
                    "Las rendiciones seleccionadas son de planillas grupales distintas ("
                    + ConsolidadoS10Agrupacion.Enumerar(planillas.Select(g => g.Codigo).OrderBy(c => c, StringComparer.Ordinal))
                    + "): sube un Consolidado del S10 por cada planilla grupal.", 409);

            var planilla = planillas[0];
            var faltan = vinculos
                .Where(v => !ids.Contains(v.RendicionId))
                .Select(v => PlanillaRendicionHelper.CodigoRendicion(v.Codigo, v.RendicionId))
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();
            if (faltan.Count > 0)
                throw new AbrilException(
                    $"La planilla grupal {planilla.Codigo} también cubre {ConsolidadoS10Agrupacion.Enumerar(faltan)}: "
                    + "el Consolidado del S10 se sube para toda la planilla a la vez.", 409);

            return planilla;
        }

        /// <summary>
        /// Corta con 409 si alguna de estas rendiciones ya está en una planilla grupal vigente: una
        /// planilla preparada no se rehace ni se reemplaza —es lo que se registró en el S10—, así
        /// que sus rendiciones tampoco pueden entrar en otra.
        /// </summary>
        private static async Task ExigirSinPlanillaGrupalAsync(
            AppDbContext ctx, List<int> ids, Func<IEnumerable<int>, string> codigos)
        {
            var tomadas = await (
                from v in ctx.GaPlanillaGrupalRendicion
                join g in ctx.GaPlanillaGrupal on v.PlanillaGrupalId equals g.Id
                where v.State && g.State && ids.Contains(v.RendicionId)
                select new { v.RendicionId, g.Codigo }
            ).ToListAsync();
            if (tomadas.Count == 0) return;

            var rendiciones = tomadas.Select(t => t.RendicionId).Distinct().ToList();
            var planillas = tomadas
                .Select(t => t.Codigo)
                .Distinct()
                .OrderBy(c => c, StringComparer.Ordinal);

            throw new AbrilException(
                $"{codigos(rendiciones)} ya {(rendiciones.Count == 1 ? "tiene" : "tienen")} su planilla "
                + $"grupal ({ConsolidadoS10Agrupacion.Enumerar(planillas)}): una planilla grupal no se "
                + "vuelve a preparar.", 409);
        }

        // ══ Lo que comparten la planilla grupal y el consolidado ═════════════

        /// <summary>Ids positivos, sin repetir y en orden: la forma en que se guardan los vínculos.</summary>
        private static List<int> Normalizar(IReadOnlyCollection<int>? rendicionIds) =>
            (rendicionIds ?? Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

        /// <summary>
        /// Carga las planillas y exige que TODAS tengan la primera revisión aprobada. Devuelve el
        /// código REN de cada una, para nombrarlas en los mensajes.
        ///
        /// RG-35: la planilla grupal y el Consolidado del S10 se habilitan SOLO después de que la
        /// jefatura apruebe la primera revisión: lo que falta no es el permiso, es el registro en el
        /// S10, que recién se hace con la planilla aprobada. El mensaje dice en qué estado está para
        /// no dejarlo adivinando.
        /// </summary>
        /// <param name="queSeHabilita">"la planilla grupal" / "el Consolidado del S10", para el mensaje.</param>
        private static async Task<Dictionary<int, string>> CargarPlanillasAprobadasAsync(
            AppDbContext ctx, List<int> ids, string queSeHabilita)
        {
            var planillas = await ctx.GaRendicion
                .Where(r => ids.Contains(r.Id))
                .Select(r => new { r.Id, r.Codigo, r.EstadoPrimeraRevisionId })
                .ToListAsync();

            if (planillas.Count != ids.Count)
                throw new AbrilException(
                    ids.Count == 1
                        ? "La planilla de rendición no existe."
                        : "Alguna de las planillas de rendición seleccionadas ya no existe.", 404);

            var codigo = planillas.ToDictionary(
                p => p.Id, p => PlanillaRendicionHelper.CodigoRendicion(p.Codigo, p.Id));

            var sinAprobar = planillas
                .Where(p => p.EstadoPrimeraRevisionId != EstadosSalida.PrimeraRevision.Aprobada)
                .ToList();
            if (sinAprobar.Count > 0)
                throw new AbrilException(
                    ids.Count == 1
                        ? MensajeSinPrimeraRevision(sinAprobar[0].EstadoPrimeraRevisionId, queSeHabilita)
                        : $"{ConsolidadoS10Agrupacion.Enumerar(sinAprobar.Select(p => codigo[p.Id]))} todavía no "
                          + (sinAprobar.Count == 1 ? "tiene" : "tienen")
                          + $" la primera revisión aprobada: {queSeHabilita} se habilita recién "
                          + "cuando la jefatura la aprueba.",
                    409);

            return codigo;
        }

        /// <summary>
        /// La carpeta de SharePoint de las planillas de rendición, donde van también el consolidado
        /// y la planilla grupal.
        /// </summary>
        private async Task<ShareLinkResolveDto> ResolverCarpetaAsync(AppDbContext ctx)
        {
            var folderUrl = await ctx.GaRendicionFolder
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.GaRendicionFolderId)
                .Select(f => f.LinkUrl)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(folderUrl))
                throw new AbrilException(
                    "No se ha configurado la carpeta de SharePoint donde guardar los consolidados del S10 " +
                    "y las planillas grupales. Pide al administrador registrarla en la tabla ga_rendicion_folder.", 409);

            var carpeta = await _sharePointService.ResolveSharePointFolderUrlAsync(folderUrl);
            if (carpeta == null || !carpeta.IsFolder)
                throw new AbrilException("No se pudo resolver la carpeta de consolidados del S10 en SharePoint.", 502);

            return carpeta;
        }

        /// <summary>El área de la rendición grupal: su nombre va impreso y su sigla arma el código.</summary>
        private sealed record AreaGrupal(string? Nombre, string? Abreviatura);

        private static async Task<AreaGrupal?> CargarAreaAsync(AppDbContext ctx, int? areaScopeId)
        {
            if (areaScopeId == null) return null;

            return await (
                from sc in ctx.AreaScope
                join it in ctx.AreaItem on sc.AreaItemId equals it.AreaItemId
                where sc.AreaScopeId == areaScopeId.Value
                select new AreaGrupal(it.AreaItemName, it.Abreviatura)
            ).FirstOrDefaultAsync();
        }

        /// <summary>Un PDF ya subido a SharePoint.</summary>
        private sealed record ArchivoSubido(string Url, string? ItemId, string Filename);

        /// <summary>
        /// Arma la PLANILLA DE REEMBOLSO (la planilla grupal) y la sube a la carpeta: los trayectos de
        /// TODAS las planillas en una sola tabla, con la planilla de la que sale cada fila. La
        /// cabecera es la del consolidador —su razón social, su nombre— y la del área del grupo.
        ///
        /// Es el ÚNICO lugar donde se arma, y solo lo llama <see cref="PrepararPlanillaGrupal"/>: la
        /// subida del consolidado se queda con la que ya existe. Sale sin número de reembolso, que
        /// todavía no existe —lo devuelve el S10 cuando se registra esta misma planilla—.
        /// </summary>
        private async Task<ArchivoSubido> ArmarYSubirPlanillaGrupalAsync(
            AppDbContext ctx, ShareLinkResolveDto carpeta, IReadOnlyCollection<int> ids, int userId,
            string codigo, string? area)
        {
            var consolidador = await ctx.Person
                .Where(p => p.UserId == userId && p.FullName != null)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync();
            var razones = await RazonSocialConsolidador.LoadPorUsuarioAsync(ctx, new[] { userId });
            razones.TryGetValue(userId, out var razon);

            var bytes = await _gestionSalidaService.GenerarPlanillaGrupal(ids, new PlanillaReembolsoCabeceraDto
            {
                Codigo          = codigo,
                RazonSocial     = razon?.Nombre,
                Ruc             = razon?.Ruc,
                Consolidador    = consolidador,
                Area            = area,
                NumeroReembolso = null,
            });

            // Mismo nombre que tenían cuando se armaban al subir el S10: el código y la hora.
            var filename = $"Planilla_Reembolso_{codigo}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            try
            {
                using var stream = new MemoryStream(bytes);
                var subido = await _sharePointService.UploadToOneDriveFolderAsync(
                    carpeta.DriveId, carpeta.ItemId, filename, stream,
                    "application/pdf",
                    autoRenameOnLock: true);

                if (subido?.WebUrl is null)
                    throw new AbrilException("No se pudo subir la planilla grupal a SharePoint (respuesta vacía).", 502);

                return new ArchivoSubido(subido.WebUrl, subido.ItemId, filename);
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la subida de la planilla de reembolso (rendiciones={RendicionIds})",
                    string.Join(",", ids));
                throw new AbrilException("Error al subir la planilla grupal a SharePoint.", 502);
            }
        }

        /// <summary>
        /// El área de la rendición grupal: la del consolidador —es el consolidador DEL ÁREA—, por su
        /// ficha vigente (una persona puede tener varias por reingreso) → puesto → área de destino.
        /// Si él no tiene área (sin ficha o sin puesto), la que más se repite entre los trabajadores
        /// de las planillas que cubre.
        /// </summary>
        private static async Task<int?> AreaDelConsolidadoAsync(
            AppDbContext ctx, int userId, IReadOnlyCollection<int> rendicionIds)
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var propia = await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .OrderByDescending(w => ctx.WorkerVinculacion.Any(v =>
                    v.WorkerId == w.Id && (v.FechaFin == null || v.FechaFin >= hoy)))
                .ThenByDescending(w => w.WorkersEstadoId == WorkersEstadoIds.Activo ? 1 : 0)
                .ThenByDescending(w => w.Id)
                .Select(w => w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null)
                .FirstOrDefaultAsync();
            if (propia != null) return propia;

            var deLosTrabajadores = await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                where s.RendicionId != null && rendicionIds.Contains(s.RendicionId.Value)
                select w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null
            ).ToListAsync();

            return deLosTrabajadores
                .Where(a => a != null)
                .GroupBy(a => a!.Value)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => (int?)g.Key)
                .FirstOrDefault();
        }

        /// <summary>
        /// El menor número libre del área en el año, no el máximo + 1: una baja no debe dejar el
        /// hueco perdido. Se miran también los dados de baja: el código de un consolidado
        /// reemplazado sigue siendo suyo en la bandeja del ERP, que muestra el documento observado
        /// tal como se observó.
        ///
        /// Mira las dos tablas: el código nace en la planilla grupal y el consolidado lo hereda, pero
        /// los consolidados anteriores a la planilla grupal (y el reemplazo que deja afuera planillas
        /// ya decididas, que no puede heredar) tienen el suyo propio.
        /// </summary>
        private static async Task<int> MenorNumeroLibreAsync(AppDbContext ctx, string prefijo, int anio)
        {
            var raiz = CodigoRendicionGrupal.Raiz(prefijo, anio);

            var usados = (await ctx.GaConsolidadoS10
                    .Where(c => c.Anio == anio && c.Numero != null
                             && c.Codigo != null && c.Codigo.StartsWith(raiz))
                    .Select(c => c.Numero!.Value)
                    .ToListAsync())
                .ToHashSet();
            usados.UnionWith(await ctx.GaPlanillaGrupal
                .Where(g => g.Anio == anio && g.Codigo.StartsWith(raiz))
                .Select(g => g.Numero)
                .ToListAsync());

            var numero = 1;
            while (usados.Contains(numero)) numero++;
            return numero;
        }

        /// <summary>
        /// Confirma, dentro de la transacción de la subida, que el código calculado antes de armar
        /// la planilla siga libre en las dos tablas. El candado por año se toma ANTES de mirar: en
        /// READ COMMITTED dos preparaciones simultáneas leerían lo mismo. Si otro lo tomó en el
        /// medio —dos grupos de la misma área en los mismos segundos—, la planilla ya tiene impreso
        /// un código ajeno: se corta con 409 en vez de guardar un papel que no coincide.
        ///
        /// El candado es <c>xact</c> y se suelta solo al cerrar la transacción.
        /// </summary>
        private static async Task VerificarCodigoLibreAsync(AppDbContext ctx, int anio, string codigo)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({CorrelativoConsolidadoLockNamespace}, {anio})");

            if (await ctx.GaConsolidadoS10.AnyAsync(c => c.Codigo == codigo)
                || await ctx.GaPlanillaGrupal.AnyAsync(g => g.Codigo == codigo))
                throw new AbrilException(
                    $"Otro grupo tomó el código {codigo} mientras se armaba este. Vuelve a intentarlo.", 409);
        }

        /// <summary>
        /// Por qué una planilla sin la primera revisión aprobada todavía no admite la planilla grupal
        /// ni el consolidado.
        /// </summary>
        private static string MensajeSinPrimeraRevision(int estadoPrimeraRevisionId, string queSeHabilita) =>
            estadoPrimeraRevisionId switch
            {
                EstadosSalida.PrimeraRevision.Borrador =>
                    $"Esta rendición todavía no se envió a primera revisión: {queSeHabilita} "
                    + "se habilita cuando la jefatura la apruebe.",
                EstadosSalida.PrimeraRevision.EnRevision =>
                    $"Esta rendición está en primera revisión: {queSeHabilita} se habilita "
                    + "cuando la jefatura la apruebe.",
                _ =>
                    "Esta rendición está observada: primero hay que corregir las capturas y los "
                    + "montos, volver a generarla y esperar la aprobación de la primera revisión.",
            };

        public async Task<ConsolidadoS10Dto?> GetForSolicitud(int solicitudId)
        {
            var map = await GetForSolicitudes(new[] { solicitudId });
            return map.TryGetValue(solicitudId, out var dto) ? dto : null;
        }

        public async Task<Dictionary<int, ConsolidadoS10Dto>> GetForSolicitudes(IEnumerable<int> solicitudIds)
        {
            var ids = solicitudIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            // rendicion_id de cada solicitud, para resolver el consolidado de la planilla.
            var rendicionPorSolicitud = await ctx.GaSolicitudSalida
                .Where(s => ids.Contains(s.Id))
                .Select(s => new { s.Id, s.RendicionId })
                .ToListAsync();

            return await ConsolidadoS10Loader.LoadAsync(
                ctx, rendicionPorSolicitud.ToDictionary(x => x.Id, x => x.RendicionId));
        }

        public async Task<Dictionary<int, ConsolidadoS10Dto>> GetForRendiciones(IEnumerable<int> rendicionIds)
        {
            var ids = rendicionIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();
            return await ConsolidadoS10Loader.LoadPorRendicionAsync(ctx, ids);
        }
    }
}
