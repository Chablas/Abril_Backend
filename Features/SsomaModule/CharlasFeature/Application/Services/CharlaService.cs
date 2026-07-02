using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Services;

public class CharlaService : ICharlaService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ISharePointHabService _sp;

    public CharlaService(IDbContextFactory<AppDbContext> factory, ISharePointHabService sp)
    {
        _factory = factory;
        _sp = sp;
    }

    // ── Project detection ─────────────────────────────────────────────────────

    public async Task<ProyectoDto?> GetMiProyectoAsync(int userId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        // Buscar worker_id del usuario via person
        var workerId = await ctx.Person
            .Where(p => p.UserId == userId)
            .Join(ctx.Worker, p => p.PersonId, w => w.PersonId, (p, w) => w.Id)
            .FirstOrDefaultAsync();

        if (workerId == 0) return null;

        // ss_hab_worker_proyecto: asignación activa (sin fecha fin o fecha fin futura)
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var proyectoId = await ctx.WorkerProyecto
            .Where(wp => wp.WorkerId == workerId && (wp.FechaFin == null || wp.FechaFin >= hoy))
            .OrderByDescending(wp => wp.FechaInicio)
            .Select(wp => wp.ProyectoId)
            .FirstOrDefaultAsync();

        if (proyectoId == 0) return null;

        var nombre = await ctx.Project
            .Where(p => p.ProjectId == proyectoId)
            .Select(p => p.ProjectDescription)
            .FirstOrDefaultAsync();

        return nombre == null ? null : new ProyectoDto(proyectoId, nombre);
    }

    public async Task<List<ProyectoDto>> GetTodosProyectosAsync()
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Project
            .Where(p => p.State)
            .OrderBy(p => p.ProjectDescription)
            .Select(p => new ProyectoDto(p.ProjectId, p.ProjectDescription ?? string.Empty))
            .ToListAsync();
    }

    public async Task<ResumenDto> GetResumenAsync(int proyectoId, int mes, int anio)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var programa = await ctx.SsCharlaProgramas
            .FirstOrDefaultAsync(p => p.ProyectoId == proyectoId && p.Mes == mes && p.Anio == anio && p.State);

        var totalCharlas = 0;
        var totalAsistencias = 0;
        if (programa != null)
        {
            var charlaIds = await ctx.SsCharlas
                .Where(c => c.ProgramaId == programa.Id && c.SupervisorId == null && c.State)
                .Select(c => c.Id)
                .ToListAsync();
            totalCharlas = charlaIds.Count;
            totalAsistencias = charlaIds.Any()
                ? await ctx.SsCharlaAsistencias.CountAsync(a => charlaIds.Contains(a.CharlaId) && a.State)
                : 0;
        }

        // capacitaciones del mes para este proyecto
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var workerIds = await ctx.WorkerProyecto
            .Where(wp => wp.ProyectoId == proyectoId && (wp.FechaFin == null || wp.FechaFin >= hoy))
            .Select(wp => wp.WorkerId)
            .Distinct()
            .ToListAsync();

        var staffIds = await ctx.Worker
            .Where(w => workerIds.Contains(w.Id)
                && w.Categoria == "Supervisor"
                && (w.ObraOficina == "Staff" || w.ObraOficina == "Oficina Central")
                && w.Estado == "ACTIVO")
            .Select(w => w.Id)
            .ToListAsync();

        var inicioMes = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var finMes = inicioMes.AddMonths(1);
        var caps = await ctx.SsCharlas
            .Where(c => c.SupervisorId != null
                && staffIds.Contains(c.SupervisorId!.Value)
                && c.Fecha >= inicioMes && c.Fecha < finMes
                && c.State)
            .Select(c => c.Estado)
            .ToListAsync();

        var capsTotal = staffIds.Count;
        var capsEnviado = caps.Count(e => e == "Enviado");
        var capsAprobado = caps.Count(e => e == "Aprobado");
        var capsRechazado = caps.Count(e => e == "Rechazado");
        var capsFalta = capsTotal - caps.Count;

        return new ResumenDto(totalCharlas, totalAsistencias, capsTotal, capsFalta, capsEnviado, capsAprobado, capsRechazado);
    }

    // ── Staff list ────────────────────────────────────────────────────────────

    public async Task<List<StaffDto>> GetStaffProyectoAsync(int proyectoId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var staff = await ctx.Worker
            .Include(w => w.Person)
            .Where(w => w.ObraOficina == "Staff"
                && w.Estado == "ACTIVO"
                && ctx.WorkerVinculacion.Any(v =>
                    v.WorkerId == w.Id
                    && v.ProyectoId == proyectoId
                    && v.FechaFin == null))
            .ToListAsync();

        return staff
            .Select(w => new StaffDto(w.Id, ToTitleCase(w.Person?.FullName), w.Ocupacion ?? string.Empty, w.Categoria))
            .OrderBy(s => s.NombreCompleto)
            .ToList();
    }

    // ── Tab 1: Asistencia ─────────────────────────────────────────────────────

    public async Task<List<CharlaResumenDto>> GetCharlasAsync(int proyectoId, int mes, int anio)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        // coordinator charlas: supervisor_id IS NULL, linked to programa for this project/month
        var programa = await ctx.SsCharlaProgramas
            .FirstOrDefaultAsync(p => p.ProyectoId == proyectoId && p.Mes == mes && p.Anio == anio && p.State);

        if (programa == null) return new List<CharlaResumenDto>();

        var charlas = await ctx.SsCharlas
            .Where(c => c.ProgramaId == programa.Id && c.SupervisorId == null && c.State)
            .ToListAsync();

        var charlaIds = charlas.Select(c => c.Id).ToList();
        var asistencias = charlaIds.Any()
            ? await ctx.SsCharlaAsistencias.Where(a => charlaIds.Contains(a.CharlaId) && a.State).ToListAsync()
            : new List<SsCharlaAsistencia>();

        return charlas.Select(c =>
        {
            var asis = asistencias.Where(a => a.CharlaId == c.Id).ToList();
            return new CharlaResumenDto(
                c.Id,
                c.Fecha,
                c.Titulo,
                c.Tema ?? string.Empty,
                c.DuracionHoras,
                asis.Count,
                asis.Select(a => a.WorkerId).ToList()
            );
        }).OrderBy(c => c.Fecha).ToList();
    }

    public async Task<CharlaResumenDto> CrearCharlaAsync(CrearCharlaDto dto, int userId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        // auto-create programa if it doesn't exist
        var mes = dto.Fecha.Month;
        var anio = dto.Fecha.Year;
        var programa = await ctx.SsCharlaProgramas
            .FirstOrDefaultAsync(p => p.ProyectoId == dto.ProyectoId && p.Mes == mes && p.Anio == anio && p.State);

        if (programa == null)
        {
            programa = new SsCharlaPrograma
            {
                ProyectoId = dto.ProyectoId,
                Mes = mes,
                Anio = anio,
                Nombre = $"Charlas {mes}/{anio}",
                Estado = "Activo",
                CreadoPorId = userId,
                CreatedAt = DateTime.UtcNow
            };
            ctx.SsCharlaProgramas.Add(programa);
            await ctx.SaveChangesAsync();
        }

        var charla = new SsCharla
        {
            ProgramaId = programa.Id,
            Fecha = dto.Fecha,
            Titulo = dto.Titulo,
            Tema = dto.Tema,
            DuracionHoras = dto.DuracionHoras,
            SupervisorId = null, // coordinator charla
            Estado = "Registrada",
            CreadoPorId = userId,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsCharlas.Add(charla);
        await ctx.SaveChangesAsync();

        return new CharlaResumenDto(charla.Id, charla.Fecha, charla.Titulo, charla.Tema ?? string.Empty, charla.DuracionHoras, 0, new List<int>());
    }

    public async Task EliminarCharlaAsync(int id)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var charla = await ctx.SsCharlas.FirstOrDefaultAsync(c => c.Id == id && c.State)
            ?? throw new AbrilException("Charla no encontrada.", 404);
        charla.State = false;
        charla.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task<List<AsistenciaDetailDto>> GetAsistenciaAsync(int charlaId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var asistencias = await ctx.SsCharlaAsistencias
            .Where(a => a.CharlaId == charlaId && a.State)
            .ToListAsync();

        var workerIds = asistencias.Select(a => a.WorkerId).Distinct().ToList();
        var workers = await ctx.Worker
            .Include(w => w.Person)
            .Where(w => workerIds.Contains(w.Id))
            .ToListAsync();

        return asistencias.Select(a =>
        {
            var w = workers.FirstOrDefault(x => x.Id == a.WorkerId);
            return new AsistenciaDetailDto(a.WorkerId, w?.Person?.FullName ?? string.Empty, a.Asistio);
        }).ToList();
    }

    public async Task GuardarAsistenciaAsync(int charlaId, GuardarAsistenciaDto dto, int userId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        if (!await ctx.SsCharlas.AnyAsync(c => c.Id == charlaId && c.State))
            throw new AbrilException("Charla no encontrada.", 404);

        // Cargar todos los registros existentes (activos e inactivos) para evitar violar el unique constraint
        var existentes = await ctx.SsCharlaAsistencias
            .Where(a => a.CharlaId == charlaId)
            .ToListAsync();

        var nuevosIds = dto.WorkerIds.Distinct().ToHashSet();

        // Activar/desactivar y actualizar Asistio según la nueva lista
        foreach (var e in existentes)
        {
            var estaEnLista = nuevosIds.Contains(e.WorkerId);
            e.State = estaEnLista;
            e.Asistio = estaEnLista;
            e.UpdatedAt = DateTime.UtcNow;
        }

        // Insertar solo los que no tienen registro previo
        var existentesWorkerIds = existentes.Select(e => e.WorkerId).ToHashSet();
        foreach (var workerId in nuevosIds.Where(id => !existentesWorkerIds.Contains(id)))
        {
            ctx.SsCharlaAsistencias.Add(new SsCharlaAsistencia
            {
                CharlaId = charlaId,
                WorkerId = workerId,
                Asistio = true,
                RegistradoPorId = userId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await ctx.SaveChangesAsync();
    }

    // ── Tab 2: Capacitaciones Staff ───────────────────────────────────────────

    public async Task<List<CapacitacionDto>> GetCapacitacionesAsync(int proyectoId, int? mes, int? anio)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var workerIds = await ctx.WorkerVinculacion
            .Where(v => v.ProyectoId == proyectoId && v.FechaFin == null)
            .Select(v => v.WorkerId)
            .Distinct()
            .ToListAsync();

        var workers = await ctx.Worker
            .Include(w => w.Person)
            .Where(w => workerIds.Contains(w.Id) && w.Estado == "ACTIVO")
            .ToListAsync();

        var workerIdsFiltrados = workers.Select(w => w.Id).ToList();

        var capsQuery = ctx.SsCharlas
            .Where(c => c.SupervisorId != null
                && workerIdsFiltrados.Contains(c.SupervisorId!.Value)
                && (c.EsCapacitacionIndividual || c.EvidenciaUrl != null)
                && c.State);

        if (mes.HasValue && mes > 0 && anio.HasValue && anio > 0)
        {
            var inicioMes = new DateTime(anio.Value, mes.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            var finMes = inicioMes.AddMonths(1);
            capsQuery = capsQuery.Where(c => c.Fecha >= inicioMes && c.Fecha < finMes);
        }
        else if (anio.HasValue && anio > 0)
        {
            var inicioAnio = new DateTime(anio.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var finAnio = inicioAnio.AddYears(1);
            capsQuery = capsQuery.Where(c => c.Fecha >= inicioAnio && c.Fecha < finAnio);
        }

        var caps = await capsQuery.ToListAsync();

        var workerDict = workers.ToDictionary(w => w.Id);
        var allCharlaIds = caps.Select(c => c.Id).ToList();
        var todosArchivos = allCharlaIds.Count > 0
            ? await ctx.SsCharlaArchivos
                .Where(a => allCharlaIds.Contains(a.CharlaId) && a.State)
                .OrderBy(a => a.Id)
                .ToListAsync()
            : [];

        var result = caps.OrderByDescending(c => c.Fecha).Select(cap =>
        {
            workerDict.TryGetValue(cap.SupervisorId!.Value, out var w);
            var archivos = todosArchivos.Where(a => a.CharlaId == cap.Id)
                .Select(a => new ArchivoItemDto(a.Id, a.Url, a.Nombre)).ToList();
            return new CapacitacionDto(cap.Id, cap.SupervisorId!.Value, w?.Person?.FullName ?? string.Empty,
                cap.Fecha, cap.Tema, cap.EvidenciaUrl, cap.EvidenciaNombre, cap.Estado, archivos);
        }).ToList();

        return result;
    }

    public async Task<CapacitacionDto> SubirCapacitacionAsync(int workerId, DateTime fecha, string tema, Stream evidencia, string fileName, int userId)
    {
        fecha = DateTime.SpecifyKind(fecha, DateTimeKind.Utc);
        await using var ctx = await _factory.CreateDbContextAsync();

        var worker = await ctx.Worker
            .Include(w => w.Person)
            .FirstOrDefaultAsync(w => w.Id == workerId)
            ?? throw new AbrilException("Trabajador no encontrado.", 404);

        var carpeta = $"Capacitaciones/{fecha.Year}/{fecha.Month}/{workerId}";
        var url = await _sp.SubirArchivoYObtenerUrlAsync(evidencia, fileName, "charlas-evidencias", carpeta);

        // find or create the capacitacion record for this worker this month
        var inicioMes = new DateTime(fecha.Year, fecha.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var finMes = inicioMes.AddMonths(1);

        var cap = await ctx.SsCharlas
            .FirstOrDefaultAsync(c => c.SupervisorId == workerId && c.Fecha >= inicioMes && c.Fecha < finMes && c.State);

        // find project for worker — use WorkerVinculacion (same source as DesempenoSupervisor)
        var proyectoIdNullable = await ctx.WorkerVinculacion
            .Where(v => v.WorkerId == workerId && v.FechaFin == null)
            .OrderByDescending(v => v.Id)
            .Select(v => (int?)v.ProyectoId)
            .FirstOrDefaultAsync();
        var proyectoId = proyectoIdNullable ?? 0;

        int programaId = 0;
        if (proyectoId > 0)
        {
            var mes = fecha.Month;
            var anio = fecha.Year;
            var programa = await ctx.SsCharlaProgramas
                .FirstOrDefaultAsync(p => p.ProyectoId == proyectoId && p.Mes == mes && p.Anio == anio && p.State);
            if (programa == null)
            {
                programa = new SsCharlaPrograma
                {
                    ProyectoId = proyectoId,
                    Mes = mes,
                    Anio = anio,
                    Nombre = $"Capacitaciones {mes}/{anio}",
                    Estado = "Activo",
                    CreadoPorId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                ctx.SsCharlaProgramas.Add(programa);
                await ctx.SaveChangesAsync();
            }
            programaId = programa.Id;
        }

        if (cap == null)
        {
            cap = new SsCharla
            {
                ProgramaId = programaId,
                Fecha = fecha,
                Titulo = tema,
                Tema = tema,
                SupervisorId = workerId,
                ProyectoId = proyectoId > 0 ? proyectoId : null,
                EsCapacitacionIndividual = true,
                EvidenciaUrl = url,
                EvidenciaNombre = fileName,
                EvidenciaSubidaPorId = userId,
                EvidenciaSubidaEn = DateTime.UtcNow,
                Estado = "Enviado",
                CreadoPorId = userId,
                CreatedAt = DateTime.UtcNow
            };
            ctx.SsCharlas.Add(cap);
        }
        else
        {
            cap.Fecha = fecha;
            cap.Titulo = tema;
            cap.Tema = tema;
            cap.ProyectoId = proyectoId > 0 ? proyectoId : cap.ProyectoId;
            cap.EsCapacitacionIndividual = true;
            cap.EvidenciaUrl = url;
            cap.EvidenciaNombre = fileName;
            cap.EvidenciaSubidaPorId = userId;
            cap.EvidenciaSubidaEn = DateTime.UtcNow;
            cap.Estado = "Enviado";
            cap.UpdatedAt = DateTime.UtcNow;
        }

        await ctx.SaveChangesAsync();

        var archivosDb = await ctx.SsCharlaArchivos
            .Where(a => a.CharlaId == cap.Id && a.State)
            .OrderBy(a => a.Id)
            .ToListAsync();
        var archivosDto = archivosDb.Select(a => new ArchivoItemDto(a.Id, a.Url, a.Nombre)).ToList();

        return new CapacitacionDto(cap.Id, workerId, worker.Person?.FullName ?? string.Empty, cap.Fecha, cap.Tema, cap.EvidenciaUrl, cap.EvidenciaNombre, cap.Estado, archivosDto);
    }

    public async Task<CapacitacionDto> SubirMiCapacitacionAsync(int userId, DateTime fecha, string tema, Stream evidencia, string fileName)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var workerId = await ctx.Person
            .Where(p => p.UserId == userId)
            .Join(ctx.Worker, p => p.PersonId, w => w.PersonId, (p, w) => w.Id)
            .FirstOrDefaultAsync();

        if (workerId == 0)
            throw new AbrilException("No se encontró el perfil de trabajador para este usuario.", 404);

        return await SubirCapacitacionAsync(workerId, fecha, tema, evidencia, fileName, userId);
    }

    public async Task<CapacitacionDto> SubirMiCapacitacionMultiAsync(int userId, DateTime fecha, string tema, List<(Stream Stream, string FileName)> archivos)
    {
        fecha = DateTime.SpecifyKind(fecha, DateTimeKind.Utc);
        await using var ctx = await _factory.CreateDbContextAsync();

        var workerId = await ctx.Person
            .Where(p => p.UserId == userId)
            .Join(ctx.Worker, p => p.PersonId, w => w.PersonId, (p, w) => w.Id)
            .FirstOrDefaultAsync();

        if (workerId == 0)
            throw new AbrilException("No se encontró el perfil de trabajador para este usuario.", 404);

        var worker = await ctx.Worker
            .Include(w => w.Person)
            .FirstOrDefaultAsync(w => w.Id == workerId)
            ?? throw new AbrilException("Trabajador no encontrado.", 404);

        // resolve proyecto
        var hoyLocal = DateOnly.FromDateTime(DateTime.UtcNow);
        var proyectoId = await ctx.WorkerProyecto
            .Where(wp => wp.WorkerId == workerId && (wp.FechaFin == null || wp.FechaFin >= hoyLocal))
            .OrderByDescending(wp => wp.FechaInicio)
            .Select(wp => wp.ProyectoId)
            .FirstOrDefaultAsync();

        int programaId = 0;
        if (proyectoId > 0)
        {
            var mes = fecha.Month;
            var anio = fecha.Year;
            var programa = await ctx.SsCharlaProgramas
                .FirstOrDefaultAsync(p => p.ProyectoId == proyectoId && p.Mes == mes && p.Anio == anio && p.State);
            if (programa == null)
            {
                programa = new SsCharlaPrograma
                {
                    ProyectoId = proyectoId,
                    Mes = mes,
                    Anio = anio,
                    Nombre = $"Capacitaciones {mes}/{anio}",
                    Estado = "Activo",
                    CreadoPorId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                ctx.SsCharlaProgramas.Add(programa);
                await ctx.SaveChangesAsync();
            }
            programaId = programa.Id;
        }

        // create a new SsCharla per upload session (one record per tema+fecha)
        var cap = new SsCharla
        {
            ProgramaId = programaId,
            Fecha = fecha,
            Titulo = tema,
            Tema = tema,
            SupervisorId = workerId,
            ProyectoId = proyectoId > 0 ? proyectoId : null,
            EsCapacitacionIndividual = true,
            EvidenciaSubidaPorId = userId,
            EvidenciaSubidaEn = DateTime.UtcNow,
            Estado = "Enviado",
            CreadoPorId = userId,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsCharlas.Add(cap);
        await ctx.SaveChangesAsync();

        // upload each file and create SsCharlaArchivo records
        var carpeta = $"Capacitaciones/{fecha.Year}/{fecha.Month}/{workerId}";
        string? firstUrl = null;
        string? firstNombre = null;

        foreach (var (stream, fileName) in archivos)
        {
            var url = await _sp.SubirArchivoYObtenerUrlAsync(stream, fileName, "charlas-evidencias", carpeta);
            if (firstUrl == null) { firstUrl = url; firstNombre = fileName; }

            ctx.SsCharlaArchivos.Add(new SsCharlaArchivo
            {
                CharlaId = cap.Id,
                Url = url,
                Nombre = fileName,
                CreatedAt = DateTime.UtcNow
            });
        }

        // keep first file on SsCharla for backward compat
        cap.EvidenciaUrl = firstUrl;
        cap.EvidenciaNombre = firstNombre;
        await ctx.SaveChangesAsync();

        var archivosDb = await ctx.SsCharlaArchivos
            .Where(a => a.CharlaId == cap.Id && a.State)
            .OrderBy(a => a.Id)
            .ToListAsync();
        var archivosDto = archivosDb.Select(a => new ArchivoItemDto(a.Id, a.Url, a.Nombre)).ToList();

        return new CapacitacionDto(cap.Id, workerId, worker.Person?.FullName ?? string.Empty, cap.Fecha, cap.Tema, firstUrl, firstNombre, cap.Estado, archivosDto);
    }

    public async Task<CapacitacionDto> CambiarEstadoAsync(int id, string estado, int userId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var cap = await ctx.SsCharlas
            .Include(c => c.Programa)
            .FirstOrDefaultAsync(c => c.Id == id && c.State)
            ?? throw new AbrilException("Capacitación no encontrada.", 404);

        var allowed = new[] { "Aprobado", "Rechazado" };
        if (!allowed.Contains(estado))
            throw new AbrilException("Estado inválido. Use 'Aprobado' o 'Rechazado'.", 400);

        cap.Estado = estado;
        cap.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var worker = cap.SupervisorId.HasValue
            ? await ctx.Worker.Include(w => w.Person).FirstOrDefaultAsync(w => w.Id == cap.SupervisorId.Value)
            : null;

        return new CapacitacionDto(cap.Id, cap.SupervisorId ?? 0, worker?.Person?.FullName ?? string.Empty, cap.Fecha, cap.Tema, cap.EvidenciaUrl, cap.EvidenciaNombre, cap.Estado, []);
    }

    public async Task EliminarCapacitacionAsync(int id)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var cap = await ctx.SsCharlas.FirstOrDefaultAsync(c => c.Id == id && c.State)
            ?? throw new AbrilException("Capacitación no encontrada.", 404);
        cap.State = false;
        cap.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    // ── NEW: Tab 1 — Dashboard Asistencia Supervisores ────────────────────────

    public async Task<List<DashSupervisoresRowDto>> GetDashboardSupervisoresAsync(int proyectoId, int mes, int anio)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var inicio = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var fin = inicio.AddMonths(1);

        var charlas = await ctx.SsCharlas
            .Where(c => c.State
                && c.SupervisorId != null
                && c.Fecha >= inicio && c.Fecha < fin)
            .ToListAsync();

        if (charlas.Count == 0) return new List<DashSupervisoresRowDto>();

        var supervisorIds = charlas.Select(c => c.SupervisorId!.Value).Distinct().ToList();
        var charlaIds = charlas.Select(c => c.Id).ToList();

        var workers = await ctx.Worker
            .Include(w => w.Person)
            .Where(w => supervisorIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Person?.FullName ?? string.Empty);

        var asistencias = await ctx.SsCharlaAsistencias
            .Where(a => charlaIds.Contains(a.CharlaId) && a.State)
            .ToListAsync();

        return charlas.Select(c =>
        {
            var supNombre = c.SupervisorId.HasValue && workers.TryGetValue(c.SupervisorId.Value, out var n) ? n : string.Empty;
            var asis = asistencias.Where(a => a.CharlaId == c.Id).ToList();
            return new DashSupervisoresRowDto(c.Id, c.Titulo, c.Fecha, c.SupervisorId, supNombre, asis.Count, asis.Count(a => a.Asistio));
        }).OrderByDescending(r => r.Fecha).ToList();
    }

    // ── NEW: Tab 2 — Comparativo Programadas vs Realizadas ───────────────────

    private static readonly string[] MesesNombres =
        ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"];

    public async Task<List<ComparativoMesDto>> GetComparativoAsync(int proyectoId, int anio)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var programadas = await ctx.SsCharlaProgramas
            .Where(p => p.ProyectoId == proyectoId && p.Anio == anio && p.State)
            .GroupBy(p => p.Mes)
            .Select(g => new { Mes = g.Key, Total = g.Count() })
            .ToListAsync();

        var inicioAnio = new DateTime(anio, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var finAnio = new DateTime(anio + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var realizadas = await ctx.SsCharlas
            .Where(c => c.State && c.Estado == "Aprobado"
                && c.Fecha >= inicioAnio && c.Fecha < finAnio)
            .Join(ctx.SsCharlaProgramas.Where(p => p.ProyectoId == proyectoId && p.State),
                c => c.ProgramaId, p => p.Id, (c, _) => c)
            .GroupBy(c => c.Fecha.Month)
            .Select(g => new { Mes = g.Key, Total = g.Count() })
            .ToListAsync();

        return Enumerable.Range(1, 12).Select(m => new ComparativoMesDto(
            m,
            MesesNombres[m - 1],
            programadas.FirstOrDefault(p => p.Mes == m)?.Total ?? 0,
            realizadas.FirstOrDefault(r => r.Mes == m)?.Total ?? 0
        )).ToList();
    }

    // ── NEW: Tab 3 — Crear nueva charla ──────────────────────────────────────

    public async Task<CharlaListItemDto> CrearNuevaCharlaAsync(NuevaCharlaCreateDto dto, int userId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        // auto-create or get programa
        var mes = dto.Fecha.Month;
        var anio = dto.Fecha.Year;
        int programaId = dto.ProgramaId ?? 0;

        if (programaId == 0)
        {
            var programa = await ctx.SsCharlaProgramas
                .FirstOrDefaultAsync(p => p.ProyectoId == dto.ProyectoId && p.Mes == mes && p.Anio == anio && p.State);

            if (programa == null)
            {
                programa = new SsCharlaPrograma
                {
                    ProyectoId = dto.ProyectoId,
                    Mes = mes,
                    Anio = anio,
                    Nombre = $"Charlas {mes}/{anio}",
                    Estado = "Activo",
                    CreadoPorId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                ctx.SsCharlaProgramas.Add(programa);
                await ctx.SaveChangesAsync();
            }
            programaId = programa.Id;
        }

        var charla = new SsCharla
        {
            ProgramaId = programaId,
            Titulo = dto.Titulo,
            Tema = dto.Tema,
            Descripcion = dto.Descripcion,
            Fecha = DateTime.SpecifyKind(dto.Fecha, DateTimeKind.Utc),
            DuracionHoras = dto.DuracionHoras,
            SupervisorId = dto.SupervisorId,
            ProyectoId = dto.ProyectoId,
            Estado = "Abierto",
            CreadoPorId = userId,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsCharlas.Add(charla);
        await ctx.SaveChangesAsync();

        foreach (var workerId in dto.WorkerIds.Distinct())
        {
            ctx.SsCharlaAsistencias.Add(new SsCharlaAsistencia
            {
                CharlaId = charla.Id,
                WorkerId = workerId,
                Asistio = false,
                RegistradoPorId = userId,
                CreatedAt = DateTime.UtcNow
            });
        }
        if (dto.WorkerIds.Count > 0) await ctx.SaveChangesAsync();

        var supNombre = string.Empty;
        if (dto.SupervisorId.HasValue)
        {
            supNombre = await ctx.User
                .Where(u => u.UserId == dto.SupervisorId.Value)
                .Select(u => u.Person != null ? u.Person.FullName : string.Empty)
                .FirstOrDefaultAsync() ?? string.Empty;
        }

        return new CharlaListItemDto(charla.Id, charla.Titulo, charla.Tema, charla.Fecha, charla.SupervisorId, supNombre, charla.Estado, charla.EvidenciaNombre, dto.WorkerIds.Count);
    }

    public async Task<List<CharlaGaleriaItemDto>> GetCharlasProyectoAsync(int proyectoId, int mes, int anio)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var inicio = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var fin = inicio.AddMonths(1);

        var charlas = await ctx.SsCharlas
            .Where(c => c.State && !c.EsCapacitacionIndividual
                && c.ProyectoId == proyectoId
                && c.Fecha >= inicio && c.Fecha < fin)
            .OrderByDescending(c => c.Fecha)
            .ToListAsync();

        if (charlas.Count == 0) return [];

        var charlaIds = charlas.Select(c => c.Id).ToList();
        var asistencias = await ctx.SsCharlaAsistencias
            .Where(a => charlaIds.Contains(a.CharlaId) && a.State)
            .ToListAsync();

        return charlas.Select(c =>
        {
            var asis = asistencias.Where(a => a.CharlaId == c.Id).ToList();
            return new CharlaGaleriaItemDto(c.Id, c.Titulo, c.Tema ?? "Seguridad", c.Fecha, asis.Count, asis.Count(a => a.Asistio));
        }).ToList();
    }

    // ── NEW: Tab 4 — Lista paginada ───────────────────────────────────────────

    public async Task<CharlaListResultDto> GetListaAsync(int? proyectoId, string? estado, int page, int pageSize)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var query = ctx.SsCharlas
            .Where(c => c.State && c.Estado != "Registrada");

        if (proyectoId.HasValue)
            query = query.Where(c => ctx.SsCharlaProgramas
                .Any(p => p.Id == c.ProgramaId && p.ProyectoId == proyectoId.Value && p.State));

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(c => c.Estado == estado);

        var total = await query.CountAsync();

        var charlas = await query
            .OrderByDescending(c => c.Fecha)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var supervisorIds = charlas.Where(c => c.SupervisorId.HasValue)
            .Select(c => c.SupervisorId!.Value).Distinct().ToList();

        var supervisores = supervisorIds.Count > 0
            ? await ctx.User
                .Where(u => supervisorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, Nombre = u.Person != null ? u.Person.FullName : string.Empty })
                .ToDictionaryAsync(u => u.UserId, u => u.Nombre ?? string.Empty)
            : new Dictionary<int, string>();

        var charlaIds = charlas.Select(c => c.Id).ToList();
        var conteos = await ctx.SsCharlaAsistencias
            .Where(a => charlaIds.Contains(a.CharlaId) && a.State)
            .GroupBy(a => a.CharlaId)
            .Select(g => new { CharlaId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.CharlaId, x => x.Total);

        var items = charlas.Select(c =>
        {
            var supNombre = c.SupervisorId.HasValue && supervisores.TryGetValue(c.SupervisorId.Value, out var n) ? n : string.Empty;
            conteos.TryGetValue(c.Id, out var total2);
            return new CharlaListItemDto(c.Id, c.Titulo, c.Tema, c.Fecha, c.SupervisorId, supNombre, c.Estado, c.EvidenciaNombre, total2);
        }).ToList();

        return new CharlaListResultDto(items, total);
    }

    // ── NEW: Tab 4 — Detalle modal ────────────────────────────────────────────

    public async Task<CharlaDetalleDto> GetDetalleAsync(int id)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var charla = await ctx.SsCharlas
            .Include(c => c.Asistencias)
            .FirstOrDefaultAsync(c => c.Id == id && c.State)
            ?? throw new AbrilException("Charla no encontrada.", 404);

        var supNombre = string.Empty;
        if (charla.SupervisorId.HasValue)
        {
            supNombre = await ctx.User
                .Where(u => u.UserId == charla.SupervisorId.Value)
                .Select(u => u.Person != null ? u.Person.FullName : string.Empty)
                .FirstOrDefaultAsync() ?? string.Empty;
        }

        var asistenciaIds = charla.Asistencias.Where(a => a.State).Select(a => a.WorkerId).Distinct().ToList();
        var workers = asistenciaIds.Count > 0
            ? await ctx.Worker
                .Include(w => w.Person)
                .Where(w => asistenciaIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id, w => w.Person?.FullName ?? string.Empty)
            : new Dictionary<int, string>();

        var asistencias = charla.Asistencias
            .Where(a => a.State)
            .Select(a =>
            {
                workers.TryGetValue(a.WorkerId, out var nombre);
                return new AsistenciaDetailDto(a.WorkerId, nombre ?? string.Empty, a.Asistio);
            }).ToList();

        return new CharlaDetalleDto(
            charla.Id, charla.Titulo, charla.Tema, charla.Descripcion,
            charla.Fecha, charla.DuracionHoras,
            charla.SupervisorId, supNombre,
            charla.Estado, charla.EvidenciaUrl, charla.EvidenciaNombre,
            asistencias.Count, asistencias,
            charla.EvidenciaSubidaEn
        );
    }

    // ── NEW: Tab 4 — Aprobar / Rechazar ──────────────────────────────────────

    public async Task AprobarAsync(int id, int userId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var charla = await ctx.SsCharlas.FirstOrDefaultAsync(c => c.Id == id && c.State)
            ?? throw new AbrilException("Charla no encontrada.", 404);

        charla.Estado = "Aprobado";
        charla.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task RechazarAsync(int id, string motivo, int userId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var charla = await ctx.SsCharlas.FirstOrDefaultAsync(c => c.Id == id && c.State)
            ?? throw new AbrilException("Charla no encontrada.", 404);

        charla.Estado = "Rechazado";
        charla.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    // ── NEW: Mis capacitaciones ───────────────────────────────────────────────

    public async Task<List<CapacitacionDto>> GetMisCapacitacionesAsync(int userId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var workerId = await ctx.Person
            .Where(p => p.UserId == userId)
            .Join(ctx.Worker, p => p.PersonId, w => w.PersonId, (p, w) => w.Id)
            .FirstOrDefaultAsync();

        if (workerId == 0) return [];

        var worker = await ctx.Worker
            .Include(w => w.Person)
            .FirstOrDefaultAsync(w => w.Id == workerId);

        var nombre = worker?.Person?.FullName ?? string.Empty;

        var caps = await ctx.SsCharlas
            .Where(c => c.State && c.EsCapacitacionIndividual && c.SupervisorId == workerId)
            .Include(c => c.Archivos.Where(a => a.State))
            .OrderByDescending(c => c.Fecha)
            .ToListAsync();

        return caps.Select(c =>
        {
            var archivosDto = c.Archivos.OrderBy(a => a.Id).Select(a => new ArchivoItemDto(a.Id, a.Url, a.Nombre)).ToList();
            return new CapacitacionDto(c.Id, workerId, nombre, c.Fecha, c.Tema, c.EvidenciaUrl, c.EvidenciaNombre, c.Estado, archivosDto);
        }).ToList();
    }

    // ── NEW: Dashboard ────────────────────────────────────────────────────────

    private static (DateTime Inicio, DateTime Fin) GetWeekRange(int anio, int semana)
    {
        var lunes = System.Globalization.ISOWeek.ToDateTime(anio, semana, DayOfWeek.Monday);
        var inicio = DateTime.SpecifyKind(lunes, DateTimeKind.Utc);
        return (inicio, inicio.AddDays(7));
    }

    public async Task<DashPersonalResultDto> GetDashPersonalAsync(int proyectoId, int mes, int anio)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var inicioMes = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var finMes = inicioMes.AddMonths(1);

        var workerIds = await ctx.WorkerVinculacion
            .Where(v => v.ProyectoId == proyectoId && v.FechaFin == null)
            .Select(v => v.WorkerId).Distinct().ToListAsync();

        var staff = await ctx.Worker
            .Include(w => w.Person)
            .Where(w => workerIds.Contains(w.Id) && w.ObraOficina == "Staff" && w.Estado == "ACTIVO")
            .ToListAsync();

        if (staff.Count == 0) return new DashPersonalResultDto([], []);

        var charlasMes = await ctx.SsCharlas
            .Where(c => c.State && !c.EsCapacitacionIndividual
                && c.ProyectoId == proyectoId
                && c.Fecha >= inicioMes && c.Fecha < finMes)
            .Select(c => new { c.Id, c.Fecha })
            .ToListAsync();

        var charlasIds = charlasMes.Select(c => c.Id).ToList();

        var asistencias = charlasIds.Count > 0
            ? await ctx.SsCharlaAsistencias
                .Where(a => charlasIds.Contains(a.CharlaId) && a.State)
                .ToListAsync()
            : [];

        // Agrupar por día calendario (varios charlas el mismo día = un solo día en el header)
        var charlasPorDia = charlasMes
            .GroupBy(c => c.Fecha.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var d = g.Key.DayOfWeek;
                var numDia = (int)d == 0 ? 7 : (int)d;
                var nombre = numDia switch
                {
                    1 => "Lun", 2 => "Mar", 3 => "Mié",
                    4 => "Jue", 5 => "Vie", 6 => "Sáb", _ => "Dom"
                };
                return new
                {
                    NumDia = g.Key.Day,
                    Nombre = nombre,
                    Fecha = DateTime.SpecifyKind(g.Key, DateTimeKind.Utc),
                    CharlaIds = g.Select(c => c.Id).ToList()
                };
            })
            .ToList();

        var diasHeader = charlasPorDia
            .Select(d => new DashDiaSemanaDto(d.NumDia, d.Nombre, d.Fecha))
            .ToList();

        var staffRows = staff.Select(w =>
        {
            var diasWorker = charlasPorDia.Select(dia =>
            {
                var tieneRegistro = dia.CharlaIds.Any(cid =>
                    asistencias.Any(a => a.CharlaId == cid && a.WorkerId == w.Id));
                var asistioAlguna = dia.CharlaIds.Any(cid =>
                    asistencias.Any(a => a.CharlaId == cid && a.WorkerId == w.Id && a.Asistio));
                bool? asistio = tieneRegistro ? asistioAlguna : (bool?)null;
                return new DashPersonaAsistDiaDto(dia.NumDia, asistio);
            }).ToList();

            var charlasAsistidas = diasWorker.Count(d => d.Asistio == true);
            var charlasTotales = charlasPorDia.Count;

            return new DashPersonalItemDto(
                w.Id, w.Person?.FullName ?? string.Empty, w.Ocupacion ?? string.Empty,
                diasWorker, charlasAsistidas, charlasTotales, 0, 0, 0
            );
        }).OrderBy(d => d.Nombre).ToList();

        return new DashPersonalResultDto(diasHeader, staffRows);
    }

    public async Task<List<DashProyectoItemDto>> GetDashProyectosAsync(int mes, int anio)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var inicioMes = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var finMes = inicioMes.AddMonths(1);

        var proyectoIds = await ctx.SsCharlas
            .Where(c => c.State && c.ProyectoId != null
                && c.Fecha >= inicioMes && c.Fecha < finMes)
            .Select(c => c.ProyectoId!.Value)
            .Distinct().ToListAsync();

        if (proyectoIds.Count == 0) return [];

        var proyectos = await ctx.Project
            .Where(p => proyectoIds.Contains(p.ProjectId))
            .Select(p => new { p.ProjectId, p.ProjectDescription })
            .ToListAsync();

        var charlasMes = await ctx.SsCharlas
            .Where(c => c.State && !c.EsCapacitacionIndividual
                && c.ProyectoId != null && proyectoIds.Contains(c.ProyectoId!.Value)
                && c.Fecha >= inicioMes && c.Fecha < finMes)
            .ToListAsync();

        var charlaIds = charlasMes.Select(c => c.Id).ToList();
        var asistencias = charlaIds.Count > 0
            ? await ctx.SsCharlaAsistencias.Where(a => charlaIds.Contains(a.CharlaId) && a.State).ToListAsync()
            : [];

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var staffPorProyecto = await ctx.WorkerProyecto
            .Where(wp => proyectoIds.Contains(wp.ProyectoId)
                && (wp.FechaFin == null || wp.FechaFin >= hoy))
            .Join(ctx.Worker.Where(w => w.ObraOficina == "Staff" && w.Estado == "ACTIVO"),
                wp => wp.WorkerId, w => w.Id, (wp, w) => wp.ProyectoId)
            .GroupBy(pid => pid)
            .Select(g => new { ProyectoId = g.Key, Count = g.Count() })
            .ToListAsync();

        return proyectos.Select(p =>
        {
            var charlasProy = charlasMes.Where(c => c.ProyectoId == p.ProjectId).ToList();
            var charlaIdsProy = charlasProy.Select(c => c.Id).ToList();
            var asisProy = asistencias.Where(a => charlaIdsProy.Contains(a.CharlaId)).ToList();
            var totalAsistio = asisProy.Count(a => a.Asistio);
            var totalPosibles = asisProy.Count;
            var totalStaff = staffPorProyecto.FirstOrDefault(s => s.ProyectoId == p.ProjectId)?.Count ?? 0;

            return new DashProyectoItemDto(
                p.ProjectId, p.ProjectDescription,
                totalStaff, charlasProy.Count,
                totalAsistio, totalPosibles,
                0, 0
            );
        }).OrderByDescending(d => d.CharlasDictadas).ToList();
    }

    // ── NEW: Supervisor search ────────────────────────────────────────────────

    public async Task<List<UsuarioDto>> GetSupervisoresAsync(string? search = null)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var query = ctx.User
            .Include(u => u.Person)
            .Where(u => u.Active && u.State);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(u => u.Person != null
                && u.Person.FullName != null
                && u.Person.FullName.ToLower().Contains(s));
        }

        return await query
            .OrderBy(u => u.Person != null ? u.Person.FullName : string.Empty)
            .Take(50)
            .Select(u => new UsuarioDto(u.UserId, u.Person != null ? u.Person.FullName ?? string.Empty : string.Empty, u.Email))
            .ToListAsync();
    }

    private static string ToTitleCase(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpper(w[0]) + w[1..].ToLower()));
    }
}
