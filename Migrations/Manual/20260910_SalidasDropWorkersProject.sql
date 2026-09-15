-- ============================================================================
-- Salidas: el revisor por proyecto pasa a leerse de worker_vinculaciones
-- Fecha: 2026-09-10
--
-- ga_salidas_workers_project (creada por 20260715_SalidasRevisorPorProyecto.sql)
-- duplicaba un dato que ya vive en worker_vinculaciones.proyecto_id: la obra del
-- trabajador. Nunca tuvo pantalla ni punto de escritura, asi que quedo VACIA y
-- JefeRevisorResolver nunca supo el proyecto de nadie -> todos los trabajadores de
-- un area marcada "filtrar por proyecto" caian al revisor de area (project_id NULL)
-- en vez del revisor de su obra.
--
-- El backend ya no la lee: el proyecto sale de la vinculacion vigente
-- (worker_vinculaciones con fecha_fin NULL), la misma que mantiene GTH con
-- "Cambiar obra / puesto de trabajo" y la misma que la ficha del trabajador usa
-- para previsualizar su revisor. No hay backfill: el revisor se resuelve en vivo.
--
-- Se dropea (hard delete) y no se marca state = false porque la tabla no tiene ni
-- una fila: no hay historico que auditar. La guarda de abajo aborta el script si
-- alguien la lleno entre este analisis y la ejecucion.
--
-- IMPORTANTE: correr DESPUES de desplegar el backend que ya no la lee.
-- ============================================================================

BEGIN;

-- Guarda: si la tabla tiene datos, alguien la empezo a usar y este drop perderia
-- informacion. Aborta sin tocar nada.
DO $$
DECLARE
    v_filas bigint;
BEGIN
    IF to_regclass('public.ga_salidas_workers_project') IS NULL THEN
        RAISE NOTICE 'ga_salidas_workers_project no existe: nada que dropear.';
        RETURN;
    END IF;

    EXECUTE 'SELECT count(*) FROM ga_salidas_workers_project' INTO v_filas;

    IF v_filas > 0 THEN
        RAISE EXCEPTION
            'ga_salidas_workers_project tiene % fila(s): se esperaba vacia. Revisar antes de dropear.',
            v_filas;
    END IF;
END $$;

-- Los indices (ux_ga_salidas_workers_project_vivo, ix_ga_salidas_workers_project_proyecto)
-- y la FK a workers/project caen con la tabla.
DROP TABLE IF EXISTS ga_salidas_workers_project;

COMMIT;

-- ============================================================================
-- Verificacion posterior (solo lectura). Debe devolver 0 filas.
--
--   SELECT to_regclass('public.ga_salidas_workers_project') AS sigue_existiendo;
--
-- Y el revisor efectivo de cada trabajador de un area filtrada por proyecto:
--
--   WITH RECURSIVE sub AS (
--       SELECT c.area_scope_id AS root, c.area_scope_id AS nodo
--       FROM ga_salidas_area_config c
--       WHERE c.state AND c.filtra_por_proyecto
--       UNION ALL
--       SELECT sub.root, s.area_scope_id
--       FROM sub JOIN area_scope s ON s.area_scope_parent_id = sub.nodo
--       WHERE s.state)
--   SELECT pe.full_name AS trabajador, pr.project_description AS obra, rp.full_name AS revisor
--   FROM sub
--   JOIN puesto pu ON pu.area_destino_scope_id = sub.nodo
--   JOIN workers w ON w.puesto_id = pu.puesto_id AND w.state
--   JOIN person pe ON pe.person_id = w.person_id AND pe.state
--   LEFT JOIN LATERAL (SELECT v.proyecto_id FROM worker_vinculaciones v
--                      WHERE v.worker_id = w.id AND v.fecha_fin IS NULL
--                      ORDER BY v.created_at DESC, v.id DESC LIMIT 1) vig ON true
--   LEFT JOIN project pr ON pr.project_id = vig.proyecto_id
--   LEFT JOIN LATERAL (SELECT p2.full_name FROM area_revisores ar
--                      JOIN workers w2 ON w2.id = ar.revisor_id
--                      JOIN person p2 ON p2.person_id = w2.person_id
--                      WHERE ar.area_scope_id = sub.root AND ar.state AND ar.active
--                        AND ar.project_id IS NOT DISTINCT FROM vig.proyecto_id
--                      ORDER BY ar.orden_prioridad, ar.area_revisores_id LIMIT 1) rp ON true
--   ORDER BY 2, 1;
-- ============================================================================
