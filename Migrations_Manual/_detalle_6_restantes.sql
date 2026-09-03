SELECT w.id AS worker_id, p.document_identity_code AS dni, p.full_name,
       wp.proyecto_id, pr.project_description AS proyecto, wp.empresa_id, c.contributor_name AS empresa,
       wp.fecha_inicio
FROM workers w
JOIN person p ON p.person_id = w.person_id
JOIN ss_hab_worker_proyecto wp ON wp.worker_id = w.id AND wp.fecha_fin IS NULL
LEFT JOIN project pr ON pr.project_id = wp.proyecto_id
LEFT JOIN contributor c ON c.contributor_id = wp.empresa_id
WHERE w.id IN (12169, 12359, 12621, 12757, 13148, 13976)
ORDER BY w.id, wp.fecha_inicio DESC;
