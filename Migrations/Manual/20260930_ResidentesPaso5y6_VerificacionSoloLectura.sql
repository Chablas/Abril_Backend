-- ============================================================================
-- Residentes — verificación de los Pasos 5b, 5c, 5d y 6 de PLAN-RESIDENTES.md
-- SOLO LECTURA: un único SELECT, no cambia nada. Se puede correr las veces que haga falta.
-- Fecha: 2026-09-30
--
-- Correrlo en PROD antes de hacer push de master con estos pasos (y en demo, si se quiere
-- comparar). En todos, la obra del residente pasa a salir de Configuración → Proyectos
-- (project.residente_workers_id, ResidenteQueries en el backend):
--
--   5b  Lecciones Aprendidas: obras que puede revisar el revisor residente (antes, user_project)
--       y las lecciones pendientes que revisa un residente.
--   5c  Evaluaciones 360°: en qué obra se evalúa a cada residente y de qué obra evalúa al staff
--       (antes, puesto de residente + vinculación). El Jefe SSOMA sigue por su vinculación.
--   5d  Penalidades: quién aprueba en el paso «Residente» (antes, cualquiera con la
--       funcionalidad aprobar-residente, en cualquier obra; ahora, solo el residente de la obra
--       con el rol RESIDENTE), los proyectos activos donde ya no se podrá registrar una (sin
--       residente con el rol, nadie la aprobaría) y las ya aprobadas por alguien que no era el
--       residente.
--   6   Lo que hará 20260930_ProjectResidentABitacora_PostDeploy.sql con cada fila de
--       project_resident (se copia, no se copia o ABORTA).
--
-- Cómo leerlo: una fila por persona, obra o penalidad. cambia = true es lo que va a cambiar al
-- desplegar (en el 6, lo que aborta el script).
-- ============================================================================

WITH obras AS (          -- ResidenteQueries.ObrasConSuResidente: obra, su residente y su usuario
  SELECT p.project_id, w.id AS worker_id, pe.person_id, u.user_id
  FROM project p
  JOIN project_tipo t ON t.project_tipo_id = p.project_tipo_id AND t.es_obra
  JOIN workers w ON w.id = p.residente_workers_id AND w.state
  JOIN person pe ON pe.person_id = w.person_id
  JOIN app_user u ON u.user_id = pe.user_id AND u.state
  WHERE p.active AND p.state AND p.project_ciclo_vida_id = 1
    AND EXISTS (SELECT 1 FROM user_role ur WHERE ur.user_id = u.user_id AND ur.role_id = 5 AND ur.state)
), del_residente AS (    -- ResidenteQueries.ProyectosDelResidente (usuario, proyecto)
  SELECT pe.user_id, p.project_id FROM project p
  JOIN workers w ON w.id = p.residente_workers_id AND w.state
  JOIN person pe ON pe.person_id = w.person_id
  WHERE pe.user_id IS NOT NULL
),

-- 5b: revisores de lecciones con puesto de residente (la categoría sigue decidiendo quién se acota)
revisores_residentes AS (
  SELECT DISTINCT pe.user_id FROM workers w
  JOIN person pe ON pe.person_id = w.person_id
  JOIN puesto pu ON pu.puesto_id = w.puesto_id
  WHERE w.state AND pu.categoria_id = 8 AND pe.user_id IS NOT NULL
), lecciones_antes AS (
  SELECT DISTINCT pe.user_id, up.project_id FROM user_project up
  JOIN workers w ON w.id = up.worker_id AND w.state
  JOIN person pe ON pe.person_id = w.person_id
  WHERE up.state AND up.active
), lecciones_pendientes AS (
  SELECT DISTINCT l.lesson_id, l.project_id, jp.user_id AS revisor_user_id
  FROM lesson l
  JOIN person ap ON ap.user_id = l.created_user_id
  JOIN workers aw ON aw.person_id = ap.person_id AND aw.state AND aw.worker_lesson_jefe_id IS NOT NULL
  JOIN workers jw ON jw.id = aw.worker_lesson_jefe_id AND jw.state
  JOIN person jp ON jp.person_id = jw.person_id AND jp.user_id IS NOT NULL
  WHERE l.state AND l.approval_status = 'PENDIENTE'
),

-- 5c: a quién se evalúa y en qué obra (con los mismos resguardos: Casa, no retirado, usuario activo)
evaluado_antes AS (
  SELECT DISTINCT p.user_id, pr.project_id
  FROM workers w
  JOIN person p ON p.person_id = w.person_id
  JOIN app_user u ON u.user_id = p.user_id
  JOIN puesto pu ON pu.puesto_id = w.puesto_id
  JOIN project pr ON pr.project_id = (SELECT wv.proyecto_id FROM worker_vinculaciones wv
                                       WHERE wv.worker_id = w.id AND wv.fecha_fin IS NULL
                                       ORDER BY wv.fecha_inicio DESC LIMIT 1)
  WHERE w.state AND pu.categoria_id = 8 AND w.contrata_casa = 'Casa' AND w.workers_estado_id IN (1, 3) AND u.active
), evaluado_ahora AS (
  SELECT DISTINCT o.user_id, o.project_id FROM obras o
  JOIN workers w ON w.id = o.worker_id
  JOIN app_user u ON u.user_id = o.user_id
  WHERE w.contrata_casa = 'Casa' AND w.workers_estado_id IN (1, 3) AND u.active
),

-- 5c: de qué obra evalúa al staff (antes por correo: puesto de residente o Jefe SSOMA + vinculación)
evalua_staff_antes AS (
  SELECT DISTINCT au.user_id, wv.proyecto_id AS project_id
  FROM app_user au
  JOIN workers w ON lower(w.email_corporativo) = lower(au.email)
  JOIN puesto pu ON pu.puesto_id = w.puesto_id
  JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
  WHERE w.state AND w.contrata_casa = 'Casa' AND w.workers_estado_id = 1
    AND (pu.categoria_id = 8 OR w.puesto_id = 189)
), evalua_staff_ahora AS (
  SELECT o.user_id, o.project_id FROM obras o
  UNION
  SELECT au.user_id, wv.proyecto_id
  FROM app_user au
  JOIN workers w ON lower(w.email_corporativo) = lower(au.email)
  JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
  WHERE w.state AND w.contrata_casa = 'Casa' AND w.workers_estado_id = 1 AND w.puesto_id = 189
    AND au.user_id NOT IN (SELECT user_id FROM obras)
),

-- 5d: el residente de cada proyecto que puede actuar como tal (ResidenteQueries.SuResidenteConRol:
--     usuario vigente con el rol RESIDENTE, sin mirar tipo ni ciclo de vida del proyecto)
con_rol AS (
  SELECT p.project_id, u.user_id FROM project p
  JOIN workers w ON w.id = p.residente_workers_id AND w.state
  JOIN person pe ON pe.person_id = w.person_id
  JOIN app_user u ON u.user_id = pe.user_id AND u.state
  WHERE EXISTS (SELECT 1 FROM user_role ur WHERE ur.user_id = u.user_id AND ur.role_id = 5 AND ur.state)
),

-- 5d: quiénes tienen la funcionalidad aprobar-residente
aprobador_feature AS (
  SELECT DISTINCT ur.user_id
  FROM feature f
  JOIN role_feature rf ON rf.feature_id = f.feature_id
  JOIN user_role ur ON ur.role_id = rf.role_id AND ur.state
  JOIN app_user u ON u.user_id = ur.user_id AND u.state AND u.active
  WHERE f.feature_key = 'ssoma.gestion.penalidades.aprobar-residente'
),

-- 6: cada fila de project_resident con la ficha del residente antes del primer cambio de la bitácora
tabla_vieja AS (
  SELECT pr.project_resident_id, pr.project_id, p.project_description, pr.user_id, pr.active, pr.state,
         pr.created_date_time,
         CASE WHEN h.id IS NOT NULL THEN h.workers_id_anterior ELSE p.residente_workers_id END AS ficha
  FROM project_resident pr
  JOIN project p ON p.project_id = pr.project_id
  LEFT JOIN LATERAL (
      SELECT h.id, h.workers_id_anterior FROM project_residente_historial h
      WHERE h.project_id = p.project_id
      ORDER BY h.cambio_date_time, h.id LIMIT 1) h ON true
),

filas AS (
  SELECT '5b Lecciones: obras del revisor residente' AS paso, u.email AS item,
         (SELECT string_agg(p.project_description, ', ' ORDER BY p.project_description)
            FROM lecciones_antes a JOIN project p ON p.project_id = a.project_id WHERE a.user_id = r.user_id) AS antes,
         (SELECT string_agg(p.project_description, ', ' ORDER BY p.project_description)
            FROM del_residente d JOIN project p ON p.project_id = d.project_id WHERE d.user_id = r.user_id) AS ahora
  FROM revisores_residentes r JOIN app_user u ON u.user_id = r.user_id

  UNION ALL
  SELECT '5b Lecciones: pendientes que revisa un residente',
         u.email || ' · ' || coalesce(p.project_description, '(sin proyecto)') || ' · ' || count(*) || ' pendiente(s)',
         CASE WHEN bool_and(EXISTS (SELECT 1 FROM lecciones_antes a WHERE a.user_id = lp.revisor_user_id AND a.project_id = lp.project_id))
              THEN 'puede revisar' ELSE 'NO puede revisar' END,
         CASE WHEN bool_and(EXISTS (SELECT 1 FROM del_residente d WHERE d.user_id = lp.revisor_user_id AND d.project_id = lp.project_id))
              THEN 'puede revisar' ELSE 'NO puede revisar' END
  FROM lecciones_pendientes lp
  JOIN app_user u ON u.user_id = lp.revisor_user_id
  LEFT JOIN project p ON p.project_id = lp.project_id
  WHERE lp.revisor_user_id IN (SELECT user_id FROM revisores_residentes)
  GROUP BY u.email, p.project_description

  UNION ALL
  SELECT '5c Evaluaciones: obra en que se evalúa al residente', u.email,
         (SELECT string_agg(p.project_description, ', ' ORDER BY p.project_description)
            FROM evaluado_antes a JOIN project p ON p.project_id = a.project_id WHERE a.user_id = x.user_id),
         (SELECT string_agg(p.project_description, ', ' ORDER BY p.project_description)
            FROM evaluado_ahora a JOIN project p ON p.project_id = a.project_id WHERE a.user_id = x.user_id)
  FROM (SELECT user_id FROM evaluado_antes UNION SELECT user_id FROM evaluado_ahora) x
  JOIN app_user u ON u.user_id = x.user_id

  UNION ALL
  SELECT '5c Evaluaciones: obra cuyo staff evalúa', u.email,
         (SELECT string_agg(p.project_description, ', ' ORDER BY p.project_description)
            FROM evalua_staff_antes a JOIN project p ON p.project_id = a.project_id WHERE a.user_id = x.user_id),
         (SELECT string_agg(p.project_description, ', ' ORDER BY p.project_description)
            FROM evalua_staff_ahora a JOIN project p ON p.project_id = a.project_id WHERE a.user_id = x.user_id)
  FROM (SELECT user_id FROM evalua_staff_antes UNION SELECT user_id FROM evalua_staff_ahora) x
  JOIN app_user u ON u.user_id = x.user_id

  UNION ALL
  SELECT '5d Penalidades: pendientes de aprobar por el residente',
         sp.codigo || ' · ' || p.project_description,
         (SELECT count(*) FROM aprobador_feature) || ' usuario(s) con aprobar-residente, en cualquier obra',
         coalesce((SELECT string_agg(u.email, ', ') FROM con_rol c JOIN app_user u ON u.user_id = c.user_id
                    WHERE c.project_id = sp.proyecto_id
                      AND c.user_id IN (SELECT user_id FROM aprobador_feature)),
                  'NADIE: el proyecto no tiene residente con el rol RESIDENTE y la funcionalidad')
  FROM ssoma_penalidad sp
  JOIN project p ON p.project_id = sp.proyecto_id
  WHERE sp.estado = 'PendienteResidente'

  UNION ALL
  -- Registrar exige que el proyecto tenga residente con el rol RESIDENTE (si no, nadie aprobaría)
  SELECT '5d Penalidades: proyectos activos donde ya no se podrá registrar',
         p.project_description || coalesce(' · residente sin el rol: ' || pe.full_name, ' · sin residente'),
         (SELECT count(*) FROM ssoma_penalidad sp WHERE sp.proyecto_id = p.project_id) || ' penalidad(es) registradas',
         'ya no se puede registrar'
  FROM project p
  LEFT JOIN workers w ON w.id = p.residente_workers_id AND w.state
  LEFT JOIN person pe ON pe.person_id = w.person_id
  WHERE p.state AND p.active AND p.project_ciclo_vida_id = 1
    AND p.project_id NOT IN (SELECT project_id FROM con_rol)

  UNION ALL
  SELECT '5d Penalidades: ya aprobadas en el paso Residente',
         sp.codigo || ' · ' || p.project_description || ' · ' || coalesce(pe.full_name, 'usuario ' || sp.aprobado_residente_por_id),
         'aprobó',
         CASE WHEN EXISTS (SELECT 1 FROM del_residente d
                           WHERE d.user_id = sp.aprobado_residente_por_id AND d.project_id = sp.proyecto_id)
              THEN 'aprobó' ELSE 'NO podría (no es el residente de la obra)' END
  FROM ssoma_penalidad sp
  JOIN project p ON p.project_id = sp.proyecto_id
  LEFT JOIN person pe ON pe.user_id = sp.aprobado_residente_por_id
  WHERE sp.aprobado_residente_por_id IS NOT NULL

  UNION ALL
  SELECT '6 project_resident → bitácora (PostDeploy)',
         t.project_description || ' · fila ' || t.project_resident_id || ' · ' || coalesce(pu.email, 'usuario ' || t.user_id),
         'desde ' || t.created_date_time::date,
         CASE WHEN NOT t.state THEN 'no se copia (dada de baja)'
              WHEN NOT t.active THEN 'ABORTA: fila inactiva'
              WHEN t.ficha IS NULL THEN 'ABORTA: el proyecto no tiene residente'
              WHEN pe.user_id IS DISTINCT FROM t.user_id THEN 'ABORTA: el residente del proyecto es otra persona'
              ELSE 'se copia como entrada de ' || coalesce(pe.full_name, 'la ficha ' || t.ficha) END
  FROM tabla_vieja t
  LEFT JOIN workers w ON w.id = t.ficha
  LEFT JOIN person pe ON pe.person_id = w.person_id
  LEFT JOIN app_user pu ON pu.user_id = t.user_id
)
SELECT paso, item, antes, ahora,
       CASE WHEN paso LIKE '6 %' THEN ahora LIKE 'ABORTA%'
            WHEN paso LIKE '5d Penalidades: pendientes%' OR paso LIKE '5d Penalidades: proyectos%' THEN true
            ELSE antes IS DISTINCT FROM ahora END AS cambia
FROM filas
ORDER BY paso, cambia DESC, item;
