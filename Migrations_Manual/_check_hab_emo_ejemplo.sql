SELECT h.id, h.worker_id, p.document_identity_code AS dni, p.full_name AS nombre,
       h.estado, h.vigencia, h.archivo_url, h.aprobado_por, h.fecha_aprobacion,
       h.created_at, h.updated_at
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre = 'Certificado de Aptitud (EMO)'
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
ORDER BY h.updated_at DESC
LIMIT 5;
