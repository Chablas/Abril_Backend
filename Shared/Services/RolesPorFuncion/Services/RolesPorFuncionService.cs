using System.Data;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Realtime;
using Abril_Backend.Shared.Services.Jerarquia;
using Abril_Backend.Shared.Services.RolesPorFuncion.Interfaces;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services.RolesPorFuncion.Services
{
    /// <summary>
    /// Implementación de <see cref="IRolesPorFuncionService"/>. Las reglas están escritas UNA vez, en
    /// SQL, y <c>Migrations/Manual/20260929_GaRolesPorFuncion.sql</c> aplica las mismas sobre la data
    /// que ya existía: si se cambia una, se cambia en los dos.
    /// </summary>
    public class RolesPorFuncionService : IRolesPorFuncionService
    {
        private const string FeatureSalidas      = "gestion-administrativa.gestion-salidas";
        private const string FeatureRendiciones  = "gestion-administrativa.gestion-rendiciones";
        private const string FeatureConsolidados = "gestion-administrativa.consolidados";

        /// <summary>
        /// La jefatura de una categoría. El orden de la lista es la precedencia cuando la persona tiene
        /// fichas de varias: manda la primera.
        /// </summary>
        private static readonly (int CategoriaId, int RoleId)[] JefaturaPorCategoria =
        {
            (CategoriaIds.Gerente,                       int.Parse(Roles.Gerente)),
            (CategoriaIds.GerenteGeneral,                int.Parse(Roles.Gerente)),
            (CategoriaIds.GerenteAdministracionFinanzas, int.Parse(Roles.Gerente)),
            (CategoriaIds.SubGerente,                    int.Parse(Roles.SubGerente)),
            (CategoriaIds.Jefe,                          int.Parse(Roles.Jefe)),
            (CategoriaIds.Residente,                     int.Parse(Roles.Residente)),
        };

        /// <summary>
        /// Los que el puesto da al crear la cuenta. GERENTE GENERAL y GERENTE DE ADMINISTRACIÓN Y
        /// FINANZAS no: el algoritmo no los pone a aprobar nada (una gerencia la manda su GERENTE) y
        /// tampoco verían nada en las bandejas. Si se los asigna a mano como aprobadores, reciben
        /// GERENTE por esa vía. TESORERO sí va por la categoría del puesto: Reembolsos es de quien
        /// es tesorero(a).
        /// </summary>
        private static readonly (int CategoriaId, int RoleId)[] RolPorPuesto =
        {
            (CategoriaIds.Gerente,    int.Parse(Roles.Gerente)),
            (CategoriaIds.SubGerente, int.Parse(Roles.SubGerente)),
            (CategoriaIds.Jefe,       int.Parse(Roles.Jefe)),
            (CategoriaIds.Residente,  int.Parse(Roles.Residente)),
            (CategoriaIds.Tesorero,   int.Parse(Roles.Tesorero)),
        };

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IRealtimeNotifier _notifier;
        private readonly ILogger<RolesPorFuncionService> _logger;

        public RolesPorFuncionService(
            IDbContextFactory<AppDbContext> factory,
            IRealtimeNotifier notifier,
            ILogger<RolesPorFuncionService> logger)
        {
            _factory  = factory;
            _notifier = notifier;
            _logger   = logger;
        }

        public async Task<List<RoleSimpleDTO>> AsignarAlCrearCuentaAsync(int userId)
        {
            try
            {
                using var ctx = _factory.CreateDbContext();
                var conn = ctx.Database.GetDbConnection();
                await conn.OpenAsync();

                using (var tx = await conn.BeginTransactionAsync())
                {
                    await AsignarPorPuestoAsync(conn, tx, userId);
                    await AgregarJefaturaAAprobadoresAsync(conn, tx, userId, soloUserId: userId);
                    await RecalcularConsolidadoresAsync(conn, tx, userId, soloUserId: userId);
                    await tx.CommitAsync();
                }

                var roles = await conn.QueryAsync<RoleSimpleDTO>("""
                    SELECT r.role_id AS RoleId, r.role_description AS RoleDescription
                    FROM user_role ur
                    JOIN role r ON r.role_id = ur.role_id
                    WHERE ur.user_id = @userId AND ur.state AND r.state
                    """, new { userId });
                return roles.ToList();
            }
            catch (Exception ex)
            {
                // El usuario entra igual con lo que ya tenía: los roles se pueden dar desde Seguridad.
                _logger.LogError(ex, "RolesPorFuncion: no se pudieron asignar los roles del usuario {UserId}", userId);
                return new List<RoleSimpleDTO>();
            }
        }

        public async Task SincronizarAsync(int? actorUserId = null)
        {
            var cambiaron = new HashSet<int>();
            try
            {
                using var ctx = _factory.CreateDbContext();
                var conn = ctx.Database.GetDbConnection();
                await conn.OpenAsync();
                using var tx = await conn.BeginTransactionAsync();

                // La jefatura primero: a quien la recibe acá ya no le hace falta CONSOLIDADOR.
                cambiaron.UnionWith(await AgregarJefaturaAAprobadoresAsync(conn, tx, actorUserId));
                cambiaron.UnionWith(await RecalcularConsolidadoresAsync(conn, tx, actorUserId));

                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RolesPorFuncion: falló la sincronización de roles (guardó el usuario {UserId})", actorUserId);
                return;
            }

            if (cambiaron.Count == 0) return;
            try
            {
                await _notifier.NotifyRoleFeaturesChanged(cambiaron);
            }
            catch (Exception ex)
            {
                // Sin el aviso se enteran en su próximo refresh de token.
                _logger.LogWarning(ex, "RolesPorFuncion: no se pudo avisar a {Cantidad} usuarios", cambiaron.Count);
            }
        }

        // ══ Reglas ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Los roles del puesto de las fichas vivas de la persona, y ADMINISTRADOR DE OBRA si
        /// administra una obra activa (el mismo criterio con el que el algoritmo la trata como tal:
        /// un proyecto de tipo PROYECTO, ver <c>ObrasLoader.Obras</c>).
        /// </summary>
        private static async Task AsignarPorPuestoAsync(IDbConnection conn, IDbTransaction tx, int userId)
        {
            await conn.ExecuteAsync("""
                INSERT INTO user_role (user_id, role_id, created_date_time, created_user_id, active, state)
                SELECT @userId, x.role_id, now(), @userId, true, true
                FROM (
                    SELECT m.role_id
                    FROM person p
                    JOIN workers w ON w.person_id = p.person_id
                                  AND w.state
                                  AND w.workers_estado_id = ANY(@adentro)
                    JOIN puesto pu ON pu.puesto_id = w.puesto_id
                    JOIN unnest(@categorias, @rolesCategoria) AS m(categoria_id, role_id)
                         ON m.categoria_id = pu.categoria_id
                    WHERE p.user_id = @userId
                    UNION
                    SELECT @rolAdministradorObra
                    FROM project pr
                    JOIN workers w ON w.id = pr.workers_coord_admin_id
                                  AND w.state
                                  AND w.workers_estado_id = ANY(@adentro)
                    JOIN person p ON p.person_id = w.person_id
                    WHERE p.user_id = @userId
                      AND pr.state AND pr.active
                      AND pr.project_tipo_id = @tipoObra
                ) x
                JOIN role r ON r.role_id = x.role_id AND r.state
                ON CONFLICT (user_id, role_id) DO UPDATE
                    SET state = true, active = true, updated_date_time = now(), updated_user_id = EXCLUDED.created_user_id
                    WHERE NOT user_role.state
                """,
                new
                {
                    userId,
                    adentro              = WorkersEstadoIds.EstanAdentro,
                    categorias           = RolPorPuesto.Select(x => x.CategoriaId).ToArray(),
                    rolesCategoria       = RolPorPuesto.Select(x => x.RoleId).ToArray(),
                    rolAdministradorObra = int.Parse(Roles.AdministradorDeObra),
                    tipoObra             = ProjectTipoIds.Proyecto,
                },
                tx);
        }

        /// <summary>
        /// Quien figura A MANO como aprobador (salida, 1.ª revisión o consolidado, o residente de una
        /// obra) y no puede entrar a la bandeja donde actúa recibe el rol de la jefatura de su puesto,
        /// o JEFE si no tiene. Con <paramref name="soloUserId"/>, solo esa persona (primer login).
        /// </summary>
        private static async Task<IEnumerable<int>> AgregarJefaturaAAprobadoresAsync(
            IDbConnection conn, IDbTransaction tx, int? actorUserId, int? soloUserId = null)
        {
            return await conn.QueryAsync<int>($$"""
                WITH designados AS (
                    {{DesignadosSql("a.ga_actor_id = ANY(@aprobadores)", "r.ga_actor_id = ANY(@aprobadores)")}}
                    UNION
                    -- El residente de una obra (Configuración → Proyectos) aprueba las salidas de su staff.
                    SELECT p.user_id, @actorSalida
                    FROM project pr
                    JOIN workers w ON w.id = pr.residente_workers_id
                    JOIN person p  ON p.person_id = w.person_id
                    WHERE pr.state
                      AND pr.project_tipo_id = @tipoObra
                      AND p.user_id IS NOT NULL
                      AND lower(trim(w.email_corporativo)) LIKE @dominio
                ),
                faltan AS (
                    SELECT DISTINCT d.user_id
                    FROM designados d
                    JOIN app_user u ON u.user_id = d.user_id AND u.state
                    WHERE (@soloUserId IS NULL OR d.user_id = @soloUserId)
                      AND NOT EXISTS (
                            SELECT 1
                            FROM user_role ur
                            JOIN role ro         ON ro.role_id    = ur.role_id AND ro.state
                            JOIN role_feature rf ON rf.role_id    = ur.role_id
                            JOIN feature f       ON f.feature_id  = rf.feature_id
                            WHERE ur.user_id = d.user_id
                              AND ur.state
                              AND f.feature_key = CASE d.ga_actor_id
                                                      WHEN @actorSalida THEN @fSalidas
                                                      WHEN @actorPrimera THEN @fRendiciones
                                                      ELSE @fConsolidados
                                                  END)
                ),
                rol AS (
                    SELECT f.user_id,
                           COALESCE((
                               SELECT m.role_id
                               FROM person p
                               JOIN workers w ON w.person_id = p.person_id
                                             AND w.state
                                             AND w.workers_estado_id = ANY(@adentro)
                               JOIN puesto pu ON pu.puesto_id = w.puesto_id
                               JOIN unnest(@categorias, @rolesCategoria) WITH ORDINALITY AS m(categoria_id, role_id, orden)
                                    ON m.categoria_id = pu.categoria_id
                               WHERE p.user_id = f.user_id
                               ORDER BY m.orden
                               LIMIT 1), @rolJefe) AS role_id
                    FROM faltan f
                )
                INSERT INTO user_role (user_id, role_id, created_date_time, created_user_id, active, state)
                SELECT rol.user_id, rol.role_id, now(), COALESCE(@actorUserId, rol.user_id), true, true
                FROM rol
                JOIN role r ON r.role_id = rol.role_id AND r.state
                ON CONFLICT (user_id, role_id) DO UPDATE
                    SET state = true, active = true, updated_date_time = now(), updated_user_id = EXCLUDED.created_user_id
                    WHERE NOT user_role.state
                RETURNING user_id
                """,
                new
                {
                    actorUserId,
                    soloUserId,
                    aprobadores    = new[] { ActorIds.AprobadorSalida, ActorIds.AprobadorPrimeraRevision, ActorIds.AprobadorConsolidado },
                    actorSalida    = ActorIds.AprobadorSalida,
                    actorPrimera   = ActorIds.AprobadorPrimeraRevision,
                    fSalidas       = FeatureSalidas,
                    fRendiciones   = FeatureRendiciones,
                    fConsolidados  = FeatureConsolidados,
                    adentro        = WorkersEstadoIds.EstanAdentro,
                    categorias     = JefaturaPorCategoria.Select(x => x.CategoriaId).ToArray(),
                    rolesCategoria = JefaturaPorCategoria.Select(x => x.RoleId).ToArray(),
                    rolJefe        = int.Parse(Roles.Jefe),
                    dominio        = "%" + EstructuraAreaLoader.EmailDomainCorp,
                    tipoObra       = ProjectTipoIds.Proyecto,
                },
                tx);
        }

        /// <summary>
        /// CONSOLIDADOR exacto: lo tiene quien figura a mano como consolidador y no entra ya a las dos
        /// bandejas del consolidador (Gestión de Rendiciones y Consolidados) por otro rol. A quien no
        /// cumple eso se le quita — con DELETE, como Seguridad, porque el token no mira user_role.state.
        /// Sin la fila del rol (el SQL todavía no corrió) no hace nada. Con
        /// <paramref name="soloUserId"/>, solo esa persona (primer login).
        /// </summary>
        private static async Task<IEnumerable<int>> RecalcularConsolidadoresAsync(
            IDbConnection conn, IDbTransaction tx, int? actorUserId, int? soloUserId = null)
        {
            return await conn.QueryAsync<int>($$"""
                WITH designados AS (
                    {{DesignadosSql("a.ga_actor_id = @actorConsolidador", "r.ga_actor_id = @actorConsolidador")}}
                ),
                cubiertos AS (
                    SELECT ur.user_id
                    FROM user_role ur
                    JOIN role ro         ON ro.role_id   = ur.role_id AND ro.state
                    JOIN role_feature rf ON rf.role_id   = ur.role_id
                    JOIN feature f       ON f.feature_id = rf.feature_id
                    WHERE ur.state
                      AND ur.role_id <> @rolConsolidador
                      AND f.feature_key IN (@fRendiciones, @fConsolidados)
                    GROUP BY ur.user_id
                    HAVING count(DISTINCT f.feature_key) = 2
                ),
                objetivo AS (
                    SELECT DISTINCT d.user_id
                    FROM designados d
                    JOIN app_user u ON u.user_id = d.user_id AND u.state
                    WHERE (@soloUserId IS NULL OR d.user_id = @soloUserId)
                      AND NOT EXISTS (SELECT 1 FROM cubiertos c WHERE c.user_id = d.user_id)
                ),
                rol AS (
                    SELECT role_id FROM role WHERE role_id = @rolConsolidador AND state
                ),
                alta AS (
                    INSERT INTO user_role (user_id, role_id, created_date_time, created_user_id, active, state)
                    SELECT o.user_id, rol.role_id, now(), COALESCE(@actorUserId, o.user_id), true, true
                    FROM objetivo o
                    CROSS JOIN rol
                    ON CONFLICT (user_id, role_id) DO UPDATE
                        SET state = true, active = true, updated_date_time = now(), updated_user_id = EXCLUDED.created_user_id
                        WHERE NOT user_role.state
                    RETURNING user_id
                ),
                baja AS (
                    DELETE FROM user_role ur
                    WHERE ur.role_id = @rolConsolidador
                      AND (@soloUserId IS NULL OR ur.user_id = @soloUserId)
                      AND EXISTS (SELECT 1 FROM rol)
                      AND NOT EXISTS (SELECT 1 FROM objetivo o WHERE o.user_id = ur.user_id)
                    RETURNING ur.user_id
                )
                SELECT user_id FROM alta
                UNION
                SELECT user_id FROM baja
                """,
                new
                {
                    actorUserId,
                    soloUserId,
                    actorConsolidador = ActorIds.Consolidador,
                    rolConsolidador   = int.Parse(Roles.Consolidador),
                    fRendiciones      = FeatureRendiciones,
                    fConsolidados     = FeatureConsolidados,
                    dominio           = "%" + EstructuraAreaLoader.EmailDomainCorp,
                },
                tx);
        }

        /// <summary>
        /// Los usuarios de las personas asignadas a mano —filas vivas y activas, con correo corporativo,
        /// el mismo filtro con que el resolver las toma— en Revisores de Áreas y en las fichas, con el
        /// actor de cada una. No se mira el estado laboral: la designación es explícita.
        /// </summary>
        private static string DesignadosSql(string filtroArea, string filtroFicha) => $"""
            SELECT p.user_id, a.ga_actor_id
            FROM area_actor_asignacion a
            JOIN workers w ON w.id = a.worker_id
            JOIN person p  ON p.person_id = w.person_id
            WHERE a.state AND a.active AND {filtroArea}
              AND p.user_id IS NOT NULL
              AND lower(trim(w.email_corporativo)) LIKE @dominio
            UNION
            SELECT p.user_id, r.ga_actor_id
            FROM workers_actor_asignacion r
            JOIN workers w ON w.id = r.asignado_id
            JOIN person p  ON p.person_id = w.person_id
            WHERE r.state AND r.active AND {filtroFicha}
              AND p.user_id IS NOT NULL
              AND lower(trim(w.email_corporativo)) LIKE @dominio
            """;
    }
}
