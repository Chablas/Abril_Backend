-- ============================================================================
-- Corregir clasificación Obra/Oficina y vigencia de EMO
-- Trabajador: DE LA CRUZ MAYORCA HIPOLITO MARIO (DNI 80193842, worker_id 14129)
-- Fecha: 2026-08-05
--
-- Está registrado como 'Oficina Central' (id 3) pero en realidad es 'Staff'
-- (id 2). Por eso su EMO de Ingreso del 17/11/2025 quedó con 24 meses de
-- vigencia (17/11/2027) en vez de 12 (17/11/2026): EmoRepository fuerza 24
-- meses solo para Oficina Central.
--
-- Nota: la convalidación pendiente de ese EMO (worker_emo_convalidaciones
-- id 35, empresa destino Aquitania) NO se toca — Medicina Ocupacional la
-- resolverá después de esta corrección, que es lo que pidieron.
-- ============================================================================

BEGIN;

-- 1) ESTADO PREVIO
SELECT w.id AS worker_id, p.document_identity_code AS dni, p.full_name,
       oo.name AS obra_oficina, w.contrata_casa, w.area, w.subarea, w.estado,
       e.id AS emo_id, t.nombre AS tipo_emo, t.vigencia_meses,
       e.fecha_emo, e.fecha_vencimiento, e.fecha_vencimiento_calculada,
       e.aptitud, e.estado AS emo_estado, e.activo
FROM workers w
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_obra_oficina_staff oo
       ON oo.workers_obra_oficina_staff_id = w.obra_oficina_staff_id
LEFT JOIN worker_emos e ON e.worker_id = w.id
LEFT JOIN ss_emo_tipos t ON t.id = e.tipo_emo_id
WHERE w.id = 14129;
-- Esperado: obra_oficina = 'Oficina Central', emo 1576 con vencimiento 2027-11-17

-- 2) Reclasificar el trabajador: Oficina Central (3) -> Staff (2)
UPDATE workers
SET obra_oficina_staff_id = 2,
    updated_at = now()
WHERE id = 14129
  AND obra_oficina_staff_id = 3;
-- Esperado: UPDATE 1

-- 3) Recalcular la vigencia de sus EMOs con la regla de Staff (12 meses):
--    fecha_emo + ss_emo_tipos.vigencia_meses. Cada columna se toca solo si
--    ya tenía valor, para no inventar vencimientos donde no había.
UPDATE worker_emos e
SET fecha_vencimiento_calculada =
        CASE WHEN e.fecha_vencimiento_calculada IS NOT NULL
             THEN (e.fecha_emo + (t.vigencia_meses || ' months')::interval)::date END,
    fecha_vencimiento =
        CASE WHEN e.fecha_vencimiento IS NOT NULL
             THEN (e.fecha_emo + (t.vigencia_meses || ' months')::interval)::date END,
    updated_at = now()
FROM ss_emo_tipos t
WHERE t.id = e.tipo_emo_id
  AND e.worker_id = 14129
  AND t.vigencia_meses IS NOT NULL
  AND COALESCE(e.fecha_vencimiento_calculada, e.fecha_vencimiento)
      > (e.fecha_emo + (t.vigencia_meses || ' months')::interval)::date;
-- Esperado: UPDATE 1 (emo 1576: 2027-11-17 -> 2026-11-17)

-- 4) Alinear la habilitación derivada del EMO, si tuviera vigencia por encima
--    del vencimiento corregido (item 4 = Cert. de aptitud, 25 = Lectura EMO).
UPDATE ss_hab_trabajador h
SET vigencia = (e.fecha_emo + (t.vigencia_meses || ' months')::interval)::date,
    updated_at = now()
FROM worker_emos e
JOIN ss_emo_tipos t ON t.id = e.tipo_emo_id
WHERE h.worker_id = 14129
  AND e.worker_id = 14129
  AND e.activo
  AND h.item_id IN (4, 25)
  AND h.vigencia IS NOT NULL
  AND t.vigencia_meses IS NOT NULL
  AND h.vigencia::date > (e.fecha_emo + (t.vigencia_meses || ' months')::interval)::date;
-- Esperado: UPDATE 0 (hoy sus items 4 y 25 están en Pendiente/Falta, sin vigencia)

-- 5) ESTADO POSTERIOR — obra_oficina debe ser 'Staff' y el EMO vencer 2026-11-17
SELECT w.id AS worker_id, p.full_name, oo.name AS obra_oficina,
       e.id AS emo_id, e.fecha_emo,
       e.fecha_vencimiento, e.fecha_vencimiento_calculada, e.estado AS emo_estado
FROM workers w
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_obra_oficina_staff oo
       ON oo.workers_obra_oficina_staff_id = w.obra_oficina_staff_id
LEFT JOIN worker_emos e ON e.worker_id = w.id
WHERE w.id = 14129;

COMMIT;
