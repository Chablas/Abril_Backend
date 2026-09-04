-- Reglas de exclusión por categoría de los 3 ítems ya trabajados (para confirmar
-- si casos como DIAZ ZAPATA / PREVENCIONISTA están correctamente excluidos).
SELECT id, nombre, aplica_a, aplica_categoria, excluye_categoria_contratista, requiere_vigencia
FROM ss_item_trabajador
WHERE nombre IN ('CarnetRetcc', 'DNI', 'Certificado de Aptitud (EMO)');

-- Estado actualizado: pendientes reales (Aprobado + Vigencia NULL, contratistas)
-- para EMO, DNI y CarnetRetcc, EXCLUYENDO explícitamente a los trabajadores cuya
-- categoría está en excluye_categoria_contratista del ítem correspondiente.
SELECT
    it.nombre AS item,
    we.nombre AS estado_worker,
    CASE WHEN v.worker_id IS NULL THEN 'SIN VINCULACION' ELSE 'CON VINCULACION ACTIVA' END AS vinculacion,
    COUNT(*) AS cantidad
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN puesto pu ON pu.puesto_id = w.puesto_id
LEFT JOIN categoria cat ON cat.categoria_id = pu.categoria_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre IN ('CarnetRetcc', 'DNI', 'Certificado de Aptitud (EMO)')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
  AND (it.excluye_categoria_contratista IS NULL
       OR cat.nombre IS NULL
       OR position(upper(cat.nombre) in upper(it.excluye_categoria_contratista)) = 0)
GROUP BY 1, 2, 3
ORDER BY it.nombre, cantidad DESC;

-- Detalle de los que siguen pendientes de verdad (activos, con vinculación,
-- categoría no excluida) para EMO, DNI y CarnetRetcc.
SELECT
    it.nombre AS item,
    p.document_identity_code AS dni,
    p.full_name AS nombre,
    cat.nombre AS categoria,
    c.contributor_name AS empresa,
    pr.project_description AS proyecto
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN puesto pu ON pu.puesto_id = w.puesto_id
LEFT JOIN categoria cat ON cat.categoria_id = pu.categoria_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
LEFT JOIN project pr ON pr.project_id = v.proyecto_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre IN ('CarnetRetcc', 'DNI', 'Certificado de Aptitud (EMO)')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
  AND we.nombre = 'Activo'
  AND (it.excluye_categoria_contratista IS NULL
       OR cat.nombre IS NULL
       OR position(upper(cat.nombre) in upper(it.excluye_categoria_contratista)) = 0)
ORDER BY it.nombre, nombre;
