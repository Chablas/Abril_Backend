-- Pasa a "Falta" el ítem Certificado de Aptitud (EMO) de los contratistas
-- RETIRADOS y sin vinculación activa que quedaron en "Aprobado" sin fecha
-- (dato histórico sucio, sin riesgo de acceso real). Los 2 casos activos
-- (CHAVEZ GALLARDO, JAUREGUI AVALOS) ya fueron corregidos aparte y quedan
-- fuera de este UPDATE porque su vigencia ya no es NULL.

-- 1) Vista previa: confirmar que son exactamente los 18 esperados antes de tocar nada.
SELECT h.id AS hab_id, p.document_identity_code AS dni, p.full_name AS nombre, h.estado, h.vigencia
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre = 'Certificado de Aptitud (EMO)'
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
  AND we.nombre = 'Retirado'
  AND v.worker_id IS NULL
ORDER BY nombre;

-- 2) Update real. Ejecutar SOLO después de confirmar que la vista previa
--    de arriba muestra exactamente los 18 esperados.
UPDATE ss_hab_trabajador h
SET estado = 'Falta',
    updated_at = now()
FROM workers w
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
WHERE h.worker_id = w.id
  AND h.item_id = (SELECT id FROM ss_item_trabajador WHERE nombre = 'Certificado de Aptitud (EMO)')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
  AND we.nombre = 'Retirado'
  AND v.worker_id IS NULL;
