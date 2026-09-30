using Dapper;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Application.Dtos;
using Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConvivirModule.Shared.Dtos;
using Abril_Backend.Features.ConvivirModule.Shared.Repositories;
using Abril_Backend.Infrastructure.Data;

namespace Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Infrastructure.Repositories
{
    public class ConvivirNotificacionesRepository : IConvivirNotificacionesRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ConvivirNotificacionesRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Tres sentencias en un viaje: crear los que falten, la lista y el resumen. Las fechas salen
        /// en hora de Perú (<c>AT TIME ZONE</c>), sin depender del timezone de la sesión.
        /// </summary>
        public async Task<ConvivirNotificacionesFilas> GetNotificaciones(int userId, int limite)
        {
            const string sql = ConvivirNotificacionesSql.Generar + ConvivirNotificacionesSql.Visibles + """
                SELECT notificacion_id,
                       tipo,
                       fecha AT TIME ZONE 'America/Lima' AS fecha,
                       leida,
                       propietario_id,
                       proyecto,
                       torre,
                       departamento,
                       contenido,
                       contenido_tipo
                FROM visibles
                ORDER BY visibles.fecha DESC, visibles.notificacion_id DESC
                LIMIT @Limite;
                """ + ConvivirNotificacionesSql.Visibles + """
                SELECT (SELECT count(*) FROM visibles WHERE NOT leida)::int AS no_leidas,
                       (SELECT count(*)
                        FROM propietario pr
                        JOIN person p ON p.person_id = pr.person_id AND p.state
                        WHERE pr.state AND p.user_id = @UserId) > 1 AS varias_propiedades;
                """;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            using var multi = await conn.QueryMultipleAsync(sql, new
            {
                UserId = userId,
                Limite = limite,
                TipoHito = ConvivirNotificacionTipo.Hito,
                TipoDocumento = ConvivirNotificacionTipo.Documento,
            });

            // Cuántos avisos creó: solo importa que se crearon antes de listarlos.
            await multi.ReadSingleAsync<int>();

            var notificaciones = (await multi.ReadAsync<ConvivirNotificacionFila>()).ToList();
            var resumen = await multi.ReadSingleAsync<ResumenFila>();

            return new ConvivirNotificacionesFilas
            {
                Notificaciones = notificaciones,
                NoLeidas = resumen.NoLeidas,
                VariasPropiedades = resumen.VariasPropiedades,
            };
        }

        public async Task MarcarLeida(int userId, int notificacionId)
        {
            // No toca updated_*: leerla no es una edición.
            const string sql = """
                UPDATE propietario_notificacion n
                SET leida_date_time = now()
                FROM person p
                WHERE p.person_id = n.person_id AND p.state AND p.user_id = @UserId
                  AND n.propietario_notificacion_id = @NotificacionId
                  AND n.state AND n.leida_date_time IS NULL;
                """;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.ExecuteAsync(sql, new { UserId = userId, NotificacionId = notificacionId });
        }

        public async Task MarcarTodasLeidas(int userId)
        {
            const string sql = """
                UPDATE propietario_notificacion n
                SET leida_date_time = now()
                FROM person p
                WHERE p.person_id = n.person_id AND p.state AND p.user_id = @UserId
                  AND n.state AND n.leida_date_time IS NULL;
                """;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.ExecuteAsync(sql, new { UserId = userId });
        }

        private sealed class ResumenFila
        {
            public int NoLeidas { get; set; }
            public bool VariasPropiedades { get; set; }
        }
    }
}
