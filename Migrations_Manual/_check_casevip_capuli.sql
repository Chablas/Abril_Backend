SELECT p.document_identity_code AS dni, p.full_name AS nombre,
       h.estado AS estado_hab_actual, h.vigencia
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE p.document_identity_code IN ('48514253','44259652')
  AND it.nombre = 'Vida ley';
