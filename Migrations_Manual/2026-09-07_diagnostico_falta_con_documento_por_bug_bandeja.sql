-- ============================================================================
-- DIAGNÓSTICO (solo lectura) — ítems que quedaron "Falta" con documento SÍ
-- subido (archivo_url no nulo), huella del bug ya corregido en
-- BandejaRepository.AprobarTrabajadorAsync: aprobaba sin calcular la fecha
-- centinela para ítems que no vencen (requiere_vigencia=false), y el cron de
-- vigencias (VigenciaRevisionService) los tumbaba a "Falta" al día siguiente
-- por vigencia null. Esto es responsabilidad nuestra, no de la contratista —
-- no deben contar para el retiro automático mientras no se corrijan.
-- ============================================================================

-- 1) Detalle: trabajador, ítem, y desde cuándo quedó así (updated_at)
SELECT h.id AS hab_id, w.id AS worker_id, p.full_name, w.contrata_casa,
       it.nombre AS item, it.requiere_vigencia, h.estado, h.vigencia,
       h.archivo_url, h.updated_at
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE h.estado = 'Falta'
  AND h.archivo_url IS NOT NULL
  AND it.requiere_vigencia = false
  AND w.workers_estado_id = (SELECT workers_estado_id FROM workers_estado WHERE codigo = 'ACTIVO')
ORDER BY h.updated_at DESC;

-- 2) Resumen por ítem, para dimensionar
SELECT it.nombre AS item, COUNT(*) AS cantidad
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE h.estado = 'Falta'
  AND h.archivo_url IS NOT NULL
  AND it.requiere_vigencia = false
  AND w.workers_estado_id = (SELECT workers_estado_id FROM workers_estado WHERE codigo = 'ACTIVO')
GROUP BY it.nombre
ORDER BY cantidad DESC;
