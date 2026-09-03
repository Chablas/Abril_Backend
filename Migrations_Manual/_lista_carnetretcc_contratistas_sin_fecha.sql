-- Lista completa para revisión manual: CarnetRetcc de contratistas "Aprobado"
-- sin fecha de vigencia. Incluye estado del trabajador (Activo/Retirado) y
-- vinculación, para poder priorizar igual que se hizo con el EMO.
SELECT
    h.id AS hab_id,
    p.document_identity_code AS dni,
    p.full_name AS nombre,
    we.nombre AS estado_worker,
    CASE WHEN v.worker_id IS NULL THEN 'SIN VINCULACION' ELSE 'CON VINCULACION ACTIVA' END AS vinculacion,
    c.contributor_name AS empresa,
    pr.project_description AS proyecto,
    h.archivo_url
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
LEFT JOIN project pr ON pr.project_id = v.proyecto_id
WHERE h.item_id = (SELECT id FROM ss_item_trabajador WHERE nombre = 'CarnetRetcc')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
ORDER BY (we.nombre = 'Activo' AND v.worker_id IS NOT NULL) DESC, nombre;
