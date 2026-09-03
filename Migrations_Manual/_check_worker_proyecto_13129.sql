SELECT wp.*, c.contributor_name, pr.project_description
FROM ss_hab_worker_proyecto wp
LEFT JOIN contributor c ON c.contributor_id = wp.empresa_id
LEFT JOIN project pr ON pr.project_id = wp.proyecto_id
WHERE wp.worker_id = 13129;
