-- Diagnóstico (SOLO LECTURA): registros en ss_hab_trabajador con Estado='Aprobado'
-- pero Vigencia NULL. Por el bug en ControlAccesoRepository (Vigencia.HasValue exige
-- fecha antes de comparar), estos quedan "Habilitado" para siempre sin importar la fecha.
-- Incluye tanto contratistas como Casa (el bug de SCTR afecta a ambos; el de EMO
-- solo a contratistas, porque en Casa el EMO se resuelve vía worker_emo, no aquí).

SELECT
    h.id                    AS hab_id,
    h.worker_id,
    p.document_identity_code AS dni,
    p.full_name             AS nombre,
    w.contrata_casa,
    CASE WHEN lower(trim(w.contrata_casa)) = 'casa' THEN 'Casa' ELSE 'Contratista' END AS tipo,
    it.nombre                 AS item,
    h.estado,
    h.vigencia,
    h.updated_at
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE h.estado = 'Aprobado'
  AND h.vigencia IS NULL
ORDER BY tipo, item, nombre;

-- Resumen por item / tipo, para dimensionar el impacto:
SELECT
    CASE WHEN lower(trim(w.contrata_casa)) = 'casa' THEN 'Casa' ELSE 'Contratista' END AS tipo,
    it.nombre AS item,
    COUNT(*) AS cantidad
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE h.estado = 'Aprobado'
  AND h.vigencia IS NULL
GROUP BY 1, 2
ORDER BY cantidad DESC;

-- Caso puntual reportado (doc 45139988):
SELECT h.id, h.worker_id, it.nombre AS item, h.estado, h.vigencia, w.contrata_casa
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE p.document_identity_code = '45139988';
