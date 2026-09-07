-- Solo lectura: ¿qué quedó realmente guardado en T-Registro de Wilter tras aprobarlo?
SELECT h.id AS hab_id, it.id AS item_id, it.nombre, it.requiere_vigencia,
       h.estado, h.vigencia, h.updated_at, h.aprobado_por, h.fecha_aprobacion
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE p.full_name ILIKE '%RENTERIA%' AND p.full_name ILIKE '%WILTER%'
  AND it.nombre = 'T-Registro';
