-- Recuento simple: de los 91 EMO de contratistas que originalmente estaban
-- "Aprobado" sin fecha (id <= 212734, fecha_aprobacion hasta 2026-06-18),
-- cuántos siguen sin fecha (pendientes de que SSOMA los revise) ahora mismo.
SELECT COUNT(*) AS pendientes_por_corregir
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre = 'Certificado de Aptitud (EMO)'
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa';

-- Detalle de quiénes siguen pendientes
SELECT
    h.id AS hab_id,
    p.document_identity_code AS dni,
    p.full_name AS nombre,
    h.estado,
    h.vigencia,
    h.updated_at
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre = 'Certificado de Aptitud (EMO)'
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
ORDER BY nombre;
