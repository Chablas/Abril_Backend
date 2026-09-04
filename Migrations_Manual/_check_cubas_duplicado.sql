-- Verificar si DNI 46941569 (CUBAS ALVARADO DENIS) tiene varios worker_id
-- (reingresos) y si el hab_id 196698 (vigencia NULL) pertenece a un worker
-- distinto del que se ve activo en pantalla.
SELECT w.id AS worker_id, w.workers_estado_id, w.contrata_casa, w.created_at AS worker_created_at
FROM workers w
JOIN person p ON p.person_id = w.person_id
WHERE p.document_identity_code = '46941569';

SELECT
    h.id AS hab_id, h.worker_id, h.estado, h.vigencia, h.updated_at
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE p.document_identity_code = '46941569'
  AND it.nombre = 'Certificado de Aptitud (EMO)'
ORDER BY h.worker_id;

SELECT v.worker_id, v.empresa_id, v.proyecto_id, v.fecha_inicio, v.fecha_fin
FROM worker_vinculaciones v
JOIN workers w ON w.id = v.worker_id
JOIN person p ON p.person_id = w.person_id
WHERE p.document_identity_code = '46941569'
ORDER BY v.worker_id, v.fecha_inicio;
