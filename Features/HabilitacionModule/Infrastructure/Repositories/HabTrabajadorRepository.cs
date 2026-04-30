using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores;
using Abril_Backend.Features.Habilitacion.Infrastructure.Helpers;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class HabTrabajadorRepository : IHabTrabajadorRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IEmailService _emailService;
        private readonly ILogger<HabTrabajadorRepository> _logger;

        private const int ItemInduccionObra = 12;
        private const int ItemRisst = 6;
        private const int ItemRegistroEpp = 5;
        private const int ItemDifusionPts = 10;
        private const int ItemEntregaRecomendaciones = 8;
        private const int ItemTRegistro = 7;
        private const int ItemSctr = 11;
        private const int ItemVidaLey = 13;
        private const int ItemCertAptitud = 4;
        private const int ItemLecturaEmo = 25;

        private const string EmailMedico = "medicinaocupacionalnm@abril.pe";
        private const string EmailGth = "gth@abril.pe";
        private const string EmailAsistentaSocial = "pquispe@abril.pe";

        public HabTrabajadorRepository(
            IDbContextFactory<AppDbContext> factory,
            IEmailService emailService,
            ILogger<HabTrabajadorRepository> logger)
        {
            _factory = factory;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<(List<WorkerHabilitacionListDto> Items, int Total)> GetWorkersHabilitacionAsync(
            string? search, int? empresaId, int? proyectoId,
            string? estadoHabilitacion, string? contratistaCasa,
            int page, int pageSize, bool soloRetirados = false)
        {
            using var ctx = _factory.CreateDbContext();

            var itemsEmoIds = await ctx.SsItemTrabajador
                .Where(i => i.Nombre.Contains("EMO"))
                .Select(i => i.Id)
                .ToListAsync();

            var baseQuery = ctx.Worker
                .Select(w => new
                {
                    Worker = w,
                    LatestVinc = ctx.WorkerVinculacion
                        .Where(v => v.WorkerId == w.Id)
                        .OrderByDescending(v => v.CreatedAt)
                        .ThenByDescending(v => v.Id)
                        .FirstOrDefault(),
                    EstadoCalc =
                        (ctx.SsHabTrabajador.Any(h => h.WorkerId == w.Id &&
                             (h.Estado == "Falta" || h.Estado == "Rechazado" || h.Estado == "Vencido") &&
                             !(w.ContrataCasa == "Casa" && itemsEmoIds.Contains(h.ItemId)))
                         || (w.ContrataCasa == "Casa" && !ctx.WorkerEmo.Any(e => e.WorkerId == w.Id &&
                             e.Activo && (e.Estado == "Vigente" || e.Estado == "Convalidado"))))
                        ? "No Autorizado"
                        : ctx.SsHabTrabajador.Any(h => h.WorkerId == w.Id && h.Estado == "En Plazo" &&
                            !(w.ContrataCasa == "Casa" && itemsEmoIds.Contains(h.ItemId)))
                        ? "Autorizado Temporalmente"
                        : "Habilitado"
                });

            if (soloRetirados)
                baseQuery = baseQuery.Where(x => x.Worker.Estado == "RETIRADO");
            else
                baseQuery = baseQuery.Where(x => x.Worker.Estado == null || x.Worker.Estado != "RETIRADO");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                baseQuery = baseQuery.Where(x =>
                    (x.Worker.ApellidoNombre != null && x.Worker.ApellidoNombre.ToLower().Contains(s)) ||
                    (x.Worker.Dni != null && x.Worker.Dni.Contains(s)));
            }

            if (empresaId.HasValue)
                baseQuery = baseQuery.Where(x => x.LatestVinc != null && x.LatestVinc.EmpresaId == empresaId.Value);

            if (proyectoId.HasValue)
                baseQuery = baseQuery.Where(x => x.LatestVinc != null && x.LatestVinc.ProyectoId == proyectoId.Value);

            if (!string.IsNullOrWhiteSpace(contratistaCasa))
            {
                var cc = contratistaCasa.Trim();
                baseQuery = baseQuery.Where(x => x.Worker.ContrataCasa == cc);
            }

            if (!string.IsNullOrWhiteSpace(estadoHabilitacion))
                baseQuery = baseQuery.Where(x => x.EstadoCalc == estadoHabilitacion);

            var total = await baseQuery.CountAsync();

            var pageRows = await baseQuery
                .OrderBy(x => x.Worker.ApellidoNombre)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var empresaIds = pageRows
                .Where(r => r.LatestVinc != null && r.LatestVinc.EmpresaId.HasValue)
                .Select(r => r.LatestVinc!.EmpresaId!.Value)
                .Distinct()
                .ToList();

            var proyectoIds = pageRows
                .Where(r => r.LatestVinc != null && r.LatestVinc.ProyectoId.HasValue)
                .Select(r => r.LatestVinc!.ProyectoId!.Value)
                .Distinct()
                .ToList();

            var empresaMap = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId, c => c.ContributorName);

            var proyectos = await ctx.Project
                .Where(p => proyectoIds.Contains(p.ProjectId))
                .Select(p => new { p.ProjectId, p.ProjectDescription })
                .ToListAsync();

            var proyectoMap = proyectos.ToDictionary(p => p.ProjectId, p => p.ProjectDescription);

            var items = pageRows.Select(r => new WorkerHabilitacionListDto
            {
                WorkerId = r.Worker.Id,
                ApellidoNombre = r.Worker.ApellidoNombre ?? string.Empty,
                Dni = r.Worker.Dni ?? string.Empty,
                EmpresaId = r.LatestVinc?.EmpresaId,
                EmpresaNombre = r.LatestVinc?.EmpresaId is int eid && empresaMap.TryGetValue(eid, out var en) ? en : null,
                ProyectoActualId = r.LatestVinc?.ProyectoId,
                ProyectoActual = r.LatestVinc?.ProyectoId is int pid && proyectoMap.TryGetValue(pid, out var pn) ? pn : null,
                EstadoHabilitacion = r.EstadoCalc,
                Categoria = r.Worker.Categoria,
                Ocupacion = r.Worker.Ocupacion,
                ContrataCasa = r.Worker.ContrataCasa,
                ObraOficina = r.Worker.ObraOficina,
                EstadoWorker = r.Worker.Estado ?? "ACTIVO"
            }).ToList();

            return (items, total);
        }

        public async Task<List<WorkerEntregableDto>> GetEntregablesWorkerAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var workerType = string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase)
                ? "CASA"
                : "CONTRATISTA";

            var esContratista = string.Equals(worker.ContrataCasa?.Trim(), "Contratista", StringComparison.OrdinalIgnoreCase);

            var items = await ctx.SsItemTrabajador
                .Where(i => i.Activo && (i.AplicaA == "TODOS" || i.AplicaA == workerType))
                .OrderBy(i => i.Orden)
                .ToListAsync();

            items = items
                .Where(i => CsvContiene(i.AplicaCategoria, worker.Categoria))
                .Where(i => CsvContiene(i.AplicaObraOficina, worker.ObraOficina))
                .Where(i => !CsvExcluye(i.ExcluyeObraOficina, worker.ObraOficina))
                .Where(i => !esContratista || !CsvExcluye(i.ExcluyeCategoriaContratista, worker.Categoria))
                .ToList();

            var emoItems = items.Where(i => i.Nombre.Contains("EMO", StringComparison.OrdinalIgnoreCase)
                                          && i.Id != ItemLecturaEmo).ToList();
            var nonEmoItems = items.Except(emoItems).ToList();
            var nonEmoIds = nonEmoItems.Select(i => i.Id).ToList();

            var existentes = await ctx.SsHabTrabajador
                .Where(h => h.WorkerId == workerId && nonEmoIds.Contains(h.ItemId))
                .ToListAsync();

            var nonEmoMap = nonEmoItems.ToDictionary(i => i.Id);

            var entregables = existentes
                .Where(h => nonEmoMap.ContainsKey(h.ItemId))
                .Select(h =>
                {
                    var item = nonEmoMap[h.ItemId];
                    return new WorkerEntregableDto
                    {
                        Id = h.Id,
                        ItemId = h.ItemId,
                        NombreItem = item.Nombre,
                        Estado = h.Estado,
                        Vigencia = h.Vigencia,
                        ArchivoUrl = h.ArchivoUrl,
                        ObsAbril = h.ObsAbril,
                        ObsContratista = h.ObsContratista,
                        RequiereVigencia = item.RequiereVigencia,
                        EsSctrVidaley = item.EsSctrVidaley,
                        Responsable = item.Responsable
                    };
                })
                .ToList();

            if (emoItems.Count > 0)
            {
                var ultimoEmo = await ctx.WorkerEmo
                    .Where(e => e.WorkerId == workerId && e.Activo)
                    .OrderByDescending(e => e.FechaEmo)
                    .FirstOrDefaultAsync();

                var vigente = ultimoEmo != null
                    && (ultimoEmo.Estado == "Vigente" || ultimoEmo.Estado == "Convalidado");

                DateTime? vigenciaEmo = null;
                if (vigente)
                {
                    var fechaVenc = ultimoEmo!.FechaVencimientoCalculada ?? ultimoEmo.FechaVencimiento;
                    if (fechaVenc.HasValue)
                        vigenciaEmo = fechaVenc.Value.ToDateTime(TimeOnly.MinValue);
                }

                foreach (var item in emoItems)
                {
                    entregables.Add(new WorkerEntregableDto
                    {
                        Id = 0,
                        ItemId = item.Id,
                        NombreItem = item.Nombre,
                        Estado = vigente ? "Aprobado" : "Falta",
                        Vigencia = vigente ? vigenciaEmo : null,
                        ArchivoUrl = null,
                        ObsAbril = "Gestionado por módulo SSOMA",
                        ObsContratista = null,
                        RequiereVigencia = item.RequiereVigencia,
                        EsSctrVidaley = item.EsSctrVidaley,
                        Responsable = item.Responsable
                    });
                }
            }

            var ordenMap = items.ToDictionary(i => i.Id, i => i.Orden);
            return entregables.OrderBy(d => ordenMap[d.ItemId]).ToList();
        }

        public async Task<SsHabTrabajador> UpdateEntregableAsync(int id, WorkerEntregableUpdateDto dto, int? userId, int? empresaId = null)
        {
            using var ctx = _factory.CreateDbContext();

            var entregable = await ctx.SsHabTrabajador
                .Include(h => h.Item)
                .FirstOrDefaultAsync(h => h.Id == id)
                ?? throw new AbrilException("Entregable no encontrado.", 404);

            var estadoAnterior = entregable.Estado;

            var esArchivoNuevo = !string.IsNullOrWhiteSpace(dto.ArchivoUrl) && dto.ArchivoUrl != entregable.ArchivoUrl;
            var esAprobacion = string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase);
            var esRechazo = string.Equals(dto.Estado, "Rechazado", StringComparison.OrdinalIgnoreCase);

            if (esArchivoNuevo || esAprobacion || esRechazo)
            {
                var vinculacion = await ctx.WorkerVinculacion
                    .Where(v => v.WorkerId == entregable.WorkerId && v.FechaFin == null)
                    .OrderByDescending(v => v.CreatedAt)
                    .ThenByDescending(v => v.Id)
                    .FirstOrDefaultAsync();

                var versionActual = await ctx.SsHabDocumentoVersion
                    .CountAsync(v => v.HabTrabajadorId == id);

                ctx.SsHabDocumentoVersion.Add(new SsHabDocumentoVersion
                {
                    HabTrabajadorId = id,
                    Version = versionActual + 1,
                    ArchivoUrl = (esArchivoNuevo ? dto.ArchivoUrl : entregable.ArchivoUrl) ?? string.Empty,
                    SubidoPorUserId = userId,
                    SubidoPorEmpresaId = empresaId,
                    EstadoAlSubir = dto.Estado,
                    EstadoAnterior = estadoAnterior,
                    ProyectoId = vinculacion?.ProyectoId,
                    EmpresaId = vinculacion?.EmpresaId,
                    AprobadoPorUserId = esAprobacion ? userId : null,
                    MotivoRechazo = esRechazo ? dto.ObsAbril : null,
                    CreatedAt = DateTime.UtcNow
                });
            }

            entregable.Estado = dto.Estado;
            entregable.Vigencia = HabilitacionDateHelper.ResolverVigencia(entregable.Item?.RequiereVigencia ?? true, dto.Estado, dto.Vigencia);
            if (dto.ArchivoUrl is not null) entregable.ArchivoUrl = dto.ArchivoUrl;
            if (dto.ObsAbril is not null) entregable.ObsAbril = dto.ObsAbril;
            if (dto.ObsContratista is not null) entregable.ObsContratista = dto.ObsContratista;
            entregable.UpdatedAt = DateTime.UtcNow;

            if (string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase))
            {
                entregable.AprobadoPor = userId;
                entregable.FechaAprobacion = DateTime.UtcNow;
            }

            await ctx.SaveChangesAsync();
            return entregable;
        }

        public async Task<List<SsHabDocumentoVersionDto>> GetVersionesDocumentoAsync(int habTrabajadorId)
        {
            using var ctx = _factory.CreateDbContext();
            var versiones = await ctx.SsHabDocumentoVersion
                .Where(v => v.HabTrabajadorId == habTrabajadorId)
                .OrderByDescending(v => v.Version)
                .ToListAsync();

            return versiones.Select(v => new SsHabDocumentoVersionDto
            {
                Id = v.Id,
                HabTrabajadorId = v.HabTrabajadorId,
                Version = v.Version,
                ArchivoUrl = v.ArchivoUrl,
                SubidoPorUserId = v.SubidoPorUserId,
                SubidoPorEmpresaId = v.SubidoPorEmpresaId,
                EstadoAlSubir = v.EstadoAlSubir,
                EstadoAnterior = v.EstadoAnterior,
                ProyectoId = v.ProyectoId,
                EmpresaId = v.EmpresaId,
                AprobadoPorUserId = v.AprobadoPorUserId,
                MotivoRechazo = v.MotivoRechazo,
                CreatedAt = v.CreatedAt
            }).ToList();
        }

        public async Task<List<WorkerEventoDto>> GetEventosAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();

            var eventos = await ctx.WorkerEvento
                .Where(e => e.WorkerId == workerId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            if (eventos.Count == 0) return [];

            var proyectoIds = eventos
                .SelectMany(e => new[] { e.ProyectoAnteriorId, e.ProyectoNuevoId })
                .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

            var empresaIds = eventos
                .SelectMany(e => new[] { e.EmpresaAnteriorId, e.EmpresaNuevaId })
                .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

            var proyectoMap = proyectoIds.Count > 0
                ? await ctx.Project
                    .Where(p => proyectoIds.Contains(p.ProjectId))
                    .ToDictionaryAsync(p => p.ProjectId, p => p.ProjectDescription)
                : new Dictionary<int, string>();

            var empresaMap = empresaIds.Count > 0
                ? await ctx.Contributor
                    .Where(c => empresaIds.Contains(c.ContributorId))
                    .ToDictionaryAsync(c => c.ContributorId, c => c.ContributorName)
                : new Dictionary<int, string>();

            return eventos.Select(e => new WorkerEventoDto
            {
                Id = e.Id,
                TipoEvento = e.TipoEvento,
                Descripcion = e.Descripcion,
                ProyectoAnteriorId = e.ProyectoAnteriorId,
                ProyectoAnteriorNombre = e.ProyectoAnteriorId is int paid && proyectoMap.TryGetValue(paid, out var pan) ? pan : null,
                ProyectoNuevoId = e.ProyectoNuevoId,
                ProyectoNuevoNombre = e.ProyectoNuevoId is int pnid && proyectoMap.TryGetValue(pnid, out var pnn) ? pnn : null,
                EmpresaAnteriorId = e.EmpresaAnteriorId,
                EmpresaAnteriorNombre = e.EmpresaAnteriorId is int eaid && empresaMap.TryGetValue(eaid, out var ean) ? ean : null,
                EmpresaNuevaId = e.EmpresaNuevaId,
                EmpresaNuevaNombre = e.EmpresaNuevaId is int enid && empresaMap.TryGetValue(enid, out var enn) ? enn : null,
                Datos = e.Datos,
                UsuarioId = e.UsuarioId,
                CreatedAt = e.CreatedAt
            }).ToList();
        }

        public async Task CambiarObraAsync(int workerId, WorkerCambiarObraDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var fechaCambio = DateOnly.FromDateTime(dto.FechaCambio);
            var now = DateTimeOffset.UtcNow;
            var esContratista = !string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase);

            var activas = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .ToListAsync();

            int? currentProyectoId = activas.Select(a => a.ProyectoId).FirstOrDefault();
            int? currentEmpresaId = activas.Select(a => a.EmpresaId).FirstOrDefault();

            var esCambioProyecto = dto.NuevoProyectoId != currentProyectoId;
            var esCambioEmpresa = dto.NuevaEmpresaId.HasValue
                && dto.NuevaEmpresaId != currentEmpresaId
                && !esContratista;

            if (dto.NuevaEmpresaId.HasValue)
                await ValidarExclusividadEmpresaAsync(ctx, workerId, dto.NuevaEmpresaId.Value);

            var itemsToReset = new HashSet<int>();
            var pendingEmails = new List<(List<string> To, string Subject, string Body)>();
            Project? proyectoDestino = null;

            if (esCambioProyecto)
            {
                proyectoDestino = await ctx.Project
                    .FirstOrDefaultAsync(p => p.ProjectId == dto.NuevoProyectoId);

                itemsToReset.Add(ItemInduccionObra);
                if (!esContratista)
                {
                    itemsToReset.Add(ItemRisst);
                    itemsToReset.Add(ItemRegistroEpp);
                    itemsToReset.Add(ItemDifusionPts);
                    itemsToReset.Add(ItemEntregaRecomendaciones);
                    itemsToReset.Add(ItemTRegistro);
                }

                if (!string.IsNullOrWhiteSpace(proyectoDestino?.EmailCoordSsoma))
                {
                    var itemsNombre = esContratista
                        ? "• Inducción Obra"
                        : "• Inducción Obra<br/>• RISST<br/>• Registro EPP<br/>• Difusión PTS<br/>• Entrega de Recomendaciones";
                    pendingEmails.Add((
                        [proyectoDestino.EmailCoordSsoma],
                        $"Cambio de obra — {worker.ApellidoNombre}",
                        BuildBodyReingreso(worker, proyectoDestino, itemsNombre)
                    ));
                }

                if (!esContratista && !string.IsNullOrWhiteSpace(proyectoDestino?.EmailCoordAdmin))
                {
                    pendingEmails.Add((
                        [proyectoDestino.EmailCoordAdmin],
                        $"Cambio de obra — T-Registro — {worker.ApellidoNombre}",
                        BuildBodyReingreso(worker, proyectoDestino, "• T-Registro")
                    ));
                }
            }

            if (esCambioEmpresa)
            {
                itemsToReset.Add(ItemSctr);
                itemsToReset.Add(ItemVidaLey);
                itemsToReset.Add(ItemCertAptitud);

                if (proyectoDestino == null)
                {
                    var pidParaEmail = (int?)dto.NuevoProyectoId ?? currentProyectoId;
                    if (pidParaEmail.HasValue)
                        proyectoDestino = await ctx.Project
                            .FirstOrDefaultAsync(p => p.ProjectId == pidParaEmail.Value);
                }

                var esOficinaOStaff =
                    string.Equals(worker.ObraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(worker.ObraOficina, "Staff", StringComparison.OrdinalIgnoreCase);

                var emailSctr = esOficinaOStaff ? EmailGth : proyectoDestino?.EmailCoordAdmin;
                if (!string.IsNullOrWhiteSpace(emailSctr))
                    pendingEmails.Add((
                        [emailSctr!],
                        $"Cambio de obra — SCTR — {worker.ApellidoNombre}",
                        BuildBodyReingreso(worker, proyectoDestino, "• SCTR")
                    ));

                var emailVidaLey = esOficinaOStaff ? EmailAsistentaSocial : proyectoDestino?.EmailCoordAdmin;
                if (!string.IsNullOrWhiteSpace(emailVidaLey))
                    pendingEmails.Add((
                        [emailVidaLey!],
                        $"Cambio de obra — Vida Ley — {worker.ApellidoNombre}",
                        BuildBodyReingreso(worker, proyectoDestino, "• Vida Ley")
                    ));

                pendingEmails.Add((
                    [EmailMedico],
                    $"Cambio de obra — Certificado de Aptitud — {worker.ApellidoNombre}",
                    BuildBodyReingreso(worker, proyectoDestino, "• Certificado de Aptitud (Homologación)")
                ));
            }

            foreach (var v in activas)
            {
                v.FechaFin = fechaCambio;
                v.UpdatedAt = now;
            }

            ctx.WorkerVinculacion.Add(new WorkerVinculacion
            {
                WorkerId = workerId,
                EmpresaId = dto.NuevaEmpresaId ?? currentEmpresaId,
                ProyectoId = dto.NuevoProyectoId,
                FechaInicio = fechaCambio,
                CreatedAt = now
            });

            if (itemsToReset.Count > 0)
            {
                var entregables = await ctx.SsHabTrabajador
                    .Where(h => h.WorkerId == workerId && itemsToReset.Contains(h.ItemId))
                    .ToListAsync();

                foreach (var e in entregables)
                {
                    e.Estado = "Falta";
                    e.UpdatedAt = DateTime.UtcNow;
                }
            }

            var nowUtc = DateTime.UtcNow;

            if (esCambioProyecto)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.CambioObra,
                    Descripcion = $"Cambio de obra registrado. Fecha: {fechaCambio:dd/MM/yyyy}",
                    ProyectoAnteriorId = currentProyectoId,
                    ProyectoNuevoId = dto.NuevoProyectoId,
                    EmpresaAnteriorId = currentEmpresaId,
                    EmpresaNuevaId = dto.NuevaEmpresaId ?? currentEmpresaId,
                    CreatedAt = nowUtc
                });

            if (esCambioEmpresa)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.CambioEmpresa,
                    Descripcion = $"Cambio de razón social. Fecha: {fechaCambio:dd/MM/yyyy}",
                    EmpresaAnteriorId = currentEmpresaId,
                    EmpresaNuevaId = dto.NuevaEmpresaId,
                    CreatedAt = nowUtc
                });

            foreach (var itemId in itemsToReset)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.EntregableReseteado,
                    Datos = itemId.ToString(),
                    CreatedAt = nowUtc
                });

            await ctx.SaveChangesAsync();

            foreach (var (to, subject, body) in pendingEmails)
                await EnviarEmailSilenciosoAsync(to, subject, body);
        }

        public async Task ReingresoAsync(int workerId, WorkerReingresoDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var fechaReingreso = dto.FechaReingreso ?? DateOnly.FromDateTime(DateTime.Today);
            var now = DateTimeOffset.UtcNow;
            var esContratista = !string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase);

            worker.Estado = "ACTIVO";
            worker.FechaRetiro = null;
            worker.UpdatedAt = now;

            var vinculActual = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .FirstOrDefaultAsync();

            var currentProyectoId = vinculActual?.ProyectoId;
            var currentEmpresaId = vinculActual?.EmpresaId;

            var esCambioProyecto = dto.NuevoProyectoId.HasValue && dto.NuevoProyectoId != currentProyectoId;
            var esCambioEmpresa = dto.NuevaEmpresaId.HasValue && !esContratista;

            var itemsToReset = new HashSet<int>();
            var pendingEmails = new List<(List<string> To, string Subject, string Body)>();

            Project? proyectoDestino = null;

            if (esCambioProyecto)
            {
                proyectoDestino = await ctx.Project
                    .FirstOrDefaultAsync(p => p.ProjectId == dto.NuevoProyectoId!.Value);

                itemsToReset.Add(ItemInduccionObra);
                if (!esContratista)
                {
                    itemsToReset.Add(ItemRisst);
                    itemsToReset.Add(ItemRegistroEpp);
                    itemsToReset.Add(ItemDifusionPts);
                    itemsToReset.Add(ItemEntregaRecomendaciones);
                    itemsToReset.Add(ItemTRegistro);
                }

                if (!string.IsNullOrWhiteSpace(proyectoDestino?.EmailCoordSsoma))
                {
                    var itemsNombre = esContratista
                        ? "• Inducción Obra"
                        : "• Inducción Obra<br/>• RISST<br/>• Registro EPP<br/>• Difusión PTS<br/>• Entrega de Recomendaciones";
                    pendingEmails.Add((
                        [proyectoDestino.EmailCoordSsoma],
                        $"Reingreso de trabajador — {worker.ApellidoNombre}",
                        BuildBodyReingreso(worker, proyectoDestino, itemsNombre)
                    ));
                }

                if (!esContratista && !string.IsNullOrWhiteSpace(proyectoDestino?.EmailCoordAdmin))
                {
                    pendingEmails.Add((
                        [proyectoDestino.EmailCoordAdmin],
                        $"Reingreso de trabajador — T-Registro — {worker.ApellidoNombre}",
                        BuildBodyReingreso(worker, proyectoDestino, "• T-Registro")
                    ));
                }
            }

            if (esCambioEmpresa)
            {
                itemsToReset.Add(ItemSctr);
                itemsToReset.Add(ItemVidaLey);
                itemsToReset.Add(ItemCertAptitud);

                if (proyectoDestino == null)
                {
                    var pidParaEmail = dto.NuevoProyectoId ?? currentProyectoId;
                    if (pidParaEmail.HasValue)
                        proyectoDestino = await ctx.Project
                            .FirstOrDefaultAsync(p => p.ProjectId == pidParaEmail.Value);
                }

                var esOficinaOStaff =
                    string.Equals(worker.ObraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(worker.ObraOficina, "Staff", StringComparison.OrdinalIgnoreCase);

                var emailSctr = esOficinaOStaff ? EmailGth : proyectoDestino?.EmailCoordAdmin;
                if (!string.IsNullOrWhiteSpace(emailSctr))
                    pendingEmails.Add((
                        [emailSctr!],
                        $"Reingreso de trabajador — SCTR — {worker.ApellidoNombre}",
                        BuildBodyReingreso(worker, proyectoDestino, "• SCTR")
                    ));

                var emailVidaLey = esOficinaOStaff ? EmailAsistentaSocial : proyectoDestino?.EmailCoordAdmin;
                if (!string.IsNullOrWhiteSpace(emailVidaLey))
                    pendingEmails.Add((
                        [emailVidaLey!],
                        $"Reingreso de trabajador — Vida Ley — {worker.ApellidoNombre}",
                        BuildBodyReingreso(worker, proyectoDestino, "• Vida Ley")
                    ));

                pendingEmails.Add((
                    [EmailMedico],
                    $"Reingreso de trabajador — Certificado de Aptitud — {worker.ApellidoNombre}",
                    BuildBodyReingreso(worker, proyectoDestino, "• Certificado de Aptitud (Homologación)")
                ));
            }

            if (esCambioProyecto || esCambioEmpresa)
            {
                if (vinculActual != null)
                {
                    vinculActual.FechaFin = fechaReingreso;
                    vinculActual.UpdatedAt = now;
                }

                ctx.WorkerVinculacion.Add(new WorkerVinculacion
                {
                    WorkerId = workerId,
                    EmpresaId = dto.NuevaEmpresaId ?? currentEmpresaId,
                    ProyectoId = dto.NuevoProyectoId ?? currentProyectoId,
                    FechaInicio = fechaReingreso,
                    CreatedAt = now
                });
            }

            if (itemsToReset.Count > 0)
            {
                var entregables = await ctx.SsHabTrabajador
                    .Where(h => h.WorkerId == workerId && itemsToReset.Contains(h.ItemId))
                    .ToListAsync();

                foreach (var e in entregables)
                {
                    e.Estado = "Falta";
                    e.UpdatedAt = DateTime.UtcNow;
                }
            }

            var nowUtc = DateTime.UtcNow;

            ctx.WorkerEvento.Add(new WorkerEvento
            {
                WorkerId = workerId,
                TipoEvento = WorkerTipoEvento.Reingreso,
                Descripcion = $"Reingreso registrado. Fecha: {fechaReingreso:dd/MM/yyyy}",
                ProyectoAnteriorId = currentProyectoId,
                ProyectoNuevoId = dto.NuevoProyectoId ?? currentProyectoId,
                EmpresaAnteriorId = currentEmpresaId,
                EmpresaNuevaId = dto.NuevaEmpresaId ?? currentEmpresaId,
                CreatedAt = nowUtc
            });

            if (esCambioProyecto)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.CambioObra,
                    Descripcion = "Cambio de proyecto en reingreso.",
                    ProyectoAnteriorId = currentProyectoId,
                    ProyectoNuevoId = dto.NuevoProyectoId,
                    CreatedAt = nowUtc
                });

            if (esCambioEmpresa)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.CambioEmpresa,
                    Descripcion = "Cambio de empresa en reingreso.",
                    EmpresaAnteriorId = currentEmpresaId,
                    EmpresaNuevaId = dto.NuevaEmpresaId,
                    CreatedAt = nowUtc
                });

            foreach (var itemId in itemsToReset)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.EntregableReseteado,
                    Datos = itemId.ToString(),
                    CreatedAt = nowUtc
                });

            await ctx.SaveChangesAsync();

            foreach (var (to, subject, body) in pendingEmails)
                await EnviarEmailSilenciosoAsync(to, subject, body);
        }

        private static string BuildBodyReingreso(Worker worker, Project? proyecto, string itemsHtml)
        {
            var proyectoNombre = proyecto?.ProjectDescription ?? "(sin proyecto asignado)";
            return $@"<p>Estimados,</p>
<p>Se notifica el <strong>reingreso del siguiente trabajador</strong>. Los entregables indicados deben ser actualizados:</p>
<table style='border-collapse:collapse;font-family:Arial,sans-serif;font-size:14px;'>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Trabajador</strong></td><td style='border:1px solid #ddd;padding:8px;'>{worker.ApellidoNombre}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>DNI</strong></td><td style='border:1px solid #ddd;padding:8px;'>{worker.Dni}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Modalidad</strong></td><td style='border:1px solid #ddd;padding:8px;'>{worker.ContrataCasa}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Proyecto</strong></td><td style='border:1px solid #ddd;padding:8px;'>{proyectoNombre}</td></tr>
</table>
<p><strong>Entregables pendientes:</strong><br/>{itemsHtml}</p>";
        }

        private async Task EnviarEmailSilenciosoAsync(List<string> to, string subject, string body)
        {
            try
            {
                await _emailService.SendAsync(to, subject, body, isHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al enviar correo de reingreso a {Destinatarios}", string.Join(", ", to));
            }
        }

        public async Task<int?> GetEmpresaActivaWorkerAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .Select(v => v.EmpresaId)
                .FirstOrDefaultAsync();
        }

        public async Task InicializarEntregablesAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var workerType = string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase)
                ? "CASA"
                : "CONTRATISTA";

            var todosItems = await ctx.SsItemTrabajador
                .Where(i => i.Activo)
                .ToListAsync();

            var esContratista = string.Equals(worker.ContrataCasa?.Trim(), "Contratista", StringComparison.OrdinalIgnoreCase);

            var itemsAplicables = todosItems
                .Where(i => i.AplicaA == "TODOS" ||
                            (i.AplicaA == "CASA" && workerType == "CASA") ||
                            (i.AplicaA == "CONTRATISTA" && workerType == "CONTRATISTA"))
                .Where(i => CsvContiene(i.AplicaCategoria, worker.Categoria))
                .Where(i => CsvContiene(i.AplicaObraOficina, worker.ObraOficina))
                .Where(i => !CsvExcluye(i.ExcluyeObraOficina, worker.ObraOficina))
                .Where(i => !esContratista || !CsvExcluye(i.ExcluyeCategoriaContratista, worker.Categoria))
                .ToList();

            var itemIds = itemsAplicables.Select(i => i.Id).ToList();

            var existentesIds = (await ctx.SsHabTrabajador
                .Where(h => h.WorkerId == workerId && itemIds.Contains(h.ItemId))
                .Select(h => h.ItemId)
                .ToListAsync())
                .ToHashSet();

            var nuevos = itemsAplicables
                .Where(i => !existentesIds.Contains(i.Id))
                .Select(i => new SsHabTrabajador
                {
                    WorkerId = workerId,
                    ItemId = i.Id,
                    Estado = "Falta",
                    Vigencia = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
                .ToList();

            if (nuevos.Count > 0)
            {
                ctx.SsHabTrabajador.AddRange(nuevos);
                await ctx.SaveChangesAsync();
            }
        }

        private static bool CsvContiene(string? csv, string? valor)
            => csv == null || csv.Split(',', StringSplitOptions.TrimEntries)
                   .Contains(valor ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        private static bool CsvExcluye(string? csv, string? valor)
            => csv != null && csv.Split(',', StringSplitOptions.TrimEntries)
                   .Contains(valor ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        public async Task<WorkerDetalleDto?> GetByIdAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();
            var w = await ctx.Worker.FirstOrDefaultAsync(x => x.Id == workerId);
            return w is null ? null : MapToDetalle(w);
        }

        public async Task<WorkerDetalleDto> UpdateAsync(int workerId, WorkerUpdateDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var w = await ctx.Worker.FirstOrDefaultAsync(x => x.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            if (dto.ApellidoNombre is not null) w.ApellidoNombre = dto.ApellidoNombre;
            if (dto.Ruc is not null) w.Ruc = dto.Ruc;
            if (dto.Celular is not null) w.Celular = dto.Celular;
            if (dto.EmailPersonal is not null) w.EmailPersonal = dto.EmailPersonal;
            if (dto.EmailCorporativo is not null) w.EmailCorporativo = dto.EmailCorporativo;
            if (dto.FechaNacimiento.HasValue) w.FechaNacimiento = dto.FechaNacimiento;
            if (dto.FechaIngreso.HasValue) w.FechaIngreso = dto.FechaIngreso;
            if (dto.FechaRetiro.HasValue) w.FechaRetiro = dto.FechaRetiro;
            if (dto.Categoria is not null) w.Categoria = dto.Categoria;
            if (dto.Ocupacion is not null) w.Ocupacion = dto.Ocupacion;
            if (dto.Area is not null) w.Area = dto.Area;
            if (dto.Subarea is not null) w.Subarea = dto.Subarea;
            if (dto.ContrataCasa is not null) w.ContrataCasa = dto.ContrataCasa;
            if (dto.ObraOficina is not null) w.ObraOficina = dto.ObraOficina;
            if (dto.Jefatura is not null) w.Jefatura = dto.Jefatura;
            if (dto.Estado is not null) w.Estado = dto.Estado;
            if (dto.HabilitadoObra.HasValue) w.HabilitadoObra = dto.HabilitadoObra;
            if (dto.Sctr.HasValue) w.Sctr = dto.Sctr;
            if (dto.CondicionMedica is not null) w.CondicionMedica = dto.CondicionMedica;
            if (dto.Procedencia is not null) w.Procedencia = dto.Procedencia;
            if (dto.Notas is not null) w.Notas = dto.Notas;
            if (dto.PuntosInfraccion.HasValue) w.PuntosInfraccion = dto.PuntosInfraccion;

            w.UpdatedAt = DateTimeOffset.UtcNow;

            await ctx.SaveChangesAsync();
            return MapToDetalle(w);
        }

        private static WorkerDetalleDto MapToDetalle(Worker w) => new()
        {
            Id = w.Id,
            IdTrabajador = w.IdTrabajador,
            ApellidoNombre = w.ApellidoNombre,
            Dni = w.Dni,
            Ruc = w.Ruc,
            Celular = w.Celular,
            EmailPersonal = w.EmailPersonal,
            EmailCorporativo = w.EmailCorporativo,
            FechaNacimiento = w.FechaNacimiento,
            FechaIngreso = w.FechaIngreso,
            FechaRetiro = w.FechaRetiro,
            Categoria = w.Categoria,
            Ocupacion = w.Ocupacion,
            Area = w.Area,
            Subarea = w.Subarea,
            ContrataCasa = w.ContrataCasa,
            ObraOficina = w.ObraOficina,
            Jefatura = w.Jefatura,
            Estado = w.Estado,
            HabilitadoObra = w.HabilitadoObra,
            Sctr = w.Sctr,
            CondicionMedica = w.CondicionMedica,
            Procedencia = w.Procedencia,
            Notas = w.Notas,
            PuntosInfraccion = w.PuntosInfraccion
        };

        public async Task BajaAsync(int workerId, DateOnly fechaRetiro)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            worker.Estado = "RETIRADO";
            worker.FechaRetiro = fechaRetiro;
            worker.UpdatedAt = DateTimeOffset.UtcNow;

            var vinculacion = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .FirstOrDefaultAsync();

            if (vinculacion != null)
            {
                vinculacion.FechaFin = fechaRetiro;
                vinculacion.UpdatedAt = DateTimeOffset.UtcNow;
            }

            ctx.WorkerEvento.Add(new WorkerEvento
            {
                WorkerId = workerId,
                TipoEvento = WorkerTipoEvento.Baja,
                Descripcion = $"Baja registrada. Fecha retiro: {fechaRetiro:dd/MM/yyyy}",
                ProyectoAnteriorId = vinculacion?.ProyectoId,
                EmpresaAnteriorId = vinculacion?.EmpresaId,
                CreatedAt = DateTime.UtcNow
            });

            await ctx.SaveChangesAsync();
        }

        public async Task BajaMasivaAsync(List<int> ids, DateOnly fechaRetiro)
        {
            if (ids is null || ids.Count == 0) return;

            using var ctx = _factory.CreateDbContext();

            var workers = await ctx.Worker
                .Where(w => ids.Contains(w.Id))
                .ToListAsync();

            if (workers.Count == 0) return;

            var now = DateTimeOffset.UtcNow;
            foreach (var w in workers)
            {
                w.Estado = "RETIRADO";
                w.FechaRetiro = fechaRetiro;
                w.UpdatedAt = now;
            }

            var workerIds = workers.Select(w => w.Id).ToList();
            var vinculaciones = await ctx.WorkerVinculacion
                .Where(v => workerIds.Contains(v.WorkerId) && v.FechaFin == null)
                .ToListAsync();

            foreach (var v in vinculaciones)
            {
                v.FechaFin = fechaRetiro;
                v.UpdatedAt = now;
            }

            var vincMap = vinculaciones
                .GroupBy(v => v.WorkerId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var w in workers)
            {
                vincMap.TryGetValue(w.Id, out var vinc);
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = w.Id,
                    TipoEvento = WorkerTipoEvento.Baja,
                    Descripcion = $"Baja masiva. Fecha retiro: {fechaRetiro:dd/MM/yyyy}",
                    ProyectoAnteriorId = vinc?.ProyectoId,
                    EmpresaAnteriorId = vinc?.EmpresaId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await ctx.SaveChangesAsync();
        }

        private static async Task ValidarExclusividadEmpresaAsync(
            AppDbContext ctx, int workerId, int empresaSolicitanteId)
        {
            var activa = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .FirstOrDefaultAsync();

            if (activa == null || !activa.EmpresaId.HasValue) return;
            if (activa.EmpresaId.Value == empresaSolicitanteId) return;

            ctx.SsHabBloqueoLog.Add(new SsHabBloqueoLog
            {
                WorkerId = workerId,
                EmpresaSolicitanteId = empresaSolicitanteId,
                EmpresaPropietariaId = activa.EmpresaId.Value,
                Motivo = "Trabajador con vinculación activa en otra empresa.",
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();

            throw new AbrilException(
                "Este trabajador está activo en otra empresa y no puede ser habilitado.",
                409);
        }
    }
}
