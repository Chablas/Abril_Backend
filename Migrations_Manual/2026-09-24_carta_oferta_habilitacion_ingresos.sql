-- ============================================================================
-- Ingresos por carta oferta sin asignación de proyecto ni entregables en Habilitación
--
-- Aprobar la carta oferta firmada (CartaOfertaRepository.Aprobar) pasaba la ficha a
-- ACTIVO con su periodo laboral y su vinculación, pero no dejaba lo que sí deja el alta
-- de trabajador: su fila en ss_hab_worker_proyecto y sus entregables en
-- ss_hab_trabajador. Por eso:
--   - «Programar Inducción» no los listaba (arma su lista desde ss_hab_worker_proyecto).
--   - Figuraban «Habilitado» con el único entregable que tenían, el del EMO.
--
-- El código ya lo hace al aprobar (Shared/Services/HabilitacionIngresoHelper.cs). Este
-- script repara las fichas aprobadas antes: al 2026-09-24 son 3 (REQ-2026-0014, 0015 y
-- 0016, workers 15801, 15761 y 15762).
--
-- Solo toca fichas que la carta oferta convirtió en trabajador (su primera vinculación
-- nace con la aprobación): a quien ya era trabajador de Abril la aprobación no le cambia
-- nada, igual que en el código. Las reglas de entregables son las de
-- HabilitacionIngresoHelper.InicializarEntregablesAsync.
--
-- Idempotente: se puede correr antes o después del deploy y volver a correr.
-- ============================================================================

BEGIN;

CREATE TEMP TABLE tmp_ingresos_carta ON COMMIT DROP AS
SELECT DISTINCT ON (w.id)
       w.id                 AS worker_id,
       r.project_id         AS proyecto_id,
       w.contributor_id     AS empresa_id,
       v.fecha_inicio       AS fecha_ingreso,
       CASE WHEN lower(btrim(coalesce(w.contrata_casa, ''))) = 'casa'
            THEN 'CASA' ELSE 'CONTRATISTA' END                    AS tipo,
       lower(btrim(coalesce(w.contrata_casa, ''))) = 'contratista' AS es_contratista,
       CASE w.obra_oficina_staff_id
            WHEN 1 THEN 'Obra' WHEN 2 THEN 'Staff'
            WHEN 3 THEN 'Oficina Central' WHEN 4 THEN 'Personal Externo' END AS obra_oficina,
       pu.categoria_id,
       c.nombre             AS categoria
FROM gth_carta_oferta ca
JOIN gth_candidato cand  ON cand.gth_candidato_id = ca.gth_candidato_id AND cand.state
JOIN gth_requerimiento r ON r.gth_requerimiento_id = cand.gth_requerimiento_id
JOIN workers w           ON w.person_id = ca.person_id AND w.state AND w.workers_estado_id = 1
JOIN LATERAL (
    SELECT v0.fecha_inicio, v0.created_at
    FROM worker_vinculaciones v0
    WHERE v0.worker_id = w.id
    ORDER BY v0.created_at, v0.id
    LIMIT 1
) v ON v.created_at >= ca.aprobada_date_time
LEFT JOIN puesto pu      ON pu.puesto_id = w.puesto_id
LEFT JOIN categoria c    ON c.categoria_id = pu.categoria_id
WHERE ca.state AND ca.aprobada_date_time IS NOT NULL
ORDER BY w.id, ca.aprobada_date_time DESC;

-- 1) Asignación al proyecto del requerimiento. Si ya tiene alguna para ese proyecto
--    (abierta o cerrada) no se toca; la guarda de abajo avisa si quedó sin una abierta.
INSERT INTO ss_hab_worker_proyecto
    (worker_id, proyecto_id, empresa_id, fecha_inicio, fecha_fin,
     induccion_completada, fecha_induccion, created_at, updated_at)
SELECT t.worker_id, t.proyecto_id, t.empresa_id, t.fecha_ingreso, NULL,
       ind.aprobada,
       CASE WHEN ind.aprobada THEN (now() AT TIME ZONE 'UTC')::date END,
       now(), NULL
FROM tmp_ingresos_carta t
CROSS JOIN LATERAL (
    SELECT EXISTS (SELECT 1 FROM ss_hab_trabajador h
                   WHERE h.worker_id = t.worker_id
                     AND h.item_id = 12            -- Inducción Obra
                     AND h.estado = 'Aprobado') AS aprobada
) ind
WHERE NOT EXISTS (SELECT 1 FROM ss_hab_worker_proyecto wp
                  WHERE wp.worker_id = t.worker_id AND wp.proyecto_id = t.proyecto_id);

-- 2) Entregables que le aplican y no tiene, en «Falta». Los CSV del catálogo se comparan
--    por token exacto e ignorando mayúsculas, igual que CsvContiene / CsvExcluye.
INSERT INTO ss_hab_trabajador (worker_id, item_id, estado, vigencia, created_at, updated_at)
SELECT t.worker_id, i.id, 'Falta', NULL, now(), now()
FROM tmp_ingresos_carta t
JOIN ss_item_trabajador i ON i.activo
WHERE (i.aplica_a = 'TODOS' OR i.aplica_a = t.tipo)
  AND (i.aplica_categoria IS NULL
       OR lower(coalesce(t.categoria, '')) IN
          (SELECT lower(btrim(x)) FROM unnest(string_to_array(i.aplica_categoria, ',')) x))
  AND (i.aplica_obra_oficina IS NULL
       OR lower(coalesce(t.obra_oficina, '')) IN
          (SELECT lower(btrim(x)) FROM unnest(string_to_array(i.aplica_obra_oficina, ',')) x))
  AND NOT (i.excluye_obra_oficina IS NOT NULL
       AND lower(coalesce(t.obra_oficina, '')) IN
          (SELECT lower(btrim(x)) FROM unnest(string_to_array(i.excluye_obra_oficina, ',')) x))
  AND NOT (t.es_contratista AND i.excluye_categoria_contratista IS NOT NULL
       AND lower(coalesce(t.categoria, '')) IN
          (SELECT lower(btrim(x)) FROM unnest(string_to_array(i.excluye_categoria_contratista, ',')) x))
  AND NOT (t.tipo = 'CASA' AND t.categoria_id = 4 AND i.id = 13)   -- practicante de Casa: sin Vida Ley
ON CONFLICT (worker_id, item_id) DO NOTHING;

-- Guarda: cada ingreso tiene que quedar con su asignación abierta al proyecto.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM tmp_ingresos_carta t
               WHERE NOT EXISTS (SELECT 1 FROM ss_hab_worker_proyecto wp
                                 WHERE wp.worker_id = t.worker_id
                                   AND wp.proyecto_id = t.proyecto_id
                                   AND wp.fecha_fin IS NULL)) THEN
        RAISE EXCEPTION 'Algún ingreso quedó sin asignación abierta al proyecto: no se aplicó nada.';
    END IF;
END $$;

-- Resultado: una asignación abierta por ingreso y sus entregables (16 para Casa en
-- Oficina Central o Staff: los 15 nuevos más el del EMO que ya tenían).
SELECT t.worker_id, t.proyecto_id,
       (SELECT count(*) FROM ss_hab_worker_proyecto wp
         WHERE wp.worker_id = t.worker_id AND wp.proyecto_id = t.proyecto_id
           AND wp.fecha_fin IS NULL)                                   AS asignacion_abierta,
       (SELECT count(*) FROM ss_hab_trabajador h WHERE h.worker_id = t.worker_id) AS entregables
FROM tmp_ingresos_carta t
ORDER BY t.worker_id;

COMMIT;
