using Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Application.Dtos;
using Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AuthModule.Shared.Dtos;
using Abril_Backend.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Infrastructure.Repositories
{
    public class FuncionalidadesRepository : IFuncionalidadesRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public FuncionalidadesRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        /// <summary>
        /// El catálogo entero con sus conteos en una sola consulta. Son pocas filas y la pantalla
        /// filtra y pagina en memoria: el nombre por el que se busca (el del sidebar) vive en el
        /// frontend, no en la base.
        /// </summary>
        public async Task<List<FuncionalidadListItemDto>> List()
        {
            using var ctx = _factory.CreateDbContext();

            const string sql = """
                SELECT f.feature_id,
                       f.feature_key,
                       f.module_id,
                       m.module_name,
                       COUNT(DISTINCT r.role_id)::int AS roles_count,
                       COUNT(DISTINCT u.user_id)::int AS users_count
                FROM feature f
                LEFT JOIN module m        ON m.module_id   = f.module_id
                LEFT JOIN role_feature rf ON rf.feature_id = f.feature_id
                LEFT JOIN role r          ON r.role_id     = rf.role_id AND r.state
                LEFT JOIN user_role ur    ON ur.role_id    = r.role_id  AND ur.state
                LEFT JOIN app_user u      ON u.user_id     = ur.user_id AND u.state
                GROUP BY f.feature_id, f.feature_key, f.module_id, m.module_name
                ORDER BY m.module_name NULLS LAST, f.feature_key
                """;

            var rows = await ctx.Database.GetDbConnection().QueryAsync<FuncionalidadListItemDto>(sql);
            return rows.ToList();
        }

        /// <summary>
        /// Cabecera, roles y usuarios en un solo viaje a la base. Los usuarios llegan como una fila
        /// por usuario y rol que le da el acceso: quien la recibe por dos roles sale dos veces y se
        /// junta acá, con los dos roles en <see cref="AccessUserDto.ViaRoles"/>.
        /// </summary>
        public async Task<FuncionalidadDetalleDto?> GetDetalle(int featureId)
        {
            using var ctx = _factory.CreateDbContext();

            const string sql = """
                SELECT f.feature_id, f.feature_key, f.module_id, m.module_name
                FROM feature f
                LEFT JOIN module m ON m.module_id = f.module_id
                WHERE f.feature_id = @featureId;

                SELECT r.role_id,
                       r.role_description,
                       (SELECT COUNT(*)::int
                          FROM user_role x
                          JOIN app_user xu ON xu.user_id = x.user_id AND xu.state
                         WHERE x.role_id = r.role_id AND x.state) AS users_count,
                       (SELECT COUNT(*)::int
                          FROM role_feature x
                         WHERE x.role_id = r.role_id) AS features_count
                FROM role_feature rf
                JOIN role r ON r.role_id = rf.role_id AND r.state
                WHERE rf.feature_id = @featureId
                ORDER BY r.role_description;

                SELECT u.user_id,
                       u.email,
                       u.active,
                       p.full_name AS display_name,
                       r.role_id,
                       r.role_description
                FROM role_feature rf
                JOIN role r       ON r.role_id  = rf.role_id AND r.state
                JOIN user_role ur ON ur.role_id = r.role_id  AND ur.state
                JOIN app_user u   ON u.user_id  = ur.user_id AND u.state
                LEFT JOIN LATERAL (
                    SELECT pe.full_name
                    FROM person pe
                    WHERE pe.user_id = u.user_id AND pe.state
                    ORDER BY pe.person_id DESC
                    LIMIT 1
                ) p ON true
                WHERE rf.feature_id = @featureId
                ORDER BY COALESCE(p.full_name, u.email), u.user_id, r.role_description;
                """;

            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();
            using var multi = await conn.QueryMultipleAsync(sql, new { featureId });

            var detalle = await multi.ReadSingleOrDefaultAsync<FuncionalidadDetalleDto>();
            if (detalle == null) return null;

            detalle.Roles = (await multi.ReadAsync<AccessRoleDto>()).ToList();
            detalle.Users = (await multi.ReadAsync<UsuarioRolRow>())
                .GroupBy(r => r.UserId)
                .Select(g => new AccessUserDto
                {
                    UserId      = g.Key,
                    Email       = g.First().Email,
                    DisplayName = g.First().DisplayName,
                    Active      = g.First().Active,
                    ViaRoles    = g.Select(r => new RoleRefDto { RoleId = r.RoleId, RoleDescription = r.RoleDescription }).ToList(),
                })
                .ToList();

            return detalle;
        }

        private sealed class UsuarioRolRow
        {
            public int UserId { get; set; }
            public string Email { get; set; } = null!;
            public string? DisplayName { get; set; }
            public bool Active { get; set; }
            public int RoleId { get; set; }
            public string RoleDescription { get; set; } = null!;
        }
    }
}
