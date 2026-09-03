-- Caso puntual: PARQUE MEDINA JAFFET JULINHO
SELECT w.id AS worker_id, p.document_identity_code AS dni, p.full_name,
       we.nombre AS estado_worker, w.contrata_casa,
       v.empresa_id, v.proyecto_id, v.fecha_fin,
       c.contributor_name AS empresa_vinculacion
FROM workers w
JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
WHERE p.document_identity_code = '48769701';

-- Todas sus vinculaciones (incluso cerradas), para ver si alguna vez tuvo LUMBRERAS
SELECT v.id, v.worker_id, v.empresa_id, c.contributor_name, v.proyecto_id, v.fecha_inicio, v.fecha_fin
FROM worker_vinculaciones v
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
WHERE v.worker_id = (SELECT w.id FROM workers w JOIN person p ON p.person_id = w.person_id WHERE p.document_identity_code = '48769701')
ORDER BY v.fecha_inicio DESC;

-- Casos similares: contratistas Activos con SCTR/VidaLey Aprobado+sin fecha
-- que además NO tienen ninguna vinculación activa (empresa "Sin asignar").
SELECT
    it.nombre AS item,
    p.document_identity_code AS dni,
    p.full_name AS nombre,
    h.estado, h.vigencia
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre IN ('SCTR', 'Vida ley')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
  AND we.nombre = 'Activo'
  AND v.worker_id IS NULL
ORDER BY item, nombre;
