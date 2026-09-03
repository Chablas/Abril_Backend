-- Reglas del ítem CarnetRetcc (categorías que aplican/excluyen)
SELECT id, nombre, aplica_a, aplica_categoria, excluye_categoria_contratista, requiere_vigencia
FROM ss_item_trabajador
WHERE nombre = 'CarnetRetcc';

-- Categoría/puesto real de DIAZ ZAPATA MELISSA MADELINE (DNI 73150001)
SELECT w.id AS worker_id, p.full_name, w.puesto_id, pu.nombre AS puesto,
       pu.categoria_id, cat.nombre AS categoria
FROM workers w
JOIN person p ON p.person_id = w.person_id
LEFT JOIN puesto pu ON pu.puesto_id = w.puesto_id
LEFT JOIN categoria cat ON cat.categoria_id = pu.categoria_id
WHERE p.document_identity_code = '73150001';
