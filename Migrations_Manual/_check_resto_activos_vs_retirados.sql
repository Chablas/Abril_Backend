-- Igual que hicimos con EMO: separa los pendientes de SCTR, Vida ley, DNI y
-- CarnetRetcc (Aprobado + Vigencia NULL, contratistas) entre "Retirado sin
-- vinculación" (limpieza histórica, no urgente) y "Activo con vinculación"
-- (urgente, riesgo de acceso real).
SELECT
    it.nombre AS item,
    we.nombre AS estado_worker,
    CASE WHEN v.worker_id IS NULL THEN 'SIN VINCULACION' ELSE 'CON VINCULACION ACTIVA' END AS vinculacion,
    COUNT(*) AS cantidad
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre IN ('SCTR', 'Vida ley', 'DNI', 'CarnetRetcc')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
GROUP BY 1, 2, 3
ORDER BY it.nombre, cantidad DESC;

-- Detalle SOLO de los urgentes: activos y con vinculación vigente hoy.
SELECT
    it.nombre AS item,
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
WHERE it.nombre IN ('SCTR', 'Vida ley', 'DNI', 'CarnetRetcc')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
  AND we.nombre = 'Activo'
ORDER BY it.nombre, nombre;
