using Abril_Backend.Features.Habilitacion.Application.Dtos.Dashboard;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class DashboardHabRepository : IDashboardHabRepository
    {
        static DashboardHabRepository()
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IConfiguration _configuration;

        public DashboardHabRepository(IDbContextFactory<AppDbContext> factory, IConfiguration configuration)
        {
            _factory = factory;
            _configuration = configuration;
        }

        private IDbConnection CreateConnection()
            => new NpgsqlConnection(_configuration["Database:PostgreSQL"]);

        private class WorkerRiesgoRaw
        {
            public int WorkerId { get; set; }
            public string Nombre { get; set; } = "";
            public string Empresa { get; set; } = "";
            public string Proyecto { get; set; } = "";
            public string? DocsVencidos { get; set; }
            public string? DocsPorVencer { get; set; }
            public bool SinEmo { get; set; }
            public bool SinInduccion { get; set; }
        }

        public async Task<DashboardAdminDto> GetResumenAsync()
        {
            using var conn = CreateConnection();

            // ── KPIs (8 resultados en un round-trip) ─────────────────────────────
            const string kpiSql = @"
SELECT COUNT(*) FROM contributor WHERE active = true;

SELECT COUNT(DISTINCT ec.contributor_id)
FROM contributor ec
WHERE ec.active = true
  AND NOT EXISTS (
      SELECT 1 FROM ss_hab_empresa he
      WHERE he.empresa_id = ec.contributor_id
        AND he.estado IN ('Falta','Rechazado','Vencido','Enviado')
  );

SELECT COUNT(DISTINCT w.id)
FROM workers w
JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
WHERE w.contrata_casa = 'Contratista'
  AND (w.estado IS NULL OR w.estado != 'RETIRADO');

SELECT COUNT(DISTINCT w.id)
FROM workers w
JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
WHERE w.contrata_casa = 'Contratista'
  AND (w.estado IS NULL OR w.estado != 'RETIRADO')
  AND NOT EXISTS (
      SELECT 1 FROM ss_hab_trabajador ht
      WHERE ht.worker_id = w.id
        AND ht.estado IN ('Falta','Rechazado','Vencido','Enviado')
  );

SELECT COUNT(*) FROM (
    SELECT id FROM ss_hab_trabajador WHERE estado = 'Enviado' AND vigencia < NOW()
    UNION ALL
    SELECT id FROM ss_hab_empresa WHERE estado = 'Enviado' AND vigencia < NOW()
) t;

SELECT COUNT(*) FROM (
    SELECT id FROM ss_hab_trabajador
    WHERE estado = 'Enviado' AND vigencia BETWEEN NOW() AND NOW() + interval '30 days'
    UNION ALL
    SELECT id FROM ss_hab_empresa
    WHERE estado = 'Enviado' AND vigencia BETWEEN NOW() AND NOW() + interval '30 days'
) t;

SELECT COUNT(*)
FROM worker_emos
WHERE fecha_vencimiento BETWEEN NOW()::date AND (NOW() + interval '30 days')::date
  AND activo = true;

SELECT COUNT(DISTINCT svw.worker_id)
FROM ss_sctr_vidaley_worker svw
JOIN ss_sctr_vidaley sv ON sv.id = svw.sctr_vidaley_id
WHERE sv.vigencia BETWEEN NOW() AND NOW() + interval '15 days';";

            using var multi = await conn.QueryMultipleAsync(kpiSql);
            var kpis = new DashboardKpisDto
            {
                EmpresasTotal          = await multi.ReadSingleAsync<int>(),
                EmpresasHabilitadas    = await multi.ReadSingleAsync<int>(),
                WorkersTotal           = await multi.ReadSingleAsync<int>(),
                WorkersHabilitados     = await multi.ReadSingleAsync<int>(),
                EntregablesVencidos    = await multi.ReadSingleAsync<int>(),
                EntregablesPorVencer30 = await multi.ReadSingleAsync<int>(),
                EmosPorVencer30        = await multi.ReadSingleAsync<int>(),
                SctrPorVencer15        = await multi.ReadSingleAsync<int>(),
            };

            // ── EmpresasEnRiesgo (top 10) ─────────────────────────────────────────
            const string empresasSql = @"
SELECT
    ec.contributor_id                                                                     AS empresa_id,
    ec.contributor_name                                                                   AS nombre,
    COUNT(CASE WHEN ht.vigencia < NOW() THEN 1 END)                                      AS entregables_vencidos,
    COUNT(CASE WHEN ht.vigencia BETWEEN NOW() AND NOW()+interval'30 days' THEN 1 END)    AS entregables_por_vencer,
    (SELECT COUNT(DISTINCT wv2.worker_id)
     FROM worker_vinculaciones wv2
     WHERE wv2.empresa_id = ec.contributor_id AND wv2.fecha_fin IS NULL)                 AS workers_activos,
    CASE
        WHEN COUNT(CASE WHEN ht.vigencia < NOW() THEN 1 END) >= 5 THEN 'ALTO'
        WHEN COUNT(CASE WHEN ht.vigencia < NOW() THEN 1 END) >= 2 THEN 'MEDIO'
        ELSE 'BAJO'
    END                                                                                   AS nivel_riesgo
FROM ss_hab_trabajador ht
JOIN worker_vinculaciones wv ON wv.worker_id = ht.worker_id AND wv.fecha_fin IS NULL
JOIN contributor ec ON ec.contributor_id = wv.empresa_id
WHERE ht.estado = 'Enviado'
GROUP BY ec.contributor_id, ec.contributor_name
HAVING COUNT(CASE WHEN ht.vigencia < NOW() THEN 1 END) > 0
ORDER BY entregables_vencidos DESC
LIMIT 10";

            var empresasEnRiesgo = (await conn.QueryAsync<EmpresaRiesgoDto>(empresasSql)).ToList();

            // ── WorkersEnRiesgo (top 20) ──────────────────────────────────────────
            const string workersSql = @"
SELECT
    w.id                                                                                  AS worker_id,
    COALESCE(per.full_name, '')                                                           AS nombre,
    ec.contributor_name                                                                   AS empresa,
    p.project_description                                                                 AS proyecto,
    STRING_AGG(DISTINCT CASE WHEN ht.vigencia < NOW() THEN i.nombre END, '|')            AS docs_vencidos,
    STRING_AGG(DISTINCT CASE WHEN ht.vigencia BETWEEN NOW() AND NOW()+interval'30 days'
                              THEN i.nombre END, '|')                                     AS docs_por_vencer,
    NOT EXISTS (
        SELECT 1 FROM worker_emos we
        WHERE we.worker_id = w.id AND we.activo = true
    )                                                                                     AS sin_emo,
    NOT EXISTS (
        SELECT 1 FROM ss_induccion si
        WHERE si.worker_id = w.id AND si.ingreso_confirmado = true
    )                                                                                     AS sin_induccion
FROM ss_hab_trabajador ht
JOIN ss_item_trabajador i ON i.id = ht.item_id
JOIN workers w ON w.id = ht.worker_id
LEFT JOIN person per ON per.person_id = w.person_id
JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
JOIN contributor ec ON ec.contributor_id = wv.empresa_id
JOIN project p ON p.project_id = wv.proyecto_id
WHERE ht.estado = 'Enviado' AND ht.vigencia < NOW()
GROUP BY w.id, per.full_name, ec.contributor_name, p.project_description
ORDER BY per.full_name
LIMIT 20";

            var workersRaw = await conn.QueryAsync<WorkerRiesgoRaw>(workersSql);
            var workersEnRiesgo = workersRaw.Select(r => new WorkerRiesgoDto
            {
                WorkerId            = r.WorkerId,
                Nombre              = r.Nombre,
                Empresa             = r.Empresa,
                Proyecto            = r.Proyecto,
                DocumentosVencidos  = r.DocsVencidos?.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
                DocumentosPorVencer = r.DocsPorVencer?.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
                SinEmo              = r.SinEmo,
                SinInduccion        = r.SinInduccion,
            }).ToList();

            // ── EstadoPorProyecto ─────────────────────────────────────────────────
            const string proyectosSql = @"
SELECT
    p.project_id                                                                          AS proyecto_id,
    p.project_description                                                                 AS nombre,
    COUNT(DISTINCT wv.worker_id)                                                          AS workers_total,
    COUNT(DISTINCT CASE WHEN NOT EXISTS (
        SELECT 1 FROM ss_hab_trabajador ht2
        WHERE ht2.worker_id = wv.worker_id
          AND ht2.estado IN ('Falta','Rechazado','Vencido','Enviado')
    ) THEN wv.worker_id END)                                                              AS workers_habilitados,
    COUNT(DISTINCT wv.empresa_id)                                                         AS empresas_activas,
    COALESCE((
        SELECT COUNT(*) FROM ss_hab_trabajador ht3
        JOIN worker_vinculaciones wv3 ON wv3.worker_id = ht3.worker_id
                                      AND wv3.fecha_fin IS NULL
                                      AND wv3.proyecto_id = p.project_id
        WHERE ht3.estado IN ('Falta','Enviado','Rechazado')
    ), 0) + COALESCE((
        SELECT COUNT(*) FROM ss_hab_empresa he3
        WHERE he3.proyecto_id = p.project_id
          AND he3.estado IN ('Falta','Enviado','Rechazado')
    ), 0)                                                                                 AS entregables_pendientes
FROM project p
JOIN worker_vinculaciones wv ON wv.proyecto_id = p.project_id AND wv.fecha_fin IS NULL
GROUP BY p.project_id, p.project_description
ORDER BY workers_total DESC, p.project_description";

            var estadoPorProyecto = (await conn.QueryAsync<ProyectoEstadoDto>(proyectosSql)).ToList();

            // ── VencimientosProximos (30 días, top 50) ────────────────────────────
            const string vencimientosSql = @"
SELECT tipo, nombre, entidad, proyecto, fecha_vencimiento, dias_restantes
FROM (
    SELECT
        'TRABAJADOR'                                                    AS tipo,
        i.nombre                                                        AS nombre,
        COALESCE(per.full_name, '')                                     AS entidad,
        COALESCE(p.project_description, '')                             AS proyecto,
        CAST(ht.vigencia AS timestamp)                                  AS fecha_vencimiento,
        EXTRACT(DAY FROM ht.vigencia - NOW())::int                      AS dias_restantes
    FROM ss_hab_trabajador ht
    JOIN ss_item_trabajador i ON i.id = ht.item_id
    JOIN workers w ON w.id = ht.worker_id
    LEFT JOIN person per ON per.person_id = w.person_id
    LEFT JOIN LATERAL (
        SELECT proyecto_id FROM worker_vinculaciones
        WHERE worker_id = w.id AND fecha_fin IS NULL
        ORDER BY created_at DESC, id DESC LIMIT 1
    ) wv ON TRUE
    LEFT JOIN project p ON p.project_id = wv.proyecto_id
    WHERE ht.estado = 'Enviado'
      AND ht.vigencia BETWEEN NOW() AND NOW() + interval '30 days'

    UNION ALL

    SELECT
        'EMPRESA'                                                       AS tipo,
        i.nombre                                                        AS nombre,
        ec.contributor_name                                             AS entidad,
        COALESCE(p.project_description, '')                             AS proyecto,
        CAST(he.vigencia AS timestamp)                                  AS fecha_vencimiento,
        EXTRACT(DAY FROM he.vigencia - NOW())::int                      AS dias_restantes
    FROM ss_hab_empresa he
    JOIN ss_item_empresa i ON i.id = he.item_id
    JOIN contributor ec ON ec.contributor_id = he.empresa_id
    JOIN project p ON p.project_id = he.proyecto_id
    WHERE he.estado = 'Enviado'
      AND he.vigencia BETWEEN NOW() AND NOW() + interval '30 days'

    UNION ALL

    SELECT
        'EMO'                                                           AS tipo,
        'EMO'                                                           AS nombre,
        COALESCE(per.full_name, '')                                     AS entidad,
        ''                                                              AS proyecto,
        we.fecha_vencimiento::timestamp                                 AS fecha_vencimiento,
        (we.fecha_vencimiento - NOW()::date)::int                       AS dias_restantes
    FROM worker_emos we
    JOIN workers w ON w.id = we.worker_id
    LEFT JOIN person per ON per.person_id = w.person_id
    WHERE we.activo = true
      AND we.fecha_vencimiento BETWEEN NOW()::date AND (NOW() + interval '30 days')::date
) t
ORDER BY fecha_vencimiento ASC
LIMIT 50";

            var vencimientos = (await conn.QueryAsync<VencimientoProximoDto>(vencimientosSql)).ToList();

            return new DashboardAdminDto
            {
                Kpis                 = kpis,
                EmpresasEnRiesgo     = empresasEnRiesgo,
                WorkersEnRiesgo      = workersEnRiesgo,
                EstadoPorProyecto    = estadoPorProyecto,
                VencimientosProximos = vencimientos,
            };
        }
    }
}
