using Microsoft.EntityFrameworkCore;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;

namespace Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Infrastructure.Repositories;

public class IndicadoresProactivosRepository : IIndicadoresProactivosRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public IndicadoresProactivosRepository(IDbContextFactory<AppDbContext> factory)
        => _factory = factory;

    // ─────────────────────────────────────────────────────────────────────────
    // PROGRAMACIÓN DE INSPECCIONES
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<InspeccionTipoDto>> GetTiposInspeccionAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaInspeccionTipo
            .OrderBy(t => t.Id)
            .Select(t => new InspeccionTipoDto(t.Id, t.Nombre))
            .ToListAsync();
    }

    public async Task<ProgInspeccionResumenDto> GetProgInspeccionAsync(int proyectoId, int mes, int anio)
    {
        using var ctx = _factory.CreateDbContext();

        var empresasContratistas = await ctx.SsTareoDetalleContratista
            .Where(d => d.Tareo!.ProyectoId == proyectoId
                     && d.Tareo.Fecha.Month == mes
                     && d.Tareo.Fecha.Year == anio
                     && d.CantidadPersonas > 0)
            .Select(d => new { d.EmpresaId, NombreEmpresa = d.Empresa!.ContributorName })
            .Distinct()
            .ToListAsync();

        var programados = await ctx.SsomaProgInspeccionEmpresa
            .Where(p => p.ProyectoId == proyectoId && p.Mes == mes && p.Anio == anio)
            .ToListAsync();

        var empresasDto = new List<EmpresaProgInspeccionDto>();

        var casaTipos = programados.Where(p => p.EmpresaTipo == "Casa").Select(p => p.InspeccionTipoId).ToList();
        empresasDto.Add(new EmpresaProgInspeccionDto(null, "Casa", "Casa — Staff Propio", casaTipos));

        foreach (var emp in empresasContratistas)
        {
            var tipos = programados
                .Where(p => p.EmpresaTipo == "Contratista" && p.EmpresaId == emp.EmpresaId)
                .Select(p => p.InspeccionTipoId).ToList();
            empresasDto.Add(new EmpresaProgInspeccionDto(emp.EmpresaId, "Contratista",
                emp.NombreEmpresa ?? $"Empresa {emp.EmpresaId}", tipos));
        }

        return new ProgInspeccionResumenDto(proyectoId, mes, anio, empresasDto);
    }

    public async Task GuardarProgInspeccionAsync(GuardarProgInspeccionRequest req, int userId)
    {
        using var ctx = _factory.CreateDbContext();

        var existentes = await ctx.SsomaProgInspeccionEmpresa
            .Where(p => p.ProyectoId == req.ProyectoId
                     && p.Mes == req.Mes
                     && p.Anio == req.Anio
                     && p.EmpresaTipo == req.EmpresaTipo
                     && p.EmpresaId == req.EmpresaId)
            .ToListAsync();

        ctx.SsomaProgInspeccionEmpresa.RemoveRange(existentes);

        var nuevos = req.TiposAplicables.Select(tipoId => new SsomaProgInspeccionEmpresa
        {
            ProyectoId = req.ProyectoId,
            EmpresaId = req.EmpresaId,
            EmpresaTipo = req.EmpresaTipo,
            Mes = req.Mes,
            Anio = req.Anio,
            InspeccionTipoId = tipoId,
            CreadoPor = userId,
            CreatedAt = DateTime.UtcNow
        });

        ctx.SsomaProgInspeccionEmpresa.AddRange(nuevos);
        await ctx.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CÁLCULO DE METAS E INDICADORES POR EMPRESA
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<MetaEmpresaDto>> GetMetasEmpresaAsync(int proyectoId, int mes, int anio)
    {
        using var ctx = _factory.CreateDbContext();
        var hoy = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var esMesActual = hoy.Month == mes && hoy.Year == anio;
        var fechaIni = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var fechaFin = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes), 0, 0, 0, DateTimeKind.Utc).AddDays(1);
        var fechaCorte = esMesActual ? hoy.AddDays(1) : fechaFin;

        var resultado = new List<MetaEmpresaDto>();
        var fechaIniOnly  = DateOnly.FromDateTime(fechaIni);
        var fechaFinOnly  = DateOnly.FromDateTime(fechaFin);
        var fechaCorteOnly = DateOnly.FromDateTime(fechaCorte);

        // Subquery reutilizable — no carga IDs en memoria
        var casaWorkerQ = ctx.Worker.Where(w => w.ContributorId == null).Select(w => w.Id);

        // ── CASA ──────────────────────────────────────────────────────────────
        var tareoCasa = await ctx.SsTareoDetalleCasa
            .Where(d => d.Tareo!.ProyectoId == proyectoId
                     && d.Tareo.Fecha >= fechaIniOnly && d.Tareo.Fecha < fechaFinOnly)
            .GroupBy(d => d.Tareo!.Fecha)
            .Select(g => new { Fecha = g.Key, Total = g.Sum(x => x.CantidadPersonas) })
            .ToListAsync();

        if (tareoCasa.Any(d => d.Total > 0))
        {
            var diasCasa = tareoCasa.Count(d => d.Total > 0);
            var promCasa = diasCasa > 0 ? (decimal)tareoCasa.Where(d => d.Total > 0).Sum(d => d.Total) / diasCasa : 0;

            const int metaCasaInsp = 20;

            var fechasCasaPresencia = tareoCasa.Where(d => d.Total > 0).Select(d => d.Fecha.ToDateTime(TimeOnly.MinValue)).ToList();
            var metaCasaCharlas = ContarDiasHabilesConPresenciaDateTime(fechasCasaPresencia, mes, anio);

            var racsGenCasa = await ctx.SsomaRacs
                .CountAsync(r => r.ProyectoId == proyectoId && r.EmpresaReportadaId == null
                              && r.FechaReporte >= fechaIni && r.FechaReporte < fechaCorte);
            var racsCerCasa = await ctx.SsomaRacs
                .CountAsync(r => r.ProyectoId == proyectoId && r.EmpresaReportadaId == null
                              && r.Estado == "Cerrado"
                              && r.FechaReporte >= fechaIni && r.FechaReporte < fechaCorte);

            var optCasa = await ctx.SsomaOpt
                .Where(o => o.ProyectoId == proyectoId && o.Fecha >= fechaIni && o.Fecha < fechaCorte)
                .SelectMany(o => o.Trabajadores)
                .Where(t => casaWorkerQ.Contains(t.TrabajadorId))
                .Select(t => t.OptId).Distinct().CountAsync();

            var atsCasa = await ctx.SsomaAuditoriaAts
                .Where(a => a.ProyectoId == proyectoId
                         && a.Fecha >= fechaIniOnly && a.Fecha < fechaCorteOnly
                         && casaWorkerQ.Contains(a.AuditadoWorkerId))
                .CountAsync();

            var charlasCasa = await ctx.SsCharlaAsistencias
                .Where(a => a.Charla!.ProyectoId == proyectoId
                         && a.Charla.Fecha >= fechaIni && a.Charla.Fecha < fechaCorte
                         && a.Asistio && casaWorkerQ.Contains(a.WorkerId))
                .Select(a => a.Charla!.Fecha.Date).Distinct().CountAsync();

            var inspCasa = await ctx.SsomaInspeccion
                .CountAsync(i => i.ProyectoId == proyectoId && i.EmpresaId == null
                              && i.Fecha >= fechaIni && i.Fecha < fechaCorte);

            resultado.Add(BuildMetaDto(null, "Casa", "Casa — Staff Propio", promCasa, diasCasa,
                (int)Math.Ceiling(promCasa * 0.40m), (int)Math.Ceiling(promCasa * 0.10m),
                (int)Math.Ceiling(promCasa * 0.30m), metaCasaCharlas, metaCasaInsp,
                racsGenCasa, racsCerCasa, optCasa, atsCasa, charlasCasa, inspCasa));
        }

        // ── CONTRATISTAS ──────────────────────────────────────────────────────
        var empresas = await ctx.SsTareoDetalleContratista
            .Where(d => d.Tareo!.ProyectoId == proyectoId
                     && d.Tareo.Fecha >= fechaIniOnly && d.Tareo.Fecha < fechaFinOnly)
            .Select(d => new { d.EmpresaId, Nombre = d.Empresa!.ContributorName })
            .Distinct().ToListAsync();

        foreach (var emp in empresas)
        {
            var tareoEmp = await ctx.SsTareoDetalleContratista
                .Where(d => d.Tareo!.ProyectoId == proyectoId && d.EmpresaId == emp.EmpresaId
                         && d.Tareo.Fecha >= fechaIniOnly && d.Tareo.Fecha < fechaFinOnly
                         && d.CantidadPersonas > 0)
                .Select(d => new { d.Tareo!.Fecha, d.CantidadPersonas }).ToListAsync();

            var diasEmp = tareoEmp.Count;
            var promEmp = diasEmp > 0 ? (decimal)tareoEmp.Sum(d => d.CantidadPersonas) / diasEmp : 0;

            const int metaInsp = 15;

            var fechasPresencia = tareoEmp.Select(d => d.Fecha.ToDateTime(TimeOnly.MinValue)).ToList();
            var metaCharlas = ContarDiasHabilesConPresenciaDateTime(fechasPresencia, mes, anio);

            // Subquery para workers del contratista — sin cargar IDs en memoria
            var empWorkerQ = ctx.Worker.Where(w => w.ContributorId == emp.EmpresaId).Select(w => w.Id);
            var tieneWorkers = await empWorkerQ.AnyAsync();

            var racsGen = await ctx.SsomaRacs
                .CountAsync(r => r.ProyectoId == proyectoId && r.EmpresaReportadaId == emp.EmpresaId
                              && r.FechaReporte >= fechaIni && r.FechaReporte < fechaCorte);
            var racsCer = await ctx.SsomaRacs
                .CountAsync(r => r.ProyectoId == proyectoId && r.EmpresaReportadaId == emp.EmpresaId
                              && r.Estado == "Cerrado"
                              && r.FechaReporte >= fechaIni && r.FechaReporte < fechaCorte);

            var opt = tieneWorkers
                ? await ctx.SsomaOpt
                    .Where(o => o.ProyectoId == proyectoId && o.Fecha >= fechaIni && o.Fecha < fechaCorte)
                    .SelectMany(o => o.Trabajadores)
                    .Where(t => empWorkerQ.Contains(t.TrabajadorId))
                    .Select(t => t.OptId).Distinct().CountAsync()
                : 0;

            var ats = tieneWorkers
                ? await ctx.SsomaAuditoriaAts
                    .Where(a => a.ProyectoId == proyectoId
                             && a.Fecha >= fechaIniOnly && a.Fecha < fechaCorteOnly
                             && empWorkerQ.Contains(a.AuditadoWorkerId))
                    .CountAsync()
                : 0;

            var charlas = tieneWorkers
                ? await ctx.SsCharlaAsistencias
                    .Where(a => a.Charla!.ProyectoId == proyectoId
                             && a.Charla.Fecha >= fechaIni && a.Charla.Fecha < fechaCorte
                             && a.Asistio && empWorkerQ.Contains(a.WorkerId))
                    .Select(a => a.Charla!.Fecha.Date).Distinct().CountAsync()
                : 0;

            var insp = await ctx.SsomaInspeccion
                .CountAsync(i => i.ProyectoId == proyectoId && i.EmpresaId == emp.EmpresaId
                              && i.Fecha >= fechaIni && i.Fecha < fechaCorte);

            resultado.Add(BuildMetaDto(emp.EmpresaId, "Contratista", emp.Nombre ?? $"Empresa {emp.EmpresaId}",
                promEmp, diasEmp,
                (int)Math.Ceiling(promEmp * 0.40m), (int)Math.Ceiling(promEmp * 0.10m),
                (int)Math.Ceiling(promEmp * 0.30m), metaCharlas, metaInsp,
                racsGen, racsCer, opt, ats, charlas, insp));
        }

        return resultado;
    }

    public async Task<List<IndicadorProactivoProyectoDto>> GetSeguimientoTodosProyectosAsync(int mes, int anio)
    {
        using var ctx = _factory.CreateDbContext();

        var hoy         = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var esMesActual = hoy.Month == mes && hoy.Year == anio;
        var fechaIni    = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var fechaFin    = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes), 0, 0, 0, DateTimeKind.Utc).AddDays(1);
        var fechaCorte  = esMesActual ? hoy.AddDays(1) : fechaFin;
        var iniOnly     = DateOnly.FromDateTime(fechaIni);
        var finOnly     = DateOnly.FromDateTime(fechaFin);
        var corteOnly   = DateOnly.FromDateTime(fechaCorte);

        var proyectos = await ctx.Project
            .Where(p => p.Active && p.Estado == "ACTIVO")
            .Select(p => new { p.ProjectId, p.ProjectDescription })
            .ToListAsync();
        var proyIds = proyectos.Select(p => p.ProjectId).ToList();
        if (!proyIds.Any()) return [];

        // ── 8 bulk queries para todos los proyectos ───────────────────────────

        // 1. Tareo casa: promedio trabajadores por proyecto
        var tareoCasaBulk = await ctx.SsTareoDetalleCasa
            .Where(d => proyIds.Contains(d.Tareo!.ProyectoId)
                     && d.Tareo.Fecha >= iniOnly && d.Tareo.Fecha < finOnly)
            .GroupBy(d => new { d.Tareo!.ProyectoId, d.Tareo.Fecha })
            .Select(g => new { g.Key.ProyectoId, g.Key.Fecha, Total = g.Sum(x => x.CantidadPersonas) })
            .ToListAsync();

        // 2. Tareo contratista: promedio trabajadores por proyecto/empresa
        var tareoContrBulk = await ctx.SsTareoDetalleContratista
            .Where(d => proyIds.Contains(d.Tareo!.ProyectoId)
                     && d.Tareo.Fecha >= iniOnly && d.Tareo.Fecha < finOnly
                     && d.CantidadPersonas > 0)
            .Select(d => new { d.Tareo!.ProyectoId, d.EmpresaId, Nombre = d.Empresa!.ContributorName, d.Tareo.Fecha, d.CantidadPersonas })
            .ToListAsync();

        // 3. RACs por proyecto/empresa
        var racsBulk = await ctx.SsomaRacs
            .Where(r => proyIds.Contains(r.ProyectoId)
                     && r.FechaReporte >= fechaIni && r.FechaReporte < fechaCorte)
            .Select(r => new { r.ProyectoId, r.EmpresaReportadaId, r.Estado })
            .ToListAsync();

        // 4. OPT por proyecto: contar OPTs distintos para casa y contratistas
        var optBulk = await ctx.SsomaOpt
            .Where(o => proyIds.Contains(o.ProyectoId) && o.Fecha >= fechaIni && o.Fecha < fechaCorte)
            .SelectMany(o => o.Trabajadores.Select(t => new { o.ProyectoId, t.TrabajadorId, t.OptId }))
            .ToListAsync();

        // 5. ATS por proyecto/trabajador
        var atsBulk = await ctx.SsomaAuditoriaAts
            .Where(a => a.ProyectoId != null && proyIds.Contains(a.ProyectoId!.Value)
                     && a.Fecha >= iniOnly && a.Fecha < corteOnly)
            .Select(a => new { ProyectoId = a.ProyectoId!.Value, WorkerId = a.AuditadoWorkerId })
            .ToListAsync();

        // 6. Charlas: días distintos con asistencia por proyecto/trabajador
        var charlasBulk = await ctx.SsCharlaAsistencias
            .Where(a => a.Charla!.ProyectoId != null
                     && proyIds.Contains(a.Charla.ProyectoId!.Value)
                     && a.Charla.Fecha >= fechaIni && a.Charla.Fecha < fechaCorte
                     && a.Asistio)
            .Select(a => new { ProyectoId = a.Charla!.ProyectoId!.Value, a.WorkerId, Fecha = a.Charla.Fecha.Date })
            .Distinct()
            .ToListAsync();

        // 7. Inspecciones por proyecto/empresa
        var inspBulk = await ctx.SsomaInspeccion
            .Where(i => proyIds.Contains(i.ProyectoId)
                     && i.Fecha >= fechaIni && i.Fecha < fechaCorte)
            .Select(i => new { i.ProyectoId, i.EmpresaId })
            .ToListAsync();

        // 8. Meta inspecciones: fijas por tipo de empresa
        const int metaInspCasa = 20;
        const int metaInspContratista = 15;

        // Workers por empresa (solo los que aparecen en el tareo)
        var empresaIds = tareoContrBulk.Select(t => t.EmpresaId).Distinct().ToList();
        var workersPorEmpresa = await ctx.Worker
            .Where(w => w.ContributorId != null && empresaIds.Contains(w.ContributorId!.Value))
            .Select(w => new { w.Id, ContributorId = w.ContributorId!.Value })
            .ToListAsync();
        var workersCasa = await ctx.Worker
            .Where(w => w.ContributorId == null)
            .Select(w => w.Id)
            .ToListAsync();
        var casaSet = workersCasa.ToHashSet();
        var empWorkerMap = workersPorEmpresa.GroupBy(w => w.ContributorId!).ToDictionary(g => g.Key, g => g.Select(w => w.Id).ToHashSet());

        // ── Agregar por proyecto ──────────────────────────────────────────────
        var result = new List<IndicadorProactivoProyectoDto>();

        foreach (var proy in proyectos)
        {
            var pid = proy.ProjectId;
            var empresasMes = tareoContrBulk
                .Where(t => t.ProyectoId == pid)
                .Select(t => new { t.EmpresaId, t.Nombre })
                .Distinct().ToList();

            var empresasDtos = new List<MetaEmpresaDto>();

            // Casa
            var tcCasa = tareoCasaBulk.Where(d => d.ProyectoId == pid && d.Total > 0).ToList();
            if (tcCasa.Any())
            {
                var diasC = tcCasa.Count;
                var promC = (decimal)tcCasa.Sum(d => d.Total) / diasC;
                var metaInspC = metaInspCasa;
                var fechasC = tcCasa.Select(d => d.Fecha.ToDateTime(TimeOnly.MinValue)).ToList();
                var metaCharlasC = ContarDiasHabilesConPresenciaDateTime(fechasC, mes, anio);
                var racsC = racsBulk.Where(r => r.ProyectoId == pid && r.EmpresaReportadaId == null);
                var optC = optBulk.Where(o => o.ProyectoId == pid && casaSet.Contains(o.TrabajadorId)).Select(o => o.OptId).Distinct().Count();
                var atsC = atsBulk.Count(a => a.ProyectoId == pid && casaSet.Contains(a.WorkerId));
                var charlasC = charlasBulk.Where(a => a.ProyectoId == pid && casaSet.Contains(a.WorkerId)).Select(a => a.Fecha).Distinct().Count();
                var inspC = inspBulk.Count(i => i.ProyectoId == pid && i.EmpresaId == null);
                empresasDtos.Add(BuildMetaDto(null, "Casa", "Casa — Staff Propio", promC, diasC,
                    (int)Math.Ceiling(promC * 0.40m), (int)Math.Ceiling(promC * 0.10m),
                    (int)Math.Ceiling(promC * 0.30m), metaCharlasC, metaInspC,
                    racsC.Count(), racsC.Count(r => r.Estado == "Cerrado"), optC, atsC, charlasC, inspC));
            }

            // Contratistas
            foreach (var emp in empresasMes)
            {
                var te = tareoContrBulk.Where(t => t.ProyectoId == pid && t.EmpresaId == emp.EmpresaId).ToList();
                var diasE = te.Count;
                var promE = diasE > 0 ? (decimal)te.Sum(t => t.CantidadPersonas) / diasE : 0;
                var wSet = empWorkerMap.GetValueOrDefault(emp.EmpresaId) ?? new HashSet<int>();
                var metaInspE = metaInspContratista;
                var fechasE = te.Select(t => t.Fecha.ToDateTime(TimeOnly.MinValue)).ToList();
                var metaCharlasE = ContarDiasHabilesConPresenciaDateTime(fechasE, mes, anio);
                var racsE = racsBulk.Where(r => r.ProyectoId == pid && r.EmpresaReportadaId == emp.EmpresaId);
                var optE = wSet.Any() ? optBulk.Where(o => o.ProyectoId == pid && wSet.Contains(o.TrabajadorId)).Select(o => o.OptId).Distinct().Count() : 0;
                var atsE = wSet.Any() ? atsBulk.Count(a => a.ProyectoId == pid && wSet.Contains(a.WorkerId)) : 0;
                var charlasE = wSet.Any() ? charlasBulk.Where(a => a.ProyectoId == pid && wSet.Contains(a.WorkerId)).Select(a => a.Fecha).Distinct().Count() : 0;
                var inspE = inspBulk.Count(i => i.ProyectoId == pid && i.EmpresaId == emp.EmpresaId);
                empresasDtos.Add(BuildMetaDto(emp.EmpresaId, "Contratista", emp.Nombre ?? $"Empresa {emp.EmpresaId}",
                    promE, diasE,
                    (int)Math.Ceiling(promE * 0.40m), (int)Math.Ceiling(promE * 0.10m),
                    (int)Math.Ceiling(promE * 0.30m), metaCharlasE, metaInspE,
                    racsE.Count(), racsE.Count(r => r.Estado == "Cerrado"), optE, atsE, charlasE, inspE));
            }

            // No se omite el proyecto aunque no tenga tareo todavía: debe aparecer
            // con 0% en vez de desaparecer de la lista de "proyectos activos".

            // Usar todas las empresas con tareo (no filtrar por EsActiva = 10 días)
            // El umbral de 10 días solo afecta las metas proporcionales dentro de BuildMetaDto
            var activas = empresasDtos;
            result.Add(new IndicadorProactivoProyectoDto
            {
                ProyectoId = pid,
                ProyectoNombre = proy.ProjectDescription ?? "",
                TotalEmpresasActivas = activas.Count(e => e.EsActiva),
                MetaRacsTotal = activas.Sum(e => e.MetaRacs),
                MetaOptTotal = activas.Sum(e => e.MetaOpt),
                MetaAtsTotal = activas.Sum(e => e.MetaAts),
                MetaCharlasTotal = activas.Sum(e => e.MetaCharlas),
                MetaInspeccionesTotal = activas.Sum(e => e.MetaInspecciones),
                ActualRacsTotal = activas.Sum(e => e.ActualRacs),
                ActualRacsCerradosTotal = activas.Sum(e => e.ActualRacsCerrados),
                ActualOptTotal = activas.Sum(e => e.ActualOpt),
                ActualAtsTotal = activas.Sum(e => e.ActualAts),
                ActualCharlasTotal = activas.Sum(e => e.ActualCharlas),
                ActualInspeccionesTotal = activas.Sum(e => e.ActualInspecciones),
                PctRacs = Pct(activas.Sum(e => e.ActualRacs), activas.Sum(e => e.MetaRacs)),
                PctRacsCerrados = Pct(activas.Sum(e => e.ActualRacsCerrados), activas.Sum(e => e.ActualRacs)),
                PctOpt = Pct(activas.Sum(e => e.ActualOpt), activas.Sum(e => e.MetaOpt)),
                PctAts = Pct(activas.Sum(e => e.ActualAts), activas.Sum(e => e.MetaAts)),
                PctCharlas = Pct(activas.Sum(e => e.ActualCharlas), activas.Sum(e => e.MetaCharlas)),
                PctInspecciones = Pct(activas.Sum(e => e.ActualInspecciones), activas.Sum(e => e.MetaInspecciones)),
                PctProactivoGeneral = activas.Any() ? Math.Round(activas.Average(e => e.PctProactivoGeneral), 1) : 0,
                Empresas = activas
            });
        }

        return result.OrderByDescending(p => p.PctProactivoGeneral).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUNTAJE DEL MES
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<PuntajeMesDto> GetPuntajeMesAsync(int proyectoId, int mes, int anio)
    {
        using var ctx = _factory.CreateDbContext();

        var proyecto = await ctx.Project
            .Where(p => p.ProjectId == proyectoId)
            .Select(p => p.ProjectDescription)
            .FirstOrDefaultAsync();

        var hoy = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var esMesActual = hoy.Month == mes && hoy.Year == anio;
        var fechaIni = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var fechaFin = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes), 0, 0, 0, DateTimeKind.Utc).AddDays(1);
        var fechaCorte = esMesActual ? hoy.AddDays(1) : fechaFin;

        // % proactivos
        var empresas = await GetMetasEmpresaAsync(proyectoId, mes, anio);
        var activas = empresas.Where(e => e.EsActiva).ToList();
        var pctProactivos = activas.Any() ? activas.Average(e => e.PctProactivoGeneral) : 0m;

        // % PASSO — ejecuciones del mes programadas vs ejecutadas
        var pasoIds = await ctx.SsomaPasos
            .Where(p => p.ProyectoId == proyectoId)
            .Select(p => p.Id)
            .ToListAsync();

        var actividadIds = pasoIds.Any()
            ? await ctx.SsomaPasoActividades
                .Where(a => pasoIds.Contains(a.PasoId) && a.Activo)
                .Select(a => a.Id)
                .ToListAsync()
            : new List<int>();

        var fechaIniOnly = DateOnly.FromDateTime(fechaIni);
        var fechaFinOnly = DateOnly.FromDateTime(fechaFin);

        var pasoProgramadas = actividadIds.Any()
            ? await ctx.SsomaPasoEjecuciones
                .CountAsync(pe => actividadIds.Contains(pe.ActividadId)
                               && pe.FechaProgramada >= fechaIniOnly
                               && pe.FechaProgramada < fechaFinOnly)
            : 0;

        var pasoEjecutadas = actividadIds.Any()
            ? await ctx.SsomaPasoEjecuciones
                .CountAsync(pe => actividadIds.Contains(pe.ActividadId)
                               && pe.FechaProgramada >= fechaIniOnly
                               && pe.FechaProgramada < fechaFinOnly
                               && pe.Estado == "Ejecutado")
            : 0;

        var pctPasso = pasoProgramadas > 0 ? (decimal)pasoEjecutadas / pasoProgramadas * 100 : 0;

        // % cierre accidentes (todos los abiertos hasta la fecha, incluye meses anteriores)
        var accidentesTotales = await ctx.SsomaAccidenteIncidente
            .CountAsync(a => a.ProyectoId == proyectoId && a.Fecha < fechaCorte);
        var accidentesCerrados = await ctx.SsomaAccidenteIncidente
            .CountAsync(a => a.ProyectoId == proyectoId && a.Fecha < fechaCorte && a.Estado == "Cerrado");
        var accidentesAbiertos = accidentesTotales - accidentesCerrados;
        var pctCierreAccidentes = accidentesTotales > 0
            ? (decimal)accidentesCerrados / accidentesTotales * 100 : 0;

        // % cierre hallazgos (join a través de inspección)
        var hallazgosTotales = await ctx.SsomaInspeccionHallazgo
            .Join(ctx.SsomaInspeccion,
                  h => h.InspeccionId,
                  i => i.Id,
                  (h, i) => new { h.Estado, i.ProyectoId, i.Fecha })
            .Where(x => x.ProyectoId == proyectoId && x.Fecha < fechaCorte)
            .CountAsync();

        var hallazgosCerrados = await ctx.SsomaInspeccionHallazgo
            .Join(ctx.SsomaInspeccion,
                  h => h.InspeccionId,
                  i => i.Id,
                  (h, i) => new { h.Estado, i.ProyectoId, i.Fecha })
            .Where(x => x.ProyectoId == proyectoId && x.Fecha < fechaCorte && x.Estado == "Cerrado")
            .CountAsync();

        var pctCierreHallazgos = hallazgosTotales > 0
            ? (decimal)hallazgosCerrados / hallazgosTotales * 100 : 0;

        // Cálculo del puntaje (sobre 100, con bonus hasta +10)
        var puntProactivos = Math.Min(pctProactivos, 100) * 0.40m;
        var puntPasso = Math.Min(pctPasso, 100) * 0.25m;
        var puntCierreAcc = Math.Min(pctCierreAccidentes, 100) * 0.20m;
        var puntCierreHall = Math.Min(pctCierreHallazgos, 100) * 0.15m;

        // Bonus: 0.2 pts por cada % sobre 100 en proactivos, máx +10 pts
        var bonusProactivos = pctProactivos > 100
            ? Math.Min(10m, (pctProactivos - 100) * 0.20m)
            : 0;

        var puntajeTotal = Math.Min(110m, puntProactivos + puntPasso + puntCierreAcc + puntCierreHall + bonusProactivos);

        return new PuntajeMesDto
        {
            ProyectoId = proyectoId,
            ProyectoNombre = proyecto ?? "",
            Mes = mes,
            Anio = anio,
            PctProactivos = Math.Round(pctProactivos, 1),
            PctPasso = Math.Round(pctPasso, 1),
            PctCierreAccidentes = Math.Round(pctCierreAccidentes, 1),
            PctCierreHallazgos = Math.Round(pctCierreHallazgos, 1),
            PuntajeProactivos = Math.Round(puntProactivos, 2),
            PuntajePasso = Math.Round(puntPasso, 2),
            PuntajeCierreAccidentes = Math.Round(puntCierreAcc, 2),
            PuntajeCierreHallazgos = Math.Round(puntCierreHall, 2),
            BonusProactivos = Math.Round(bonusProactivos, 2),
            PuntajeTotal = Math.Round(puntajeTotal, 1),
            AccidentesAbiertos = accidentesAbiertos,
            AccidentesTotales = accidentesTotales,
            HallazgosCerrados = hallazgosCerrados,
            HallazgosTotales = hallazgosTotales
        };
    }

    public async Task<List<PuntajeMesDto>> GetPuntajeTodosProyectosAsync(
        int mes, int anio, List<IndicadorProactivoProyectoDto>? seguimiento = null)
    {
        using var ctx = _factory.CreateDbContext();

        var hoy        = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var esMesActual = hoy.Month == mes && hoy.Year == anio;
        var fechaIni   = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var fechaFin   = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes), 0, 0, 0, DateTimeKind.Utc).AddDays(1);
        var fechaCorte = esMesActual ? hoy.AddDays(1) : fechaFin;
        var iniOnly    = DateOnly.FromDateTime(fechaIni);
        var finOnly    = DateOnly.FromDateTime(fechaFin);

        var proyectos = await ctx.Project
            .Where(p => p.Active && p.Estado == "ACTIVO")
            .Select(p => new { p.ProjectId, p.ProjectDescription })
            .ToListAsync();
        var proyIds = proyectos.Select(p => p.ProjectId).ToList();
        if (!proyIds.Any()) return [];

        // Bulk: seguimiento (proactivos) — reutiliza el resultado ya calculado si viene provisto
        // (evita repetir las 8 bulk queries cuando el controller ya lo tiene cacheado)
        var seguimientoTask = seguimiento != null
            ? Task.FromResult(seguimiento)
            : GetSeguimientoTodosProyectosAsync(mes, anio);

        // Bulk: PASSO — todo en memoria, sin GroupBy en EF Core
        var pasos = await ctx.SsomaPasos
            .Where(p => p.ProyectoId != null && proyIds.Contains(p.ProyectoId!.Value))
            .Select(p => new { p.Id, ProyectoId = p.ProyectoId!.Value })
            .ToListAsync();
        var pasoIds2 = pasos.Select(p => p.Id).ToList();

        var actividades = await ctx.SsomaPasoActividades
            .Where(a => a.Activo && pasoIds2.Contains(a.PasoId))
            .Select(a => new { a.Id, a.PasoId })
            .ToListAsync();
        // actividadId → proyectoId
        var actProyMap = actividades.ToDictionary(
            a => a.Id,
            a => pasos.FirstOrDefault(p => p.Id == a.PasoId)?.ProyectoId ?? 0);
        var actIds = actProyMap.Keys.ToList();

        var ejec = actIds.Any()
            ? await ctx.SsomaPasoEjecuciones
                .Where(pe => actIds.Contains(pe.ActividadId)
                          && pe.FechaProgramada >= iniOnly && pe.FechaProgramada < finOnly)
                .Select(pe => new { pe.ActividadId, pe.Estado })
                .ToListAsync()
            : [];

        var pasoProgFinal = ejec
            .GroupBy(pe => actProyMap.GetValueOrDefault(pe.ActividadId))
            .Select(g => new { ProyectoId = g.Key, Total = g.Count(), Ejecutadas = g.Count(x => x.Estado == "Ejecutado") })
            .ToList();

        // Bulk: accidentes por proyecto
        var accBulk = await ctx.SsomaAccidenteIncidente
            .Where(a => proyIds.Contains(a.ProyectoId) && a.Fecha < fechaCorte)
            .GroupBy(a => new { a.ProyectoId, Cerrado = a.Estado == "Cerrado" })
            .Select(g => new { g.Key.ProyectoId, g.Key.Cerrado, Count = g.Count() })
            .ToListAsync();

        // Bulk: hallazgos por proyecto
        var hallBulk = await ctx.SsomaInspeccionHallazgo
            .Join(ctx.SsomaInspeccion, h => h.InspeccionId, i => i.Id,
                  (h, i) => new { h.Estado, i.ProyectoId, i.Fecha })
            .Where(x => proyIds.Contains(x.ProyectoId) && x.Fecha < fechaCorte)
            .GroupBy(x => new { x.ProyectoId, Cerrado = x.Estado == "Cerrado" })
            .Select(g => new { g.Key.ProyectoId, g.Key.Cerrado, Count = g.Count() })
            .ToListAsync();

        var seguimientoFinal = await seguimientoTask;

        var result = new List<PuntajeMesDto>();
        foreach (var proy in proyectos)
        {
            var pid = proy.ProjectId;
            var seg = seguimientoFinal.FirstOrDefault(s => s.ProyectoId == pid);
            var pctProactivos = seg != null ? seg.PctProactivoGeneral : 0m;

            var paso = pasoProgFinal.FirstOrDefault(p => p.ProyectoId == pid);
            var pctPasso = paso is { Total: > 0 } ? (decimal)paso.Ejecutadas / paso.Total * 100 : 0m;

            var accTotal = accBulk.Where(a => a.ProyectoId == pid).Sum(a => a.Count);
            var accCerr  = accBulk.Where(a => a.ProyectoId == pid && a.Cerrado).Sum(a => a.Count);
            var pctAcc   = accTotal > 0 ? (decimal)accCerr / accTotal * 100 : 0m;

            var hallTotal = hallBulk.Where(h => h.ProyectoId == pid).Sum(h => h.Count);
            var hallCerr  = hallBulk.Where(h => h.ProyectoId == pid && h.Cerrado).Sum(h => h.Count);
            var pctHall   = hallTotal > 0 ? (decimal)hallCerr / hallTotal * 100 : 0m;

            var puntProac = Math.Min(pctProactivos, 100) * 0.40m;
            var puntPasso = Math.Min(pctPasso, 100) * 0.25m;
            var puntAcc   = Math.Min(pctAcc, 100) * 0.20m;
            var puntHall  = Math.Min(pctHall, 100) * 0.15m;
            var bonus     = pctProactivos > 100 ? Math.Min(10m, (pctProactivos - 100) * 0.20m) : 0m;
            var total     = Math.Min(110m, puntProac + puntPasso + puntAcc + puntHall + bonus);

            result.Add(new PuntajeMesDto
            {
                ProyectoId = pid,
                ProyectoNombre = proy.ProjectDescription ?? "",
                Mes = mes, Anio = anio,
                PctProactivos = Math.Round(pctProactivos, 1),
                PctPasso = Math.Round(pctPasso, 1),
                PctCierreAccidentes = Math.Round(pctAcc, 1),
                PctCierreHallazgos = Math.Round(pctHall, 1),
                PuntajeProactivos = Math.Round(puntProac, 2),
                PuntajePasso = Math.Round(puntPasso, 2),
                PuntajeCierreAccidentes = Math.Round(puntAcc, 2),
                PuntajeCierreHallazgos = Math.Round(puntHall, 2),
                BonusProactivos = Math.Round(bonus, 2),
                PuntajeTotal = Math.Round(total, 1),
                AccidentesAbiertos = accTotal - accCerr,
                AccidentesTotales = accTotal,
                HallazgosCerrados = hallCerr,
                HallazgosTotales = hallTotal
            });
        }

        return result.OrderByDescending(p => p.PuntajeTotal).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS PRIVADOS
    // ─────────────────────────────────────────────────────────────────────────

    private static MetaEmpresaDto BuildMetaDto(
        int? empresaId, string tipo, string nombre,
        decimal prom, int dias,
        int metaRacs, int metaOpt, int metaAts, int metaCharlas, int metaInsp,
        int actualRacs, int actualRacsCer, int actualOpt, int actualAts, int actualCharlas, int actualInsp)
    {
        var pctRacs = Pct(actualRacs, metaRacs);
        var pctRacsCer = Pct(actualRacsCer, actualRacs);
        var pctOpt = Pct(actualOpt, metaOpt);
        var pctAts = Pct(actualAts, metaAts);
        var pctCharlas = Pct(actualCharlas, metaCharlas);
        var pctInsp = Pct(actualInsp, metaInsp);
        var pctGeneral = (pctRacs + pctRacsCer + pctOpt + pctAts + pctCharlas + pctInsp) / 6;

        return new MetaEmpresaDto
        {
            EmpresaId = empresaId,
            EmpresaTipo = tipo,
            EmpresaNombre = nombre,
            PromedioTrabajadores = Math.Round(prom, 1),
            DiasLaborados = dias,
            EsActiva = dias >= 10,
            MetaRacs = metaRacs,
            MetaOpt = metaOpt,
            MetaAts = metaAts,
            MetaCharlas = metaCharlas,
            MetaInspecciones = metaInsp,
            ActualRacs = actualRacs,
            ActualRacsCerrados = actualRacsCer,
            ActualOpt = actualOpt,
            ActualAts = actualAts,
            ActualCharlas = actualCharlas,
            ActualInspecciones = actualInsp,
            PctRacs = Math.Round(pctRacs, 1),
            PctRacsCerrados = Math.Round(pctRacsCer, 1),
            PctOpt = Math.Round(pctOpt, 1),
            PctAts = Math.Round(pctAts, 1),
            PctCharlas = Math.Round(pctCharlas, 1),
            PctInspecciones = Math.Round(pctInsp, 1),
            PctProactivoGeneral = Math.Round(pctGeneral, 1)
        };
    }

    private static decimal Pct(int actual, int meta)
        => meta > 0 ? Math.Round((decimal)actual / meta * 100, 1) : 0m;

    private static HashSet<DateTime> FeriadosPeruDateTime(int anio)
    {
        var easter = CalcularPascua(anio);
        return new HashSet<DateTime>
        {
            new(anio, 1, 1),
            easter.AddDays(-3),     // Jueves Santo
            easter.AddDays(-2),     // Viernes Santo
            new(anio, 5, 1),
            new(anio, 6, 7),
            new(anio, 6, 29),
            new(anio, 7, 23),
            new(anio, 7, 28),
            new(anio, 7, 29),
            new(anio, 8, 30),
            new(anio, 10, 8),
            new(anio, 11, 1),
            new(anio, 12, 8),
            new(anio, 12, 9),
            new(anio, 12, 25)
        };
    }

    private static DateTime CalcularPascua(int anio)
    {
        int a = anio % 19, b = anio / 100, c = anio % 100;
        int d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int mes = (h + l - 7 * m + 114) / 31;
        int dia = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateTime(anio, mes, dia, 0, 0, 0, DateTimeKind.Utc);
    }

    private static bool EsDiaHabilDateTime(DateTime fecha, HashSet<DateTime> feriados)
        => fecha.DayOfWeek != DayOfWeek.Sunday && !feriados.Contains(fecha.Date);

    private static int ContarDiasHabilesConPresenciaDateTime(List<DateTime> fechas, int mes, int anio)
    {
        var feriados = FeriadosPeruDateTime(anio);
        return fechas
            .Where(f => f.Month == mes && f.Year == anio && EsDiaHabilDateTime(f, feriados))
            .Select(f => f.Date)
            .Distinct()
            .Count();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INDICADORES REACTIVOS IF / IG / IA
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IndicadorReactivoProyectoDto> GetIndicadoresReactivosAsync(int proyectoId, int mes, int anio)
    {
        using var ctx = _factory.CreateDbContext();
        return await CalcularReactivosAsync(ctx, proyectoId, mes, anio);
    }

    public async Task<List<IndicadorReactivoProyectoDto>> GetIndicadoresReactivosTodosAsync(int mes, int anio)
    {
        using var ctx = _factory.CreateDbContext();

        var proyectos = await ctx.Project
            .Where(p => p.Active && p.Estado == "ACTIVO")
            .Select(p => new { p.ProjectId, p.ProjectDescription })
            .ToListAsync();
        var proyIds = proyectos.Select(p => p.ProjectId).ToList();
        if (!proyIds.Any()) return [];

        // ── HHT: personas-día casa + contratista, agrupado por proyecto ─────────
        var personasCasaBulk = await ctx.SsTareoDetalleCasa
            .Where(d => proyIds.Contains(d.Tareo!.ProyectoId)
                     && d.Tareo.Fecha.Month == mes && d.Tareo.Fecha.Year == anio
                     && d.CantidadPersonas > 0)
            .GroupBy(d => d.Tareo!.ProyectoId)
            .Select(g => new { ProyectoId = g.Key, Total = g.Sum(x => (long)x.CantidadPersonas) })
            .ToListAsync();

        var personasContrBulk = await ctx.SsTareoDetalleContratista
            .Where(d => proyIds.Contains(d.Tareo!.ProyectoId)
                     && d.Tareo.Fecha.Month == mes && d.Tareo.Fecha.Year == anio
                     && d.CantidadPersonas > 0)
            .GroupBy(d => d.Tareo!.ProyectoId)
            .Select(g => new { ProyectoId = g.Key, Total = g.Sum(x => (long)x.CantidadPersonas) })
            .ToListAsync();

        // ── Accidentes Tópico por proyecto ───────────────────────────────────────
        var accTopicoBulk = await ctx.SsAccidenteTrabajo
            .Where(a => a.ProyectoId != null && proyIds.Contains(a.ProyectoId!.Value)
                     && a.FechaAccidente.Month == mes && a.FechaAccidente.Year == anio)
            .GroupBy(a => a.ProyectoId!.Value)
            .Select(g => new {
                ProyectoId = g.Key,
                Count = g.Count(),
                DiasPerdidos = g.Sum(a => (int?)(a.DiasDescansoReales ?? a.DiasDescansoEstimados) ?? 0)
            })
            .ToListAsync();

        // ── Flash Report tipo "AC" sin vincular a un accidente de Tópico ─────────
        var flashIdsYaVinculados = await ctx.SsAccidenteTrabajo
            .Where(a => a.FlashReportId != null)
            .Select(a => a.FlashReportId!.Value)
            .ToListAsync();

        var flashSinVincular = await (
            from fr in ctx.SsomaAccidenteIncidente
            join t in ctx.SsomaFlashTipo on fr.TipoId equals t.Id
            where proyIds.Contains(fr.ProyectoId)
               && fr.Fecha.Month == mes && fr.Fecha.Year == anio
               && t.Codigo == "AC"
               && !flashIdsYaVinculados.Contains(fr.Id)
            select new { fr.Id, fr.ProyectoId }
        ).ToListAsync();

        var flashIds = flashSinVincular.Select(f => f.Id).ToList();
        var flashProyMap = flashSinVincular.ToDictionary(f => f.Id, f => f.ProyectoId);

        var diasPerdidosFlashRaw = flashIds.Any()
            ? await ctx.SsomaInvestigacionRm050
                .Where(inv => flashIds.Contains(inv.AccidenteIncidenteId))
                .Select(inv => new { inv.AccidenteIncidenteId, inv.DiasPerdidos })
                .ToListAsync()
            : [];

        var flashCountBulk = flashSinVincular
            .GroupBy(f => f.ProyectoId)
            .Select(g => new { ProyectoId = g.Key, Count = g.Count() })
            .ToList();

        var diasPerdidosFlashBulk = diasPerdidosFlashRaw
            .GroupBy(x => flashProyMap.GetValueOrDefault(x.AccidenteIncidenteId))
            .Select(g => new { ProyectoId = g.Key, Total = g.Sum(x => (int?)x.DiasPerdidos ?? 0) })
            .ToList();

        // ── Agregar por proyecto ─────────────────────────────────────────────────
        var resultado = new List<IndicadorReactivoProyectoDto>();
        foreach (var proy in proyectos)
        {
            var pid = proy.ProjectId;
            var hht = ((personasCasaBulk.FirstOrDefault(x => x.ProyectoId == pid)?.Total ?? 0)
                     + (personasContrBulk.FirstOrDefault(x => x.ProyectoId == pid)?.Total ?? 0)) * 8L;

            var accTopico = accTopicoBulk.FirstOrDefault(x => x.ProyectoId == pid);
            var totalAccidentes = (accTopico?.Count ?? 0)
                + (flashCountBulk.FirstOrDefault(x => x.ProyectoId == pid)?.Count ?? 0);
            var totalDiasPerdidos = (accTopico?.DiasPerdidos ?? 0)
                + (diasPerdidosFlashBulk.FirstOrDefault(x => x.ProyectoId == pid)?.Total ?? 0);

            decimal if_ = 0, ig = 0, ia = 0;
            if (hht > 0)
            {
                if_ = Math.Round((decimal)totalAccidentes * 1_000_000m / hht, 2);
                ig  = Math.Round((decimal)totalDiasPerdidos * 1_000_000m / hht, 2);
                ia  = Math.Round(if_ * ig / 1000m, 2);
            }

            resultado.Add(new IndicadorReactivoProyectoDto
            {
                ProyectoId = pid,
                ProyectoNombre = proy.ProjectDescription ?? "",
                Mes = mes,
                Anio = anio,
                HorasHombreTrabajadas = hht,
                TotalAccidentes = totalAccidentes,
                TotalDiasPerdidos = totalDiasPerdidos,
                IndiceFrecuencia = if_,
                IndiceGravedad = ig,
                IndiceAccidentabilidad = ia,
            });
        }
        return resultado;
    }

    private static async Task<IndicadorReactivoProyectoDto> CalcularReactivosAsync(
        AppDbContext ctx, int proyectoId, int mes, int anio)
    {
        // ── 1. Nombre del proyecto ────────────────────────────────────────────
        var proyectoNombre = await ctx.Project
            .Where(p => p.ProjectId == proyectoId)
            .Select(p => p.ProjectDescription ?? "")
            .FirstOrDefaultAsync() ?? "";

        // ── 2. Horas Hombre Trabajadas (HHT) ─────────────────────────────────
        // Personas-día registradas en tareo × 8 h/día
        var personasDiaCasa = await ctx.SsTareoDetalleCasa
            .Where(d => d.Tareo!.ProyectoId == proyectoId
                     && d.Tareo.Fecha.Month == mes
                     && d.Tareo.Fecha.Year == anio
                     && d.CantidadPersonas > 0)
            .SumAsync(d => (long)d.CantidadPersonas);

        var personasDiaContr = await ctx.SsTareoDetalleContratista
            .Where(d => d.Tareo!.ProyectoId == proyectoId
                     && d.Tareo.Fecha.Month == mes
                     && d.Tareo.Fecha.Year == anio
                     && d.CantidadPersonas > 0)
            .SumAsync(d => (long)d.CantidadPersonas);

        var hht = (personasDiaCasa + personasDiaContr) * 8L;

        // ── 3. Accidentes con lesión en el período ────────────────────────────
        // Solo tipos que generan AT: código "AC" (Accidente).
        // Se cuentan los de SsAccidenteTrabajo (originados en Tópico) más los
        // Flash Report tipo "AC" que NO estén ya vinculados a uno de esos (evita duplicar).
        var totalAccidentesTopico = await ctx.SsAccidenteTrabajo
            .CountAsync(a => a.ProyectoId == proyectoId
                          && a.FechaAccidente.Month == mes
                          && a.FechaAccidente.Year == anio);

        var flashIdsYaVinculados = await ctx.SsAccidenteTrabajo
            .Where(a => a.FlashReportId != null)
            .Select(a => a.FlashReportId!.Value)
            .ToListAsync();

        var flashAccidentesSinVincular = await (
            from fr in ctx.SsomaAccidenteIncidente
            join t in ctx.SsomaFlashTipo on fr.TipoId equals t.Id
            where fr.ProyectoId == proyectoId
               && fr.Fecha.Month == mes && fr.Fecha.Year == anio
               && t.Codigo == "AC"
               && !flashIdsYaVinculados.Contains(fr.Id)
            select fr.Id
        ).ToListAsync();

        var totalAccidentes = totalAccidentesTopico + flashAccidentesSinVincular.Count;

        // ── 4. Días perdidos ─────────────────────────────────────────────────
        var diasPerdidosTopico = await ctx.SsAccidenteTrabajo
            .Where(a => a.ProyectoId == proyectoId
                     && a.FechaAccidente.Month == mes
                     && a.FechaAccidente.Year == anio)
            .SumAsync(a => (int?)(a.DiasDescansoReales ?? a.DiasDescansoEstimados) ?? 0);

        var diasPerdidosFlash = flashAccidentesSinVincular.Count == 0
            ? 0
            : await ctx.SsomaInvestigacionRm050
                .Where(inv => flashAccidentesSinVincular.Contains(inv.AccidenteIncidenteId))
                .SumAsync(inv => (int?)inv.DiasPerdidos ?? 0);

        var totalDiasPerdidos = diasPerdidosTopico + diasPerdidosFlash;

        // ── 5. Cálculo de índices ─────────────────────────────────────────────
        decimal if_ = 0, ig = 0, ia = 0;
        if (hht > 0)
        {
            if_ = Math.Round((decimal)totalAccidentes * 1_000_000m / hht, 2);
            ig  = Math.Round((decimal)totalDiasPerdidos * 1_000_000m / hht, 2);
            ia  = Math.Round(if_ * ig / 1000m, 2);
        }

        return new IndicadorReactivoProyectoDto
        {
            ProyectoId = proyectoId,
            ProyectoNombre = proyectoNombre,
            Mes = mes,
            Anio = anio,
            HorasHombreTrabajadas = hht,
            TotalAccidentes = totalAccidentes,
            TotalDiasPerdidos = totalDiasPerdidos,
            IndiceFrecuencia = if_,
            IndiceGravedad = ig,
            IndiceAccidentabilidad = ia,
        };
    }
}
