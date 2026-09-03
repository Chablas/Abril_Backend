-- Pasa a "Falta" el CarnetRetcc de contratistas RETIRADOS y sin vinculación
-- activa que quedaron en "Aprobado" sin fecha (limpieza de dato histórico,
-- sin riesgo de acceso real). Los 39 activos quedan fuera porque tienen
-- vinculación vigente y requieren revisión real de SSOMA.

-- 1) Vista previa
SELECT h.id AS hab_id, p.document_identity_code AS dni, p.full_name AS nombre, h.estado, h.vigencia
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
WHERE h.item_id = (SELECT id FROM ss_item_trabajador WHERE nombre = 'CarnetRetcc')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
  AND we.nombre = 'Retirado'
  AND v.worker_id IS NULL
ORDER BY nombre;

-- 2) UPDATE real. Ejecutar SOLO después de confirmar que la vista previa
--    de arriba muestra exactamente los 14 esperados.
UPDATE ss_hab_trabajador h
SET estado = 'Falta',
    updated_at = now()
FROM workers w
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
WHERE h.worker_id = w.id
  AND h.item_id = (SELECT id FROM ss_item_trabajador WHERE nombre = 'CarnetRetcc')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
  AND we.nombre = 'Retirado'
  AND v.worker_id IS NULL;
