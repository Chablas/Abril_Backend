-- ============================================================================
-- Residentes — verificación de los Pasos 4a a 4d de PLAN-RESIDENTES.md
-- SOLO LECTURA: un único SELECT, no cambia nada. Se puede correr las veces que haga falta.
-- Fecha: 2026-09-29
--
-- Correrlo en PROD antes de hacer push de master con los Pasos 4a–4d (y en demo, si se quiere
-- comparar). Compara lo que hoy sale de la tabla vieja project_resident con lo que va a salir del
-- residente de Configuración → Proyectos (ResidenteQueries en el backend):
--
--   4a  Cronograma de Hitos: tarjetas (y a quién le llega el recordatorio mensual).
--   4b  Control de respuesta de informes: obras de cada RESIDENTE.
--   4c  IVTs y Cuaderno de obra: filtros, «mis proyectos» del modal de subir y quién subió
--       desde junio (con el 4c solo puede subir el residente de la obra).
--   4d  Seguimiento y medición de residentes: una fila por obra, con su residente.
--
-- Cómo leerlo: una fila por obra, persona o subida. cambia = true es lo que va a cambiar al
-- desplegar; lo esperado en prod es que solo cambien filas de gente que ya no es residente.
-- ============================================================================

WITH excl AS (          -- obras excluidas de RESIDENTES en el filtro por funcionalidad (11)
  SELECT pf.project_id FROM proyecto_filtro pf WHERE pf.funcionalidad_id = 11 AND NOT pf.active
), universo AS (        -- ResidenteQueries.ObrasConResidente
  SELECT p.project_id FROM project p
  JOIN project_tipo t ON t.project_tipo_id = p.project_tipo_id AND t.es_obra
  JOIN workers w ON w.id = p.residente_workers_id AND w.state
  JOIN person pe ON pe.person_id = w.person_id
  JOIN app_user u ON u.user_id = pe.user_id AND u.state
  WHERE p.active AND p.state AND p.project_ciclo_vida_id = 1
    AND EXISTS (SELECT 1 FROM user_role ur WHERE ur.user_id = u.user_id AND ur.role_id = 5 AND ur.state)
), del_residente AS (   -- ResidenteQueries.ProyectosDelResidente (usuario, proyecto)
  SELECT pe.user_id, p.project_id FROM project p
  JOIN workers w ON w.id = p.residente_workers_id AND w.state
  JOIN person pe ON pe.person_id = w.person_id
  WHERE pe.user_id IS NOT NULL
), es_residente AS (    -- usuarios con el rol RESIDENTE
  SELECT ur.user_id FROM user_role ur WHERE ur.role_id = 5 AND ur.state
),

-- 4a: tarjetas del cronograma
tarjetas_antes AS (
  SELECT p.project_id FROM project p
  WHERE p.active AND p.state
    AND (EXISTS (SELECT 1 FROM project_resident pr WHERE pr.project_id = p.project_id AND pr.active AND pr.state)
         OR (p.residente_workers_id IS NOT NULL AND p.tiene_unidad_de_proyectos))
),

-- 4b: obras de cada RESIDENTE en Control de respuesta de informes
informes_antes AS (
  SELECT pr.user_id, p.project_description FROM project_resident pr JOIN project p ON p.project_id = pr.project_id
  WHERE pr.active AND pr.state AND p.active AND p.project_id NOT IN (SELECT project_id FROM excl)
), informes_ahora AS (
  SELECT d.user_id, p.project_description FROM del_residente d JOIN project p ON p.project_id = d.project_id
  WHERE p.active AND p.project_id NOT IN (SELECT project_id FROM excl)
),

-- 4c: filtros de IVTs y Cuaderno (GetProjectsDescription)
filtros_antes AS (
  SELECT DISTINCT p.project_id FROM project_resident pr JOIN project p ON p.project_id = pr.project_id
  WHERE pr.active AND pr.state AND p.active AND p.project_id NOT IN (SELECT project_id FROM excl)
), filtros_ahora AS (
  SELECT p.project_id FROM project p
  WHERE p.active AND p.project_id IN (SELECT project_id FROM universo)
    AND p.project_id NOT IN (SELECT project_id FROM excl)
),

-- 4c: «mis proyectos» del modal de subir (la consulta vieja no miraba pr.state ni pr.active;
--     la nueva exige el rol RESIDENTE)
mis_antes AS (
  SELECT pr.user_id, p.project_description FROM project_resident pr JOIN project p ON p.project_id = pr.project_id
  WHERE p.active AND p.project_id NOT IN (SELECT project_id FROM excl)
), mis_ahora AS (
  SELECT d.user_id, p.project_description FROM del_residente d JOIN project p ON p.project_id = d.project_id
  WHERE p.active AND p.project_id NOT IN (SELECT project_id FROM excl)
    AND d.user_id IN (SELECT user_id FROM es_residente)
),

-- 4d: filas del Seguimiento (obra y su residente, con usuario vigente y activo)
seguimiento_antes AS (
  SELECT p.project_id, pe.full_name FROM project_resident pr
  JOIN project p ON p.project_id = pr.project_id
  JOIN app_user u ON u.user_id = pr.user_id AND u.state AND u.active
  JOIN person pe ON pe.user_id = u.user_id
  WHERE pr.state AND pr.active AND p.active AND p.project_id NOT IN (SELECT project_id FROM excl)
), seguimiento_ahora AS (
  SELECT p.project_id, pe.full_name FROM project p
  JOIN workers w ON w.id = p.residente_workers_id AND w.state
  JOIN person pe ON pe.person_id = w.person_id
  JOIN app_user u ON u.user_id = pe.user_id AND u.state AND u.active
  WHERE p.project_id IN (SELECT project_id FROM filtros_ahora)
),

-- 4c: quién subió IVTs y cuadernos desde junio
subidas AS (
  SELECT 'IVT' AS tipo, x.project_id, x.created_user_id, x.created_date_time FROM ivt_control_pdf x WHERE x.state
  UNION ALL
  SELECT 'CUADERNO', x.project_id, x.created_user_id, x.created_date_time FROM construction_site_logbook_control x WHERE x.state
),

filas AS (
  SELECT '4a Cronograma: tarjetas' AS paso, p.project_description AS item,
         CASE WHEN p.project_id IN (SELECT project_id FROM tarjetas_antes) THEN 'sale' ELSE '-' END AS antes,
         CASE WHEN p.project_id IN (SELECT project_id FROM universo) THEN 'sale' ELSE '-' END AS ahora
  FROM project p
  WHERE p.project_id IN (SELECT project_id FROM tarjetas_antes UNION SELECT project_id FROM universo)

  UNION ALL
  SELECT '4b Informes: obras de cada RESIDENTE', u.email,
         (SELECT string_agg(a.project_description, ', ' ORDER BY a.project_description) FROM informes_antes a WHERE a.user_id = u.user_id),
         (SELECT string_agg(n.project_description, ', ' ORDER BY n.project_description) FROM informes_ahora n WHERE n.user_id = u.user_id)
  FROM app_user u
  WHERE u.user_id IN (SELECT user_id FROM es_residente)

  UNION ALL
  SELECT '4c IVTs y Cuaderno: filtros', p.project_description,
         CASE WHEN p.project_id IN (SELECT project_id FROM filtros_antes) THEN 'sale' ELSE '-' END,
         CASE WHEN p.project_id IN (SELECT project_id FROM filtros_ahora) THEN 'sale' ELSE '-' END
  FROM project p
  WHERE p.project_id IN (SELECT project_id FROM filtros_antes UNION SELECT project_id FROM filtros_ahora)

  UNION ALL
  SELECT '4c IVTs y Cuaderno: mis proyectos', u.email,
         (SELECT string_agg(a.project_description, ', ' ORDER BY a.project_description) FROM mis_antes a WHERE a.user_id = u.user_id),
         (SELECT string_agg(n.project_description, ', ' ORDER BY n.project_description) FROM mis_ahora n WHERE n.user_id = u.user_id)
  FROM app_user u
  WHERE u.user_id IN (SELECT user_id FROM mis_antes UNION SELECT user_id FROM mis_ahora)

  UNION ALL
  SELECT '4d Seguimiento: filas', p.project_description,
         (SELECT string_agg(a.full_name, ', ' ORDER BY a.full_name) FROM seguimiento_antes a WHERE a.project_id = p.project_id),
         (SELECT string_agg(n.full_name, ', ' ORDER BY n.full_name) FROM seguimiento_ahora n WHERE n.project_id = p.project_id)
  FROM project p
  WHERE p.project_id IN (SELECT project_id FROM seguimiento_antes UNION SELECT project_id FROM seguimiento_ahora)

  UNION ALL
  SELECT '4c IVTs y Cuaderno: quién subió desde junio',
         s.tipo || ' · ' || p.project_description || ' · ' || coalesce(pe.full_name, 'usuario ' || s.created_user_id),
         count(*) || ' archivo(s), el último el ' || max(s.created_date_time)::date,
         CASE WHEN EXISTS (SELECT 1 FROM del_residente d
                           WHERE d.user_id = s.created_user_id AND d.project_id = s.project_id)
                   AND s.created_user_id IN (SELECT user_id FROM es_residente)
              THEN 'puede seguir subiendo'
              ELSE 'NO podrá subir (no es el residente de la obra)' END
  FROM subidas s
  JOIN project p ON p.project_id = s.project_id
  LEFT JOIN person pe ON pe.user_id = s.created_user_id
  WHERE s.created_date_time >= '2026-06-01'
  GROUP BY s.tipo, s.project_id, s.created_user_id, p.project_description, pe.full_name
)
SELECT paso, item, antes, ahora,
       (antes IS DISTINCT FROM ahora AND paso NOT LIKE '%quién subió%')
       OR coalesce(ahora LIKE 'NO podrá%', false) AS cambia
FROM filas
ORDER BY paso, cambia DESC, item;
