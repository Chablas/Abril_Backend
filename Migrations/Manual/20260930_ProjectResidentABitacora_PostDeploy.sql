-- ============================================================================
-- Residentes — la tabla antigua project_resident pasa a la bitácora y se bota
-- SOLO DESPUÉS DE DESPLEGAR EL BACKEND con los Pasos 4e y 6 de PLAN-RESIDENTES.md
-- Fecha: 2026-09-30
--
-- project_resident guardaba, sin pantalla y con SQL a mano, qué usuario era el residente de qué
-- proyecto. Desde los Pasos 4a–4e ningún código la lee (el residente sale de Configuración →
-- Proyectos, project.residente_workers_id) y desde el Paso 6 el backend ya no la mapea.
--
-- Correrlo ANTES del deploy rompe lo que el backend viejo todavía lee de ahí (42P01): en prod,
-- Cronograma de Hitos, IVTs, Cuaderno de obra, Control de respuesta de informes, Seguimiento de
-- residentes y Planeamiento BIM; en demo, Planeamiento BIM hasta que llegue el Paso 4e.
--
-- Qué hace, en una sola transacción:
--   1) Copia a project_residente_historial cada fila vigente (state) como la entrada de su
--      residente en su fecha: workers_id_anterior NULL, workers_id_nuevo = su ficha,
--      cambio_date_time = created_date_time (guardada en UTC, sin zona) y cambio_user_id =
--      created_user_id (quien cargó la fila). La ficha es la del residente del proyecto antes del
--      primer cambio que registró la bitácora, o la de hoy si no hubo cambios: así la historia
--      empalma con lo que la bitácora ya tiene.
--   2) DROP TABLE project_resident (nada la referencia: solo tiene sus propias FK hacia afuera).
--
-- Guardas (aborta sin tocar nada y dice qué filas):
--   · Una fila vigente inactiva (active = false), o cuyo usuario no es ese residente: pasarla
--     obligaría a inventar cuándo dejó de serlo.
--   · Una fila vigente de un proyecto sin residente.
-- Las filas dadas de baja (state = false) no se copian (en el sistema, dado de baja = eliminado)
-- y se listan como aviso: en prod es solo la de prueba de calvarez en TORRE ABRIL (2026-03-23),
-- que el Paso 2 dio de baja. Se pierde con el DROP.
--
-- Idempotente: si project_resident ya no existe, no hace nada; una entrada ya copiada no se repite.
-- ============================================================================

BEGIN;

SET LOCAL lock_timeout = '15s';

DO $$
DECLARE
    v_problemas text;
    v_bajas     text;
    v_copiadas  integer;
BEGIN
    IF to_regclass('public.project_resident') IS NULL THEN
        RAISE NOTICE 'project_resident ya no existe: nada que hacer.';
        RETURN;
    END IF;

    IF to_regclass('public.project_residente_historial') IS NULL THEN
        RAISE EXCEPTION 'Falta project_residente_historial (20260928_ProyectosRolesYHistorialResidente.sql). Abortado.';
    END IF;

    -- Cada fila con la ficha del residente del proyecto antes del primer cambio registrado en la
    -- bitácora (sin contar las entradas que ya se hubieran copiado de esta tabla), o la de hoy.
    CREATE TEMP TABLE tmp_project_resident ON COMMIT DROP AS
    SELECT pr.project_resident_id, pr.project_id, p.project_description, pr.user_id,
           pr.active, pr.state, pr.created_date_time AT TIME ZONE 'UTC' AS desde, pr.created_user_id,
           CASE WHEN h.id IS NOT NULL THEN h.workers_id_anterior ELSE p.residente_workers_id END AS ficha
    FROM project_resident pr
    JOIN project p ON p.project_id = pr.project_id
    LEFT JOIN LATERAL (
        SELECT h.id, h.workers_id_anterior
        FROM project_residente_historial h
        WHERE h.project_id = p.project_id
          AND NOT (h.workers_id_anterior IS NULL AND EXISTS (
                SELECT 1 FROM project_resident x
                WHERE x.project_id = h.project_id
                  AND h.cambio_date_time = x.created_date_time AT TIME ZONE 'UTC'))
        ORDER BY h.cambio_date_time, h.id
        LIMIT 1
    ) h ON true;

    SELECT string_agg(format('%s (fila %s, usuario %s): %s', t.project_description, t.project_resident_id, t.user_id,
               CASE WHEN NOT t.active   THEN 'fila inactiva'
                    WHEN t.ficha IS NULL THEN 'el proyecto no tiene residente'
                    ELSE 'el residente del proyecto es otra persona' END),
               '; ' ORDER BY t.project_description, t.project_resident_id)
    INTO v_problemas
    FROM tmp_project_resident t
    LEFT JOIN workers w ON w.id = t.ficha
    LEFT JOIN person pe ON pe.person_id = w.person_id
    WHERE t.state
      AND (NOT t.active OR t.ficha IS NULL OR pe.user_id IS DISTINCT FROM t.user_id);

    IF v_problemas IS NOT NULL THEN
        RAISE EXCEPTION 'Filas de project_resident que no empalman con Configuración → Proyectos: %. Abortado, no se tocó nada.', v_problemas;
    END IF;

    SELECT string_agg(format('%s (fila %s, usuario %s, del %s)', t.project_description, t.project_resident_id,
               t.user_id, t.desde::date), '; ' ORDER BY t.project_resident_id)
    INTO v_bajas
    FROM tmp_project_resident t
    WHERE NOT t.state;

    IF v_bajas IS NOT NULL THEN
        RAISE NOTICE 'No se copian las filas dadas de baja: %', v_bajas;
    END IF;

    INSERT INTO project_residente_historial
        (project_id, workers_id_anterior, workers_id_nuevo, cambio_date_time, cambio_user_id)
    SELECT t.project_id, NULL, t.ficha, t.desde, t.created_user_id
    FROM tmp_project_resident t
    WHERE t.state
      AND NOT EXISTS (SELECT 1 FROM project_residente_historial h
                      WHERE h.project_id = t.project_id
                        AND h.workers_id_anterior IS NULL
                        AND h.workers_id_nuevo = t.ficha
                        AND h.cambio_date_time = t.desde);
    GET DIAGNOSTICS v_copiadas = ROW_COUNT;
    RAISE NOTICE 'Copiadas a project_residente_historial: % fila(s).', v_copiadas;

    DROP TABLE project_resident;
END $$;

COMMIT;

-- ── Verificación ──────────────────────────────────────────────────────────────
-- a) La tabla ya no existe (debe dar NULL):
-- SELECT to_regclass('public.project_resident');
--
-- b) Lo que quedó en la bitácora, por obra (las copiadas tienen created_date_time de hoy y
--    workers_id_anterior NULL):
-- SELECT p.project_description, pe.full_name AS residente, h.cambio_date_time::date AS desde,
--        h.created_date_time::date AS copiada
-- FROM project_residente_historial h
-- JOIN project p ON p.project_id = h.project_id
-- LEFT JOIN workers w ON w.id = h.workers_id_nuevo
-- LEFT JOIN person pe ON pe.person_id = w.person_id
-- WHERE h.workers_id_anterior IS NULL
-- ORDER BY 1, 3;
