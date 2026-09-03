-- Por qué algunos DNI de la lista de 91 no aparecen al buscar en pantalla:
-- revisa si el worker está retirado, sin vinculación activa, o duplicado.
SELECT
    h.id AS hab_id,
    p.document_identity_code AS dni,
    p.full_name AS nombre,
    w.id AS worker_id,
    we.nombre AS estado_worker,
    v.fecha_inicio,
    v.fecha_fin,
    CASE
        WHEN v.worker_id IS NULL THEN 'SIN VINCULACION (nunca tuvo o no encontrada)'
        WHEN v.fecha_fin IS NOT NULL THEN 'VINCULACION CERRADA (fecha_fin no nula)'
        ELSE 'VINCULACION ACTIVA'
    END AS diagnostico_busqueda,
    h.vigencia
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre = 'Certificado de Aptitud (EMO)'
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
ORDER BY diagnostico_busqueda, nombre;
