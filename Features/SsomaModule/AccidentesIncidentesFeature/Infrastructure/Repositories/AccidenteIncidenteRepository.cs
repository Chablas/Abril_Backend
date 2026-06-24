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

    private NpgsqlConnection Conn() => new(_config["Database:PostgreSQL"]);

    public async Task<FlashReportInicializarDto> GetInicializarAsync()
    {
        const string sql = """
            SELECT project_id AS id, project_description AS nombre, abbreviation AS abreviatura, email_coord_ssoma AS emailCoordsoma FROM project WHERE active = true ORDER BY project_description;
            SELECT id, nombre, codigo FROM ssoma_flash_tipo ORDER BY orden;
            SELECT id, nombre FROM ssoma_flash_etapa_proyecto ORDER BY nombre;
            SELECT id, nombre FROM ssoma_flash_parte_afectada ORDER BY nombre;
            SELECT contributor_id AS id, contributor_name AS nombre FROM contributor WHERE active = true AND es_abril = true ORDER BY contributor_name;
            SELECT partida_id AS id, partida_description AS nombre FROM partida WHERE active = true ORDER BY partida_description;
            SELECT contributor_id AS id, contributor_name AS razon_social, contributor_ruc AS ruc FROM contributor WHERE active = true AND es_abril = false ORDER BY contributor_name;
            SELECT w.id, p.full_name AS nombre_completo, p.document_identity_code AS documento,
                   NULLIF(TRIM(COALESCE(w.categoria,'') || CASE WHEN w.categoria IS NOT NULL AND w.ocupacion IS NOT NULL THEN ' / ' ELSE '' END || COALESCE(w.ocupacion,'')), '') AS cargo,
                   CASE WHEN w.fecha_nacimiento IS NOT NULL THEN DATE_PART('year', AGE(w.fecha_nacimiento))::int ELSE NULL END AS edad,
                   w.anios_experiencia, w.contributor_id
            FROM workers w JOIN person p ON p.person_id = w.person_id WHERE w.estado = 'ACTIVO' ORDER BY p.full_name;
            SELECT psc.project_id, c.contributor_id
            FROM project_sub_contractor psc JOIN contractor c ON c.contractor_id = psc.contractor_id WHERE c.active = true;
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
        var proyectoContratistas = (await multi.ReadAsync<ProyectoContratistaDto>()).ToList();

        return new FlashReportInicializarDto
        {
            Proyectos = proyectos,
            Tipos = tipos,
            EtapasProyecto = etapas,
            PartesAfectadas = partes,
            EmpresasAbril = empresas,
            Partidas = partidas,
            Contratistas = contratistas,
            Trabajadores = trabajadores,
            ProyectoContratistas = proyectoContratistas
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
            FROM ss_accidente_incidente a
            JOIN project p ON p.project_id = a.proyecto_id
            JOIN ssoma_flash_tipo t ON t.id = a.tipo_id
            {whereClause}
            ORDER BY a.fecha DESC, a.id DESC
            LIMIT @limit OFFSET @offset;

            SELECT COUNT(*) FROM ss_accidente_incidente a {whereClause};
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
                a.empresa_abril_id, ea.contributor_name AS empresa_abril_nombre,
                a.contributor_id, c.contributor_name AS contributor_nombre,
                a.jefe_inmediato_nombre,
                a.etapa_proyecto_id, ep.nombre AS etapa_proyecto_nombre,
                a.partida_id, pa.partida_description AS partida_nombre,
                a.worker_id, a.trabajador_nombre, a.puesto_trabajo, a.edad, a.anios_experiencia, a.celular_trabajador,
                a.parte_afectada_id, paf.nombre AS parte_afectada_nombre,
                a.dano_proceso, a.consecuencia_real_personal, a.consecuencia_potencial_personal,
                a.acciones_inmediatas,
                a.elaborado_por_id, a.elaborado_por_nombre, a.elaborado_por_cargo, a.elaborado_por_email, a.elaborado_por_telefono,
                a.url_foto1, a.url_foto2,
                a.enviado, a.fecha_envio, a.url_pdf_sharepoint,
                a.created_at
            FROM ss_accidente_incidente a
            JOIN project p ON p.project_id = a.proyecto_id
            JOIN ssoma_flash_tipo t ON t.id = a.tipo_id
            LEFT JOIN contributor ea ON ea.contributor_id = a.empresa_abril_id
            LEFT JOIN contributor c ON c.contributor_id = a.contributor_id
            LEFT JOIN ssoma_flash_etapa_proyecto ep ON ep.id = a.etapa_proyecto_id
            LEFT JOIN partida pa ON pa.partida_id = a.partida_id
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
            SELECT COALESCE(p.abbreviation, 'XXX') FROM project p WHERE p.project_id = @pid;
            SELECT COUNT(*) FROM ss_accidente_incidente
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

        var tiposEntregable = await ctx.Set<SsomaEntregableTipo>()
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden)
            .ToListAsync();

        var entregables = tiposEntregable.Select(t => new SsomaEntregable
        {
            AccidenteIncidenteId = entity.Id,
            TipoId = t.Id,
            Estado = "Pendiente"
        }).ToList();

        ctx.Set<SsomaEntregable>().AddRange(entregables);
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

    public async Task<List<EntregableDto>> GetEntregablesAsync(int accidenteId)
    {
        const string sql = """
            SELECT e.id, e.tipo_id, t.nombre AS tipo_nombre, t.orden, e.estado,
                   e.fecha_limite, e.url_archivo, e.nombre_archivo, e.observacion, e.updated_at
            FROM ss_entregable e
            JOIN ss_entregable_tipo t ON t.id = e.tipo_id
            WHERE e.accidente_incidente_id = @id
            ORDER BY t.orden;

            SELECT r.id, r.entregable_id, r.worker_id, r.nombre
            FROM ss_entregable_responsable r
            JOIN ss_entregable e ON e.id = r.entregable_id
            WHERE e.accidente_incidente_id = @id;
            """;

        await using var conn = Conn();
        await conn.OpenAsync();
        await using var multi = await conn.QueryMultipleAsync(sql, new { id = accidenteId });

        var entregables = (await multi.ReadAsync<EntregableDto>()).ToList();
        var responsables = (await multi.ReadAsync<dynamic>()).ToList();

        foreach (var e in entregables)
            e.Responsables = responsables
                .Where(r => r.entregable_id == e.Id)
                .Select(r => new EntregableResponsableDto
                {
                    Id = r.id,
                    WorkerId = r.worker_id,
                    Nombre = r.nombre
                }).ToList();

        return entregables;
    }

    public async Task ActualizarEntregableAsync(int entregableId, ActualizarEntregableRequest req)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = await ctx.Set<SsomaEntregable>()
            .Include(e => e.Responsables)
            .FirstOrDefaultAsync(e => e.Id == entregableId)
            ?? throw new AbrilException("Entregable no encontrado.", 404);

        entity.Estado = req.Estado;
        entity.FechaLimite = req.FechaLimite;
        entity.Observacion = req.Observacion;
        entity.UpdatedAt = DateTime.UtcNow;

        ctx.Set<SsomaEntregableResponsable>().RemoveRange(entity.Responsables);
        entity.Responsables = req.Responsables
            .Select(n => new SsomaEntregableResponsable { EntregableId = entregableId, Nombre = n })
            .Concat(req.ResponsableWorkerIds.Select(wid => new SsomaEntregableResponsable
                { EntregableId = entregableId, WorkerId = wid, Nombre = "" }))
            .ToList();

        await ctx.SaveChangesAsync();
    }

    public async Task SubirArchivoEntregableAsync(int entregableId, string urlArchivo, string nombreArchivo)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = await ctx.Set<SsomaEntregable>().FindAsync(entregableId)
            ?? throw new AbrilException("Entregable no encontrado.", 404);
        entity.UrlArchivo = urlArchivo;
        entity.NombreArchivo = nombreArchivo;
        if (entity.Estado == "Pendiente") entity.Estado = "Presentado";
        entity.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    // ── RM-050 ────────────────────────────────────────────────────────────────

    public async Task<Rm050Dto?> GetRm050Async(int accidenteId)
    {
        using var ctx = _factory.CreateDbContext();
        var inv = await ctx.Set<SsomaInvestigacionRm050>()
            .Include(i => i.AccionesCorrectivas)
            .FirstOrDefaultAsync(i => i.AccidenteIncidenteId == accidenteId);

        if (inv == null) return null;

        return new Rm050Dto
        {
            Id = inv.Id,
            DescripcionDetallada = inv.DescripcionDetallada,
            Mecanismo = inv.Mecanismo,
            AgenteCausante = inv.AgenteCausante,
            ActosSubestandar = inv.ActosSubestandar,
            CondicionesSubestandar = inv.CondicionesSubestandar,
            FactoresPersonales = inv.FactoresPersonales,
            FactoresTrabajo = inv.FactoresTrabajo,
            DiasPerdidos = inv.DiasPerdidos,
            TipoAccidente = inv.TipoAccidente,
            GravedadAccidente = inv.GravedadAccidente,
            NroTrabajadoresAfectados = inv.NroTrabajadoresAfectados,
            Testigos = inv.Testigos,
            ElaboradoPorNombre = inv.ElaboradoPorNombre,
            ElaboradoPorCargo = inv.ElaboradoPorCargo,
            ElaboradoPorFecha = inv.ElaboradoPorFecha,
            AprobadoPorNombre = inv.AprobadoPorNombre,
            AprobadoPorCargo = inv.AprobadoPorCargo,
            Estado = inv.Estado,
            UpdatedAt = inv.UpdatedAt,
            AccionesCorrectivas = inv.AccionesCorrectivas.Select(a => new AccionCorrectivaDto
            {
                Id = a.Id,
                Descripcion = a.Descripcion,
                Tipo = a.Tipo,
                ResponsableNombre = a.ResponsableNombre,
                ResponsableWorkerId = a.ResponsableWorkerId,
                FechaCompromiso = a.FechaCompromiso,
                FechaCumplimiento = a.FechaCumplimiento,
                Estado = a.Estado,
                EvidenciaUrl = a.EvidenciaUrl,
            }).ToList(),
        };
    }

    public async Task GuardarRm050Async(int accidenteId, GuardarRm050Request req)
    {
        using var ctx = _factory.CreateDbContext();
        var inv = await ctx.Set<SsomaInvestigacionRm050>()
            .Include(i => i.AccionesCorrectivas)
            .FirstOrDefaultAsync(i => i.AccidenteIncidenteId == accidenteId);

        if (inv == null)
        {
            inv = new SsomaInvestigacionRm050 { AccidenteIncidenteId = accidenteId };
            ctx.Set<SsomaInvestigacionRm050>().Add(inv);
        }

        inv.DescripcionDetallada = req.DescripcionDetallada;
        inv.Mecanismo = req.Mecanismo;
        inv.AgenteCausante = req.AgenteCausante;
        inv.ActosSubestandar = req.ActosSubestandar;
        inv.CondicionesSubestandar = req.CondicionesSubestandar;
        inv.FactoresPersonales = req.FactoresPersonales;
        inv.FactoresTrabajo = req.FactoresTrabajo;
        inv.DiasPerdidos = req.DiasPerdidos;
        inv.TipoAccidente = req.TipoAccidente;
        inv.GravedadAccidente = req.GravedadAccidente;
        inv.NroTrabajadoresAfectados = req.NroTrabajadoresAfectados;
        inv.Testigos = req.Testigos;
        inv.ElaboradoPorNombre = req.ElaboradoPorNombre;
        inv.ElaboradoPorCargo = req.ElaboradoPorCargo;
        inv.ElaboradoPorFecha = req.ElaboradoPorFecha;
        inv.AprobadoPorNombre = req.AprobadoPorNombre;
        inv.AprobadoPorCargo = req.AprobadoPorCargo;
        inv.UpdatedAt = DateTime.UtcNow;

        // Reemplazar acciones correctivas
        ctx.Set<SsomaAccionCorrectiva>().RemoveRange(inv.AccionesCorrectivas);
        inv.AccionesCorrectivas = req.AccionesCorrectivas.Select(a => new SsomaAccionCorrectiva
        {
            Descripcion = a.Descripcion,
            Tipo = a.Tipo,
            ResponsableNombre = a.ResponsableNombre,
            ResponsableWorkerId = a.ResponsableWorkerId,
            FechaCompromiso = a.FechaCompromiso,
            FechaCumplimiento = a.FechaCumplimiento,
            Estado = a.Estado,
        }).ToList();

        await ctx.SaveChangesAsync();
    }
}
