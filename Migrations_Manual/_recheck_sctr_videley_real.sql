-- Conteo limpio y actualizado: cuántos casos reales de "Aprobado + sin fecha"
-- quedan HOY en SCTR y Vida Ley, contra el estado actual de ss_hab_trabajador
-- (la fuente real), sin depender del estado de las pólizas viejas.
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
WHERE it.nombre IN ('SCTR', 'Vida ley')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
GROUP BY 1, 2, 3
ORDER BY it.nombre, cantidad DESC;

-- Detalle completo
SELECT
    it.nombre AS item,
    p.document_identity_code AS dni,
    p.full_name AS nombre,
    we.nombre AS estado_worker,
    c.contributor_name AS empresa,
    pr.project_description AS proyecto
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
LEFT JOIN project pr ON pr.project_id = v.proyecto_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre IN ('SCTR', 'Vida ley')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
ORDER BY it.nombre, nombre;
