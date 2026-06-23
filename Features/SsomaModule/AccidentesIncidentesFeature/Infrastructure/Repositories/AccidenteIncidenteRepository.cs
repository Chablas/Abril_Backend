using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Repositories;

public class AccidenteIncidenteRepository : IAccidenteIncidenteRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IConfiguration _config;

    public AccidenteIncidenteRepository(IDbContextFactory<AppDbContext> factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    private NpgsqlConnection Conn() => new(_config.GetConnectionString("DefaultConnection"));

    public async Task<FlashReportInicializarDto> GetInicializarAsync()
    {
        const string sql = """
            SELECT id, project_description AS nombre, abbreviation AS abreviatura, email_coord_ssoma AS emailCoordsoma FROM "Project" WHERE active = true ORDER BY project_description;
            SELECT id, nombre, codigo FROM ssoma_flash_tipo ORDER BY orden;
            SELECT id, nombre FROM ssoma_flash_etapa_proyecto ORDER BY nombre;
            SELECT id, nombre FROM ssoma_flash_parte_afectada ORDER BY nombre;
            SELECT id, razon_social AS nombre FROM ssoma_empresa_abril WHERE activa = true ORDER BY razon_social;
            SELECT id, nombre FROM ssoma_flash_partida ORDER BY nombre;
            SELECT id, razon_social, ruc FROM "Contributor" WHERE is_active = true ORDER BY razon_social;
            SELECT w.id, CONCAT(w.nombres, ' ', w.apellido_paterno, ' ', w.apellido_materno) AS nombre_completo, w.numero_documento AS documento, w.cargo FROM "Worker" w WHERE w.estado = 'Activo' ORDER BY nombre_completo;
            """;

        await using var conn = Conn();
        await conn.OpenAsync();
        await using var multi = await conn.QueryMultipleAsync(sql);

        var proyectos = (await multi.ReadAsync<FlashProyectoDto>()).ToList();
        var tipos = (await multi.ReadAsync<CatalogoItemDto>()).ToList();
        var etapas = (await multi.ReadAsync<CatalogoItemDto>()).ToList();
        var partes = (await multi.ReadAsync<CatalogoItemDto>()).ToList();
        var empresas = (await multi.ReadAsync<CatalogoItemDto>()).ToList();
        var partidas = (await multi.ReadAsync<CatalogoItemDto>()).ToList();
        var contratistas = (await multi.ReadAsync<ContratistaCatalogoDto>()).ToList();
        var trabajadores = (await multi.ReadAsync<TrabajadorCatalogoDto>()).ToList();

        return new FlashReportInicializarDto
        {
            Proyectos = proyectos,
            Tipos = tipos,
            EtapasProyecto = etapas,
            PartesAfectadas = partes,
            EmpresasAbril = empresas,
            Partidas = partidas,
            Contratistas = contratistas,
            Trabajadores = trabajadores
        };
    }

    public async Task<(List<FlashReportListItemDto> Items, int Total)> GetListAsync(
        int? proyectoId, int? tipoId, string? estado,
        DateTime? fechaDesde, DateTime? fechaHasta,
        bool? soloEnviados, int page, int pageSize)
    {
        var where = new List<string>();
        var p = new DynamicParameters();
        p.Add("offset", (page - 1) * pageSize);
        p.Add("limit", pageSize);

        if (proyectoId.HasValue)  { where.Add("a.proyecto_id = @pid"); p.Add("pid", proyectoId); }
        if (tipoId.HasValue)      { where.Add("a.tipo_id = @tid"); p.Add("tid", tipoId); }
        if (!string.IsNullOrEmpty(estado)) { where.Add("a.estado = @estado"); p.Add("estado", estado); }
        if (fechaDesde.HasValue)  { where.Add("a.fecha >= @fd"); p.Add("fd", fechaDesde.Value.Date); }
        if (fechaHasta.HasValue)  { where.Add("a.fecha <= @fh"); p.Add("fh", fechaHasta.Value.Date); }
        if (soloEnviados == true) { where.Add("a.enviado = true"); }

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        var sql = $"""
            SELECT
                a.id, a.codigo, p.project_description AS proyecto_nombre, a.fecha,
                t.nombre AS tipo_nombre, t.codigo AS tipo_codigo,
                a.trabajador_nombre, a.estado, a.enviado, a.fecha_envio,
                a.consecuencia_real_personal
            FROM ssoma_accidente_incidente a
            JOIN "Project" p ON p.id = a.proyecto_id
            JOIN ssoma_flash_tipo t ON t.id = a.tipo_id
            {whereClause}
            ORDER BY a.fecha DESC, a.id DESC
            LIMIT @limit OFFSET @offset;

            SELECT COUNT(*) FROM ssoma_accidente_incidente a {whereClause};
            """;

        await using var conn = Conn();
        await conn.OpenAsync();
        await using var multi = await conn.QueryMultipleAsync(sql, p);
        var items = (await multi.ReadAsync<FlashReportListItemDto>()).ToList();
        var total = await multi.ReadSingleAsync<int>();
        return (items, total);
    }

    public async Task<FlashReportDetalleDto?> GetDetalleAsync(int id)
    {
        const string sql = """
            SELECT
                a.id, a.codigo,
                a.proyecto_id, p.project_description AS proyecto_nombre, p.abbreviation AS proyecto_abreviatura,
                a.tipo_id, t.nombre AS tipo_nombre, t.codigo AS tipo_codigo,
                a.fecha, a.hora, a.lugar_exacto, a.descripcion, a.estado,
                a.empresa_abril_id, ea.razon_social AS empresa_abril_nombre,
                a.contributor_id, c.razon_social AS contributor_nombre,
                a.jefe_inmediato_nombre,
                a.etapa_proyecto_id, ep.nombre AS etapa_proyecto_nombre,
                a.partida_id, pa.nombre AS partida_nombre,
                a.worker_id, a.trabajador_nombre, a.puesto_trabajo, a.edad, a.anios_experiencia, a.celular_trabajador,
                a.parte_afectada_id, paf.nombre AS parte_afectada_nombre,
                a.dano_proceso, a.consecuencia_real_personal, a.consecuencia_potencial_personal,
                a.acciones_inmediatas,
                a.elaborado_por_id, a.elaborado_por_nombre, a.elaborado_por_cargo, a.elaborado_por_email, a.elaborado_por_telefono,
                a.url_foto1, a.url_foto2,
                a.enviado, a.fecha_envio, a.url_pdf_sharepoint,
                a.created_at
            FROM ssoma_accidente_incidente a
            JOIN "Project" p ON p.id = a.proyecto_id
            JOIN ssoma_flash_tipo t ON t.id = a.tipo_id
            LEFT JOIN ssoma_empresa_abril ea ON ea.id = a.empresa_abril_id
            LEFT JOIN "Contributor" c ON c.id = a.contributor_id
            LEFT JOIN ssoma_flash_etapa_proyecto ep ON ep.id = a.etapa_proyecto_id
            LEFT JOIN ssoma_flash_partida pa ON pa.id = a.partida_id
            LEFT JOIN ssoma_flash_parte_afectada paf ON paf.id = a.parte_afectada_id
            WHERE a.id = @id;

            SELECT id, fecha_inicio, fecha_fin, observacion FROM ssoma_flash_descanso WHERE accidente_incidente_id = @id ORDER BY fecha_inicio;
            """;

        await using var conn = Conn();
        await conn.OpenAsync();
        await using var multi = await conn.QueryMultipleAsync(sql, new { id });
        var detalle = await multi.ReadFirstOrDefaultAsync<FlashReportDetalleDto>();
        if (detalle == null) return null;
        detalle.Descansos = (await multi.ReadAsync<DescansoDto>()).ToList();
        return detalle;
    }

    public async Task<string> GenerarCodigoAsync(int proyectoId, string tipoCodigoCorto)
    {
        const string sql = """
            SELECT COALESCE(p.abbreviation, 'XXX') FROM "Project" p WHERE p.id = @pid;
            SELECT COUNT(*) FROM ssoma_accidente_incidente
            WHERE proyecto_id = @pid AND tipo_id = (SELECT id FROM ssoma_flash_tipo WHERE codigo = @tipoCodigo);
            """;

        await using var conn = Conn();
        await conn.OpenAsync();
        await using var multi = await conn.QueryMultipleAsync(sql, new { pid = proyectoId, tipoCodigo = tipoCodigoCorto });
        var abrev = (await multi.ReadFirstAsync<string>()).ToUpperInvariant();
        var count = await multi.ReadFirstAsync<int>();
        var correlativo = (count + 1).ToString("D2");
        return $"{abrev}-{tipoCodigoCorto}-{correlativo}";
    }

    public async Task<int> CrearAsync(CrearFlashReportRequest req, string codigo, string? urlFoto1, string? urlFoto2, int? usuarioId)
    {
        using var ctx = _factory.CreateDbContext();

        TimeSpan? hora = null;
        if (!string.IsNullOrWhiteSpace(req.Hora) && TimeSpan.TryParse(req.Hora, out var ts))
            hora = ts;

        var entity = new SsomaAccidenteIncidente
        {
            Codigo = codigo,
            ProyectoId = req.ProyectoId,
            TipoId = req.TipoId,
            Fecha = DateTime.SpecifyKind(req.Fecha.Date, DateTimeKind.Utc),
            Hora = hora,
            LugarExacto = req.LugarExacto,
            Descripcion = req.Descripcion,
            Estado = "Borrador",
            EmpresaAbrilId = req.EmpresaAbrilId,
            ContributorId = req.ContributorId,
            JefeInmediatoNombre = req.JefeInmediatoNombre,
            EtapaProyectoId = req.EtapaProyectoId,
            PartidaId = req.PartidaId,
            WorkerId = req.WorkerId,
            TrabajadorNombre = req.TrabajadorNombre,
            PuestoTrabajo = req.PuestoTrabajo,
            Edad = req.Edad,
            AniosExperiencia = req.AniosExperiencia,
            CelularTrabajador = req.CelularTrabajador,
            ParteAfectadaId = req.ParteAfectadaId,
            DanoProceso = req.DanoProceso,
            ConsecuenciaRealPersonal = req.ConsecuenciaRealPersonal,
            ConsecuenciaPotencialPersonal = req.ConsecuenciaPotencialPersonal,
            AccionesInmediatas = req.AccionesInmediatas,
            ElaboradoPorId = usuarioId,
            ElaboradoPorNombre = req.ElaboradoPorNombre,
            ElaboradoPorCargo = req.ElaboradoPorCargo,
            ElaboradoPorEmail = req.ElaboradoPorEmail,
            ElaboradoPorTelefono = req.ElaboradoPorTelefono,
            UrlFoto1 = urlFoto1,
            UrlFoto2 = urlFoto2,
            Descansos = req.Descansos.Select(d => new SsomaFlashDescanso
            {
                FechaInicio = DateTime.SpecifyKind(d.FechaInicio.Date, DateTimeKind.Utc),
                FechaFin = DateTime.SpecifyKind(d.FechaFin.Date, DateTimeKind.Utc),
                Observacion = d.Observacion
            }).ToList()
        };

        ctx.Set<SsomaAccidenteIncidente>().Add(entity);
        await ctx.SaveChangesAsync();
        return entity.Id;
    }

    public async Task ActualizarAsync(int id, ActualizarFlashReportRequest req, string? urlFoto1, string? urlFoto2)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = await ctx.Set<SsomaAccidenteIncidente>()
            .Include(a => a.Descansos)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new AbrilException("Flash Report no encontrado.", 404);

        TimeSpan? hora = null;
        if (!string.IsNullOrWhiteSpace(req.Hora) && TimeSpan.TryParse(req.Hora, out var ts))
            hora = ts;

        entity.TipoId = req.TipoId;
        entity.Fecha = DateTime.SpecifyKind(req.Fecha.Date, DateTimeKind.Utc);
        entity.Hora = hora;
        entity.LugarExacto = req.LugarExacto;
        entity.Descripcion = req.Descripcion;
        entity.EmpresaAbrilId = req.EmpresaAbrilId;
        entity.ContributorId = req.ContributorId;
        entity.JefeInmediatoNombre = req.JefeInmediatoNombre;
        entity.EtapaProyectoId = req.EtapaProyectoId;
        entity.PartidaId = req.PartidaId;
        entity.WorkerId = req.WorkerId;
        entity.TrabajadorNombre = req.TrabajadorNombre;
        entity.PuestoTrabajo = req.PuestoTrabajo;
        entity.Edad = req.Edad;
        entity.AniosExperiencia = req.AniosExperiencia;
        entity.CelularTrabajador = req.CelularTrabajador;
        entity.ParteAfectadaId = req.ParteAfectadaId;
        entity.DanoProceso = req.DanoProceso;
        entity.ConsecuenciaRealPersonal = req.ConsecuenciaRealPersonal;
        entity.ConsecuenciaPotencialPersonal = req.ConsecuenciaPotencialPersonal;
        entity.AccionesInmediatas = req.AccionesInmediatas;
        entity.ElaboradoPorNombre = req.ElaboradoPorNombre;
        entity.ElaboradoPorCargo = req.ElaboradoPorCargo;
        entity.ElaboradoPorEmail = req.ElaboradoPorEmail;
        entity.ElaboradoPorTelefono = req.ElaboradoPorTelefono;
        entity.UpdatedAt = DateTime.UtcNow;

        if (urlFoto1 != null) entity.UrlFoto1 = urlFoto1;
        if (urlFoto2 != null) entity.UrlFoto2 = urlFoto2;

        // Reemplazar descansos
        ctx.Set<SsomaFlashDescanso>().RemoveRange(entity.Descansos);
        entity.Descansos = req.Descansos.Select(d => new SsomaFlashDescanso
        {
            AccidenteIncidenteId = id,
            FechaInicio = DateTime.SpecifyKind(d.FechaInicio.Date, DateTimeKind.Utc),
            FechaFin = DateTime.SpecifyKind(d.FechaFin.Date, DateTimeKind.Utc),
            Observacion = d.Observacion
        }).ToList();

        await ctx.SaveChangesAsync();
    }

    public async Task MarcarEnviadoAsync(int id, string urlPdf)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = await ctx.Set<SsomaAccidenteIncidente>().FindAsync(id)
            ?? throw new AbrilException("Flash Report no encontrado.", 404);
        entity.Enviado = true;
        entity.FechaEnvio = DateTime.UtcNow;
        entity.UrlPdfSharepoint = urlPdf;
        entity.Estado = "Enviado";
        entity.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task EliminarAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = await ctx.Set<SsomaAccidenteIncidente>().FindAsync(id)
            ?? throw new AbrilException("Flash Report no encontrado.", 404);
        ctx.Set<SsomaAccidenteIncidente>().Remove(entity);
        await ctx.SaveChangesAsync();
    }
}
