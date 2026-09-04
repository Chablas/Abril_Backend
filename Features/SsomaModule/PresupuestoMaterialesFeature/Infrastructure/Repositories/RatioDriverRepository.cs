using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.Shared;
using Abril_Backend.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

public class RatioDriverRepository : IRatioDriverRepository
{
    private readonly IConfiguration _config;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public RatioDriverRepository(IConfiguration config, IDbContextFactory<AppDbContext> factory)
    {
        _config = config;
        _factory = factory;
    }

    private NpgsqlConnection Conn() => new(_config["Database:PostgreSQL"]!);

    public async Task<List<ProyectoAreaRow>> ObtenerProyectosConAreaAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Project
            .Where(p => p.AreaTechadaM2 != null && p.AreaTechadaM2 > 0)
            .Select(p => new ProyectoAreaRow
            {
                ProjectId = p.ProjectId,
                AreaTechada = p.AreaTechadaM2!.Value,
                // Sin default a "Activo": un proyecto viejo sin este campo cargado (null) se
                // trata como cerrado (ver esManualConfiable en RatioDriverService) — solo un
                // "Activo" explícito se considera obra en curso/parcial.
                CicloVida = p.Activo,
                HhTotalCasa = p.HhTotalCasa,
                CantTrabajadoresCasa = p.CantTrabajadoresCasa,
                HhFuente = p.HhFuente,
            })
            .ToListAsync();
    }

    /// <summary>
    /// HH real por proyecto. Prioridad por proyecto (no se mezclan las dos fuentes para el mismo
    /// proyecto, para no arriesgar doble conteo entre semanas de planilla y días de Tareo que se
    /// solapen):
    ///   1. Excel de planilla/Tareo semanal (HhCargaService) si el proyecto tiene alguna carga
    ///      activa — trae TODAS las partidas de control (no solo SSOMA), excluyendo únicamente
    ///      personal "EMPLEADO" (staff, no obrero); es la fuente más completa cuando alguien se
    ///      tomó el trabajo de subirla.
    ///   2. Si no, Tareo de Control de Acceso (personas del día x horas de jornada de ese día,
    ///      sumado en todo el rango registrado) — misma fórmula que el dashboard de Horas Hombre.
    ///      OJO: si el Tareo no arranca junto con el proyecto (se empezó a registrar después de
    ///      que la obra ya había avanzado), este total queda por debajo del HH real de toda la
    ///      obra — es la limitación que la carga por Excel busca complementar.
    /// </summary>
    public async Task<List<ProyectoHhRealRow>> ObtenerHhRealPorProyectoAsync(List<int> projectIds)
    {
        if (projectIds.Count == 0) return [];
        using var ctx = _factory.CreateDbContext();

        // Practicante no cuenta para el driver de dotación (ni HH ni trabajadores distintos):
        // no es mano de obra que impulse el ratio de m²/persona, aunque sí se haya importado
        // igual que EMPLEADO (el import solo descarta EMPLEADO, para otros usos de la carga).
        var hhExcel = await ctx.SsHhCargaLinea
            .Where(l => l.Activo && projectIds.Contains(l.ProjectId)
                && (l.Ocupacion == null || !l.Ocupacion.ToUpper().Contains("PRACTICANTE")))
            .Select(l => new { l.ProjectId, l.Anio, l.SemanaNum, l.HorasLaboradas })
            .ToListAsync();

        var resultado = hhExcel.GroupBy(l => l.ProjectId).Select(g => new ProyectoHhRealRow
        {
            ProjectId = g.Key,
            HhTotal = g.Sum(l => l.HorasLaboradas),
            // No hay fecha diaria en el Excel de planilla, solo semana — se aproxima a días
            // calendario para que sea comparable en escala con DiasRegistrados del Tareo.
            DiasRegistrados = g.Select(l => (l.Anio, l.SemanaNum)).Distinct().Count() * 7,
        }).ToList();

        var proyectosConExcel = resultado.Select(r => r.ProjectId).ToHashSet();
        var projectIdsSoloTareo = projectIds.Where(id => !proyectosConExcel.Contains(id)).ToList();
        if (projectIdsSoloTareo.Count == 0) return resultado;

        var tareos = await ctx.SsTareo
            .Where(t => projectIdsSoloTareo.Contains(t.ProyectoId))
            .Select(t => new { t.Id, t.ProyectoId, t.Fecha })
            .ToListAsync();

        if (tareos.Count == 0) return resultado;

        var tareoIds = tareos.Select(t => t.Id).ToList();

        var casaPorTareo = await ctx.SsTareoDetalleCasa
            .Where(d => tareoIds.Contains(d.TareoId))
            .GroupBy(d => d.TareoId)
            .Select(g => new { TareoId = g.Key, Cantidad = g.Sum(d => d.CantidadPersonas) })
            .ToDictionaryAsync(g => g.TareoId, g => g.Cantidad);

        var contratistaPorTareo = await ctx.SsTareoDetalleContratista
            .Where(d => tareoIds.Contains(d.TareoId))
            .GroupBy(d => d.TareoId)
            .Select(g => new { TareoId = g.Key, Cantidad = g.Sum(d => d.CantidadPersonas) })
            .ToDictionaryAsync(g => g.TareoId, g => g.Cantidad);

        resultado.AddRange(tareos.GroupBy(t => t.ProyectoId).Select(g =>
        {
            decimal hhTotal = 0;
            foreach (var t in g)
            {
                var casa = casaPorTareo.GetValueOrDefault(t.Id);
                var contratista = contratistaPorTareo.GetValueOrDefault(t.Id);
                hhTotal += (casa + contratista) * HorarioLaboralCalculator.HorasPorDia(t.Fecha);
            }
            return new ProyectoHhRealRow
            {
                ProjectId = g.Key,
                HhTotal = hhTotal,
                DiasRegistrados = g.Select(t => t.Fecha).Distinct().Count(),
            };
        }));

        return resultado;
    }

    /// <summary>
    /// N Trabajadores real, misma prioridad por proyecto que ObtenerHhRealPorProyectoAsync:
    ///   1. Excel de planilla (HhCargaService) si el proyecto tiene alguna carga activa — cada
    ///      línea trae el nombre del trabajador, así que se cuentan nombres DISTINTOS (mismo
    ///      Excel que ya se sube para HH, sin necesidad de subir nada aparte).
    ///   2. Si no, cantidad de trabajadores DISTINTOS que alguna vez tuvieron una vinculación
    ///      (worker_vinculaciones) a ese proyecto — "los que alguna vez pisaron la obra". Esta
    ///      fuente puede estar casi vacía para proyectos viejos que nunca se migraron ahí (ver
    ///      caso SAUCO) — subir el Excel resuelve ese hueco.
    /// </summary>
    public async Task<List<ProyectoTrabajadoresRealRow>> ObtenerTrabajadoresRealPorProyectoAsync(List<int> projectIds)
    {
        if (projectIds.Count == 0) return [];
        using var ctx = _factory.CreateDbContext();

        var trabajadoresExcel = await ctx.SsHhCargaLinea
            .Where(l => l.Activo && projectIds.Contains(l.ProjectId)
                && (l.Ocupacion == null || !l.Ocupacion.ToUpper().Contains("PRACTICANTE")))
            .Select(l => new { l.ProjectId, l.Trabajador })
            .ToListAsync();

        var resultado = trabajadoresExcel.GroupBy(l => l.ProjectId).Select(g => new ProyectoTrabajadoresRealRow
        {
            ProjectId = g.Key,
            // Normaliza el nombre (espacios/mayúsculas) para no inflar el conteo si el mismo
            // trabajador aparece escrito distinto entre semanas.
            TotalTrabajadoresDistintos = g.Select(l => l.Trabajador.Trim().ToUpperInvariant()).Distinct().Count(),
        }).ToList();

        var proyectosConExcel = resultado.Select(r => r.ProjectId).ToHashSet();
        var projectIdsSoloVinculacion = projectIds.Where(id => !proyectosConExcel.Contains(id)).ToList();
        if (projectIdsSoloVinculacion.Count == 0) return resultado;

        var vinculaciones = await ctx.WorkerVinculacion
            .Where(v => v.ProyectoId != null && projectIdsSoloVinculacion.Contains(v.ProyectoId.Value))
            .GroupBy(v => v.ProyectoId!.Value)
            .Select(g => new ProyectoTrabajadoresRealRow
            {
                ProjectId = g.Key,
                TotalTrabajadoresDistintos = g.Select(v => v.WorkerId).Distinct().Count(),
            })
            .ToListAsync();

        resultado.AddRange(vinculaciones);
        return resultado;
    }

    public async Task UpsertRatiosBulkAsync(List<RatioDriverUpsertItem> items)
    {
        if (items.Count == 0) return;
        using var conn = Conn();
        const string sql = """
            INSERT INTO ss_ratio_proyecto_driver
              (tipo_driver, project_id, area_techada, cantidad, ratio, cantidad_calculado, cantidad_manual, cantidad_proyectado, fuente_cantidad, dias_registrados, es_outlier, incluido_manual)
            VALUES
              (@TipoDriver, @ProjectId, @AreaTechada, @Cantidad, @Ratio, @CantidadCalculado, @CantidadManual, @CantidadProyectado, @FuenteCantidadDefault, @DiasRegistrados, false, @IncluidoManualDefault)
            ON CONFLICT (tipo_driver, project_id)
            DO UPDATE SET
              area_techada        = EXCLUDED.area_techada,
              cantidad_calculado  = EXCLUDED.cantidad_calculado,
              cantidad_manual     = EXCLUDED.cantidad_manual,
              cantidad_proyectado = EXCLUDED.cantidad_proyectado,
              dias_registrados    = EXCLUDED.dias_registrados,
              es_outlier          = false,
              calculado_en        = now()
            """;
        // cantidad / ratio / fuente_cantidad / incluido_manual solo se aplican en el INSERT
        // (fila nueva): en el UPDATE no se tocan, porque son decision del responsable (el
        // selector "Usar" y el checkbox Incluir en la pantalla de Ratios) y no deben pisarse
        // cada vez que se recalculan los valores crudos.
        await conn.ExecuteAsync(sql, items);
    }

    public async Task<List<RatioDriverOutlierRow>> ObtenerTodosParaOutlierAsync()
    {
        using var conn = Conn();
        const string sql = "SELECT id AS Id, tipo_driver AS TipoDriver, ratio AS Ratio FROM ss_ratio_proyecto_driver";
        var result = await conn.QueryAsync<RatioDriverOutlierRow>(sql);
        return result.ToList();
    }

    public async Task ActualizarOutliersBulkAsync(List<RatioDriverOutlierUpdate> updates)
    {
        if (updates.Count == 0) return;
        using var conn = Conn();
        const string sql = "UPDATE ss_ratio_proyecto_driver SET es_outlier = @EsOutlier WHERE id = @Id";
        await conn.ExecuteAsync(sql, updates);
    }

    public async Task<List<RatioDriverProyectoDto>> ObtenerPorTipoAsync(string tipoDriver)
    {
        using var conn = Conn();
        const string sql = """
            SELECT d.project_id AS ProjectId, p.project_description AS ProjectDescription,
                   COALESCE(p.activo, 'Activo') AS CicloVida, d.dias_registrados AS DiasRegistrados,
                   d.area_techada AS AreaTechada, d.cantidad AS Cantidad, d.ratio AS Ratio,
                   d.cantidad_calculado AS CantidadCalculado, d.cantidad_manual AS CantidadManual,
                   d.cantidad_proyectado AS CantidadProyectado, d.fuente_cantidad AS FuenteCantidad,
                   p.hh_fuente AS HhFuente,
                   d.es_outlier AS EsOutlier, d.incluido_manual AS IncluidoManual
            FROM ss_ratio_proyecto_driver d
            JOIN project p ON p.project_id = d.project_id
            WHERE d.tipo_driver = @tipoDriver
            ORDER BY d.ratio
            """;
        var result = await conn.QueryAsync<RatioDriverProyectoDto>(sql, new { tipoDriver });
        return result.ToList();
    }

    public async Task ActualizarIncluidoManualAsync(string tipoDriver, int projectId, bool incluir)
    {
        using var conn = Conn();
        const string sql = """
            UPDATE ss_ratio_proyecto_driver
            SET incluido_manual = @incluir
            WHERE tipo_driver = @tipoDriver AND project_id = @projectId
            """;
        await conn.ExecuteAsync(sql, new { tipoDriver, projectId, incluir });
    }

    public async Task ActualizarFuenteCantidadAsync(string tipoDriver, int projectId, string? fuente)
    {
        using var conn = Conn();
        const string sqlLeer = """
            SELECT area_techada AS AreaTechada, cantidad_calculado AS CantidadCalculado,
                   cantidad_manual AS CantidadManual, cantidad_proyectado AS CantidadProyectado
            FROM ss_ratio_proyecto_driver
            WHERE tipo_driver = @tipoDriver AND project_id = @projectId
            """;
        var fila = await conn.QuerySingleOrDefaultAsync<FuenteCantidadRow>(sqlLeer, new { tipoDriver, projectId });
        if (fila is null) return;

        decimal cantidad = fuente switch
        {
            "CALCULADO" => fila.CantidadCalculado,
            "MANUAL" => fila.CantidadManual ?? 0,
            "PROYECTADO" => fila.CantidadProyectado ?? 0,
            _ => 0,
        };
        var ratio = fila.AreaTechada > 0 ? cantidad / fila.AreaTechada : 0;
        // Si el responsable elige "ninguno" (fuente null), no tiene sentido dejarlo marcado
        // como incluido en la mediana — no hay ningun valor que aportar.
        const string sqlActualizar = """
            UPDATE ss_ratio_proyecto_driver
            SET fuente_cantidad = @fuente, cantidad = @cantidad, ratio = @ratio,
                incluido_manual = CASE WHEN @fuente IS NULL THEN false ELSE incluido_manual END
            WHERE tipo_driver = @tipoDriver AND project_id = @projectId
            """;
        await conn.ExecuteAsync(sqlActualizar, new { tipoDriver, projectId, fuente, cantidad, ratio });
    }

    private class FuenteCantidadRow
    {
        public decimal AreaTechada { get; set; }
        public decimal CantidadCalculado { get; set; }
        public decimal? CantidadManual { get; set; }
        public decimal? CantidadProyectado { get; set; }
    }
}
