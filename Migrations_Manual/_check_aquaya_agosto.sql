-- Detalle de los trabajadores de las pólizas VIDA_LEY de AQUAYA PERU
-- (agosto y julio 2026, "En revisión"): por qué no se cerraron.
SELECT s.id AS poliza_id, s.anio, s.mes,
       p.document_identity_code AS dni, p.full_name AS nombre,
       h.estado AS estado_hab_actual, h.vigencia,
       EXISTS (
           SELECT 1 FROM ss_sctr_vidaley s2
           JOIN ss_sctr_vidaley_worker svw2 ON svw2.sctr_vidaley_id = s2.id
           WHERE svw2.worker_id = svw.worker_id
             AND s2.tipo = 'VIDA_LEY'
             AND (s2.anio > 2026 OR (s2.anio = 2026 AND s2.mes > 8))
       ) AS tiene_poliza_setiembre_o_mas
FROM ss_sctr_vidaley s
JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
JOIN workers w ON w.id = svw.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN ss_hab_trabajador h ON h.worker_id = w.id
    AND h.item_id = (SELECT id FROM ss_item_trabajador WHERE nombre ILIKE '%Vida%' LIMIT 1)
WHERE s.tipo = 'VIDA_LEY'
  AND s.estado = 'En revision'
  AND s.empresa_id = (SELECT contributor_id FROM contributor WHERE contributor_name ILIKE '%AQUAYA%' LIMIT 1)
ORDER BY s.anio DESC, s.mes DESC, nombre;
