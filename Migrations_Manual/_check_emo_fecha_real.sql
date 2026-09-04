-- Fuente real del EMO (worker_emos) para los casos donde ss_hab_trabajador.vigencia
-- quedo en NULL. FechaVencimientoCalculada (o FechaVencimiento si la calculada es null)
-- es la fecha real de vencimiento del examen médico.
SELECT
    h.id AS hab_id,
    p.document_identity_code AS dni,
    p.full_name AS nombre,
    h.estado AS estado_hab,
    h.vigencia AS vigencia_hab_null,
    we.id AS worker_emo_id,
    we.estado AS estado_emo,
    we.fecha_emo,
    we.fecha_vencimiento,
    we.fecha_vencimiento_calculada,
    COALESCE(we.fecha_vencimiento_calculada, we.fecha_vencimiento) AS vencimiento_real,
    CASE WHEN COALESCE(we.fecha_vencimiento_calculada, we.fecha_vencimiento) < CURRENT_DATE
         THEN 'VENCIDO' ELSE 'VIGENTE' END AS diagnostico
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
LEFT JOIN worker_emos we ON we.worker_id = w.id
WHERE it.nombre = 'Certificado de Aptitud (EMO)'
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND lower(trim(w.contrata_casa)) <> 'casa'
ORDER BY we.fecha_emo DESC NULLS LAST;
