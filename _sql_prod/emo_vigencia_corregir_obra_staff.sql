-- ============================================================================
-- EMO — corregir vigencias mal asignadas (Obra/Staff con 2 años)
-- Fecha: 2026-08-05
--
-- Regla de negocio (confirmada por Medicina Ocupacional):
--   workers_obra_oficina_staff = 'Obra'            (id 1) -> 12 meses
--   workers_obra_oficina_staff = 'Staff'           (id 2) -> 12 meses
--   workers_obra_oficina_staff = 'Oficina Central' (id 3) -> 24 meses
--
-- El cálculo lo hace EmoRepository.Create/Update:
--   vigenciaMeses = ss_emo_tipos.vigencia_meses (hoy 12 para Ingreso/Anual)
--   y se fuerza a 24 solo si el worker es Oficina Central.
-- Por eso el desvío es puntual: EMOs registrados cuando el trabajador estaba
-- clasificado como Oficina Central y que después pasaron a Obra/Staff.
--
-- A 2026-08-05 esto afecta 1 registro: emo 1077 / worker 12567
-- (FLORES NOLORBE JHON PITER, hoy RETIRADO), fecha_emo 2026-03-02
-- con vencimiento 2028-03-02 → debe ser 2027-03-02.
-- ============================================================================

BEGIN;

-- ----------------------------------------------------------------------------
-- 0) Conjunto objetivo: Obra/Staff con vigencia mayor a 12 meses.
--    Se materializa para que los UPDATE posteriores toquen exactamente
--    estas filas y nada más.
-- ----------------------------------------------------------------------------
CREATE TEMP TABLE tmp_emo_vigencia_fix AS
SELECT e.id                                                            AS emo_id,
       e.worker_id,
       e.fecha_emo,
       COALESCE(e.fecha_vencimiento_calculada, e.fecha_vencimiento)     AS venc_actual,
       (e.fecha_emo + (t.vigencia_meses || ' months')::interval)::date  AS venc_correcto,
       (e.fecha_vencimiento IS NOT NULL)                               AS tiene_raw,
       (e.fecha_vencimiento_calculada IS NOT NULL)                     AS tiene_calc
FROM worker_emos e
JOIN workers w      ON w.id = e.worker_id
JOIN ss_emo_tipos t ON t.id = e.tipo_emo_id
WHERE t.vigencia_meses IS NOT NULL
  AND w.obra_oficina_staff_id IN (1, 2)                 -- Obra, Staff
  AND COALESCE(e.fecha_vencimiento_calculada, e.fecha_vencimiento)
      > e.fecha_emo + INTERVAL '13 months';

-- 1) VERIFICACIÓN PREVIA — revisar esta lista antes de seguir.
SELECT f.emo_id, f.worker_id, p.full_name, oo.name AS obra_oficina,
       t.nombre AS tipo_emo, f.fecha_emo, f.venc_actual, f.venc_correcto,
       e.aptitud, e.estado, e.activo
FROM tmp_emo_vigencia_fix f
JOIN worker_emos e ON e.id = f.emo_id
JOIN workers w     ON w.id = f.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_obra_oficina_staff oo
       ON oo.workers_obra_oficina_staff_id = w.obra_oficina_staff_id
LEFT JOIN ss_emo_tipos t ON t.id = e.tipo_emo_id
ORDER BY f.fecha_emo DESC;

-- ----------------------------------------------------------------------------
-- 2) CORRECCIÓN del EMO.
--    Se recalcula como fecha_emo + vigencia del tipo (no un "- 1 year" ciego),
--    para que quede idéntico a lo que el backend habría calculado.
--    Cada columna se toca solo si ya tenía valor.
-- ----------------------------------------------------------------------------
UPDATE worker_emos e
SET fecha_vencimiento_calculada = CASE WHEN f.tiene_calc THEN f.venc_correcto ELSE NULL END,
    fecha_vencimiento           = CASE WHEN f.tiene_raw  THEN f.venc_correcto ELSE NULL END,
    updated_at                  = now()
FROM tmp_emo_vigencia_fix f
WHERE e.id = f.emo_id;
-- Esperado: UPDATE 1

-- ----------------------------------------------------------------------------
-- 3) Alinear la habilitación derivada de ESOS mismos EMOs
--    (item 4 = Cert. de aptitud médica, item 25 = Lectura de EMO),
--    que el backend sincroniza con el vencimiento del EMO.
--    Solo se toca si la habilitación quedó por encima del vencimiento correcto.
--    NOTA: a propósito NO se corrigen otras filas de ss_hab_trabajador que estén
--    desalineadas por causas ajenas a esto (carga manual/importación).
-- ----------------------------------------------------------------------------
UPDATE ss_hab_trabajador h
SET vigencia   = f.venc_correcto,
    updated_at = now()
FROM tmp_emo_vigencia_fix f
WHERE h.worker_id = f.worker_id
  AND h.item_id IN (4, 25)
  AND h.vigencia IS NOT NULL
  AND h.vigencia::date > f.venc_correcto;
-- Esperado a 2026-08-05: UPDATE 0
-- (la habilitación del worker 12567 ya estaba en 2027-03-02)

-- ----------------------------------------------------------------------------
-- 4) VERIFICACIÓN POSTERIOR — debe devolver 0 filas.
-- ----------------------------------------------------------------------------
SELECT e.id AS emo_id, w.id AS worker_id, e.fecha_emo,
       COALESCE(e.fecha_vencimiento_calculada, e.fecha_vencimiento) AS vence
FROM worker_emos e
JOIN workers w      ON w.id = e.worker_id
JOIN ss_emo_tipos t ON t.id = e.tipo_emo_id
WHERE t.vigencia_meses IS NOT NULL
  AND w.obra_oficina_staff_id IN (1, 2)
  AND COALESCE(e.fecha_vencimiento_calculada, e.fecha_vencimiento)
      > e.fecha_emo + INTERVAL '13 months';

DROP TABLE tmp_emo_vigencia_fix;

COMMIT;
