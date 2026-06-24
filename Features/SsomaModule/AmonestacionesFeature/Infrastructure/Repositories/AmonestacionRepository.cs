using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Repositories;

public class AmonestacionRepository : IAmonestacionRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IConfiguration _config;

    public AmonestacionRepository(IDbContextFactory<AppDbContext> factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    private NpgsqlConnection Conn() => new(_config["Database:PostgreSQL"]);

    public async Task<AmonestacionInitDto> GetInitAsync()
    {
        const string sql = """
            SELECT id, nombre, nivel_gravedad AS nivelGravedad, genera_suspension AS generaSuspension
            FROM ssoma_amonestacion_tipo_sanciones WHERE state = true ORDER BY id;

            SELECT id, nombre FROM ssoma_amonestacion_infraccion_tipos WHERE state = true ORDER BY nombre;

            SELECT id, nombre, monto_fijo AS montoFijo, factor_uit AS factorUit
            FROM ssoma_rac_infraccion WHERE activo = true ORDER BY nombre;

            SELECT project_id AS id, project_description AS nombre
            FROM project WHERE active = true ORDER BY project_description;

            SELECT partida_id AS id, partida_description AS nombre
            FROM partida WHERE active = true ORDER BY partida_description;

            SELECT COALESCE(valor, 0) FROM ssoma_uit_anio
            WHERE anio = EXTRACT(YEAR FROM CURRENT_DATE)::int AND activo = true
            LIMIT 1;
            """;

        await using var conn = Conn();
        await conn.OpenAsync();
        await using var multi = await conn.QueryMultipleAsync(sql);

        var tiposSancion = (await multi.ReadAsync<TipoSancionDto>()).ToList();
        var infraccionesTipo = (await multi.ReadAsync<InfraccionTipoDto>()).ToList();
        var racInfracciones = (await multi.ReadAsync<AmonCatalogoDto>()).ToList();
        var proyectos = (await multi.ReadAsync<AmonCatalogoDto>()).ToList();
        var partidas = (await multi.ReadAsync<AmonPartidaDto>()).ToList();
        var uitActual = await multi.ReadFirstOrDefaultAsync<decimal?>() ?? 0m;

        return new AmonestacionInitDto
        {
            TiposSancion = tiposSancion,
            InfraccionesTipo = infraccionesTipo,
            RacInfracciones = racInfracciones,
            Proyectos = proyectos,
            Partidas = partidas,
            UitActual = uitActual
        };
    }

    public async Task<string> GenerarCodigoAsync(int proyectoId)
    {
        const string sql = """
            SELECT COALESCE(abbreviation, 'XXX') FROM project WHERE project_id = @pid;
            SELECT COUNT(*) FROM ssoma_amonestaciones WHERE proyecto_id = @pid;
            """;

        await using var conn = Conn();
        await conn.OpenAsync();
        await using var multi = await conn.QueryMultipleAsync(sql, new { pid = proyectoId });
        var abbrev = ((await multi.ReadFirstAsync<string>()) ?? "XXX").ToUpperInvariant();
        var count = await multi.ReadFirstAsync<long>();
        var correlativo = (count + 1).ToString("D3");
        return $"{abbrev}-AMON-{correlativo}";
    }

    public async Task<int> CrearAsync(AmonestacionDetalleDto detalle, List<(string Base64, string NombreArchivo)> fotos)
    {
        using var ctx = _factory.CreateDbContext();

        var entity = new SsomaAmonestacion
        {
            Codigo               = detalle.Codigo,
            ProyectoId           = detalle.ProyectoId,
            Fecha                = DateTime.SpecifyKind(detalle.Fecha, DateTimeKind.Utc),
            WorkerId             = detalle.WorkerId,
            PartidaId            = detalle.PartidaId,
            TipoSancionId        = detalle.TipoSancionId,
            InfraccionTipoId     = detalle.InfraccionTipoId,
            Descripcion          = detalle.Descripcion,
            AplicaPenalizacion   = detalle.AplicaPenalizacion,
            SancionInfraccionId  = detalle.SancionInfraccionId,
            MontoCalculado       = detalle.MontoCalculado,
            UitReferencia        = detalle.AplicaPenalizacion ? detalle.MontoCalculado : 0m,
            PuntosInfraccion     = detalle.PuntosInfraccion,
            DiasSuspension       = detalle.DiasSuspension,
            FechaInicioSuspension = detalle.FechaInicioSuspension,
            FechaFinSuspension   = detalle.FechaFinSuspension,
            PersonaReportaId     = detalle.PersonaReportaId,
            CreatedBy            = null,
            State                = true
        };

        ctx.SsomaAmonestaciones.Add(entity);
        await ctx.SaveChangesAsync();

        // Guardar fotos como URLs vacías por ahora (se pueden subir a SharePoint después)
        for (int i = 0; i < fotos.Count; i++)
        {
            ctx.SsomaAmonestacionFotos.Add(new SsomaAmonestacionFoto
            {
                AmonestacionId = entity.Id,
                Url            = "",
                NombreArchivo  = fotos[i].NombreArchivo,
                Orden          = i + 1
            });
        }

        await ctx.SaveChangesAsync();
        return entity.Id;
    }

    public async Task<(List<AmonestacionListItemDto> Items, int Total)> GetListAsync(AmonestacionListQuery q)
    {
        var where = new List<string> { "a.state = true" };
        var p = new DynamicParameters();
        p.Add("offset", (q.Page - 1) * q.PageSize);
        p.Add("limit", q.PageSize <= 0 ? 20 : Math.Min(q.PageSize, 100));

        if (q.ProyectoId.HasValue)   { where.Add("a.proyecto_id = @pid");   p.Add("pid", q.ProyectoId); }
        if (q.WorkerId.HasValue)     { where.Add("a.worker_id = @wid");     p.Add("wid", q.WorkerId); }
        if (q.TipoSancionId.HasValue){ where.Add("a.tipo_sancion_id = @tsid"); p.Add("tsid", q.TipoSancionId); }
        if (q.FechaDesde.HasValue)   { where.Add("a.fecha >= @fd"); p.Add("fd", q.FechaDesde.Value.Date); }
        if (q.FechaHasta.HasValue)   { where.Add("a.fecha <= @fh"); p.Add("fh", q.FechaHasta.Value.Date); }

        var whereClause = "WHERE " + string.Join(" AND ", where);

        var sql = $"""
            SELECT
                a.id, a.codigo,
                pr.project_description AS proyectoNombre,
                a.fecha,
                pe.full_name AS workerNombre,
                pe.document_identity_code AS workerDni,
                c.contributor_name AS empresaNombre,
                ts.nombre AS tipoSancionNombre,
                ts.nivel_gravedad AS nivelGravedad,
                a.puntos_infraccion AS puntosInfraccion,
                a.aplica_penalizacion AS aplicaPenalizacion,
                a.monto_calculado AS montoCalculado
            FROM ssoma_amonestaciones a
            JOIN project pr ON pr.project_id = a.proyecto_id
            JOIN workers w ON w.id = a.worker_id
            JOIN person pe ON pe.person_id = w.person_id
            LEFT JOIN worker_vinculaciones wv ON wv.worker_id = w.id
                AND (wv.fecha_fin IS NULL OR wv.fecha_fin >= CURRENT_DATE)
            LEFT JOIN contributor c ON c.contributor_id = wv.empresa_id
            JOIN ssoma_amonestacion_tipo_sanciones ts ON ts.id = a.tipo_sancion_id
            {whereClause}
            ORDER BY a.fecha DESC, a.id DESC
            LIMIT @limit OFFSET @offset;

            SELECT COUNT(*) FROM ssoma_amonestaciones a {whereClause};
            """;

        await using var conn = Conn();
        await conn.OpenAsync();
        await using var multi = await conn.QueryMultipleAsync(sql, p);
        var items = (await multi.ReadAsync<AmonestacionListItemDto>()).ToList();
        var total = await multi.ReadSingleAsync<int>();
        return (items, total);
    }

    public async Task<AmonestacionDetalleDto?> GetDetalleAsync(int id)
    {
        const string sql = """
            SELECT
                a.id, a.codigo,
                a.proyecto_id AS proyectoId,
                pr.project_description AS proyectoNombre,
                a.fecha,
                a.worker_id AS workerId,
                pe.full_name AS workerNombre,
                pe.document_identity_code AS workerDni,
                w.categoria AS workerCategoria,
                w.ocupacion AS workerCargo,
                CASE WHEN w.fecha_nacimiento IS NOT NULL
                     THEN DATE_PART('year', AGE(w.fecha_nacimiento))::int ELSE NULL END AS workerEdad,
                COALESCE(c.contributor_name, '') AS empresaNombre,
                COALESCE(c.es_abril, false) AS esEmpresaAbril,
                ct.logo_file_url AS empresaLogoUrl,
                a.partida_id AS partidaId,
                pt.partida_description AS partidaNombre,
                a.tipo_sancion_id AS tipoSancionId,
                ts.nombre AS tipoSancionNombre,
                ts.nivel_gravedad AS nivelGravedad,
                ts.genera_suspension AS generaSuspension,
                a.infraccion_tipo_id AS infraccionTipoId,
                it.nombre AS infraccionTipoNombre,
                a.descripcion,
                a.aplica_penalizacion AS aplicaPenalizacion,
                a.sancion_infraccion_id AS sancionInfraccionId,
                ri.nombre AS sancionInfraccionNombre,
                a.monto_calculado AS montoCalculado,
                a.puntos_infraccion AS puntosInfraccion,
                a.dias_suspension AS diasSuspension,
                a.fecha_inicio_suspension AS fechaInicioSuspension,
                a.fecha_fin_suspension AS fechaFinSuspension,
                up.full_name AS personaReportaNombre,
                a.pdf_url AS pdfUrl,
                a.created_at AS createdAt,
                COALESCE((SELECT SUM(a2.puntos_infraccion) FROM ssoma_amonestaciones a2
                           WHERE a2.worker_id = a.worker_id AND a2.state = true), 0)::int AS puntosAcumulados
            FROM ssoma_amonestaciones a
            JOIN project pr ON pr.project_id = a.proyecto_id
            JOIN workers w ON w.id = a.worker_id
            JOIN person pe ON pe.person_id = w.person_id
            LEFT JOIN worker_vinculaciones wv ON wv.worker_id = w.id
                AND (wv.fecha_fin IS NULL OR wv.fecha_fin >= CURRENT_DATE)
            LEFT JOIN contributor c ON c.contributor_id = wv.empresa_id
            LEFT JOIN contractor ct ON ct.contractor_id = c.contributor_id
            LEFT JOIN partida pt ON pt.partida_id = a.partida_id
            JOIN ssoma_amonestacion_tipo_sanciones ts ON ts.id = a.tipo_sancion_id
            JOIN ssoma_amonestacion_infraccion_tipos it ON it.id = a.infraccion_tipo_id
            LEFT JOIN ssoma_rac_infraccion ri ON ri.id = a.sancion_infraccion_id
            LEFT JOIN app_user au ON au.user_id = a.persona_reporta_id
            LEFT JOIN person up ON LOWER(up.email) = LOWER(au.email)
            WHERE a.id = @id AND a.state = true;

            SELECT id, url, nombre_archivo AS nombreArchivo, orden
            FROM ssoma_amonestacion_fotos WHERE amonestacion_id = @id ORDER BY orden;
            """;

        await using var conn = Conn();
        await conn.OpenAsync();
        await using var multi = await conn.QueryMultipleAsync(sql, new { id });

        var detalle = await multi.ReadFirstOrDefaultAsync<AmonestacionDetalleDto>();
        if (detalle is null) return null;

        detalle.Fotos = (await multi.ReadAsync<AmonFotoDto>()).ToList();
        detalle.Inhabilitado = detalle.PuntosAcumulados >= 10;

        return detalle;
    }

    public async Task<AmonestacionDashboardDto> GetDashboardAsync()
    {
        const string sql = """
            SELECT
                COUNT(*)::int AS totalAmonestaciones,
                COUNT(CASE WHEN puntos_acc >= 5 AND puntos_acc < 10 THEN 1 END)::int AS trabajadoresConMas5Puntos,
                COUNT(CASE WHEN puntos_acc >= 10 THEN 1 END)::int AS trabajadoresInhabilitados,
                COUNT(CASE WHEN EXTRACT(MONTH FROM fecha_mes) = EXTRACT(MONTH FROM CURRENT_DATE)
                            AND EXTRACT(YEAR FROM fecha_mes) = EXTRACT(YEAR FROM CURRENT_DATE) THEN 1 END)::int AS amonestacionesMesActual
            FROM (
                SELECT a.id, a.fecha AS fecha_mes,
                    SUM(a2.puntos_infraccion) OVER (PARTITION BY a.worker_id) AS puntos_acc
                FROM ssoma_amonestaciones a
                JOIN ssoma_amonestaciones a2 ON a2.worker_id = a.worker_id AND a2.state = true
                WHERE a.state = true
            ) sub;

            SELECT ts.nombre AS tipoNombre, COUNT(*)::int AS total
            FROM ssoma_amonestaciones a
            JOIN ssoma_amonestacion_tipo_sanciones ts ON ts.id = a.tipo_sancion_id
            WHERE a.state = true
            GROUP BY ts.nombre ORDER BY total DESC LIMIT 10;

            SELECT pr.project_description AS proyectoNombre, COUNT(*)::int AS total
            FROM ssoma_amonestaciones a
            JOIN project pr ON pr.project_id = a.proyecto_id
            WHERE a.state = true
            GROUP BY pr.project_description ORDER BY total DESC LIMIT 10;

            SELECT EXTRACT(YEAR FROM fecha)::int AS anio, EXTRACT(MONTH FROM fecha)::int AS mes, COUNT(*)::int AS total
            FROM ssoma_amonestaciones WHERE state = true
            GROUP BY anio, mes ORDER BY anio, mes;
            """;

        await using var conn = Conn();
        await conn.OpenAsync();
        await using var multi = await conn.QueryMultipleAsync(sql);

        var resumen = await multi.ReadFirstAsync<AmonestacionDashboardDto>();
        resumen.PorTipoSancion = (await multi.ReadAsync<AmonPorTipoDto>()).ToList();
        resumen.PorProyecto    = (await multi.ReadAsync<AmonPorProyectoDto>()).ToList();
        resumen.Tendencia      = (await multi.ReadAsync<AmonTendenciaDto>()).ToList();

        return resumen;
    }

    public async Task<WorkerPuntajeDto?> GetPuntajeWorkerAsync(int workerId)
    {
        const string sqlWorker = """
            SELECT w.id AS workerId,
                   pe.full_name AS nombre,
                   pe.document_identity_code AS dni,
                   COALESCE(c.contributor_name, '') AS empresaNombre,
                   COALESCE(SUM(a.puntos_infraccion), 0)::int AS puntosAcumulados
            FROM workers w
            JOIN person pe ON pe.person_id = w.person_id
            LEFT JOIN worker_vinculaciones wv ON wv.worker_id = w.id
                AND (wv.fecha_fin IS NULL OR wv.fecha_fin >= CURRENT_DATE)
            LEFT JOIN contributor c ON c.contributor_id = wv.empresa_id
            LEFT JOIN ssoma_amonestaciones a ON a.worker_id = w.id AND a.state = true
            WHERE w.id = @wid
            GROUP BY w.id, pe.full_name, pe.document_identity_code, c.contributor_name;
            """;

        await using var conn = Conn();
        await conn.OpenAsync();

        var workerDto = await conn.QueryFirstOrDefaultAsync<WorkerPuntajeDto>(sqlWorker, new { wid = workerId });
        if (workerDto is null) return null;

        workerDto.Inhabilitado = workerDto.PuntosAcumulados >= 10;

        // Historial de amonestaciones
        var (historial, _) = await GetListAsync(new AmonestacionListQuery { WorkerId = workerId, PageSize = 100 });
        workerDto.Historial = historial;

        return workerDto;
    }

    public async Task GuardarPdfUrlAsync(int id, string url)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = await ctx.SsomaAmonestaciones.FindAsync(id);
        if (entity is null) return;
        entity.PdfUrl = url;
        entity.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }
}
