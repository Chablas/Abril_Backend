using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.ControlAcceso;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class ControlAccesoRepository : IControlAccesoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IConfiguration _configuration;
        private const int ItemSctr = 11;

        public ControlAccesoRepository(IDbContextFactory<AppDbContext> factory, IConfiguration configuration)
        {
            _factory = factory;
            _configuration = configuration;
        }

        public async Task<List<ControlAccesoWorkerDto>> GetConsultaAsync(string? search, int? proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.Worker.Include(w => w.Person).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                var esDni = s.Length == 8 && s.All(char.IsDigit);
                if (esDni)
                    query = query.Where(w => w.Person != null && w.Person.DocumentIdentityCode == s);
                else
                    query = query.Where(w => w.Person != null && w.Person.FullName != null && w.Person.FullName.ToLower().Contains(s.ToLower()));
            }

            if (proyectoId.HasValue)
            {
                var ids = await ctx.WorkerVinculacion
                    .Where(v => v.ProyectoId == proyectoId.Value && v.FechaFin == null)
                    .Select(v => v.WorkerId)
                    .ToListAsync();
                query = query.Where(w => ids.Contains(w.Id));
            }

            var oficinaId = _configuration.GetValue<int>("OficinaCentral:ProjectId");
            var esOficinaCentral = proyectoId.HasValue && proyectoId.Value == oficinaId;

            var workers = await query.Take(100).ToListAsync();
            return await BuildDtosAsync(ctx, workers, esOficinaCentral);
        }

        public async Task<List<ControlAccesoWorkerDto>> GetNoAutorizadosAsync(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var idsVinc = await ctx.WorkerVinculacion
                .Where(v => v.ProyectoId == proyectoId && v.FechaFin == null)
                .Select(v => v.WorkerId)
                .ToListAsync();

            var idsProyecto = await ctx.WorkerProyecto
                .Where(wp => wp.ProyectoId == proyectoId)
                .Select(wp => wp.WorkerId)
                .ToListAsync();

            var workerIds = idsVinc.Union(idsProyecto).Distinct().ToList();

            var ahora = DateTime.UtcNow;

            var noAutorizadosIds = await ctx.SsHabTrabajador
                .Where(h => workerIds.Contains(h.WorkerId) &&
                            (h.Estado == "Falta" || h.Estado == "Rechazado" || h.Estado == "Vencido" ||
                             (h.Estado == "Aprobado" && h.Vigencia.HasValue && h.Vigencia.Value <= ahora)))
                .Select(h => h.WorkerId)
                .Distinct()
                .ToListAsync();

            var workers = await ctx.Worker
                .Include(w => w.Person)
                .Where(w => noAutorizadosIds.Contains(w.Id))
                .ToListAsync();

            return await BuildDtosAsync(ctx, workers);
        }

        public async Task<List<ControlAccesoWorkerDto>> GetOficinaCentralAsync(int? proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.Worker.Where(w =>
                w.ObraOficina != null &&
                (w.ObraOficina.ToLower() == "oficina central" || w.ObraOficina.ToLower() == "staff"));

            if (proyectoId.HasValue)
            {
                var ids = await ctx.WorkerVinculacion
                    .Where(v => v.ProyectoId == proyectoId.Value && v.FechaFin == null)
                    .Select(v => v.WorkerId)
                    .ToListAsync();
                query = query.Where(w => ids.Contains(w.Id));
            }

            var candidatos = await query.Select(w => w.Id).ToListAsync();

            var ahora = DateTime.UtcNow;
            var conSctrIds = await ctx.SsHabTrabajador
                .Where(h => candidatos.Contains(h.WorkerId) &&
                            h.ItemId == ItemSctr &&
                            h.Estado == "Aprobado" &&
                            h.Vigencia > ahora)
                .Select(h => h.WorkerId)
                .Distinct()
                .ToListAsync();

            var workers = await ctx.Worker
                .Include(w => w.Person)
                .Where(w => conSctrIds.Contains(w.Id))
                .ToListAsync();

            return await BuildDtosAsync(ctx, workers);
        }

        public async Task<List<InduccionHoyDto>> GetInduccionesHoyAsync()
        {
            using var ctx = _factory.CreateDbContext();

            // fecha_programada se almacena en hora local Lima (UTC-5)
            var ahoraLima = DateTime.UtcNow.AddHours(-5);
            var hoyLima = ahoraLima.Date;
            var mananaLima = hoyLima.AddDays(1);
            var fechaLimite = ahoraLima.Hour >= 12 ? mananaLima.AddDays(1) : mananaLima;
            var inducciones = await ctx.SsInduccion
                .Where(i => i.Estado == "PROGRAMADA" &&
                            i.FechaProgramada >= hoyLima &&
                            i.FechaProgramada < fechaLimite)
                .ToListAsync();

            var workerIds = inducciones.Select(i => i.WorkerId).Distinct().ToList();
            var empresaIds = inducciones.Select(i => i.EmpresaId).Distinct().ToList();

            var workerMap = await ctx.Worker
                .Include(w => w.Person)
                .Where(w => workerIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id);

            var empresaMap = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId, c => c.ContributorName);

            return inducciones.Select(i =>
            {
                workerMap.TryGetValue(i.WorkerId, out var w);
                empresaMap.TryGetValue(i.EmpresaId, out var empNombre);

                return new InduccionHoyDto
                {
                    InduccionId = i.Id,
                    WorkerId = i.WorkerId,
                    ApellidoNombre = w?.Person?.FullName ?? "",
                    Dni = w?.Person?.DocumentIdentityCode ?? "",
                    EmpresaNombre = empNombre ?? "",
                    FechaProgramada = i.FechaProgramada,
                    TrabajoAltura = i.TrabajoAltura,
                    EquipoElectrico = i.EquipoElectrico,
                    Estado = i.Estado,
                    IngresoConfirmado = i.IngresoConfirmado,
                    FechaIngreso = i.FechaIngreso
                };
            }).ToList();
        }

        public async Task ConfirmarIngresoAsync(int induccionId)
        {
            using var ctx = _factory.CreateDbContext();

            var induccion = await ctx.SsInduccion.FirstOrDefaultAsync(i => i.Id == induccionId)
                ?? throw new AbrilException("Inducción no encontrada.", 404);

            induccion.IngresoConfirmado = true;
            induccion.FechaIngreso = DateTime.UtcNow;
            induccion.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task<List<TareoPartidaDto>> GetPartidasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsTareoPartida
                .Where(p => p.Activo)
                .OrderBy(p => p.Orden)
                .Select(p => new TareoPartidaDto { Id = p.Id, Nombre = p.Nombre })
                .ToListAsync();
        }

        public async Task<List<TareoEmpresaDto>> GetEmpresasContratistasByProyectoAsync(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var empresaIds = await ctx.WorkerVinculacion
                .Where(v => v.ProyectoId == proyectoId && v.FechaFin == null && v.EmpresaId.HasValue)
                .Select(v => v.EmpresaId!.Value)
                .Distinct()
                .ToListAsync();

            return await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId) && !c.EsAbril)
                .OrderBy(c => c.ContributorName)
                .Select(c => new TareoEmpresaDto { EmpresaId = c.ContributorId, EmpresaNombre = c.ContributorName })
                .ToListAsync();
        }

        public async Task<TareoDto?> GetTareoAsync(int proyectoId, DateOnly fecha)
        {
            using var ctx = _factory.CreateDbContext();

            var tareo = await ctx.SsTareo
                .Include(t => t.Proyecto)
                .FirstOrDefaultAsync(t => t.ProyectoId == proyectoId && t.Fecha == fecha);

            if (tareo == null) return null;

            var (casa, contratista) = await LoadDetallesAsync(ctx, tareo.Id);
            return MapTareoDto(tareo, casa, contratista);
        }

        public async Task<TareoDto> CreateTareoAsync(TareoCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var existe = await ctx.SsTareo
                .AnyAsync(t => t.ProyectoId == dto.ProyectoId && t.Fecha == dto.Fecha);
            if (existe)
                throw new AbrilException("Ya existe un tareo para este proyecto y fecha.", 409);

            var tareo = new SsTareo
            {
                ProyectoId = dto.ProyectoId,
                Fecha = dto.Fecha,
                Observaciones = dto.Observaciones,
                CreadoPor = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            ctx.SsTareo.Add(tareo);
            await ctx.SaveChangesAsync();

            InsertDetalles(ctx, tareo.Id, dto);
            await ctx.SaveChangesAsync();

            await ctx.Entry(tareo).Reference(t => t.Proyecto).LoadAsync();
            var (casa, contratista) = await LoadDetallesAsync(ctx, tareo.Id);
            return MapTareoDto(tareo, casa, contratista);
        }

        public async Task<TareoDto> UpdateTareoAsync(int id, TareoCreateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var tareo = await ctx.SsTareo
                .Include(t => t.Proyecto)
                .FirstOrDefaultAsync(t => t.Id == id)
                ?? throw new AbrilException("Tareo no encontrado.", 404);

            tareo.ProyectoId = dto.ProyectoId;
            tareo.Fecha = dto.Fecha;
            tareo.Observaciones = dto.Observaciones;
            tareo.UpdatedAt = DateTime.UtcNow;

            var oldCasa = await ctx.SsTareoDetalleCasa.Where(d => d.TareoId == id).ToListAsync();
            ctx.SsTareoDetalleCasa.RemoveRange(oldCasa);

            var oldContratista = await ctx.SsTareoDetalleContratista.Where(d => d.TareoId == id).ToListAsync();
            ctx.SsTareoDetalleContratista.RemoveRange(oldContratista);

            InsertDetalles(ctx, id, dto);
            await ctx.SaveChangesAsync();

            var (casa, contratista) = await LoadDetallesAsync(ctx, id);
            return MapTareoDto(tareo, casa, contratista);
        }

        // ─── helpers ───────────────────────────────────────────────────────────

        private static void InsertDetalles(AppDbContext ctx, int tareoId, TareoCreateDto dto)
        {
            if (dto.DetallesCasa.Count > 0)
                ctx.SsTareoDetalleCasa.AddRange(dto.DetallesCasa.Select(d => new SsTareoDetalleCasa
                {
                    TareoId = tareoId,
                    PartidaId = d.PartidaId,
                    CantidadPersonas = d.CantidadPersonas
                }));

            if (dto.DetallesContratista.Count > 0)
                ctx.SsTareoDetalleContratista.AddRange(dto.DetallesContratista.Select(d => new SsTareoDetalleContratista
                {
                    TareoId = tareoId,
                    EmpresaId = d.EmpresaId,
                    CantidadPersonas = d.CantidadPersonas
                }));
        }

        private static async Task<(List<TareoDetalleCasaDto> casa, List<TareoDetalleContratistaDto> contratista)>
            LoadDetallesAsync(AppDbContext ctx, int tareoId)
        {
            var casa = await ctx.SsTareoDetalleCasa
                .Where(d => d.TareoId == tareoId)
                .Join(ctx.SsTareoPartida, d => d.PartidaId, p => p.Id, (d, p) => new TareoDetalleCasaDto
                {
                    PartidaId = d.PartidaId,
                    PartidaNombre = p.Nombre,
                    CantidadPersonas = d.CantidadPersonas
                })
                .ToListAsync();

            var contratista = await ctx.SsTareoDetalleContratista
                .Where(d => d.TareoId == tareoId)
                .Join(ctx.Contributor, d => d.EmpresaId, c => c.ContributorId, (d, c) => new TareoDetalleContratistaDto
                {
                    EmpresaId = d.EmpresaId,
                    EmpresaNombre = c.ContributorName,
                    CantidadPersonas = d.CantidadPersonas
                })
                .ToListAsync();

            return (casa, contratista);
        }

        private static TareoDto MapTareoDto(
            SsTareo t,
            List<TareoDetalleCasaDto>? detallesCasa = null,
            List<TareoDetalleContratistaDto>? detallesContratista = null) => new()
        {
            Id = t.Id,
            ProyectoId = t.ProyectoId,
            ProyectoNombre = t.Proyecto?.ProjectDescription ?? "",
            Fecha = t.Fecha,
            Observaciones = t.Observaciones,
            CreadoPor = t.CreadoPor,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            DetallesCasa = detallesCasa ?? [],
            DetallesContratista = detallesContratista ?? []
        };

        private static async Task<List<ControlAccesoWorkerDto>> BuildDtosAsync(
            AppDbContext ctx, List<Worker> workers, bool esOficinaCentral = false)
        {
            if (workers.Count == 0) return [];

            var workerIds = workers.Select(w => w.Id).ToList();

            var allVincs = await ctx.WorkerVinculacion
                .Where(v => workerIds.Contains(v.WorkerId) && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                .ToListAsync();

            var vincByWorker = allVincs
                .GroupBy(v => v.WorkerId)
                .ToDictionary(g => g.Key, g => g.First());

            var empresaIds = vincByWorker.Values
                .Where(v => v.EmpresaId.HasValue)
                .Select(v => v.EmpresaId!.Value)
                .Distinct().ToList();

            var contribMap = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId);

            var proyIds = vincByWorker.Values
                .Where(v => v.ProyectoId.HasValue)
                .Select(v => v.ProyectoId!.Value)
                .Distinct().ToList();

            var proyMap = await ctx.Project
                .Where(p => proyIds.Contains(p.ProjectId))
                .ToDictionaryAsync(p => p.ProjectId, p => p.ProjectDescription);

            var itemCatalog = await ctx.SsItemTrabajador
                .ToDictionaryAsync(i => i.Id, i => i.Nombre);

            var habItems = await ctx.SsHabTrabajador
                .Where(h => workerIds.Contains(h.WorkerId))
                .ToListAsync();

            var habByWorker = habItems
                .GroupBy(h => h.WorkerId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var ahora = DateTime.UtcNow;
            var en7dias = ahora.AddDays(7);
            const int sctrItemId = ItemSctr;
            const string itemEmoNombre = "Certificado de Aptitud (EMO)";

            var casaIds = workers
                .Where(w => string.Equals(w.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase))
                .Select(w => w.Id)
                .ToHashSet();

            Dictionary<int, WorkerEmo> emoByWorker = [];
            if (!esOficinaCentral && casaIds.Count > 0)
            {
                var emos = await ctx.WorkerEmo
                    .Where(e => casaIds.Contains(e.WorkerId) && e.Activo)
                    .ToListAsync();
                emoByWorker = emos
                    .GroupBy(e => e.WorkerId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.FechaEmo).ThenByDescending(e => e.Id).First());
            }

            return workers.Select(w =>
            {
                vincByWorker.TryGetValue(w.Id, out var vinc);

                var empresaNombre = "";
                var empresaActiva = false;
                var proyectoNombre = "";

                if (vinc?.EmpresaId is int eid && contribMap.TryGetValue(eid, out var contrib))
                {
                    empresaNombre = contrib.ContributorName;
                    empresaActiva = contrib.Active;
                }
                if (vinc?.ProyectoId is int pid && proyMap.TryGetValue(pid, out var pNombre))
                    proyectoNombre = pNombre ?? "";

                habByWorker.TryGetValue(w.Id, out var items);
                items ??= [];

                bool hasPendientes;
                List<string> faltantes;
                List<string> porVencer;
                List<EntregableResumenDto> entregables = [];

                if (esOficinaCentral)
                {
                    var sctr = items.FirstOrDefault(h => h.ItemId == sctrItemId);
                    var sctrOk = sctr != null &&
                                 string.Equals(sctr.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase) &&
                                 sctr.Vigencia.HasValue && sctr.Vigencia.Value > ahora;
                    hasPendientes = !sctrOk;
                    faltantes = sctrOk ? [] : ["SCTR"];
                    porVencer = [];
                }
                else
                {
                    hasPendientes = items.Any(h =>
                        h.Estado == "Falta" || h.Estado == "Rechazado" || h.Estado == "Vencido" ||
                        (h.Estado == "Aprobado" && h.Vigencia.HasValue && h.Vigencia.Value <= ahora));

                    faltantes = items
                        .Where(h => h.Estado == "Falta" || h.Estado == "Rechazado" ||
                                    (h.Estado == "Aprobado" && h.Vigencia.HasValue && h.Vigencia.Value <= ahora))
                        .Select(h => itemCatalog.TryGetValue(h.ItemId, out var n) ? n : null)
                        .Where(n => n != null).Select(n => n!)
                        .ToList();

                    porVencer = items
                        .Where(h => h.Estado == "Aprobado" && h.Vigencia.HasValue
                                    && h.Vigencia.Value > ahora && h.Vigencia.Value <= en7dias)
                        .Select(h => itemCatalog.TryGetValue(h.ItemId, out var n) ? n : null)
                        .Where(n => n != null).Select(n => n!)
                        .ToList();

                    entregables = items
                        .Select(h => new EntregableResumenDto
                        {
                            Nombre = itemCatalog.TryGetValue(h.ItemId, out var n) ? n : "",
                            Estado = h.Estado,
                            Vigencia = h.Vigencia
                        })
                        .Where(e => e.Nombre != "")
                        .ToList();

                    if (casaIds.Contains(w.Id))
                    {
                        emoByWorker.TryGetValue(w.Id, out var emo);

                        string emoEstado;
                        DateTime? emoVigencia = null;

                        if (emo != null && string.Equals(emo.Aptitud, "Apto", StringComparison.OrdinalIgnoreCase))
                        {
                            emoEstado = "Aprobado";
                            if (emo.FechaVencimiento.HasValue)
                                emoVigencia = emo.FechaVencimiento.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                        }
                        else
                        {
                            emoEstado = "Falta";
                        }

                        entregables.Add(new EntregableResumenDto
                        {
                            Nombre = itemEmoNombre,
                            Estado = emoEstado,
                            Vigencia = emoVigencia
                        });

                        var emoEsFaltante = emoEstado == "Falta" ||
                                            (emoEstado == "Aprobado" && emoVigencia.HasValue && emoVigencia.Value <= ahora);
                        if (emoEsFaltante)
                        {
                            hasPendientes = true;
                            faltantes.Add(itemEmoNombre);
                        }
                        if (emoEstado == "Aprobado" && emoVigencia.HasValue && emoVigencia.Value > ahora && emoVigencia.Value <= en7dias)
                            porVencer.Add(itemEmoNombre);
                    }
                }

                return new ControlAccesoWorkerDto
                {
                    WorkerId = w.Id,
                    ApellidoNombre = w.Person?.FullName ?? "",
                    Dni = w.Person?.DocumentIdentityCode ?? "",
                    EmpresaNombre = empresaNombre,
                    ProyectoNombre = proyectoNombre,
                    EstadoHabilitacion = hasPendientes ? "No Autorizado" : "Habilitado",
                    EmpresaActiva = empresaActiva,
                    DocumentosFaltantes = faltantes,
                    DocumentosPorVencer = porVencer,
                    Entregables = entregables
                };
            }).ToList();
        }
    }
}
