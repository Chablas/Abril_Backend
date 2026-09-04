using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Dapper;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

public class PersonalHitoRepository : IPersonalHitoRepository
{
    private readonly IConfiguration _config;
    public PersonalHitoRepository(IConfiguration config) => _config = config;
    private NpgsqlConnection Conn() => new(_config["Database:PostgreSQL"]!);

    // Cronograma vigente de un proyecto: la versión de historial marcada como la actual.
    private const string CronogramaVigenteCte = """
        cronograma_vigente AS (
            SELECT ms.milestone_schedule_id, ms.milestone_id, ms.custom_description,
                   ms.planned_start_date, ms.es_hito_critico
            FROM milestone_schedule ms
            JOIN milestone_schedule_history msh
              ON msh.milestone_schedule_history_id = ms.milestone_schedule_history_id
            WHERE msh.project_id = @projectId
              AND msh.is_equal_to_last_version = true
              AND msh.active = true
              AND ms.active = true
              AND ms.es_hito_critico = true
        )
        """;

    public async Task<List<HitoCriticoDisponibleDto>> ObtenerHitosCriticosAsync(int projectId)
    {
        using var conn = Conn();
        var sql = $"""
            WITH {CronogramaVigenteCte}
            SELECT cv.milestone_schedule_id AS HitoId,
                   COALESCE(m.milestone_description, cv.custom_description, 'Hito') AS HitoDescripcion,
                   cv.planned_start_date AS HitoFecha
            FROM cronograma_vigente cv
            LEFT JOIN milestone m ON m.milestone_id = cv.milestone_id
            ORDER BY cv.planned_start_date NULLS LAST
            """;
        var result = await conn.QueryAsync<HitoCriticoDisponibleDto>(sql, new { projectId });
        return result.ToList();
    }

    public async Task<List<PersonalHitoDto>> ObtenerPorProyectoAsync(int projectId)
    {
        using var conn = Conn();
        var sql = $"""
            WITH {CronogramaVigenteCte}
            SELECT ph.id AS Id, ph.hito_id AS HitoId,
                   COALESCE(m.milestone_description, cv.custom_description, 'Hito') AS HitoDescripcion,
                   cv.planned_start_date AS HitoFecha,
                   cv.es_hito_critico AS EsHitoCritico,
                   ph.hito_salida_id AS HitoSalidaId,
                   COALESCE(m2.milestone_description, cv2.custom_description) AS HitoSalidaDescripcion,
                   cv2.planned_start_date AS HitoSalidaFecha,
                   ph.rol AS Rol, ph.cantidad AS Cantidad,
                   CASE
                       WHEN ph.hito_salida_id IS NOT NULL
                            AND cv.planned_start_date IS NOT NULL
                            AND cv2.planned_start_date IS NOT NULL
                       THEN GREATEST(0, (cv2.planned_start_date - cv.planned_start_date) / 7.0)
                       ELSE ph.semanas
                   END AS Semanas,
                   ph.costo_mensual AS CostoMensual, ph.total AS Total
            FROM ss_presupuesto_personal_hito ph
            JOIN ss_presupuesto p ON p.id = ph.presupuesto_id
            JOIN cronograma_vigente cv ON cv.milestone_schedule_id = ph.hito_id
            LEFT JOIN cronograma_vigente cv2 ON cv2.milestone_schedule_id = ph.hito_salida_id
            LEFT JOIN milestone m ON m.milestone_id = cv.milestone_id
            LEFT JOIN milestone m2 ON m2.milestone_id = cv2.milestone_id
            WHERE p.project_id = @projectId
            ORDER BY cv.planned_start_date NULLS LAST, ph.rol
            """;
        var result = await conn.QueryAsync<PersonalHitoDto>(sql, new { projectId });
        return result.ToList();
    }

    private class FechaHitoRow
    {
        public int HitoId { get; set; }
        public DateOnly? Fecha { get; set; }
    }

    public async Task GuardarAsync(int projectId, List<PersonalHitoItemInputDto> items, int userId)
    {
        using var conn = Conn();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        var presupuestoId = await conn.QuerySingleOrDefaultAsync<int?>(
            """
            SELECT id FROM ss_presupuesto WHERE project_id = @projectId
            ORDER BY generado_en DESC LIMIT 1
            """, new { projectId }, tx);

        if (presupuestoId == null)
        {
            presupuestoId = await conn.QuerySingleAsync<int>(
                """
                INSERT INTO ss_presupuesto (project_id, version, estado, hh_usado, area_usada,
                    trabajadores_usados, total_estimado, generado_por, generado_en, notas)
                VALUES (@projectId, 1, 'BORRADOR', 0, 0, 0, 0, @userId, now(),
                    'Generado automáticamente para dotación de personal por hito')
                RETURNING id
                """, new { projectId, userId }, tx);
        }

        await conn.ExecuteAsync(
            "DELETE FROM ss_presupuesto_personal_hito WHERE presupuesto_id = @presupuestoId",
            new { presupuestoId }, tx);

        // Fechas del cronograma vigente — para recalcular Semanas cuando la fila trae un hito de salida.
        var fechasSql = $"""
            WITH {CronogramaVigenteCte}
            SELECT milestone_schedule_id AS HitoId, planned_start_date AS Fecha FROM cronograma_vigente
            """;
        var fechas = (await conn.QueryAsync<FechaHitoRow>(fechasSql, new { projectId }, tx))
            .ToDictionary(f => f.HitoId, f => f.Fecha);

        // La tarifa ("costo_mensual" en la columna, por compatibilidad de esquema) se calcula y se
        // carga en la práctica por SEMANA, igual que la planilla real (ss_hh_carga_linea) — no se
        // convierte a mensual. Total = cantidad × tarifa semanal × semanas, directo.
        var filas = items.Select(i =>
        {
            var semanas = i.Semanas;
            if (i.HitoSalidaId.HasValue
                && fechas.TryGetValue(i.HitoId, out var fechaIngreso) && fechaIngreso.HasValue
                && fechas.TryGetValue(i.HitoSalidaId.Value, out var fechaSalida) && fechaSalida.HasValue)
            {
                var dias = fechaSalida.Value.DayNumber - fechaIngreso.Value.DayNumber;
                semanas = Math.Max(0, dias / 7m);
            }
            return new
            {
                presupuestoId,
                i.HitoId,
                i.HitoSalidaId,
                i.Rol,
                i.Cantidad,
                Semanas = semanas,
                i.CostoMensual,
                Total = i.Cantidad * i.CostoMensual * semanas,
            };
        });

        await conn.ExecuteAsync(
            """
            INSERT INTO ss_presupuesto_personal_hito
                (presupuesto_id, hito_id, hito_salida_id, rol, cantidad, semanas, costo_mensual, total)
            VALUES (@presupuestoId, @HitoId, @HitoSalidaId, @Rol, @Cantidad, @Semanas, @CostoMensual, @Total)
            """, filas, tx);

        await PresupuestoTotalHelper.RecalcularTotalAsync(conn, presupuestoId.Value, tx);

        await tx.CommitAsync();
    }

    /// <summary>Estima el "S/ semana" de Oficial/Peón desde el pago REAL registrado en la planilla
    /// semanal de Horas Hombre (tabla ss_hh_carga_linea, la que llena "Subir Excel de Horas Hombre" en
    /// Cargar Consumos) — ahí sí hay plata real pagada por trabajador/semana/ocupación, a diferencia de
    /// ss_presupuesto_personal_hito que recién se empieza a usar y está vacío.
    /// La planilla real (verificado con datos de producción) NUNCA usa la palabra "Peón" — el nivel
    /// equivalente ahí es "Ayudante *" (Ayudante albañil/carpintero/etc., misma banda salarial).
    /// "Oficial" sí aparece literal ("Oficial albañil/carpintero/fierrero/agregados"). Se excluye
    /// "Operario *" a propósito: es un tercer nivel salarial más alto que no existe en esta matriz de
    /// solo 2 categorías, mezclarlo infllaría la sugerencia de "Oficial".
    /// Suma el pago semanal por trabajador (puede tener varias partidas la misma semana) y devuelve
    /// ese promedio TAL CUAL — Personal se carga y calcula por semana (no se convierte a mensual, para
    /// que coincida con cómo se arma el presupuesto acá). Solo mira las últimas ~12 semanas cargadas
    /// (de cualquier proyecto) — así siempre jala de los proyectos actuales/activos, no se diluye con
    /// data vieja de proyectos ya cerrados. Es un punto de partida sugerido, siempre editable.</summary>
    public async Task<PersonalTarifasSugeridasDto> ObtenerTarifasSugeridasAsync()
    {
        using var conn = Conn();
        const string sql = """
            WITH limite AS (
                SELECT MAX(anio * 52 + semana_num) AS max_semana FROM ss_hh_carga_linea WHERE activo = true
            ),
            base AS (
                SELECT
                    CASE WHEN ocupacion ILIKE '%OFICIAL%' THEN 'OFICIAL'
                         WHEN ocupacion ILIKE 'AYUDANTE%' THEN 'PEON' END AS categoria,
                    project_id, trabajador, anio, semana_num,
                    SUM(parcial) AS pago_semana
                FROM ss_hh_carga_linea, limite
                WHERE activo = true
                  AND parcial IS NOT NULL
                  AND (ocupacion ILIKE '%OFICIAL%' OR ocupacion ILIKE 'AYUDANTE%')
                  AND (anio * 52 + semana_num) >= limite.max_semana - 12
                GROUP BY categoria, project_id, trabajador, anio, semana_num
            )
            SELECT
                COALESCE((SELECT AVG(pago_semana) FROM base WHERE categoria = 'OFICIAL'), 0) AS Oficial,
                COALESCE((SELECT AVG(pago_semana) FROM base WHERE categoria = 'PEON'), 0) AS Peon
            """;
        var result = await conn.QuerySingleAsync<PersonalTarifasSugeridasDto>(sql);
        return result;
    }
}
