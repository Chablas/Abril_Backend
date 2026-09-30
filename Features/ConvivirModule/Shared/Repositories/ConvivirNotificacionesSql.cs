namespace Abril_Backend.Features.ConvivirModule.Shared.Repositories
{
    /// <summary>
    /// SQL de la campana de la app (RF-05), compartido por Inicio (el número de la campana) y
    /// Notificaciones (la lista). Los dos crean antes los avisos que falten, así el número y la
    /// lista nunca discrepan. Parámetros: @UserId, @TipoHito y @TipoDocumento
    /// (<see cref="Dtos.ConvivirNotificacionTipo"/>).
    /// </summary>
    internal static class ConvivirNotificacionesSql
    {
        /// <summary>
        /// Crea los avisos que le falten al usuario. Una sentencia con resultado propio (cuántos
        /// creó), para que el lector de Dapper pase limpio a las siguientes:
        /// <list type="bullet">
        /// <item>HITO: cada owner_milestone culminado (con fecha real) en la versión vigente del
        /// cronograma de cada proyecto donde tiene propiedades (mismo criterio que
        /// <see cref="ConvivirPropiedadesRepository"/>), si lo culminaron desde el día en que tiene
        /// su primera propiedad en ese proyecto: lo anterior ya lo ve en Inicio como avance. El
        /// Cronograma de Hitos no avisa cuando se culmina un hito, por eso se detecta acá.</item>
        /// <item>DOCUMENTO: cada documento vigente de sus propiedades. Si ya lo abrió, el aviso nace
        /// leído.</item>
        /// </list>
        /// El NOT EXISTS evita gastar la secuencia en cada llamada; el ON CONFLICT cubre dos
        /// llamadas a la vez (los índices únicos parciales de la tabla).
        /// </summary>
        public const string Generar = """
            WITH propiedades AS (
                SELECT pr.person_id,
                       pr.project_id,
                       (min(pr.created_date_time) AT TIME ZONE 'America/Lima')::date AS desde
                FROM propietario pr
                JOIN person p ON p.person_id = pr.person_id AND p.state
                WHERE pr.state AND p.user_id = @UserId
                GROUP BY pr.person_id, pr.project_id
            ),
            hitos AS (
                INSERT INTO propietario_notificacion
                    (person_id, propietario_notificacion_tipo_id, project_id, owner_milestone_id)
                SELECT pp.person_id, t.propietario_notificacion_tipo_id, pp.project_id, om.owner_milestone_id
                FROM propiedades pp
                JOIN propietario_notificacion_tipo t ON t.codigo = @TipoHito AND t.state
                JOIN LATERAL (
                    SELECT h.milestone_schedule_history_id
                    FROM milestone_schedule_history h
                    WHERE h.project_id = pp.project_id AND h.active AND h.state
                    ORDER BY h.created_date_time DESC
                    LIMIT 1
                ) v ON true
                JOIN owner_milestone om ON om.state AND om.active
                JOIN LATERAL (
                    SELECT x.fecha_real_fin
                    FROM milestone_schedule x
                    WHERE x.milestone_schedule_history_id = v.milestone_schedule_history_id
                      AND x.milestone_id = om.milestone_id
                      AND x.state
                    ORDER BY x.milestone_schedule_id DESC
                    LIMIT 1
                ) ms ON ms.fecha_real_fin >= pp.desde
                WHERE NOT EXISTS (SELECT 1
                                  FROM propietario_notificacion n
                                  WHERE n.person_id = pp.person_id
                                    AND n.project_id = pp.project_id
                                    AND n.owner_milestone_id = om.owner_milestone_id
                                    AND n.state)
                ON CONFLICT DO NOTHING
                RETURNING 1
            ),
            documentos AS (
                INSERT INTO propietario_notificacion
                    (person_id, propietario_notificacion_tipo_id, propietario_documento_id, leida_date_time)
                SELECT pr.person_id, t.propietario_notificacion_tipo_id, d.propietario_documento_id, d.leido_date_time
                FROM propietario_documento d
                JOIN propietario pr ON pr.propietario_id = d.propietario_id AND pr.state
                JOIN person p       ON p.person_id = pr.person_id AND p.state
                JOIN propietario_notificacion_tipo t ON t.codigo = @TipoDocumento AND t.state
                WHERE d.state AND p.user_id = @UserId
                  AND NOT EXISTS (SELECT 1
                                  FROM propietario_notificacion n
                                  WHERE n.propietario_documento_id = d.propietario_documento_id
                                    AND n.state)
                ON CONFLICT DO NOTHING
                RETURNING 1
            )
            SELECT ((SELECT count(*) FROM hitos) + (SELECT count(*) FROM documentos))::int AS creadas;
            """;

        /// <summary>
        /// Los avisos que se muestran: los del usuario cuyo contenido sigue vigente. El de hito,
        /// mientras tenga una propiedad vigente en ese proyecto (se abre en la primera, en el
        /// orden de la lista de propiedades); el de documento, mientras el documento y su
        /// propiedad sigan vigentes. «fecha» es cuando apareció el contenido: cuando la app detectó
        /// el hito, cuando la intranet subió el documento. Va seguido de un SELECT sobre «visibles».
        /// </summary>
        public const string Visibles = """
            WITH visibles AS (
                SELECT n.propietario_notificacion_id AS notificacion_id,
                       t.codigo                      AS tipo,
                       n.created_date_time           AS fecha,
                       n.leida_date_time IS NOT NULL AS leida,
                       pr.propietario_id,
                       pj.project_description        AS proyecto,
                       NULL::text                    AS torre,
                       NULL::text                    AS departamento,
                       om.description                AS contenido,
                       NULL::text                    AS contenido_tipo
                FROM propietario_notificacion n
                JOIN person p                        ON p.person_id = n.person_id AND p.state
                JOIN propietario_notificacion_tipo t ON t.propietario_notificacion_tipo_id = n.propietario_notificacion_tipo_id
                JOIN owner_milestone om              ON om.owner_milestone_id = n.owner_milestone_id
                JOIN project pj                      ON pj.project_id = n.project_id
                JOIN LATERAL (
                    SELECT x.propietario_id
                    FROM propietario x
                    WHERE x.person_id = n.person_id AND x.project_id = n.project_id AND x.state
                    ORDER BY x.torre NULLS FIRST, x.departamento, x.propietario_id
                    LIMIT 1
                ) pr ON true
                WHERE n.state AND p.user_id = @UserId

                UNION ALL

                SELECT n.propietario_notificacion_id,
                       t.codigo,
                       d.created_date_time,
                       n.leida_date_time IS NOT NULL,
                       pr.propietario_id,
                       pj.project_description,
                       pr.torre,
                       pr.departamento,
                       d.nombre,
                       dt.nombre
                FROM propietario_notificacion n
                JOIN person p                        ON p.person_id = n.person_id AND p.state
                JOIN propietario_notificacion_tipo t ON t.propietario_notificacion_tipo_id = n.propietario_notificacion_tipo_id
                JOIN propietario_documento d         ON d.propietario_documento_id = n.propietario_documento_id AND d.state
                JOIN propietario_documento_tipo dt   ON dt.propietario_documento_tipo_id = d.propietario_documento_tipo_id
                JOIN propietario pr                  ON pr.propietario_id = d.propietario_id AND pr.state
                JOIN project pj                      ON pj.project_id = pr.project_id
                WHERE n.state AND p.user_id = @UserId
            )
            """;
    }
}
