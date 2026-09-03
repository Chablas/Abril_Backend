-- Pone vigencia = 31/12/2030 a todos los DNI de contratistas que quedaron
-- "Aprobado" sin fecha (el DNI no vence realmente, se usa fecha lejana fija
-- como ya se hace para otros ítems sin vigencia real, ej. Induccion Obra).

-- 1) Vista previa: confirmar cuántos/cuáles se van a tocar antes de ejecutar el UPDATE.
SELECT h.id AS hab_id, p.document_identity_code AS dni, p.full_name AS nombre, h.estado, h.vigencia
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
WHERE h.item_id = (SELECT id FROM ss_item_trabajador WHERE nombre = 'DNI')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
ORDER BY nombre;

-- 2) UPDATE real. Ejecutar SOLO después de confirmar la vista previa de arriba.
UPDATE ss_hab_trabajador h
SET vigencia = '2030-12-31 00:00:00+00'::timestamptz,
    updated_at = now()
FROM workers w
WHERE h.worker_id = w.id
  AND h.item_id = (SELECT id FROM ss_item_trabajador WHERE nombre = 'DNI')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa';
