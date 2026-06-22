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

        var inicioMes = new DateTime(anio, mes, 1);
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

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var workerIds = await ctx.WorkerProyecto
            .Where(wp => wp.ProyectoId == proyectoId && (wp.FechaFin == null || wp.FechaFin >= hoy))
            .Select(wp => wp.WorkerId)
            .Distinct()
            .ToListAsync();

        var staff = await ctx.Worker
            .Include(w => w.Person)
            .Where(w => workerIds.Contains(w.Id)
                && w.Categoria == "Supervisor"
                && (w.ObraOficina == "Staff" || w.ObraOficina == "Oficina Central")
                && w.Estado == "ACTIVO")
            .ToListAsync();

        return staff
            .Select(w => new StaffDto(w.Id, w.Person?.FullName ?? string.Empty, w.Ocupacion ?? string.Empty))
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

        // soft-delete all existing attendance then re-create from the new list
        var existentes = await ctx.SsCharlaAsistencias
            .Where(a => a.CharlaId == charlaId && a.State)
            .ToListAsync();

        foreach (var e in existentes)
        {
            e.State = false;
            e.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var workerId in dto.WorkerIds.Distinct())
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

    public async Task<List<CapacitacionDto>> GetCapacitacionesAsync(int proyectoId, int mes, int anio)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        // get all staff workers for the project
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var workerIds = await ctx.WorkerProyecto
            .Where(wp => wp.ProyectoId == proyectoId && (wp.FechaFin == null || wp.FechaFin >= hoy))
            .Select(wp => wp.WorkerId)
            .Distinct()
            .ToListAsync();

        var staff = await ctx.Worker
            .Include(w => w.Person)
            .Where(w => workerIds.Contains(w.Id)
                && w.Categoria == "Supervisor"
                && (w.ObraOficina == "Staff" || w.ObraOficina == "Oficina Central")
                && w.Estado == "ACTIVO")
            .ToListAsync();

        var staffIds = staff.Select(w => w.Id).ToList();

        // get capacitaciones this month
        var inicioMes = new DateTime(anio, mes, 1);
        var finMes = inicioMes.AddMonths(1);
        var caps = await ctx.SsCharlas
            .Where(c => c.SupervisorId != null
                && staffIds.Contains(c.SupervisorId!.Value)
                && c.Fecha >= inicioMes && c.Fecha < finMes
                && c.State)
            .ToListAsync();

        var result = new List<CapacitacionDto>();
        foreach (var w in staff)
        {
            var cap = caps.FirstOrDefault(c => c.SupervisorId == w.Id);
            result.Add(new CapacitacionDto(
                cap?.Id,
                w.Id,
                w.Person?.FullName ?? string.Empty,
                cap?.Fecha,
                cap?.Tema,
                cap?.EvidenciaUrl,
                cap?.EvidenciaNombre,
                cap == null ? "Falta" : cap.Estado
            ));
        }

        return result;
    }

    public async Task<CapacitacionDto> SubirCapacitacionAsync(int workerId, DateTime fecha, string tema, Stream evidencia, string fileName, int userId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var worker = await ctx.Worker
            .Include(w => w.Person)
            .FirstOrDefaultAsync(w => w.Id == workerId)
            ?? throw new AbrilException("Trabajador no encontrado.", 404);

        var carpeta = $"Capacitaciones/{fecha.Year}/{fecha.Month}/{workerId}";
        var url = await _sp.SubirArchivoYObtenerUrlAsync(evidencia, fileName, "charlas-evidencias", carpeta);

        // find or create the capacitacion record for this worker this month
        var inicioMes = new DateTime(fecha.Year, fecha.Month, 1);
        var finMes = inicioMes.AddMonths(1);

        var cap = await ctx.SsCharlas
            .FirstOrDefaultAsync(c => c.SupervisorId == workerId && c.Fecha >= inicioMes && c.Fecha < finMes && c.State);

        // need a programa_id — use 0 sentinel or get/create for worker's project
        // find project for worker
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

        if (cap == null)
        {
            cap = new SsCharla
            {
                ProgramaId = programaId,
                Fecha = fecha,
                Titulo = $"Capacitación {worker.Person?.FullName ?? workerId.ToString()}",
                Tema = tema,
                SupervisorId = workerId,
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
            cap.Tema = tema;
            cap.EvidenciaUrl = url;
            cap.EvidenciaNombre = fileName;
            cap.EvidenciaSubidaPorId = userId;
            cap.EvidenciaSubidaEn = DateTime.UtcNow;
            cap.Estado = "Enviado";
            cap.UpdatedAt = DateTime.UtcNow;
        }

        await ctx.SaveChangesAsync();

        return new CapacitacionDto(cap.Id, workerId, worker.Person?.FullName ?? string.Empty, cap.Fecha, cap.Tema, cap.EvidenciaUrl, cap.EvidenciaNombre, cap.Estado);
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

        return new CapacitacionDto(cap.Id, cap.SupervisorId ?? 0, worker?.Person?.FullName ?? string.Empty, cap.Fecha, cap.Tema, cap.EvidenciaUrl, cap.EvidenciaNombre, cap.Estado);
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
}
