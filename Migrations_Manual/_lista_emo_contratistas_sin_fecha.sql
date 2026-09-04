-- Lista completa para revisión manual de SSOMA: contratistas con Certificado de
-- Aptitud (EMO) = "Aprobado" pero sin fecha de vigencia registrada. NO se toca nada,
-- es solo para que cada caso se revise contra el PDF adjunto (archivo_url) y se
-- cargue la fecha real de vencimiento manualmente antes de aplicar cualquier fix.
SELECT
    h.id AS hab_id,
    p.document_identity_code AS dni,
    p.full_name AS nombre,
    c.contributor_name AS empresa,
    pr.project_description AS proyecto,
    h.archivo_url,
    h.aprobado_por,
    h.fecha_aprobacion,
    h.created_at,
    h.updated_at
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
LEFT JOIN project pr ON pr.project_id = v.proyecto_id
WHERE it.nombre = 'Certificado de Aptitud (EMO)'
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
ORDER BY h.fecha_aprobacion DESC;
