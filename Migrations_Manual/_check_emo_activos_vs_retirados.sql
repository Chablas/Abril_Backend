-- Separa los pendientes en dos grupos: los que importan de verdad (activos, con
-- vinculación vigente a un proyecto) vs. los que son solo limpieza histórica
-- (ya retirados, sin riesgo real de acceso).
SELECT
    we.nombre AS estado_worker,
    CASE WHEN v.worker_id IS NULL THEN 'SIN VINCULACION' ELSE 'CON VINCULACION ACTIVA' END AS vinculacion,
    COUNT(*) AS cantidad
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre = 'Certificado de Aptitud (EMO)'
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
GROUP BY 1, 2
ORDER BY cantidad DESC;

-- Detalle SOLO de los urgentes: activos y con vinculación vigente hoy.
SELECT
    p.document_identity_code AS dni,
    p.full_name AS nombre,
    c.contributor_name AS empresa,
    pr.project_description AS proyecto,
    h.archivo_url
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
LEFT JOIN project pr ON pr.project_id = v.proyecto_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre = 'Certificado de Aptitud (EMO)'
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
  AND we.nombre = 'Activo'
ORDER BY nombre;
