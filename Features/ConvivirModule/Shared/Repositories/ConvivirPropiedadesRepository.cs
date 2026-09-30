using Dapper;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Features.ConvivirModule.Shared.Dtos;
using Abril_Backend.Features.ConvivirModule.Shared.Interfaces;
using Abril_Backend.Infrastructure.Data;

namespace Abril_Backend.Features.ConvivirModule.Shared.Repositories
{
    public class ConvivirPropiedadesRepository : IConvivirPropiedadesRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ConvivirPropiedadesRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Cuatro sentencias en un solo viaje. «seleccion» ordena primero la propiedad pedida y
        /// después en el mismo orden que la lista (proyecto, torre, departamento), así que si la
        /// pedida no es suya cae en la primera, igual que <see cref="ConvivirContextoDto.Seleccionada"/>.
        /// Las fechas (<c>date</c>) salen como timestamp para que Dapper las lea como DateTime sin
        /// depender del mapeo por defecto de Npgsql.
        ///
        /// El avance sale del Cronograma de Hitos: la versión vigente es la más reciente activa
        /// (mismo criterio que MilestoneScheduleRepository.GetFaltantesAsync) y los hitos para
        /// propietarios son <c>owner_milestone</c>, cada uno atado a un hito interno. El cronograma
        /// es por proyecto: la torre del propietario no lo cambia.
        ///
        /// Con <paramref name="conNotificaciones"/> (Inicio), el mismo viaje suma dos sentencias:
        /// antes, crea los avisos que falten; al final, cuenta los no leídos de la campana
        /// (<see cref="ConvivirNotificacionesSql"/>).
        /// </summary>
        public async Task<ConvivirContextoDto> GetContexto(int userId, int? propietarioId, bool conNotificaciones)
        {
            const string cte = """
                WITH seleccion AS (
                    SELECT pr.propietario_id, pr.project_id
                    FROM propietario pr
                    JOIN person p   ON p.person_id = pr.person_id AND p.state
                    JOIN project pj ON pj.project_id = pr.project_id
                    WHERE pr.state AND p.user_id = @UserId
                    ORDER BY (pr.propietario_id = @PropietarioId) DESC,
                             pj.project_description, pr.torre NULLS FIRST, pr.departamento, pr.propietario_id
                    LIMIT 1
                ),
                version AS (
                    SELECT h.milestone_schedule_history_id
                    FROM milestone_schedule_history h
                    JOIN seleccion s ON s.project_id = h.project_id
                    WHERE h.active AND h.state
                    ORDER BY h.created_date_time DESC
                    LIMIT 1
                )
                """;

            var sql = """
                SELECT coalesce(p.first_names, p.full_name)
                FROM person p
                WHERE p.user_id = @UserId AND p.state
                ORDER BY p.person_id DESC
                LIMIT 1;

                SELECT pr.propietario_id,
                       pr.project_id,
                       pj.project_description AS proyecto,
                       pr.torre,
                       pr.departamento
                FROM propietario pr
                JOIN person p   ON p.person_id = pr.person_id AND p.state
                JOIN project pj ON pj.project_id = pr.project_id
                WHERE pr.state AND p.user_id = @UserId
                ORDER BY pj.project_description, pr.torre NULLS FIRST, pr.departamento, pr.propietario_id;
                """ + cte + """
                SELECT EXISTS (SELECT 1 FROM version) AS tiene_cronograma,
                       (SELECT pj.fin_obra::timestamp FROM project pj JOIN seleccion s ON s.project_id = pj.project_id) AS fin_obra_proyecto,
                       (SELECT count(*)::int FROM propietario_documento d JOIN seleccion s ON s.propietario_id = d.propietario_id
                        WHERE d.state AND d.leido_date_time IS NULL) AS documentos_nuevos;
                """ + cte + """
                SELECT om.owner_milestone_order AS orden,
                       om.description           AS descripcion,
                       ms.milestone_schedule_id IS NOT NULL AS en_cronograma,
                       ms.planned_start_date::timestamp AS planned_start_date,
                       ms.planned_end_date::timestamp   AS planned_end_date,
                       ms.fecha_real_fin::timestamp     AS fecha_real_fin
                FROM owner_milestone om
                CROSS JOIN version v
                LEFT JOIN LATERAL (
                    SELECT x.milestone_schedule_id, x.planned_start_date, x.planned_end_date, x.fecha_real_fin
                    FROM milestone_schedule x
                    WHERE x.milestone_schedule_history_id = v.milestone_schedule_history_id
                      AND x.milestone_id = om.milestone_id
                      AND x.state
                    ORDER BY x.milestone_schedule_id DESC
                    LIMIT 1
                ) ms ON true
                WHERE om.state AND om.active
                ORDER BY om.owner_milestone_order;
                """;

            if (conNotificaciones)
            {
                sql = ConvivirNotificacionesSql.Generar + sql + ConvivirNotificacionesSql.Visibles + """
                    SELECT count(*)::int FROM visibles WHERE NOT leida;
                    """;
            }

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            using var multi = await conn.QueryMultipleAsync(sql, new
            {
                UserId = userId,
                PropietarioId = propietarioId ?? 0,
                TipoHito = ConvivirNotificacionTipo.Hito,
                TipoDocumento = ConvivirNotificacionTipo.Documento,
            });

            // Cuántos avisos creó: solo importa que se crearon antes de contarlos.
            if (conNotificaciones)
                await multi.ReadSingleAsync<int>();

            var contexto = new ConvivirContextoDto
            {
                Nombres = await multi.ReadSingleOrDefaultAsync<string?>(),
                Propiedades = (await multi.ReadAsync<ConvivirPropiedadDto>()).ToList(),
            };

            var cabecera = await multi.ReadSingleAsync<CabeceraFila>();
            contexto.TieneCronograma = cabecera.TieneCronograma;
            contexto.FinObraProyecto = cabecera.FinObraProyecto;
            contexto.DocumentosNuevos = cabecera.DocumentosNuevos;
            contexto.Hitos = (await multi.ReadAsync<ConvivirHitoFila>()).ToList();

            if (conNotificaciones)
                contexto.NotificacionesNuevas = await multi.ReadSingleAsync<int>();

            contexto.Seleccionada = contexto.Propiedades.FirstOrDefault(p => p.PropietarioId == propietarioId)
                ?? contexto.Propiedades.FirstOrDefault();

            return contexto;
        }
    }

    /// <summary>Fila de la tercera sentencia de <see cref="ConvivirPropiedadesRepository.GetContexto"/>.</summary>
    internal sealed class CabeceraFila
    {
        public bool TieneCronograma { get; set; }
        public DateTime? FinObraProyecto { get; set; }
        public int DocumentosNuevos { get; set; }
    }
}
