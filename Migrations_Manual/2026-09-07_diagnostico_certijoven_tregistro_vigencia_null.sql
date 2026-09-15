-- ============================================================================
-- DIAGNÓSTICO (solo lectura) — ¿Certijoven / T-Registro están fuera de
-- ItemsCentinela (HabilitacionDateHelper.cs) y por eso el cron de
-- VigenciaRevisionService los está tumbando a "Falta" cuando quedan
-- Aprobado con vigencia NULL?
-- ============================================================================

-- 1) Catálogo completo de ítems, para ubicar el item_id real de cada uno.
SELECT id, nombre
FROM ss_item_trabajador
ORDER BY id;

-- 2) Cuántos registros Aprobado+vigencia NULL hay hoy por ítem (dimensiona el
--    impacto real, no solo el caso de Wilter Rentería).
SELECT it.id AS item_id, it.nombre AS item, COUNT(*) AS cantidad_afectada
FROM ss_hab_trabajador h
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE h.estado = 'Aprobado' AND h.vigencia IS NULL
GROUP BY it.id, it.nombre
ORDER BY cantidad_afectada DESC;

-- 3) Caso puntual: Wilter Rentería Herrera
SELECT h.id AS hab_id, it.id AS item_id, it.nombre AS item, h.estado, h.vigencia, h.updated_at
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE p.full_name ILIKE '%RENTERIA%' AND p.full_name ILIKE '%WILTER%';
