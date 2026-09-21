using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Implementación de <see cref="IConsolidadoS10Service"/>. El PDF se guarda en la misma carpeta
    /// de SharePoint que las planillas de rendición (<c>ga_rendicion_folder</c>): el consolidado es
    /// la contraparte de las planillas en el S10 y así no hace falta configurar una carpeta extra
    /// por entorno.
    /// </summary>
    public class ConsolidadoS10Service : IConsolidadoS10Service
    {
        /// <summary>
        /// Espacio del <c>pg_advisory_xact_lock</c> con el que se serializa el correlativo
        /// CONS-ÁREA-AAAA-NNN. Va después del de las solicitudes (8472) y el de las planillas (8473):
        /// son tres series independientes y cada una toma su propio candado por año.
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

            var ids = (rendicionIds ?? Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            if (ids.Count == 0)
                throw new AbrilException("Selecciona al menos una rendición.", 400);

            using var ctx = _factory.CreateDbContext();

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

            string Codigos(IEnumerable<int> deIds) => ConsolidadoS10Agrupacion.Enumerar(
                deIds.Select(id => codigo.TryGetValue(id, out var c) ? c : $"#{id}"));

            // RG-35: el Consolidado del S10 se habilita SOLO después de que la jefatura apruebe la
            // primera revisión: lo que falta no es el permiso, es el registro en el S10, que recién
            // se hace con la planilla aprobada. El mensaje dice en qué estado está para no dejarlo
            // adivinando.
            var sinAprobar = planillas
                .Where(p => p.EstadoPrimeraRevisionId != EstadosSalida.PrimeraRevision.Aprobada)
                .ToList();
            if (sinAprobar.Count > 0)
                throw new AbrilException(
                    ids.Count == 1
                        ? MensajeSinPrimeraRevision(sinAprobar[0].EstadoPrimeraRevisionId)
                        : $"{Codigos(sinAprobar.Select(p => p.Id))} todavía no "
                          + (sinAprobar.Count == 1 ? "tiene" : "tienen")
                          + " la primera revisión aprobada: el Consolidado del S10 se habilita recién "
                          + "cuando la jefatura la aprueba.",
                    409);

            // Quiénes pueden compartir un consolidado: no se mezclan jefaturas (JEFE, SUB GERENTE,
            // RESIDENTE) con el resto del equipo —a una jefatura la firma su gerencia, y acá
            // terminaría firmando un documento donde ella misma está incluida— y todos tienen que
            // colgar de un mismo nodo de área por debajo de la gerencia. Es la MISMA regla que se
            // aplica al rendir, así que no se puede consolidar lo que no se pudo agrupar (ni al
            // revés). La planilla grupal sale de este documento, así que la hereda sin código extra.
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
                    $"{Codigos(cerradas)} ya {(cerradas.Count == 1 ? "tiene" : "tienen")} el reembolso "
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
                    + "que " + (faltantes.Count == 1 ? "sigue" : "siguen") + " con el reembolso por decidir: "
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

            // ── Carpeta destino (la misma de las planillas de rendición) ──────
            var folderUrl = await ctx.GaRendicionFolder
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.GaRendicionFolderId)
                .Select(f => f.LinkUrl)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(folderUrl))
                throw new AbrilException(
                    "No se ha configurado la carpeta de SharePoint donde guardar los consolidados del S10. " +
                    "Pide al administrador registrarla en la tabla ga_rendicion_folder.", 409);

            var carpeta = await _sharePointService.ResolveSharePointFolderUrlAsync(folderUrl);
            if (carpeta == null || !carpeta.IsFolder)
                throw new AbrilException("No se pudo resolver la carpeta de consolidados del S10 en SharePoint.", 502);

            // ── Qué pasa con los consolidados anteriores ──────────────────────
            // Se resuelve ANTES de generar los PDF: si este documento reemplaza entero a otro,
            // hereda su código, y el código va impreso en la planilla de reembolso y en el nombre
            // de los archivos.

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
            // Se mintea uno nuevo cuando no hay de quién heredarlo: consolidado nuevo, varios
            // anteriores que se fusionan en uno, o un anterior que sigue vivo cubriendo planillas
            // con el reembolso ya decidido (ahí el código sigue siendo suyo).
            var heredado = anterioresIds.Count == 1 && anterioresDeBaja.Count == 1
                        && !string.IsNullOrWhiteSpace(anterioresDeBaja[0].Codigo)
                            ? anterioresDeBaja[0]
                            : null;

            // ── Área y código de la rendición grupal ─────────────────────────
            var now = DateTimeOffset.UtcNow;

            var areaScopeId = heredado?.AreaScopeId ?? await AreaDelConsolidadoAsync(ctx, userId, ids);
            var area = areaScopeId == null
                ? null
                : await (
                    from sc in ctx.AreaScope
                    join it in ctx.AreaItem on sc.AreaItemId equals it.AreaItemId
                    where sc.AreaScopeId == areaScopeId.Value
                    select new { it.AreaItemName, it.Abreviatura }
                  ).FirstOrDefaultAsync();

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
            else
            {
                prefijoNuevo = CodigoRendicionGrupal.Prefijo(area?.Abreviatura, area?.AreaItemName);
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

            // ── Planilla de reembolso (la planilla grupal) ──────────────────
            // El tercer documento: los trayectos de TODO lo que cubre este consolidado en una sola
            // tabla, con la planilla de la que sale cada fila. Se arma acá y no al rendir porque
            // recién en este paso queda definido qué planillas van juntas, y se rehace en cada
            // reemplazo porque los montos pueden haber cambiado en la subsanación. La cabecera es
            // la del consolidador: su razón social, su nombre y el área del consolidado.
            var consolidador = await ctx.Person
                .Where(p => p.UserId == userId && p.FullName != null)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync();
            var razones = await RazonSocialConsolidador.LoadPorUsuarioAsync(ctx, new[] { userId });
            razones.TryGetValue(userId, out var razon);

            var grupalBytes = await _gestionSalidaService.GenerarPlanillaGrupal(ids, new PlanillaReembolsoCabeceraDto
            {
                Codigo          = codigoGrupo,
                RazonSocial     = razon?.Nombre,
                Ruc             = razon?.Ruc,
                Consolidador    = consolidador,
                Area            = area?.AreaItemName,
                NumeroReembolso = reembolso,
            });

            var grupalFilename = $"Planilla_Reembolso_{codigoGrupo}_{stamp}.pdf";

            string grupalUrl;
            string? grupalItemId;
            try
            {
                using var grupalStream = new MemoryStream(grupalBytes);
                var grupalResult = await _sharePointService.UploadToOneDriveFolderAsync(
                    carpeta.DriveId, carpeta.ItemId, grupalFilename, grupalStream,
                    "application/pdf",
                    autoRenameOnLock: true);

                if (grupalResult?.WebUrl is null)
                    throw new AbrilException("No se pudo subir la planilla de reembolso a SharePoint (respuesta vacía).", 502);

                grupalUrl = grupalResult.WebUrl;
                grupalItemId = grupalResult.ItemId;
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la subida de la planilla de reembolso (rendiciones={RendicionIds})",
                    string.Join(",", ids));
                throw new AbrilException("Error al subir la planilla de reembolso a SharePoint.", 502);
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
                PlanillaGrupalUrl      = grupalUrl,
                PlanillaGrupalItemId   = grupalItemId,
                PlanillaGrupalDriveId  = carpeta.DriveId,
                PlanillaGrupalFilename = grupalFilename,
                UploadedById = userId,
                UploadedAt   = now,
                State        = true,
            };

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
        /// El área del consolidado: la del consolidador que lo sube —es el consolidador DEL ÁREA—,
        /// por su ficha vigente (una persona puede tener varias por reingreso) → puesto → área de
        /// destino. Si él no tiene área (sin ficha o sin puesto), la que más se repite entre los
        /// trabajadores de las planillas que cubre.
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
        /// hueco perdido. Se miran también los dados de baja: el código de uno reemplazado sigue
        /// siendo suyo en la bandeja del ERP, que muestra el documento observado tal como se
        /// observó.
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

            var numero = 1;
            while (usados.Contains(numero)) numero++;
            return numero;
        }

        /// <summary>
        /// Confirma, dentro de la transacción de la subida, que el código calculado antes de armar
        /// la planilla siga libre. El candado por año se toma ANTES de mirar: en READ COMMITTED dos
        /// consolidaciones simultáneas leerían lo mismo. Si otro lo tomó en el medio —dos
        /// consolidaciones de la misma área en los mismos segundos—, la planilla ya tiene impreso
        /// un código ajeno: se corta con 409 en vez de guardar un papel que no coincide.
        ///
        /// El candado es <c>xact</c> y se suelta solo al cerrar la transacción.
        /// </summary>
        private static async Task VerificarCodigoLibreAsync(AppDbContext ctx, int anio, string codigo)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({CorrelativoConsolidadoLockNamespace}, {anio})");

            if (await ctx.GaConsolidadoS10.AnyAsync(c => c.Codigo == codigo))
                throw new AbrilException(
                    $"Otro consolidado tomó el código {codigo} mientras se subía este. "
                    + "Vuelve a adjuntar el Consolidado del S10.", 409);
        }

        /// <summary>Por qué una planilla sin la primera revisión aprobada todavía no admite el consolidado.</summary>
        private static string MensajeSinPrimeraRevision(int estadoPrimeraRevisionId) =>
            estadoPrimeraRevisionId switch
            {
                EstadosSalida.PrimeraRevision.Borrador =>
                    "Esta rendición todavía no se envió a primera revisión: el Consolidado del S10 "
                    + "se habilita cuando la jefatura la apruebe.",
                EstadosSalida.PrimeraRevision.EnRevision =>
                    "Esta rendición está en primera revisión: el Consolidado del S10 se habilita "
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
